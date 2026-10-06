namespace Yipe.Core;

/// <summary>Recovered equipment labels, including the original Silly Mode tables.</summary>
public sealed class ItemNames
{
    private static readonly string[] Armor = [
    "T-Shirt", "Robe", "Cloth Armor", "Leather Jacket", "Leather Armor",
    "Padded Armor", "Magician's Robe", "Chainmail", "Scale Armor", "Platemail",
    "Heavy Platemail", "Mithril Mail", "Enhanced Chainmail", "Fighter's Armor",
    "Magic Scale Armor", "Magic Chainmail", "Magic Platemail", "Glow Plate",
    "Spiked Scale Armor", "Spiked Chainmail", "Spiked Platemail", "Royal Knight's Armor",
    "Majestic Chainmail", "Majestic Platemail", "King's Armor", "Yipe! Magic Armor",
    "Armor of Maynard", "Holy Yipe! Armor", "Quizilog Platemail", "Dragonscale Armor",
    ];
    private static readonly string[] Weapons = [
    "Hands", "Small Dagger", "Sling", "Dull Sword", "Axe", "Short Sword",
    "Black Mace", "Shiny Sword", "Flail", "Broadsword", "Spear", "Nunchucks",
    "Mystic Sword", "Two-Handed Axe", "Bow and Arrows", "Longsword",
    "Two-Handed Sword", "Jeweled Dagger", "Magic Mace", "Giant Sword", "Magic Spear",
    "Enhanced Spear", "Crossbow and Bolts", "Sai Swords", "Dragonsword", "Mystic Mace",
    "Magic Yipe! Flail", "Magic Yipe! Sword", "Deathsword", "Maynard Light Sword",
    "Quizilog Mystic Sword", "Fire Battle Axe", "Razor Boomerangs", "King's Sword",
    "Frost Sword", "Yipe! Deathblade", "Manynard Power Sword", "Almighty Longsword",
    ];
    private static readonly string[] SillyArmor = [
    "T-Shirt", "T-Shirt with Pocket", "Tank Top", "Pillow Case", "Collared Shirt",
    "Potato Sack", "Red Turtle Neck", "Sleeveless Sweater", "Soccer Jersey",
    "Hooded Sweatshirt", "Swimsuit", "Muscle Shirt", "Toga Sheet", "Electric Blanket",
    "Rain Jacket", "Life Preserver", "Apron", "Hunt Coat", "Batman Cape", "Wedding Dress",
    "Camouflage Jacket", "Police Uniform", "Suit and Tie", "Birthday Suit",
    "Spaghetti Plate", "Scuba Gear", "Army Uniform", "King's Yipe! Armor",
    "Quizilog Magic Armor", "Magic Yipe! Armor",
    ];
    private static readonly string[] SillyWeapons = [
    "Hands", "Thumbtack", "Nail Clippers", "Pencap", "Toothbrush", "Big Pillow",
    "Mousepad", "Drumstick", "Scissors (Dull)", "Scissors (Sharp)", "Pencil Sharpener",
    "Frying Pan", "Fish Net", "Car Antenna", "Egg Beater", "Dog Leash", "Dustpan",
    "Cookie Jar", "Phonecord Whip", "Ping-Pong Paddle", "Rubber Hose", "Broken Flower Pot",
    "Universal TV Remote", "Pool Cue", "Rusty Horseshoe", "Curtain Rod",
    "Electric Toothbrush", "Cattle Prod", "Pepper Spray", "Gardening Hoe", "Cordless Drill",
    "Insect Spray", "Five-Iron", "Monkey Wrench", "Stun Gun", "Zulu Spear", "Lawn Mower",
    "Bottle Rockets",
    ];
    private static readonly string[] Consumables = ["Empty", "French Fries", "Juicy Cheeseburger", "Potion of SP Healing", "Stink Potion", "Return to Surface Potion", "Strength Potion", "Dexterity Potion", "Intelligence Potion", "Potion of HP Increase", "Potion of SP Increase"];
    public static string Consumable(int id) => id >= 0 && id < Consumables.Length ? Consumables[id] : $"Unknown item {id}";
    private readonly string[] normalArmor;
    private readonly string[] normalWeapons;
    public ItemNames() { normalArmor = Armor; normalWeapons = Weapons; }
    private ItemNames(string[] names) { normalArmor = names[..30]; normalWeapons = names[30..]; }

    // Original Yipe3Items.txt accepts 30 armor names followed by 38 weapon names.
    public static ItemNames Load(TextReader reader)
    {
        var names = new List<string>();
        while (names.Count < 68 && reader.ReadLine() is { } line)
        {
            line = line.Trim();
            if (line.Length == 0 || line.Contains('#')) continue;
            names.Add(line.Length > 22 ? line[..22] : line);
        }
        return names.Count == 68 ? new(names.ToArray()) : new();
    }
    public string Equipment(Player player, EquipmentKind kind, int rank)
    {
        var names = kind == EquipmentKind.Weapon ? player.SillyMode ? SillyWeapons : normalWeapons : player.SillyMode ? SillyArmor : normalArmor;
        return rank < 1 ? "None" : rank <= names.Length ? names[rank - 1] : $"Rank {rank}";
    }

    public static string Accessories(Player player)
    {
        string[] helmets = ["Yipe! Baseball Hat", "Shower Cap", "Sombrero", "Miner's Helmet", "Raiders Hard Hat", "Horned Wooden Helmet", "Double Horned Helmet"];
        string[] gloves = ["Wet Mittens", "Kitchen Gloves", "Working Gloves", "Leather Gloves", "Magical Spiked Gloves"];
        string[] rings = ["Cheap Ring", "Aluminum Ring", "Platinum Ring", "Glowing Ring", "Protection Ring"];
        string[] boots = ["Sandals", "Leather Boots", "Steel Toe Boots", "Strong Hiking Boots", "Combat Boots", "Speedy Boots"];
        string[] amulets = ["Plain Necklace", "Gold Medallion", "Protection Chain", "Glowing Amulet", "Fire Amulet", "Eye-Piercing Amulet"];
        var helmet = unchecked((short)player.HelmetRank); var glove = unchecked((short)player.GlovesRank);
        var ring = unchecked((short)player.RingRank); var boot = unchecked((short)player.BootsRank); var amulet = unchecked((short)player.NecklaceRank);
        static string Row(string label, int rank, string[] names, int ac, int wc) =>
            $"{label}: {(rank < 1 ? "None" : rank <= names.Length ? names[rank - 1] : $"Rank {rank}")} (AC {ac}, WC {wc})";
        return string.Join("\n", Row("Helmet", helmet, helmets, helmet, helmet switch { 5 => 1, 6 => 2, 7 => 4, _ => 0 }),
            Row("Gloves", glove, gloves, glove, glove is 4 or 5 ? 1 : 0), Row("Ring", ring, rings, ring == 5 ? 6 : ring, 0),
            Row("Boots", boot, boots, boot == 6 ? 7 : boot, 0), Row("Amulet", amulet, amulets, amulet, amulet == 5 ? 1 : amulet == 6 ? 2 : 0));
    }
}
