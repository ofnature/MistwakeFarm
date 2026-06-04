using Lumina.Excel.Sheets;
using System;

namespace SealBreaker.Services;

/// <summary>
/// Maps GC exchange items to UI tab indices: Weapons=0, Armor=1, Materiel=2, Materials=3.
/// Uses ItemUICategory.OrderMajor (1=Arms, 3=Armor, 4=Accessories, 5/6=Materials, 7=Other/Materiel).
/// GCScripShopCategory.SubCategory is only used for dedicated weapon/materiel rows.
/// </summary>
internal static class GcShopCategoryResolver
{
    public const int TabWeapons = 0;
    public const int TabArmor = 1;
    public const int TabMateriel = 2;
    public const int TabMaterials = 3;

    // ItemUICategory row ids (see saddlebag item category wiki).
    private const uint UiCategoryShields = 17;
    private const uint UiCategoryMateria = 57;

    public static int ResolveUiTab(Item item, int categorySubCategory)
    {
        return categorySubCategory switch
        {
            2 => TabWeapons,
            1 => TabMateriel,
            _ => ResolveFromItemUiCategory(item),
        };
    }

    private static int ResolveFromItemUiCategory(Item item)
    {
        var ui = item.ItemUICategory.Value;
        var uiRowId = item.ItemUICategory.RowId;
        var uiName = ui.Name.ExtractText();
        var itemName = item.Name.ExtractText();

        if (uiRowId == UiCategoryShields
            || uiName.Contains("shield", StringComparison.OrdinalIgnoreCase)
            || item.EquipSlotCategory.RowId == 2)
            return TabWeapons;

        if (uiRowId == UiCategoryMateria
            || IsMaterielUiCategory(uiRowId, uiName, itemName))
            return TabMateriel;

        return ui.OrderMajor switch
        {
            1 => TabWeapons,
            3 or 4 => TabArmor,
            5 or 6 => TabMaterials,
            7 => IsMaterielUiCategory(uiRowId, uiName, itemName) ? TabMateriel : TabMaterials,
            2 => TabMaterials,
            _ => ResolveFallback(item, uiRowId, uiName, itemName),
        };
    }

    private static int ResolveFallback(Item item, uint uiRowId, string uiName, string itemName)
    {
        if (IsMaterielUiCategory(uiRowId, uiName, itemName))
            return TabMateriel;

        var slot = item.EquipSlotCategory.RowId;
        if (slot is 1 or 2)
            return TabWeapons;

        if (slot is 3 or 4 or 5 or 7 or 8 or 9 or 10 or 11 or 12 or 13)
            return TabArmor;

        return TabMaterials;
    }

    private static bool IsMaterielUiCategory(uint uiRowId, string uiName, string itemName)
    {
        if (uiRowId == UiCategoryMateria)
            return true;

        if (!string.IsNullOrWhiteSpace(uiName))
        {
            if (uiName.Contains("dark matter", StringComparison.OrdinalIgnoreCase))
                return true;

            if (uiName.Contains("materia", StringComparison.OrdinalIgnoreCase)
                && !uiName.Contains("material", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return ContainsAny(itemName,
            "dark matter",
            "venture",
            "manual",
            "barding",
            "chocobo issuance",
            "sanction",
            "project catalogue",
            "safety manual");
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(haystack))
            return false;

        foreach (var needle in needles)
        {
            if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
