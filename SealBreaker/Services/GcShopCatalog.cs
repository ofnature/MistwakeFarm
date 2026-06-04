using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using SealBreaker;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SealBreaker.Services;

[Serializable]
public sealed class GcShopCatalogEntry
{
    public int GrandCompanyIndex { get; set; }
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int SealCost { get; set; }
    /// <summary>UI tab: Weapons=0, Armor=1, Materiel=2, Materials=3.</summary>
    public int CategoryTab { get; set; }
    /// <summary>UI rank icons: top=0, middle=1, bottom=2.</summary>
    public int RankTab { get; set; }
    public int RequiredGrandCompanyRank { get; set; }
    /// <summary>0-based row within the exchange list for this tab/rank (fallback).</summary>
    public int ListRow { get; set; }
    /// <summary>Sheet SubCategory callback (not the UI tab index).</summary>
    public int CategoryCallback { get; set; }
}

[Serializable]
internal sealed class GcShopCatalogFile
{
    public int Version { get; set; }
    public List<GcShopCatalogEntry> Entries { get; set; } = [];
}

/// <summary>
/// GC seal shop catalog from GCScripShopCategory / GCScripShopItem (Lumina) or exported JSON.
/// </summary>
internal static class GcShopCatalog
{
    private static readonly string[] CategoryNames = ["Weapons", "Armor", "Materiel", "Materials"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static List<GcShopCatalogEntry> _entries = [];
    private static bool _initialized;

    public const int CatalogFormatVersion = 4;

    public static string CatalogFilePath =>
        Path.Combine(Service.PluginInterface.ConfigDirectory.FullName, "gc_shop_catalog.json");

    public static string BundledCatalogPath
    {
        get
        {
            var dir = Service.PluginInterface.AssemblyLocation.DirectoryName;
            return Path.Combine(dir ?? ".", "Data", "gc_shop_catalog.json");
        }
    }

    public static IReadOnlyList<GcShopCatalogEntry> Entries
    {
        get
        {
            EnsureInitialized();
            return _entries;
        }
    }

    public static string CategoryName(int categoryTab) =>
        categoryTab >= 0 && categoryTab < CategoryNames.Length
            ? CategoryNames[categoryTab]
            : $"Tab {categoryTab}";

    public static void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;
        if (TryLoadFromFile(CatalogFilePath) || TryLoadFromFile(BundledCatalogPath))
        {
            Service.PluginLog.Information($"GcShopCatalog: loaded {_entries.Count} entries from JSON.");
            return;
        }

        RefreshFromGameData();
    }

    public static void RefreshFromGameData()
    {
        _entries = BuildFromGameData();
        _initialized = true;
        Service.PluginLog.Information($"GcShopCatalog: built {_entries.Count} entries from game sheets (v{CatalogFormatVersion}).");
    }

    public static bool ExportToConfigFile()
    {
        if (_entries.Count == 0)
            RefreshFromGameData();

        try
        {
            Directory.CreateDirectory(Service.PluginInterface.ConfigDirectory.FullName);
            File.WriteAllText(CatalogFilePath, JsonSerializer.Serialize(new GcShopCatalogFile
            {
                Version = CatalogFormatVersion,
                Entries = _entries,
            }, JsonOptions));
            return true;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "GcShopCatalog export failed.");
            return false;
        }
    }

    public static IReadOnlyList<GcShopCatalogEntry> GetForGrandCompany(int gcIndex, int? categoryTab = null, string? search = null)
    {
        IEnumerable<GcShopCatalogEntry> query = Entries.Where(e => e.GrandCompanyIndex == gcIndex);

        if (categoryTab is >= 0 and <= 3)
            query = query.Where(e => e.CategoryTab == categoryTab);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e =>
                e.ItemName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(e => e.CategoryTab)
            .ThenBy(e => e.RankTab)
            .ThenBy(e => e.ListRow)
            .ThenBy(e => e.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void ApplyToBuyEntry(GcShopBuyEntry buyEntry, GcShopCatalogEntry catalogEntry)
    {
        buyEntry.ItemName = catalogEntry.ItemName;
        buyEntry.ItemId = catalogEntry.ItemId;
        buyEntry.SealCost = catalogEntry.SealCost;
        buyEntry.CategoryTab = catalogEntry.CategoryTab;
        buyEntry.RankTab = catalogEntry.RankTab;
        buyEntry.ListRow = catalogEntry.ListRow;
    }

    public static GcShopBuyEntry CreateBuyEntryFromCatalog(GcShopCatalogEntry catalogEntry) =>
        new()
        {
            Enabled = true,
            ItemName = catalogEntry.ItemName,
            ItemId = catalogEntry.ItemId,
            SealCost = catalogEntry.SealCost,
            CategoryTab = catalogEntry.CategoryTab,
            RankTab = catalogEntry.RankTab,
            ListRow = catalogEntry.ListRow,
        };

    /// <summary>
    /// 0-based row within a single GCScripShopCategory list (matches in-game list for that tab callback).
    /// </summary>
    public static int ComputeCategoryListRowForPlayer(uint categoryRowId, uint itemId, int sealCost = 0)
    {
        unsafe
        {
            var gcRank = PlayerState.Instance()->GetGrandCompanyRank();
            var itemSheet = Service.DataManager.GetSubrowExcelSheet<GCScripShopItem>();
            if (itemSheet == null)
                return -1;

            foreach (var shopItems in itemSheet)
            {
                if (shopItems.RowId != categoryRowId)
                    continue;

                var row = 0;
                foreach (var shopItem in shopItems)
                {
                    if (shopItem.CostGCSeals <= 0)
                        continue;
                    if (gcRank < shopItem.RequiredGrandCompanyRank.RowId)
                        continue;

                    if (shopItem.Item.Value.RowId == itemId
                        && (sealCost <= 0 || shopItem.CostGCSeals == (uint)sealCost))
                        return row;

                    row++;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// UI list row for the logged-in player: merged items on the same GC tab + rank (catalog export only).
    /// </summary>
    public static int ComputeUiListRowForPlayer(int uiCategoryTab, int rankTab, uint itemId, int sealCost = 0)
    {
        unsafe
        {
            var gcRank = PlayerState.Instance()->GetGrandCompanyRank();
            var gcIdx = PlayerGrandCompanyIndex();
            if (gcIdx < 0)
                return -1;

            var raw = CollectRawShopEntries(filterByPlayerRank: true, gcRank);
            var ordered = raw
                .Where(x => x.Entry.GrandCompanyIndex == gcIdx
                            && x.Entry.CategoryTab == uiCategoryTab
                            && x.Entry.RankTab == rankTab)
                .OrderBy(x => x.SortKey)
                .ThenBy(x => x.Entry.SealCost)
                .ThenBy(x => x.Entry.ItemName, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Entry)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                var entry = ordered[i];
                if (entry.ItemId != itemId)
                    continue;
                if (sealCost > 0 && entry.SealCost != sealCost)
                    continue;
                return i;
            }
        }

        return -1;
    }

    private static int PlayerGrandCompanyIndex()
    {
        unsafe
        {
            return GrandCompanyRowToIndex(PlayerState.Instance()->GrandCompany);
        }
    }

    private static bool TryLoadFromFile(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            if (!TryDeserializeCatalog(json, out var loaded))
                return false;

            _entries = loaded;
            return true;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Warning(ex, $"GcShopCatalog: failed to read {path}");
            return false;
        }
    }

    private static bool TryDeserializeCatalog(string json, out List<GcShopCatalogEntry> entries)
    {
        entries = [];

        try
        {
            var wrapped = JsonSerializer.Deserialize<GcShopCatalogFile>(json, JsonOptions);
            if (wrapped is { Version: CatalogFormatVersion, Entries.Count: > 0 })
            {
                entries = wrapped.Entries;
                return true;
            }
        }
        catch
        {
            // Fall through to legacy array format.
        }

        var legacy = JsonSerializer.Deserialize<List<GcShopCatalogEntry>>(json, JsonOptions);
        if (legacy is not { Count: > 0 })
            return false;

        Service.PluginLog.Information("GcShopCatalog: ignoring outdated JSON export — click Reload from game data.");
        return false;
    }

    private static List<GcShopCatalogEntry> BuildFromGameData() =>
        ReassignListRows(CollectRawShopEntries(filterByPlayerRank: false));

    private static List<(GcShopCatalogEntry Entry, uint SortKey)> CollectRawShopEntries(bool filterByPlayerRank, uint gcRank = 0)
    {
        var raw = new List<(GcShopCatalogEntry Entry, uint SortKey)>();
        var categorySheet = Service.DataManager.GetExcelSheet<GCScripShopCategory>();
        var itemSheet = Service.DataManager.GetSubrowExcelSheet<GCScripShopItem>();
        if (categorySheet == null || itemSheet == null)
            return raw;

        foreach (var category in categorySheet)
        {
            var gcIdx = GrandCompanyRowToIndex(category.GrandCompany.RowId);
            if (gcIdx < 0)
                continue;

            var rankTab = Math.Clamp((int)category.Tier - 1, 0, 2);
            var categoryCallback = (int)category.SubCategory;

            foreach (var shopItems in itemSheet)
            {
                if (shopItems.RowId != category.RowId)
                    continue;

                var subRow = 0;
                foreach (var shopItem in shopItems)
                {
                    if (shopItem.CostGCSeals <= 0)
                        continue;
                    if (filterByPlayerRank && gcRank < shopItem.RequiredGrandCompanyRank.RowId)
                        continue;

                    var item = shopItem.Item.Value;
                    if (item.RowId == 0)
                        continue;

                    var name = item.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    raw.Add((new GcShopCatalogEntry
                    {
                        GrandCompanyIndex = gcIdx,
                        ItemId = item.RowId,
                        ItemName = name,
                        SealCost = (int)shopItem.CostGCSeals,
                        CategoryTab = GcShopCategoryResolver.ResolveUiTab(item, categoryCallback),
                        RankTab = rankTab,
                        RequiredGrandCompanyRank = (int)shopItem.RequiredGrandCompanyRank.RowId,
                        CategoryCallback = categoryCallback,
                    }, category.RowId * 1000u + (uint)subRow));
                    subRow++;
                }
            }
        }

        return raw;
    }

    private static List<GcShopCatalogEntry> ReassignListRows(List<(GcShopCatalogEntry Entry, uint SortKey)> raw)
    {
        var results = new List<GcShopCatalogEntry>(raw.Count);
        foreach (var group in raw.GroupBy(x => (x.Entry.GrandCompanyIndex, x.Entry.CategoryTab, x.Entry.RankTab)))
        {
            var ordered = group
                .OrderBy(x => x.SortKey)
                .ThenBy(x => x.Entry.SealCost)
                .ThenBy(x => x.Entry.ItemName, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Entry)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].ListRow = i;
                results.Add(ordered[i]);
            }
        }

        return results
            .OrderBy(e => e.GrandCompanyIndex)
            .ThenBy(e => e.CategoryTab)
            .ThenBy(e => e.RankTab)
            .ThenBy(e => e.ListRow)
            .ThenBy(e => e.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int GrandCompanyRowToIndex(uint rowId) => rowId switch
    {
        1 => 0, // Maelstrom
        2 => 1, // Twin Adder
        3 => 2, // Immortal Flames
        _ => -1,
    };
}
