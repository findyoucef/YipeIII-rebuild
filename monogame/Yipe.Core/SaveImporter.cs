using System.Text;
using System.Text.Json;

namespace Yipe.Core;

/// <summary>Explicit, read-only conversion to .y3m bytes; never enters a map or advances RNG.</summary>
public static class SaveImporter
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static byte[] Convert(byte[] bytes, string extension, MapCatalog catalog)
    {
        if (bytes.Length > GameSaveCodec.MaximumFileSize) throw new InvalidDataException("The import is too large.");
        try
        {
            return extension.ToLowerInvariant() switch
            {
                ".json" => Preview(bytes, catalog),
                ".y3r" => Native(bytes, catalog),
                ".yp3" => Original(bytes, catalog),
                _ => throw new InvalidDataException("Import requires a preview .json, C++ .y3r or original .yp3 file."),
            };
        }
        catch (Exception error) when (error is EndOfStreamException or DecoderFallbackException
            or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("The import is truncated or contains invalid data.", error);
        }
    }

    private static byte[] Preview(byte[] bytes, MapCatalog catalog)
    {
        if (bytes.Length > 4096) throw new InvalidDataException("The preview save is too large.");
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var fields = root.EnumerateObject().Select(p => p.Name).ToArray();
        if (fields.Length != 4 || !fields.Order().SequenceEqual(new[] { "MapId", "Version", "X", "Y" })
            || root.GetProperty("Version").GetInt32() != 1 || root.GetProperty("MapId").GetInt32() != 17)
            throw new InvalidDataException("Unsupported preview-save schema or version.");
        var position = new MapPosition(root.GetProperty("X").GetInt32(), root.GetProperty("Y").GetInt32());
        var map = new TownMap(catalog.CreateMap(17) ?? throw new InvalidDataException("Maynard is unavailable."));
        if (!map.CanResume(position)) throw new InvalidDataException("The preview position is not walkable.");
        // The preview never stored a character. Create the documented default, explicitly.
        return GameSaveCodec.EncodeState(new Player(), new() { Position = position }, 1, [], [], catalog);
    }

    private static byte[] Native(byte[] bytes, MapCatalog catalog)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var r = new BinaryReader(stream, Utf8);
        if (!r.ReadBytes(4).AsSpan().SequenceEqual("Y3RS"u8)) throw new InvalidDataException("Not a C++ game save.");
        if (r.ReadUInt16() != 20)
            throw new InvalidDataException("Import supports C++ save version 20. Open and re-save older versions in the supplied C++ rebuild first.");
        if (r.ReadUInt32() != bytes.Length - 14) throw new InvalidDataException("Invalid C++ save length.");
        var expected = r.ReadUInt32();
        uint crc = uint.MaxValue;
        foreach (var value in bytes.AsSpan(14))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }
        if (~crc != expected) throw new InvalidDataException("The C++ save checksum failed.");
        var p = new Player { Name = Utf8.GetString(r.ReadBytes(r.ReadUInt16())) };
        p.Dexterity = r.ReadInt32(); p.Intelligence = r.ReadInt32(); p.Strength = r.ReadInt32();
        p.Health = r.ReadInt32(); p.MaximumHealth = r.ReadInt32();
        p.SpellPoints = r.ReadInt32(); p.MaximumSpellPoints = r.ReadInt32(); p.Level = r.ReadInt32();
        for (var i = 0; i < p.Inventory.Length; i++) p.Inventory[i] = r.ReadUInt16();
        p.AlternateMapSet = r.ReadBoolean(); p.HasBonusHealingRing = r.ReadBoolean();
        p.CurrentMapId = r.ReadInt32(); p.StatCapMode = r.ReadInt32(); p.SurfaceReturnMapId = r.ReadInt16();
        p.RandomEncounterThreshold = r.ReadUInt16(); p.SavedRandomEncounterThreshold = r.ReadUInt16();
        p.MonsterRepellentSteps = r.ReadUInt16(); p.PendingRelocation = r.ReadUInt16();
        p.Experience = r.ReadInt32(); p.Gold = r.ReadInt32(); p.WeaponRank = r.ReadInt32(); p.ArmorRank = r.ReadInt32();
        p.BootsRank = r.ReadUInt16(); p.GlovesRank = r.ReadUInt16(); p.HelmetRank = r.ReadUInt16();
        p.RingRank = r.ReadUInt16(); p.NecklaceRank = r.ReadUInt16();
        p.Difficulty = r.ReadInt32(); p.CombatStyle = r.ReadInt32();
        p.EquipmentUpgradeCount = r.ReadInt32(); p.LootRollPenalty = r.ReadInt32();
        var l = new WorldLocation
        {
            Position = new(r.ReadInt32(), r.ReadInt32()), RegionX = r.ReadInt32(), RegionY = r.ReadInt32(),
            MapLayer = r.ReadInt32(), AlternateMapSet = r.ReadBoolean(), MapId = r.ReadInt32(),
        };
        r.ReadBoolean(); // Redundant pre-v9 Giant Beetle flag; v20 uses the placement list.
        var random = r.ReadUInt32();
        p.AbilityPoints = r.ReadInt32(); p.MaximumAbilityPoints = r.ReadInt32();
        var ability = r.ReadInt32();
        if (ability is < 0 or > 4) throw new InvalidDataException("Invalid C++ special ability.");
        p.SpecialAbility = (SpecialAbility)ability; p.SpecialAbilityDuration = r.ReadInt32();
        p.EnrageBaseDexterity = r.ReadInt32(); p.EnrageBaseIntelligence = r.ReadInt32(); p.EnrageBaseStrength = r.ReadInt32();
        p.RestorationUses = r.ReadInt32(); p.HasWaterproofShoeSpray = r.ReadBoolean();
        p.HasRainbowKey = r.ReadBoolean(); p.HasUnderworldKey = r.ReadBoolean(); p.HasTreeClimbingShoes = r.ReadBoolean();
        var count = r.ReadUInt32();
        if (count > (catalog.Ids.Count + 3) * Map.TileCount || count > (stream.Length - stream.Position) / 14)
            throw new InvalidDataException("Invalid C++ map-change count.");
        var changes = new MapTileChange[count];
        for (var i = 0; i < changes.Length; i++)
            changes[i] = new(r.ReadInt32(), new(r.ReadInt32(), r.ReadInt32()), r.ReadUInt16());
        for (var i = 0; i < p.Spells.Length; i++) p.Spells[i] = r.ReadUInt16();
        p.QuestScore = r.ReadInt32(); p.HasLuckyRabbitFoot = r.ReadBoolean();
        for (var i = 0; i < p.QuestFlags.Length; i++) p.QuestFlags[i] = r.ReadBoolean();
        for (var i = 0; i < p.InventoryValues.Length; i++) p.InventoryValues[i] = r.ReadUInt16();
        p.QuestProgressTotal = r.ReadInt32(); p.GatekeeperState = r.ReadUInt16();
        p.StampsFound = r.ReadUInt16(); p.PrincessesFound = r.ReadUInt16(); p.PrincessMysteryUses = r.ReadUInt16();
        count = r.ReadUInt32();
        if (count > 186) throw new InvalidDataException("Invalid C++ defeated-placement count.");
        var defeated = new ushort[count];
        for (var i = 0; i < defeated.Length; i++) defeated[i] = r.ReadUInt16();
        p.SurfaceReturnRegionX = r.ReadInt16(); p.SurfaceReturnRegionY = r.ReadInt16();
        for (var i = 0; i < p.EncounteredMonsterTypes.Length; i++) p.EncounteredMonsterTypes[i] = r.ReadBoolean();
        p.DeathCount = r.ReadInt32(); p.HellGatekeeperState = (HellGatekeeperState)r.ReadInt16();
        p.EnteredHellThroughSecretPassage = r.ReadBoolean(); p.BridgeTreasureAvailable = r.ReadBoolean();
        p.HealingWaterAvailable = r.ReadBoolean(); p.CaveCoinsCollected = r.ReadBoolean(); p.SnowSearchCount = r.ReadUInt16();
        p.HasDefeatedWormoid = r.ReadBoolean();
        for (var i = 0; i < p.MonsterDefeats.Length; i++) p.MonsterDefeats[i] = r.ReadInt32();
        p.MonsterDefeatHistoryComplete = r.ReadBoolean(); p.Specialty = r.ReadInt32();
        var silly = r.ReadByte();
        if (silly > 1) throw new InvalidDataException("Invalid C++ Silly Mode flag.");
        p.SillyMode = silly != 0;
        for (var i = 0; i < p.OpenedChests.Length; i++) p.OpenedChests[i] = r.ReadByte();
        p.MiscellaneousShopStage = r.ReadUInt16(); p.Value06a4 = r.ReadUInt16();
        if (stream.Position != stream.Length) throw new InvalidDataException("Extra C++ save data.");
        return GameSaveCodec.EncodeState(p, l, random, changes, defeated, catalog);
    }

    private static byte[] Original(byte[] bytes, MapCatalog catalog)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var r = new BinaryReader(stream);
        uint length = r.ReadByte();
        if (length == 255)
        {
            length = r.ReadUInt16();
            if (length == 65534) throw new InvalidDataException("Unicode CString .yp3 files are unsupported by the reference reader.");
            if (length == 65535) length = r.ReadUInt32();
        }
        if (length > 65535 || stream.Length - stream.Position - length != 5064)
            throw new InvalidDataException("Unsupported original save size or name framing.");
        // Original names are Windows ANSI, not UTF-8. Use a fixed code page across hosts.
        var ansi = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;
        var p = new Player { Name = ansi.GetString(r.ReadBytes((int)length)) };
        p.Dexterity = r.ReadInt32(); p.Intelligence = r.ReadInt32(); p.Strength = r.ReadInt32();
        p.Health = r.ReadInt32(); p.MaximumHealth = r.ReadInt32(); p.SpellPoints = r.ReadInt32();
        p.MaximumSpellPoints = r.ReadInt32(); p.AbilityPoints = r.ReadInt32(); p.MaximumAbilityPoints = r.ReadInt32();
        p.Level = r.ReadInt32(); p.QuestScore = r.ReadInt32(); p.WeaponRank = r.ReadInt32(); p.ArmorRank = r.ReadInt32();
        var validation = r.ReadInt32();
        p.BootsRank = r.ReadUInt16(); p.GlovesRank = r.ReadUInt16(); p.HelmetRank = r.ReadUInt16();
        p.RingRank = r.ReadUInt16(); p.NecklaceRank = r.ReadUInt16(); p.DeathCount = r.ReadInt32();
        r.ReadUInt16(); p.RestorationUses = r.ReadInt32(); r.ReadUInt16(); p.Difficulty = r.ReadInt16();
        var ability = r.ReadInt32();
        if (ability is < 0 or > 4) throw new InvalidDataException("Invalid original special ability.");
        p.SpecialAbility = (SpecialAbility)ability;
        r.ReadInt32(); r.ReadInt32();
        p.BridgeTreasureAvailable = Flag(); p.HealingWaterAvailable = Flag(); p.AlternateMapSet = Flag();
        p.HasLuckyRabbitFoot = Flag(); p.HasWaterproofShoeSpray = Flag(); r.ReadInt32();
        for (var i = 0; i < 512; i++)
        {
            if (Flag()) p.OpenedChests[i / 4] |= (byte)(1 << (i % 4));
            r.ReadInt32();
        }
        for (var i = 0; i < p.Spells.Length; i++) p.Spells[i] = r.ReadUInt16();
        for (var i = 0; i < 10; i++)
        {
            r.ReadInt32(); var value = r.ReadUInt16();
            if (i == 0) p.HasRainbowKey = value != 0;
            if (i == 1) p.HasUnderworldKey = value != 0;
        }
        for (var i = 0; i < p.Inventory.Length; i++)
        {
            p.Inventory[i] = r.ReadUInt16(); p.InventoryValues[i] = r.ReadUInt16(); p.QuestFlags[i] = Flag();
        }
        var values = new ushort[50];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = r.ReadUInt16(); p.MonsterDefeats[i] = r.ReadInt32(); var encountered = Flag();
            p.QuestProgressTotal = unchecked(p.QuestProgressTotal + p.MonsterDefeats[i]);
            if (i < p.EncounteredMonsterTypes.Length) p.EncounteredMonsterTypes[i] = encountered;
        }
        p.GatekeeperState = values[2]; p.SurfaceReturnMapId = unchecked((short)values[3]);
        p.SurfaceReturnRegionX = unchecked((short)values[4]); p.SurfaceReturnRegionY = unchecked((short)values[5]);
        p.RandomEncounterThreshold = values[6]; p.StampsFound = values[8]; p.PrincessesFound = values[9];
        p.HasTreeClimbingShoes = values[11] == 1; p.EnteredHellThroughSecretPassage = values[14] != 0;
        p.CombatStyle = unchecked((short)values[15]); p.SavedRandomEncounterThreshold = values[16];
        p.MonsterRepellentSteps = values[17]; p.PrincessMysteryUses = values[18];
        p.HellGatekeeperState = (HellGatekeeperState)unchecked((short)values[19]); p.MiscellaneousShopStage = values[20];
        p.PendingRelocation = values[26]; p.CaveCoinsCollected = values[27] != 0; p.SnowSearchCount = values[30];
        p.HasBonusHealingRing = values[31] == 1; p.HasDefeatedWormoid = p.MonsterDefeats[34] != 0; p.Value06a4 = values[35];
        var l = new WorldLocation
        {
            Position = new(r.ReadInt16(), r.ReadInt16()), RegionX = r.ReadInt16(), RegionY = r.ReadInt16(),
            MapLayer = r.ReadInt16(), AlternateMapSet = p.AlternateMapSet,
        };
        l.MapId = l.RegionX - l.RegionY * 6 + 17 + l.MapLayer * 40 + (l.AlternateMapSet ? 98 : 0); p.CurrentMapId = l.MapId;
        var storedGold = r.ReadInt32();
        if (storedGold > int.MaxValue - 203) throw new InvalidDataException("Invalid original gold value.");
        p.Gold = storedGold + 203; p.Experience = r.ReadInt32(); r.ReadInt32();
        var expected = (long)p.Gold / 2 + ((long)p.MaximumHealth - p.Health) * 2
            + p.RingRank + p.HelmetRank + p.BootsRank + p.ArmorRank + p.WeaponRank;
        if (Math.Abs(validation - expected) > 1) throw new InvalidDataException("Original save validation failed.");
        return GameSaveCodec.EncodeState(p, l, 1, [], [], catalog);

        bool Flag() => r.ReadInt32() != 0;
    }
}
