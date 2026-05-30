using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using MistwakeFarm.Services;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace MistwakeFarm.Services;

public sealed class FarmController : IDisposable
{
    private const uint MistwakeContentId = 1314;

    private static readonly string[] GcTpCommand =
        ["Limsa Lominsa Lower Decks", "New Gridania", "Ul'dah - Steps of Nald"];
    private static readonly uint[]    GcZoneId     = [129, 133, 130];
    private static readonly Vector3[] GcOfficerPos =
    [
        new(-67.8f, 21.4f, -18.1f),
        new(-72.3f, -1.0f, -14.1f),
        new(-148.9f, 4.1f, -107.0f),
    ];
    private static readonly Vector3[] GcShopPos =
    [
        new(-72.2f, 21.4f, -14.9f),
        new(-74.5f, -1.0f, -12.0f),
        new(-145.7f, 4.1f, -107.0f),
    ];
    private static readonly string[] GcOfficerName =
        ["Storm Personnel Officer", "Serpent Personnel Officer", "Flame Personnel Officer"];
    private static readonly string[] GcShopName =
        ["Storm Quartermaster", "Serpent Quartermaster", "Flame Quartermaster"];

    public enum FarmState
    {
        Idle, StartDuty, WaitingForDutyStart, WaitingForDutyComplete,
        TeleportToGC, WaitingForZone, NavigateToOfficer,
        OpenExpertDelivery, ProcessDelivery,
        NavigateToShop, OpenGCShop, BuyDuckbones,
        CycleComplete, Error,
    }

    public FarmState State         { get; private set; } = FarmState.Idle;
    public bool      IsRunning     { get; private set; }
    public string    StatusMessage { get; private set; } = "Idle";
    public string?   LastError     { get; private set; }
    public int       TotalCycles    { get; private set; }
    public int       TotalRuns      { get; private set; }
    public int       TotalSeals     { get; private set; }
    public int       TotalDuckbones { get; private set; }
    public DateTime  StartTime      { get; private set; }

    private int   _runsThisCycle;
    private int   _deliveryRow;
    private int   _deliverySkipped;
    private int   _sealsBefore;
    private Task? _currentTask;

    public FarmController()  => Service.Framework.Update += OnFrameworkUpdate;
    public void Dispose()    { Service.Framework.Update -= OnFrameworkUpdate; IpcManager.VnavStop(); }

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true; TotalCycles = 0; TotalRuns = 0; TotalSeals = 0;
        TotalDuckbones = 0; StartTime = DateTime.Now; _runsThisCycle = 0; LastError = null;
        GotoState(FarmState.StartDuty);
        Log("Farm started.");
    }

    public void Stop()
    {
        IsRunning = false;
        IpcManager.VnavStop(); IpcManager.AutoDutyStop();
        GotoState(FarmState.Idle);
        Log("Farm stopped.");
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        if (!IsRunning) return;
        if (_currentTask is { IsCompleted: false }) return;
        if (_currentTask is { IsFaulted: true })
        {
            SetError(_currentTask.Exception?.InnerException?.Message ?? "Unknown task error");
            _currentTask = null; return;
        }
        _currentTask = null;
        try { Tick(); } catch (Exception ex) { SetError(ex.Message); }
    }

    private void Tick()
    {
        var cfg   = Plugin.Config;
        var gcIdx = cfg.GrandCompanyIndex;

        switch (State)
        {
            case FarmState.StartDuty:
                if (!IpcManager.AutoDutyAvailable) { SetError("AutoDuty IPC not available"); return; }
                TotalCycles++; _runsThisCycle = 0;
                Status($"Cycle {TotalCycles}: starting run {_runsThisCycle + 1}/{cfg.RunsPerCycle}");
                IpcManager.AutoDutyRun(MistwakeContentId, 1);
                GotoState(FarmState.WaitingForDutyStart);
                break;

            case FarmState.WaitingForDutyStart:
                if (InDuty()) { Status($"In duty — run {_runsThisCycle + 1}/{cfg.RunsPerCycle}"); GotoState(FarmState.WaitingForDutyComplete); }
                break;

            case FarmState.WaitingForDutyComplete:
                if (!InDuty())
                {
                    _runsThisCycle++; TotalRuns++;
                    Status($"Run {_runsThisCycle}/{cfg.RunsPerCycle} complete (total {TotalRuns})");
                    GotoState(_runsThisCycle >= cfg.RunsPerCycle ? FarmState.TeleportToGC : FarmState.StartDuty);
                }
                break;

            case FarmState.TeleportToGC:
                if (!IpcManager.LifestreamAvailable) { SetError("Lifestream IPC not available"); return; }
                if (Service.ClientState.TerritoryType == GcZoneId[gcIdx]) { GotoState(FarmState.NavigateToOfficer); return; }
                Status($"Teleporting to {GcTpCommand[gcIdx]}...");
                IpcManager.LifestreamExecute(GcTpCommand[gcIdx]);
                GotoState(FarmState.WaitingForZone);
                break;

            case FarmState.WaitingForZone:
                if (Service.ClientState.TerritoryType == GcZoneId[gcIdx]
                    && PlayerPos().HasValue
                    && !IpcManager.LifestreamIsBusy()
                    && !IsBetweenAreas())
                { Status("Arrived at GC city"); GotoState(FarmState.NavigateToOfficer); }
                break;

            case FarmState.NavigateToOfficer:
                if (!IpcManager.VnavAvailable) { SetError("vnavmesh IPC not available"); return; }
                Status($"Navigating to {GcOfficerName[gcIdx]}...");
                _currentTask = NavigateAndInteractAsync(GcOfficerPos[gcIdx], GcOfficerName[gcIdx], FarmState.OpenExpertDelivery);
                break;

            case FarmState.OpenExpertDelivery:  _currentTask = OpenExpertDeliveryAsync(); break;
            case FarmState.ProcessDelivery:      _currentTask = ProcessDeliveryRowAsync(); break;

            case FarmState.NavigateToShop:
                Status($"Navigating to {GcShopName[gcIdx]}...");
                _currentTask = NavigateAndInteractAsync(GcShopPos[gcIdx], GcShopName[gcIdx], FarmState.OpenGCShop);
                break;

            case FarmState.OpenGCShop:   _currentTask = OpenGCShopAsync(); break;
            case FarmState.BuyDuckbones: _currentTask = BuyDuckbonesTickAsync(); break;

            case FarmState.CycleComplete:
                var elapsed = DateTime.Now - StartTime;
                Log($"Cycle {TotalCycles} complete | Runs:{TotalRuns} Seals:{TotalSeals} Duckbones:{TotalDuckbones} Runtime:{elapsed:hh\\:mm\\:ss}");
                GotoState(FarmState.StartDuty);
                break;

            case FarmState.Idle:
            case FarmState.Error:
                break;
        }
    }

    // ── Async steps ───────────────────────────────────────────

    private async Task NavigateAndInteractAsync(Vector3 dest, string npcName, FarmState nextState)
    {
        Status($"Navigating to {npcName}...");
        await IpcManager.VnavMoveToAsync(dest, false);

        var deadline = DateTime.Now.AddSeconds(60);
        while (DateTime.Now < deadline)
        {
            await Task.Delay(200);
            var pos = PlayerPos();
            if (pos.HasValue && Vector3.Distance(pos.Value, dest) <= 5f)
            { IpcManager.VnavStop(); break; }
        }
        IpcManager.VnavStop();
        await Task.Delay(500);

        var npc = FindNpcByName(npcName);
        if (npc == null) { SetError($"Could not find NPC: {npcName}"); return; }
        TargetAndInteract(npc);
        await Task.Delay(1000);
        GotoState(nextState);
    }

    private async Task OpenExpertDeliveryAsync()
    {
        if (!await WaitForAddonAsync("SelectString", 8000)) { SetError("Officer menu did not open"); return; }
        SendCallback("SelectString", true, 1);
        await Task.Delay(800);
        if (!await WaitForAddonAsync("GrandCompanySupplyList", 8000)) { CloseAddonSafe("SelectString"); SetError("Expert Delivery did not open"); return; }
        _deliveryRow = 0; _deliverySkipped = 0; _sealsBefore = GetCurrentSeals();
        Status("Expert Delivery open — processing items...");
        GotoState(FarmState.ProcessDelivery);
    }

    private async Task ProcessDeliveryRowAsync()
    {
        if (GetCurrentSeals() >= Plugin.Config.SealCap - 100)
        { Log("Seal cap reached"); CloseAddonSafe("GrandCompanySupplyList"); CloseAddonSafe("SelectString"); FinishDelivery(); return; }

        if (!IsAddonOpen("GrandCompanySupplyList")) { FinishDelivery(); return; }

        SendCallback("GrandCompanySupplyList", true, 0, _deliveryRow);
        await Task.Delay(600);

        if (IsAddonOpen("SelectYesno"))
        {
            SendCallback("SelectYesno", true, 0);
            await Task.Delay(600);
            Log($"Delivered row {_deliveryRow} | Seals: {GetCurrentSeals()}");
            _deliveryRow = _deliverySkipped;
        }
        else
        {
            // No confirm = list empty
            CloseAddonSafe("GrandCompanySupplyList"); CloseAddonSafe("SelectString"); FinishDelivery();
        }
    }

    private void FinishDelivery()
    {
        var gained = GetCurrentSeals() - _sealsBefore;
        TotalSeals += Math.Max(0, gained);
        Log($"Delivery done | +{gained} seals | Now: {GetCurrentSeals()}");
        GotoState(FarmState.NavigateToShop);
    }

    private async Task OpenGCShopAsync()
    {
        if (await WaitForAddonAsync("SelectString", 8000)) { SendCallback("SelectString", true, 0); await Task.Delay(800); }
        if (!await WaitForAddonAsync("GCShop", 8000)) { CloseAddonSafe("SelectString"); SetError("GCShop did not open"); return; }
        SendCallback("GCShop", true, 0);
        await Task.Delay(500);
        Status("GC Shop open — buying Duckbones...");
        GotoState(FarmState.BuyDuckbones);
    }

    private async Task BuyDuckbonesTickAsync()
    {
        var cfg = Plugin.Config;
        var cur = GetCurrentSeals();

        if (cur - cfg.DuckboneSealCost < cfg.SealReserve)
        { Log($"Seals low ({cur}) — done"); CloseAddonSafe("GCShop"); CloseAddonSafe("SelectString"); GotoState(FarmState.CycleComplete); return; }

        if (!IsAddonOpen("GCShop")) { GotoState(FarmState.CycleComplete); return; }

        SendCallback("GCShop", true, 0, cfg.DuckboneShopRow);
        await Task.Delay(600);

        if (IsAddonOpen("ShopExchangeDialog"))
        {
            var qty = Math.Clamp((cur - cfg.SealReserve) / cfg.DuckboneSealCost, 1, 99);
            SendCallback("ShopExchangeDialog", true, 0, qty, 0);
            await Task.Delay(600);
            TotalDuckbones += qty;
            Log($"Bought {qty} Duckbones | Seals: {GetCurrentSeals()}");
        }
        else if (IsAddonOpen("SelectYesno"))
        {
            SendCallback("SelectYesno", true, 0);
            await Task.Delay(600);
            TotalDuckbones++;
        }
        else if (IsAddonOpen("GCShop"))
        {
            Log("WARN: No buy dialog — check Duckbone Shop Row in config");
            CloseAddonSafe("GCShop"); GotoState(FarmState.CycleComplete);
        }
    }

    // ── Game helpers ──────────────────────────────────────────

    private static bool InDuty()        => Service.Condition[ConditionFlag.BoundByDuty];
    private static bool IsBetweenAreas() => Service.Condition[ConditionFlag.BetweenAreas];

    private static Vector3? PlayerPos()
    {
        var player = Service.ObjectTable.LocalPlayer;
        return player?.Position;
    }

    private static int GetCurrentSeals()
    {
        unsafe
        {
            var id = Plugin.Config.GrandCompanyIndex switch { 0 => 20u, 1 => 21u, 2 => 22u, _ => 20u };
            return (int)InventoryManager.Instance()->GetInventoryItemCount(id);
        }
    }

    private static bool ShouldTurnIn(uint itemId)
    {
        var cfg = Plugin.Config;
        if (cfg.ListMode == 0) return true;
        var inList = cfg.FilteredItemIds.Contains(itemId);
        return cfg.ListMode switch { 1 => !inList, 2 => inList, _ => true };
    }

    private static async Task<bool> WaitForAddonAsync(string name, int timeoutMs)
    {
        var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < deadline)
        {
            if (IsAddonOpen(name)) return true;
            await Task.Delay(100);
        }
        return false;
    }

    private static unsafe bool IsAddonOpen(string name)
    {
        var addon = Service.GameGui.GetAddonByName<AtkUnitBase>(name);
        return addon != null && addon->IsVisible && addon->IsReady;
    }

    private static unsafe void SendCallback(string name, bool update, params object[] args)
    {
        var addon = Service.GameGui.GetAddonByName<AtkUnitBase>(name);
        if (addon == null || !addon->IsVisible) return;

        var values = new AtkValue[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            values[i] = args[i] switch
            {
                int n  => new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int,  Int  = n },
                uint n => new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.UInt, UInt = n },
                bool b => new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Bool, Byte = (byte)(b ? 1 : 0) },
                _      => new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int,  Int  = 0 },
            };
        }
        fixed (AtkValue* ptr = values)
            addon->FireCallback((uint)args.Length, ptr, update);
    }

    private static unsafe void CloseAddonSafe(string name)
    {
        var addon = Service.GameGui.GetAddonByName<AtkUnitBase>(name);
        if (addon == null || !addon->IsVisible) return;
        var v = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int, Int = -1 };
        addon->FireCallback(1, &v, true);
    }

    private static IGameObject? FindNpcByName(string name) =>
        Service.ObjectTable.FirstOrDefault(o =>
            o.ObjectKind == ObjectKind.EventNpc &&
            o.Name.TextValue.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static void TargetAndInteract(IGameObject obj)
    {
        Service.TargetManager.Target = obj;
        unsafe
        {
            FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem
                .Instance()->InteractWithObject(
                    (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)obj.Address);
        }
    }

    private void GotoState(FarmState next) { Log($"State: {State} -> {next}"); State = next; }
    private void Status(string msg)        { StatusMessage = msg; Log(msg); }

    private void SetError(string msg)
    {
        LastError = msg; StatusMessage = $"ERROR: {msg}"; State = FarmState.Error; IsRunning = false;
        Service.PluginLog.Error($"[MistwakeFarm] {msg}");
        Service.ChatGui.PrintError($"[MistwakeFarm] {msg}");
    }

    private static void Log(string msg)
    {
        Service.PluginLog.Information($"[MistwakeFarm] {msg}");
        if (Plugin.Config.EchoToChat) Service.ChatGui.Print($"[MistwakeFarm] {msg}");
    }
}
