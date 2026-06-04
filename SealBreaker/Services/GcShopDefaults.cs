using System;
using System.Linq;

namespace SealBreaker.Services;

/// <summary>Hardcoded GC exchange fallbacks when sheet/catalog lookup is unavailable.</summary>
internal static class GcShopDefaults
{
    public const uint DuckboneItemId = 10119;
    public const string DuckboneItemName = "Duck Bones";
    public const int DuckboneCategoryTab = 3;
    public const int DuckboneRankTab = 2;
    public const int DuckboneListRow = 40;
    public const int DuckboneSealCost = 600;

    public static readonly string[] PortTicketItemNames =
    [
        "Maelstrom Aetheryte Ticket",
        "Twin Adder Aetheryte Ticket",
        "Immortal Flames Aetheryte Ticket",
    ];

    public const int PortTicketSealCost = 2000;
    public const int PortTicketCategoryTab = 2;
    public const int PortTicketRankTab = 2;
    public const int PortTicketKeepAmount = 10;
    public const int PortTicketBuyQty = 1;

    public static GcShopBuyEntry CreateDuckboneBuyEntry(int gcIdx)
    {
        GcShopCatalog.EnsureInitialized();

        var catalog = GcShopCatalog.GetForGrandCompany(gcIdx)
            .FirstOrDefault(e => e.ItemId == DuckboneItemId
                                 || e.ItemName.Equals(DuckboneItemName, StringComparison.OrdinalIgnoreCase));
        if (catalog != null)
            return GcShopCatalog.CreateBuyEntryFromCatalog(catalog);

        return new GcShopBuyEntry
        {
            Enabled = true,
            ItemName = DuckboneItemName,
            ItemId = DuckboneItemId,
            CategoryTab = DuckboneCategoryTab,
            RankTab = DuckboneRankTab,
            ListRow = DuckboneListRow,
            SealCost = DuckboneSealCost,
        };
    }

    public static GcShopBuyEntry CreatePortTicketBuyEntry(int gcIdx)
    {
        GcShopCatalog.EnsureInitialized();
        var name = PortTicketItemNames[Math.Clamp(gcIdx, 0, PortTicketItemNames.Length - 1)];

        var catalog = GcShopCatalog.GetForGrandCompany(gcIdx)
            .FirstOrDefault(e => e.ItemName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (catalog != null)
        {
            var entry = GcShopCatalog.CreateBuyEntryFromCatalog(catalog);
            entry.KeepAmount = PortTicketKeepAmount;
            entry.BuyQtyPerPurchase = PortTicketBuyQty;
            return entry;
        }

        return new GcShopBuyEntry
        {
            Enabled = true,
            ItemName = name,
            CategoryTab = PortTicketCategoryTab,
            RankTab = PortTicketRankTab,
            SealCost = PortTicketSealCost,
            BuyQtyPerPurchase = PortTicketBuyQty,
            KeepAmount = PortTicketKeepAmount,
        };
    }
}
