using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using MistwakeFarm.Services;

namespace MistwakeFarm;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // ── Duty ──────────────────────────────────────────────────
    public int RunsPerCycle { get; set; } = 5;

    // ── Grand Company ─────────────────────────────────────────
    /// <summary>0=Maelstrom  1=Twin Adder  2=Immortal Flames</summary>
    public int GrandCompanyIndex { get; set; } = 0;
    public int SealCap           { get; set; } = 90000;
    public int SealReserve       { get; set; } = 1500;

    // ── Item Filter ───────────────────────────────────────────
    /// <summary>0=Off  1=Blacklist  2=Whitelist</summary>
    public int ListMode { get; set; } = 0;
    public List<uint> FilteredItemIds { get; set; } = new();

    // ── Shop ──────────────────────────────────────────────────
    public int DuckboneShopRow  { get; set; } = 0;
    public int DuckboneSealCost { get; set; } = 200;

    // ── Misc ──────────────────────────────────────────────────
    public bool EchoToChat { get; set; } = false;

    public void Save() =>
        Service.PluginInterface.SavePluginConfig(this);
}
