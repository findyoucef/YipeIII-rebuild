using System.Security.Cryptography;
using System.Text;
using Yipe.Core;

internal static class ItemNameChecks
{
    public static void Run(Action<bool, string> check, string? path = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 3; mode++)
        {
            using var reader = File.OpenText(Path.Combine(AppContext.BaseDirectory, "Content", "Yipe3Items.txt"));
            var names = mode == 1 ? ItemNames.Load(reader) : new ItemNames();
            var player = new Player { SillyMode = mode == 2 };
            for (var i = 1; i <= 30; i++) trace.Append($"{mode}|A|{i}|{names.Equipment(player, EquipmentKind.Armor, i)}\n");
            for (var i = 1; i <= 38; i++) trace.Append($"{mode}|W|{i}|{names.Equipment(player, EquipmentKind.Weapon, i)}\n");
        }
        if (path is not null) File.WriteAllText(path, trace.ToString());
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trace.ToString()))).ToLowerInvariant();
        check(digest == "0e4fe4123d2eec64d54a106ffaec14cd8115c387a24ac525fb366168c6d57802", "204 C++ equipment names match packaged, embedded fallback and Silly Mode catalogs");
        using var partial = new StringReader("Only one item\n# Ignored\n");
        check(ItemNames.Load(partial).Equipment(new(), EquipmentKind.Weapon, 2) == "Small Dagger", "Partial custom catalog falls back as a whole");
        using var custom = new StringReader("# heading\nignored # comment\n\n" + string.Join("\n", Enumerable.Range(0, 68).Select(i => $"  Custom item number {i:D2} with a long label  ")));
        var loaded = ItemNames.Load(custom);
        check(loaded.Equipment(new(), EquipmentKind.Armor, 1) == "Custom item number 00 "
            && loaded.Equipment(new() { SillyMode = true }, EquipmentKind.Weapon, 2) == "Thumbtack", "Custom labels obey the length limit and Silly Mode retains its own catalog");
        check(loaded.Equipment(new(), EquipmentKind.Weapon, 0) == "None" && loaded.Equipment(new(), EquipmentKind.Armor, 31) == "Rank 31",
            "Unusual imported equipment ranks retain the original fallback labels");
    }
}
