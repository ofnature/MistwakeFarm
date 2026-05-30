using Dalamud.Interface.Windowing;
using Dalamud.Interface;
using MistwakeFarm.Services;
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace MistwakeFarm.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private static readonly Vector4 ColGreen  = new(0.2f, 0.9f, 0.2f, 1f);
    private static readonly Vector4 ColRed    = new(0.9f, 0.2f, 0.2f, 1f);
    private static readonly Vector4 ColYellow = new(0.9f, 0.9f, 0.2f, 1f);
    private static readonly Vector4 ColGray   = new(0.6f, 0.6f, 0.6f, 1f);

    private string _newItemIdInput = string.Empty;

    public MainWindow() : base("Mistwake Farm##main")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 520),
            MaximumSize = new Vector2(600, 800),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        var cfg  = Plugin.Config;
        var ctrl = Plugin.Controller;

        DrawStatusPanel(ctrl);
        ImGui.Separator();
        DrawStatsPanel(ctrl);
        ImGui.Separator();
        DrawConfigPanel(cfg, ctrl);
        ImGui.Separator();
        DrawControlButtons(ctrl);
    }

    private static void DrawStatusPanel(FarmController ctrl)
    {
        ImGui.TextColored(ColGray, "Status  ");
        ImGui.SameLine();

        var (label, col) = ctrl.State switch
        {
            FarmController.FarmState.Idle  => ("Idle",    ColGray),
            FarmController.FarmState.Error => ("Error",   ColRed),
            _                              => ("Running", ColGreen),
        };
        ImGui.TextColored(col, label);
        ImGui.TextWrapped(ctrl.StatusMessage);

        if (ctrl.LastError != null)
            ImGui.TextColored(ColRed, $"Last error: {ctrl.LastError}");
    }

    private static void DrawStatsPanel(FarmController ctrl)
    {
        ImGui.TextColored(ColGray, "Statistics");

        var elapsed = ctrl.IsRunning
            ? DateTime.Now - ctrl.StartTime
            : TimeSpan.Zero;

        ImGui.Columns(2, "stats", false);
        ImGui.Text("Cycles:");       ImGui.NextColumn(); ImGui.Text($"{ctrl.TotalCycles}");      ImGui.NextColumn();
        ImGui.Text("Runs:");         ImGui.NextColumn(); ImGui.Text($"{ctrl.TotalRuns}");        ImGui.NextColumn();
        ImGui.Text("Seals earned:"); ImGui.NextColumn(); ImGui.Text($"{ctrl.TotalSeals:N0}");   ImGui.NextColumn();
        ImGui.Text("Duckbones:");    ImGui.NextColumn(); ImGui.Text($"{ctrl.TotalDuckbones}");  ImGui.NextColumn();
        ImGui.Text("Runtime:");      ImGui.NextColumn(); ImGui.Text($"{elapsed:hh\\:mm\\:ss}"); ImGui.NextColumn();
        ImGui.Columns(1);
    }

    private void DrawConfigPanel(Configuration cfg, FarmController ctrl)
    {
        ImGui.TextColored(ColGray, "Configuration");

        var runs = cfg.RunsPerCycle;
        if (ImGui.SliderInt("Runs per cycle", ref runs, 1, 50))
        { cfg.RunsPerCycle = runs; cfg.Save(); }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("How many Mistwake runs before heading to the Grand Company.");

        var gcItems = new[] { "Maelstrom (Limsa)", "Order of the Twin Adder (Gridania)", "Immortal Flames (Ul'dah)" };
        var gcIdx = cfg.GrandCompanyIndex;
        if (ImGui.Combo("Grand Company", ref gcIdx, gcItems, gcItems.Length))
        { cfg.GrandCompanyIndex = gcIdx; cfg.Save(); }

        var sealCap = cfg.SealCap;
        if (ImGui.SliderInt("Seal cap", ref sealCap, 10000, 90000))
        { cfg.SealCap = sealCap; cfg.Save(); }

        var sealRes = cfg.SealReserve;
        if (ImGui.SliderInt("Seal reserve", ref sealRes, 0, 10000))
        { cfg.SealReserve = sealRes; cfg.Save(); }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Stop buying Duckbones when seals drop to this amount.");

        var shopRow = cfg.DuckboneShopRow;
        if (ImGui.InputInt("Duckbone shop row", ref shopRow))
        { cfg.DuckboneShopRow = Math.Max(0, shopRow); cfg.Save(); }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("0-based row index of Duckbones in the GC Shop list.");

        var echo = cfg.EchoToChat;
        if (ImGui.Checkbox("Echo log to chat", ref echo))
        { cfg.EchoToChat = echo; cfg.Save(); }

        ImGui.Spacing();
        DrawFilterPanel(cfg);
    }

    private void DrawFilterPanel(Configuration cfg)
    {
        ImGui.TextColored(ColGray, "Item Filter");

        var modeItems = new[] { "Off (turn in everything)", "Blacklist (protect listed IDs)", "Whitelist (only deliver listed IDs)" };
        var mode = cfg.ListMode;
        if (ImGui.Combo("List mode", ref mode, modeItems, modeItems.Length))
        { cfg.ListMode = mode; cfg.Save(); }

        if (cfg.ListMode == 0)
        {
            ImGui.TextColored(ColGray, "  All items will be turned in.");
            return;
        }

        var modeLabel = cfg.ListMode == 1
            ? "Protected item IDs (never turn in):"
            : "Whitelist item IDs (only turn in):";
        ImGui.TextColored(ColYellow, modeLabel);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Find item IDs: hover item in inventory, run /xldata items");

        uint? toRemove = null;
        foreach (var id in cfg.FilteredItemIds)
        {
            ImGui.BulletText($"{id}");
            ImGui.SameLine();
            ImGui.PushID((int)id);
            if (ImGui.SmallButton("X")) toRemove = id;
            ImGui.PopID();
        }

        if (toRemove.HasValue)
        { cfg.FilteredItemIds.Remove(toRemove.Value); cfg.Save(); }

        ImGui.SetNextItemWidth(120);
        ImGui.InputText("##newid", ref _newItemIdInput, 16);
        ImGui.SameLine();
        if (ImGui.Button("Add ID"))
        {
            if (uint.TryParse(_newItemIdInput.Trim(), out var newId) && newId > 0)
            {
                if (!cfg.FilteredItemIds.Contains(newId))
                { cfg.FilteredItemIds.Add(newId); cfg.Save(); }
                _newItemIdInput = string.Empty;
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Enter an item ID number and click Add.");
    }

    private static void DrawControlButtons(FarmController ctrl)
    {
        if (!IpcManager.AutoDutyAvailable)
            ImGui.TextColored(ColRed, "! AutoDuty not detected");
        if (!IpcManager.VnavAvailable)
            ImGui.TextColored(ColRed, "! vnavmesh not detected");
        if (!IpcManager.LifestreamAvailable)
            ImGui.TextColored(ColRed, "! Lifestream not detected");

        ImGui.Spacing();

        var allReady = IpcManager.AutoDutyAvailable
                    && IpcManager.VnavAvailable
                    && IpcManager.LifestreamAvailable;

        if (ctrl.IsRunning)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ColRed);
            if (ImGui.Button("Stop##farm", new Vector2(120, 30))) ctrl.Stop();
            ImGui.PopStyleColor();
        }
        else
        {
            if (!allReady) ImGui.BeginDisabled();
            ImGui.PushStyleColor(ImGuiCol.Button, ColGreen);
            if (ImGui.Button("Start##farm", new Vector2(120, 30))) ctrl.Start();
            ImGui.PopStyleColor();
            if (!allReady) ImGui.EndDisabled();
        }

        if (ctrl.State == FarmController.FarmState.Error)
        {
            ImGui.SameLine();
            if (ImGui.Button("Clear Error", new Vector2(120, 30))) ctrl.Stop();
        }
    }
}
