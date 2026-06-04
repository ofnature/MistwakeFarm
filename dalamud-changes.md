# Dalamud v15.0.1.1 to v15.0.2.0 - Public Report

Checked: `2026-06-02`

This is a public-safe summary of the official Dalamud `15.0.1.1...15.0.2.0` and FFXIVClientStructs `d892ad8f...5deef083` changes. It intentionally omits private project names, local paths, and private implementation details.

## Sources

- Dalamud compare: `https://github.com/goatcorp/Dalamud/compare/15.0.1.1...15.0.2.0`
- FFXIVClientStructs compare: `https://github.com/aers/FFXIVClientStructs/compare/d892ad8f45bbcabbdda7e0078424be129b67771d...5deef083b822c65f17bb64bbc3993c938fe4b743`
- Dalamud v15 page: `https://dalamud.dev/versions/v15/`
- Dalamud API reference: `https://dalamud.dev/api/`
- Dalamud SDK package index: `https://api.nuget.org/v3-flatcontainer/dalamud.net.sdk/index.json`
- Dalamud packager package index: `https://api.nuget.org/v3-flatcontainer/dalamudpackager/index.json`

## Summary

The official runtime tag moved from `15.0.1.1` to `15.0.2.0`.

- Dalamud range: 36 commits, 17 changed files.
- FFXIVClientStructs range: 91 commits, 92 changed files.
- Public plugin SDK remains `Dalamud.NET.Sdk/15.0.0`.
- Public packager remains `DalamudPackager/15.0.0`.

Plugin projects should not switch to a nonexistent `15.0.2.0` SDK. Keep:

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">
```

## Main Codebase Impacts

1. Addon and agent lifecycle registration is now internally locked.
2. `IClientState.IsClientIdle()` behavior changed and should not be treated as a stable feature-specific safety gate.
3. `ITextureReadbackProvider.GetAllRawImagesAsync` was added for full mipmap and array-slice readback.
4. `IUnlockState` gained maintained class-job and unlock-link sequence helpers.
5. Plugin updates are more idempotent and favorites/pinned installer behavior moved.
6. FFXIVClientStructs added or changed several native UI, Excel, object, event, WKS, graphics, and resource structures.

## Common Before > After Examples

### Lifecycle listeners

Before:

```csharp
public void Enable()
{
    addonLifecycle.RegisterListener(AddonEvent.PostDraw, "SomeAddon", OnAddon);
}

public void Disable()
{
    addonLifecycle.UnregisterListener(AddonEvent.PostDraw, "SomeAddon", OnAddon);
}
```

After:

```csharp
private bool registered;

public void Enable()
{
    if (registered)
        return;

    addonLifecycle.RegisterListener(AddonEvent.PostDraw, "SomeAddon", OnAddon);
    registered = true;
}

public void Disable()
{
    if (!registered)
        return;

    addonLifecycle.UnregisterListener(OnAddon);
    registered = false;
}
```

### AtkValue string constants

Before:

```csharp
return value->Type switch
{
    AtkValueType.String or AtkValueType.String8 or AtkValueType.ManagedString
        => value->String.ToString(),
    _ => string.Empty,
};
```

After:

```csharp
return value->Type switch
{
    AtkValueType.String or AtkValueType.ConstString or AtkValueType.ManagedString
        => value->String.ToString(),
    _ => string.Empty,
};
```

### Idle checks

Before:

```csharp
if (clientState.IsClientIdle())
    StartAutomation();
```

After:

```csharp
if (!clientState.IsLoggedIn)
    return;

if (condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.Occupied])
    return;

StartAutomation();
```

### Texture readback

Before:

```csharp
var image = await readback.GetRawImageAsync(textureWrap, leaveWrapOpen: true);
```

After:

```csharp
var allImages = await readback.GetAllRawImagesAsync(textureWrap, leaveWrapOpen: true);

foreach (var image in allImages.Images)
{
    var spec = image.Specification;
    var bytes = image.RawData;
    // Handle pitch, mip, and array-slice assumptions explicitly.
}
```

### Unlock gates

Before:

```csharp
var unlocked = questRow.RowId == 0 || questState.IsQuestComplete(questRow.RowId);
```

After:

```csharp
var unlocked = unlockState.IsUnlockLinkUnlocked(unlockLink, minimumQuestSequence);
```

## Retest Checklist

- Rebuild after replacing stale FFXIVClientStructs aliases.
- Load and unload plugins that register addon or agent lifecycle listeners.
- Enable and disable hook-heavy features while watching HookVerifier logs.
- Smoke texture preview/readback features.
- Smoke automation that depends on idle-state assumptions.
- Smoke unlock-gated features.
- Test plugin update/install replacement behavior.

## Current Conclusion

This release is a runtime and native-structure review update, not a new SDK package update. The highest-value code cleanup is stale FFXIVClientStructs alias usage, followed by lifecycle idempotency, hook validation, texture readback assumptions, unlock-helper adoption, and idle-gating smoke.
