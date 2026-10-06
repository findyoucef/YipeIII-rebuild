namespace Yipe.Core;

public enum ShopItemKind { Consumable, Equipment }
public readonly record struct ShopStockItem(string Name, int Price, ShopItemKind Kind, int Type, int Tier);
public enum PurchaseStatus { Purchased, InsufficientGold, InventoryFull, AlreadyEquipped }
public readonly record struct PurchaseResult(PurchaseStatus Status, int InventorySlot = 0);
public enum EquipmentKind { Weapon, Armor }
public readonly record struct EquipmentShopItem(int Rank, int Price);
public enum EquipmentTransactionStatus { Purchased, Sold, InsufficientGold, MustSellCurrent, NothingToSell }
public readonly record struct HealingService(string Name, int Price);
public enum ServiceStatus { Completed, InsufficientGold, AlreadyFull }
public readonly record struct ServiceResult(ServiceStatus Status, int Amount = 0);
public readonly record struct SpellOffer(int Index, string Name, string Description, int Price, int RequiredLevel);
public enum SpellLearningStatus { Learned, Unavailable, InsufficientGold }
public readonly record struct AbilityOffer(SpecialAbility Ability, string Name, string Description, int Price);
public enum AbilityTrainingStatus { Learned, Unavailable, AlreadyTrained, InsufficientGold }

/// <summary>Original shop catalogs and transactions, independent of their overlay presentation.</summary>
public static class Shops
{
    // Four catalogs transcribed from src/shop.cpp in Maynard, Ervyn, Quizilog, Outback order.
    private static readonly ShopStockItem[][] Stock = [
        [
            new("French Fries", 75, ShopItemKind.Consumable, 1, 0),
            new("Juicy Cheeseburger", 250, ShopItemKind.Consumable, 2, 0),
            new("Strength Potion", 1000, ShopItemKind.Consumable, 6, 0),
            new("Intelligence Potion", 1000, ShopItemKind.Consumable, 8, 0),
            new("Potion of HP Increase", 2000, ShopItemKind.Consumable, 9, 0),
            new("Potion of SP Increase", 2500, ShopItemKind.Consumable, 10, 0),
            new("Return to Surface Potion", 250, ShopItemKind.Consumable, 5, 0),
            new("Wet Mittens", 50, ShopItemKind.Equipment, 2, 1),
            new("Cheap Ring", 100, ShopItemKind.Equipment, 3, 1),
            new("Sandals", 50, ShopItemKind.Equipment, 4, 1),
            new("Plain Necklace", 75, ShopItemKind.Equipment, 7, 1),
        ],
        [
            new("French Fries", 80, ShopItemKind.Consumable, 1, 0),
            new("Juicy Cheeseburger", 275, ShopItemKind.Consumable, 2, 0),
            new("Intelligence Potion", 1000, ShopItemKind.Consumable, 8, 0),
            new("Dexterity Potion", 900, ShopItemKind.Consumable, 7, 0),
            new("Potion of HP Increase", 2100, ShopItemKind.Consumable, 9, 0),
            new("Stink Potion", 1200, ShopItemKind.Consumable, 4, 0),
            new("Return to Surface Potion", 200, ShopItemKind.Consumable, 5, 0),
            new("Working Gloves", 1500, ShopItemKind.Equipment, 2, 3),
            new("Platinum Ring", 3000, ShopItemKind.Equipment, 3, 3),
            new("Strong Hiking Boots", 2500, ShopItemKind.Equipment, 4, 4),
            new("Gold Medallion", 500, ShopItemKind.Equipment, 7, 2),
        ],
        [
            new("French Fries", 75, ShopItemKind.Consumable, 1, 0),
            new("Juicy Cheeseburger", 245, ShopItemKind.Consumable, 2, 0),
            new("Intelligence Potion", 800, ShopItemKind.Consumable, 8, 0),
            new("Potion of HP Increase", 1900, ShopItemKind.Consumable, 9, 0),
            new("Potion of SP Increase", 2750, ShopItemKind.Consumable, 10, 0),
            new("Potion of SP Healing", 2000, ShopItemKind.Consumable, 3, 0),
            new("Return to Surface Potion", 300, ShopItemKind.Consumable, 5, 0),
            new("Double Horned Helmet", 10000, ShopItemKind.Equipment, 1, 7),
            new("Leather Gloves", 2500, ShopItemKind.Equipment, 2, 4),
            new("Protection Ring", 6000, ShopItemKind.Equipment, 3, 5),
            new("Speedy Boots", 8000, ShopItemKind.Equipment, 4, 6),
        ],
        [
            new("French Fries", 70, ShopItemKind.Consumable, 1, 0),
            new("Juicy Cheeseburger", 240, ShopItemKind.Consumable, 2, 0),
            new("Strength Potion", 900, ShopItemKind.Consumable, 6, 0),
            new("Dexterity Potion", 1000, ShopItemKind.Consumable, 7, 0),
            new("Potion of SP Increase", 2600, ShopItemKind.Consumable, 10, 0),
            new("Stink Potion", 1000, ShopItemKind.Consumable, 4, 0),
            new("Return to Surface Potion", 350, ShopItemKind.Consumable, 5, 0),
            new("Shower Cap", 500, ShopItemKind.Equipment, 1, 2),
            new("Wet Mittens", 25, ShopItemKind.Equipment, 2, 1),
            new("Aluminum Ring", 1000, ShopItemKind.Equipment, 3, 2),
            new("Fire Amulet", 5000, ShopItemKind.Equipment, 7, 5),
        ],
    ];
    private static readonly int[] WeaponPrices = [0, 0, 50, 110, 220, 350, 500, 700, 900, 1100, 1400, 1700, 2000,
    2400, 2800, 3300, 3800, 4300, 4900, 6600, 7800, 9500, 11000,
    12500, 14250, 16150, 18100, 20100, 22000, 24000, 26500, 30000,
    32700, 35000, 40000, 44000, 48500, 53000, 55000];
    private static readonly int[] ArmorPrices = [0, 0, 40, 75, 150, 300, 425, 600, 800, 1100, 1300, 1444, 1575,
    2000, 2500, 3300, 3800, 4300, 4900, 6600, 8000, 12500, 13000,
    14000, 15000, 16500, 18000, 19500, 21000, 22500, 24250];
    private static readonly int[] SequentialStock = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
    private static readonly int[] TownWeapons = [2, 3, 4, 5, 6, 7, 8, 9, 10, 13, 16];
    private static readonly int[] CaveWeapons = [11, 12, 14, 15, 17, 18, 20, 22, 23, 24, 25];
    private static readonly int[] RemoteWeapons = [19, 21, 26, 27, 28, 29, 30, 31, 33, 35, 36];
    private static readonly int[] TownArmor = [2, 3, 4, 5, 6, 7, 8, 10, 12, 13, 15];
    private static readonly int[] CaveArmor = [9, 11, 14, 16, 17, 18, 20, 22, 23, 25, 28];

    public static bool EnterMiscellaneous(Player p)
    {
        var increase = p.MiscellaneousShopStage == 0 && (long)p.Strength + p.Dexterity + p.Intelligence > 60L - p.Difficulty;
        if (increase) p.MiscellaneousShopStage = 1;
        if (p.MiscellaneousShopStage == 1 && p.Level > 25) p.MiscellaneousShopStage = 2;
        return increase;
    }
    public static void LeaveMiscellaneous(Player p) { if (p.MiscellaneousShopStage == 3) p.MiscellaneousShopStage = 4; }

    public static ShopStockItem[] MiscellaneousStock(Player p, int mapId)
    {
        var catalog = mapId switch { 17 => 0, 27 => 1, 8 => 2, _ => 3 };
        var stock = (ShopStockItem[])Stock[catalog].Clone();
        if (catalog == 3 && (p.MiscellaneousShopStage == 3 || p.MiscellaneousShopStage == 2 && !p.HasRainbowKey))
            stock[8] = stock[8] with { Name = p.MiscellaneousShopStage == 2 ? "Rainbow Key" : stock[8].Name,
                Type = 8, Tier = 5, Price = (p.Difficulty + 6) * 500 };
        if (p.MiscellaneousShopStage > 0)
            for (var i = 0; i < stock.Length; i++)
                if (stock[i].Kind == ShopItemKind.Consumable && stock[i].Type is >= 6 and <= 8)
                    stock[i] = stock[i] with { Price = stock[i].Price + 1500 };
        return stock;
    }

    public static PurchaseResult Purchase(Player p, ShopStockItem item)
    {
        if (p.Gold < item.Price) return new(PurchaseStatus.InsufficientGold);
        if (item.Kind == ShopItemKind.Consumable)
        {
            var slot = Array.FindIndex(p.Inventory, 0, Player.CarriedInventorySize, value => value == 0);
            if (slot < 0) return new(PurchaseStatus.InventoryFull);
            p.Gold -= item.Price; p.Inventory[slot] = unchecked((ushort)item.Type);
            return new(PurchaseStatus.Purchased, slot);
        }
        if (item.Type == 8)
        {
            if (p.HasRainbowKey) return new(PurchaseStatus.AlreadyEquipped);
            p.HasRainbowKey = true; p.MiscellaneousShopStage = 3; p.Gold -= item.Price;
            return new(PurchaseStatus.Purchased);
        }
        var equipped = item.Type switch
        {
            1 => p.HelmetRank, 2 => p.GlovesRank, 3 => p.RingRank, 4 => p.BootsRank, 7 => p.NecklaceRank,
            _ => throw new InvalidOperationException("Unsupported shop equipment type."),
        };
        if (equipped >= item.Tier) return new(PurchaseStatus.AlreadyEquipped);
        p.Gold -= item.Price;
        var tier = unchecked((ushort)item.Tier);
        switch (item.Type)
        {
            case 1: p.HelmetRank = tier; break;
            case 2: p.GlovesRank = tier; break;
            case 3: p.RingRank = tier; break;
            case 4: p.BootsRank = tier; break;
            case 7: p.NecklaceRank = tier; break;
        }
        return new(PurchaseStatus.Purchased);
    }

    public static int EquipmentPrice(EquipmentKind kind, int rank)
    {
        var prices = kind == EquipmentKind.Weapon ? WeaponPrices : ArmorPrices;
        return rank >= 0 && rank < prices.Length ? prices[rank] : 0;
    }
    public static EquipmentShopItem[] EquipmentStock(EquipmentKind kind, int mapId)
    {
        var ranks = kind == EquipmentKind.Weapon
            ? mapId switch { 8 => RemoteWeapons, 17 => TownWeapons, 27 => CaveWeapons, _ => SequentialStock }
            : mapId switch { 17 => TownArmor, 27 => CaveArmor, _ => SequentialStock };
        return ranks.Select(rank => new EquipmentShopItem(rank, EquipmentPrice(kind, rank))).ToArray();
    }
    public static int SaleValue(Player p, EquipmentKind kind) => EquipmentPrice(kind, kind == EquipmentKind.Weapon ? p.WeaponRank : p.ArmorRank) / 2;
    public static bool SaleRequiresConfirmation(Player p, EquipmentKind kind)
    {
        var rank = kind == EquipmentKind.Weapon ? p.WeaponRank : p.ArmorRank;
        return rank != 1 && p.Gold + SaleValue(p, kind) < EquipmentPrice(kind, rank + 1);
    }
    public static EquipmentTransactionStatus PurchaseEquipment(Player p, EquipmentKind kind, int rank)
    {
        if ((kind == EquipmentKind.Weapon ? p.WeaponRank : p.ArmorRank) >= 2) return EquipmentTransactionStatus.MustSellCurrent;
        var price = EquipmentPrice(kind, rank);
        if (price <= 0 || p.Gold < price) return EquipmentTransactionStatus.InsufficientGold;
        p.Gold -= price;
        if (kind == EquipmentKind.Weapon) p.WeaponRank = rank; else p.ArmorRank = rank;
        return EquipmentTransactionStatus.Purchased;
    }
    public static EquipmentTransactionStatus SellEquipment(Player p, EquipmentKind kind)
    {
        if ((kind == EquipmentKind.Weapon ? p.WeaponRank : p.ArmorRank) == 1) return EquipmentTransactionStatus.NothingToSell;
        p.Gold += SaleValue(p, kind);
        if (kind == EquipmentKind.Weapon) p.WeaponRank = 1; else p.ArmorRank = 1;
        return EquipmentTransactionStatus.Sold;
    }

    public static IReadOnlyList<HealingService> HealingServices { get; } = Array.AsReadOnly<HealingService>([
        new("Tasty Apple", 5), new("Vegetables", 25), new("Pumpkin Pie", 60), new("Turkey Dinner", 125)]);
    public static ServiceResult PurchaseHealing(Player p, int service, MsvcRandom random)
    {
        if (service < 0 || service >= HealingServices.Count) throw new ArgumentOutOfRangeException(nameof(service));
        var price = HealingServices[service].Price;
        if (p.Gold < price) return new(ServiceStatus.InsufficientGold);
        if (p.Health >= p.MaximumHealth) return new(ServiceStatus.AlreadyFull);
        var bonus = p.Level >= 18 ? 1 : 0;
        var amount = service switch
        {
            0 => random.Next() % 3 + 3 + bonus, 1 => random.Next() % 9 + 18 + bonus,
            2 => random.Next() % 16 + 42 + bonus, _ => p.MaximumHealth - p.Health,
        };
        p.Health = Math.Min(p.Health + amount, p.MaximumHealth); p.Gold -= price;
        return new(ServiceStatus.Completed, amount);
    }
    public static ServiceResult PurchaseSpellHealing(Player p, bool full, MsvcRandom random)
    {
        var price = full ? 250 : 50;
        if (p.Gold < price) return new(ServiceStatus.InsufficientGold);
        if (p.SpellPoints >= p.MaximumSpellPoints) return new(ServiceStatus.AlreadyFull);
        var amount = full ? p.MaximumSpellPoints - p.SpellPoints
            : random.Next() % 12 + 11 + (p.Level >= 36 ? 10 : p.Level >= 26 ? 6 : p.Level >= 16 ? 3 : 1);
        p.SpellPoints = Math.Min(p.SpellPoints + amount, p.MaximumSpellPoints); p.Gold -= price;
        return new(ServiceStatus.Completed, amount);
    }
    public static SpellOffer? NextSpell(Player p)
    {
        for (var i = 0; i < Spells.Definitions.Count; i++)
            if (p.Spells[i] == 0)
            {
                var spell = Spells.Definitions[i]; var required = Spells.RequiredLevel(spell, p.Difficulty);
                return p.Level < required ? null : new(i, spell.Name, spell.Description, spell.Price, required);
            }
        return null;
    }
    public static SpellLearningStatus LearnNextSpell(Player p)
    {
        if (NextSpell(p) is not { } offer) return SpellLearningStatus.Unavailable;
        // Original handler uses a strict comparison: exact gold is insufficient.
        if (p.Gold <= offer.Price) return SpellLearningStatus.InsufficientGold;
        p.Spells[offer.Index] = 1; p.Gold -= offer.Price;
        return SpellLearningStatus.Learned;
    }
    public static AbilityOffer? AbilityForEnvironment(int environment) => environment switch
    {
        1 => new(SpecialAbility.Backstab, "Backstab", "Sneak around the first monster and stab it. Stronger monsters take more damage.", 150),
        2 => new(SpecialAbility.Frighten, "Frighten", "Scare away the third monster when fighting three monsters at once.", 250),
        4 => new(SpecialAbility.Enrage, "Enrage", "Briefly raise all skills and tend to deal more damage.", 400),
        7 => new(SpecialAbility.Invisibility, "Invisibility", "Briefly avoid monster attacks and gain a better chance to hit.", 400),
        _ => null,
    };
    public static AbilityTrainingStatus TrainAbility(Player p, int environment)
    {
        if (AbilityForEnvironment(environment) is not { } offer) return AbilityTrainingStatus.Unavailable;
        if (p.SpecialAbility == offer.Ability) return AbilityTrainingStatus.AlreadyTrained;
        if (p.Gold < offer.Price) return AbilityTrainingStatus.InsufficientGold;
        p.SpecialAbility = offer.Ability; p.Gold -= offer.Price;
        return AbilityTrainingStatus.Learned;
    }
}
