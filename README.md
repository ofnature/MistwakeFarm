# MistwakeFarm

Dalamud plugin that automates the Mistwake gil farming loop:

1. Runs Mistwake via AutoDuty (Trust/Duty Support) for X runs
2. Teleports to your Grand Company via Lifestream
3. Turns in armor loot to Expert Delivery for seals (with blacklist/whitelist filter)
4. Buys Duckbones at the GC shop until seals are spent down to reserve
5. Loops indefinitely until stopped

---

## Requirements

- [AutoDuty](https://github.com/ffxivcode/AutoDuty)
- [vnavmesh](https://github.com/awgil/ffxiv_navmesh)
- [Lifestream](https://github.com/NightmareXIV/Lifestream)

---

## Building

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- XIVLauncher installed (provides Dalamud DLLs)

### Build steps

```bash
git clone https://github.com/ofnature/MistwakeFarm
cd MistwakeFarm
dotnet build MistwakeFarm/MistwakeFarm.csproj
```

The `.csproj` references Dalamud DLLs from:
```
%APPDATA%\XIVLauncher\addon\Hooks\dev\
```
If your XIVLauncher is installed elsewhere, update `DalamudLibPath` in the `.csproj`.

### Installing for development

1. Build in Debug configuration
2. Copy the `bin/Debug/net9.0-windows/` output folder to:
   ```
   %APPDATA%\XIVLauncher\devPlugins\MistwakeFarm\
   ```
3. In-game: `/xlsettings` → Developer Mode on → `/xlplugins` → Dev Plugin Locations → add the folder
4. Enable MistwakeFarm in the plugin list

---

## Usage

- `/mistwake` — Open the plugin window
- Configure settings in the UI (runs per cycle, GC, filter mode, item IDs)
- Click **Start** — requires AutoDuty, vnavmesh, and Lifestream to be loaded
- Click **Stop** at any time to halt cleanly

### Finding Item IDs

Hover any item in your inventory and run `/xldata items` in chat — it prints the item ID. Add it to the filter list in the plugin UI.

### Duckbone Shop Row

Open the GC Shop manually, count rows from 0 (top), and enter that number in the config. If the wrong item is being purchased the plugin will warn you in the log.

---

## Project structure

```
MistwakeFarm/
├── Plugin.cs                  — Entry point, command registration
├── Configuration.cs           — Serialized settings
├── Services/
│   ├── Service.cs             — Dalamud service locator
│   ├── IpcManager.cs          — AutoDuty / vnavmesh / Lifestream IPC wrappers
│   └── FarmController.cs      — State machine (the actual farm logic)
└── Windows/
    └── MainWindow.cs          — ImGui UI
```
