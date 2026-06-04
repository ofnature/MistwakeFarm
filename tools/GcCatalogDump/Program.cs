using Lumina;
using Lumina.Excel.Sheets;
using System.Reflection;

static string FindGamePath()
{
    var candidates = new[]
    {
        Environment.GetEnvironmentVariable("FFXIV_GAME_PATH"),
        @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XIVLauncher", "game"),
    };

    foreach (var path in candidates)
    {
        if (string.IsNullOrWhiteSpace(path))
            continue;
        if (File.Exists(Path.Combine(path, "sqpack", "exd", "root.exl")))
            return path;
        if (File.Exists(Path.Combine(path, "exd", "root.exl")))
            return path;
    }

    throw new InvalidOperationException("Could not find FFXIV game folder with exd/root.exl");
}

var gamePath = FindGamePath();
Console.WriteLine($"Game path: {gamePath}");
var lumina = new GameData(gamePath);

Console.WriteLine("GCScripShopCategory properties:");
foreach (var prop in typeof(GCScripShopCategory).GetProperties(BindingFlags.Public | BindingFlags.Instance))
    Console.WriteLine($"  {prop.Name}");

Console.WriteLine();
Console.WriteLine("GCShopItemCategory properties:");
foreach (var prop in typeof(GCShopItemCategory).GetProperties(BindingFlags.Public | BindingFlags.Instance))
    Console.WriteLine($"  {prop.Name}");

var gcShopCatSheet = lumina.GetExcelSheet<GCShopItemCategory>()!;
Console.WriteLine();
Console.WriteLine("GCShopItemCategory rows:");
foreach (var row in gcShopCatSheet)
{
    var parts = typeof(GCShopItemCategory).GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(p => $"{p.Name}={p.GetValue(row)}");
    Console.WriteLine($"  row {row.RowId}: {string.Join(", ", parts)}");
}

var categorySheet = lumina.GetExcelSheet<GCScripShopCategory>()!;
var itemSheet = lumina.GetSubrowExcelSheet<GCScripShopItem>()!;

Console.WriteLine();
Console.WriteLine("Maelstrom GCScripShopCategory samples:");
foreach (var category in categorySheet.Where(c => c.GrandCompany.RowId == 1))
{
    var first = itemSheet.Where(s => s.RowId == category.RowId).SelectMany(s => s)
        .FirstOrDefault(i => i.CostGCSeals > 0);
    if (first.RowId == 0)
        continue;

    var catParts = typeof(GCScripShopCategory).GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.Name != "RowId")
        .Select(p => $"{p.Name}={p.GetValue(category)}");
    Console.WriteLine($"  cat {category.RowId}: {string.Join(", ", catParts)} | first={first.Item.Value.Name.ExtractText()}");
}
