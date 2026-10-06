using System.Security.Cryptography;
using System.Text;

namespace Yipe.Core;

/// <summary>Version 1 MonoGame save bytes. No filesystem, rendering or runtime reflection.</summary>
public static class GameSaveCodec
{
    public const int MaximumFileSize = 4 * 1024 * 1024;
    private const int HeaderSize = 8 + 2 + 4 + 32;
    private const ushort Version = 1;
    private const int MonsterPlacementCount = 186; // include/yipe/encounter.hpp
    private static readonly byte[] Magic = "YIPE3MG\0"u8.ToArray();
    private static readonly UTF8Encoding Utf8 = new(false, true);

    // Runs synchronously on the game thread: returned bytes own the complete snapshot.
    public static byte[] Encode(GameSession session, MapCatalog catalog)
    {
        if (session.CurrentMap.Id != session.Location.MapId)
            throw new InvalidDataException("The session's current map and location disagree.");
        return EncodeState(session.Player, session.Location, session.Random.State, session.TileChanges,
            session.DefeatedMonsterPlacements, catalog);
    }

    // Import writes the saved state without entering a map or consuming RNG. Normal
    // Decode performs the original startup sequence exactly once when the file is opened.
    internal static byte[] EncodeState(Player player, WorldLocation location, uint randomState,
        IReadOnlyList<MapTileChange> changes, IReadOnlyList<ushort> defeated, MapCatalog catalog)
    {
        Validate(player, location, changes, defeated, catalog);
        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, Utf8, leaveOpen: true))
        {
            WritePlayer(writer, player);
            var l = location;
            writer.Write(l.Position.X); writer.Write(l.Position.Y);
            writer.Write(l.RegionX); writer.Write(l.RegionY); writer.Write(l.MapLayer);
            writer.Write(l.AlternateMapSet); writer.Write(l.MapId);
            writer.Write(randomState);
            writer.Write(changes.Count);
            foreach (var change in changes)
            {
                writer.Write(change.MapId); writer.Write(change.Position.X); writer.Write(change.Position.Y);
                writer.Write(change.Tile);
            }
            writer.Write(defeated.Count);
            foreach (var placement in defeated) writer.Write(placement);
        }
        var bytes = payload.ToArray();
        if (bytes.Length > MaximumFileSize - HeaderSize) throw new InvalidDataException("The save is too large.");
        using var file = new MemoryStream();
        using (var writer = new BinaryWriter(file, Utf8, leaveOpen: true))
        {
            writer.Write(Magic); writer.Write(Version); writer.Write(bytes.Length);
            writer.Write(SHA256.HashData(bytes)); writer.Write(bytes);
        }
        return file.ToArray();
    }

    // All parsing and validation precede construction of a separate session. The caller
    // accepts that session only after its own presentation/platform checks have passed.
    public static GameSession Decode(byte[] bytes, MapCatalog catalog)
    {
        if (bytes.Length < HeaderSize || bytes.Length > MaximumFileSize)
            throw new InvalidDataException("Invalid MonoGame save length.");
        using var file = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(file, Utf8);
        if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("This is not a MonoGame game save.");
        if (reader.ReadUInt16() != Version) throw new InvalidDataException("Unsupported MonoGame save version.");
        if (reader.ReadInt32() != bytes.Length - HeaderSize)
            throw new InvalidDataException("The save is truncated or has extra data.");
        var hash = reader.ReadBytes(32);
        if (!CryptographicOperations.FixedTimeEquals(hash, SHA256.HashData(bytes.AsSpan(HeaderSize))))
            throw new InvalidDataException("The save's integrity check failed.");
        try
        {
            var player = ReadPlayer(reader);
            var location = new WorldLocation
            {
                Position = new(reader.ReadInt32(), reader.ReadInt32()),
                RegionX = reader.ReadInt32(), RegionY = reader.ReadInt32(), MapLayer = reader.ReadInt32(),
                AlternateMapSet = ReadFlag(reader), MapId = reader.ReadInt32(),
            };
            var randomState = reader.ReadUInt32();
            var changeCount = reader.ReadInt32();
            if (changeCount < 0 || changeCount > (catalog.Ids.Count + 3) * Map.TileCount
                || changeCount > (file.Length - file.Position) / 14)
                throw new InvalidDataException("Invalid map-change count.");
            var changes = new MapTileChange[changeCount];
            for (var i = 0; i < changes.Length; i++)
                changes[i] = new(reader.ReadInt32(), new(reader.ReadInt32(), reader.ReadInt32()), reader.ReadUInt16());
            var defeatedCount = reader.ReadInt32();
            if (defeatedCount < 0 || defeatedCount > MonsterPlacementCount)
                throw new InvalidDataException("Invalid defeated-placement count.");
            var defeated = new ushort[defeatedCount];
            for (var i = 0; i < defeated.Length; i++) defeated[i] = reader.ReadUInt16();
            if (file.Position != file.Length) throw new InvalidDataException("The save payload has extra data.");
            Validate(player, location, changes, defeated, catalog);
            return new(catalog, player, location, randomState, changes, defeated);
        }
        catch (Exception error) when (error is EndOfStreamException or DecoderFallbackException)
        {
            throw new InvalidDataException("The save payload is truncated or invalid.", error);
        }
    }

    private static void Validate(Player p, WorldLocation l, IReadOnlyList<MapTileChange> changes,
        IReadOnlyList<ushort> defeated, MapCatalog catalog)
    {
        if (p.Name is null) throw new InvalidDataException("The character name is missing.");
        try
        {
            if (Utf8.GetByteCount(p.Name) > ushort.MaxValue) throw new InvalidDataException("The character name is too long.");
        }
        catch (EncoderFallbackException error) { throw new InvalidDataException("Invalid character name encoding.", error); }
        if (p.Specialty is < 0 or > 3 || p.SpecialAbility > SpecialAbility.Enrage
            || p.HellGatekeeperState is < HellGatekeeperState.Defeated or > HellGatekeeperState.Waiting
            || p.MiscellaneousShopStage > 4 || p.PendingRelocation > (ushort)PendingRelocation.StoneCircle
            || p.OpenedChests.Any(flags => flags > 15))
            throw new InvalidDataException("Invalid character progression flags.");
        // Protect the rule engine's divisors and playable world state. Preserve signed/raw
        // inventory metadata, counters and stat-cap modes instead of clamping saved values.
        if (p.Intelligence < 1 || p.Level is < 1 or > 100 || p.Difficulty is < 1 or > 3
            || p.MaximumHealth < 1 || p.MaximumSpellPoints < 0 || p.MaximumAbilityPoints < 0)
            throw new InvalidDataException("Invalid character statistics.");
        if (!Map.Contains(l.Position) || l.RegionX is < -4 or > 1 || l.RegionY is < -3 or > 2
            || l.MapLayer is < 0 or > 2
            || l.MapId != l.RegionX - l.RegionY * 6 + 17 + l.MapLayer * 40 + (l.AlternateMapSet ? 98 : 0)
            || p.CurrentMapId != l.MapId || p.AlternateMapSet != l.AlternateMapSet)
            throw new InvalidDataException("Invalid or inconsistent world location.");
        if (!catalog.Contains(l.MapId))
            throw new InvalidDataException("This save's map is not supported by this build.");
        if (MapCatalog.IsGenerated(l.MapId) && (l.Position.X is < 1 or > 11 || l.Position.Y is < 1 or > 10))
            throw new InvalidDataException("Generated maps require an interior saved position.");
        if (p.SurfaceReturnMapId is not (0 or 1)
            && (p.SurfaceReturnRegionX is < -4 or > 1 || p.SurfaceReturnRegionY is < -3 or > 2
                || p.SurfaceReturnMapId != p.SurfaceReturnRegionX - p.SurfaceReturnRegionY * 6 + 17
                || !catalog.Ids.Contains(p.SurfaceReturnMapId)))
            throw new InvalidDataException("Invalid saved surface-return location.");
        if (p.PendingRelocation == (ushort)PendingRelocation.Surface && p.SurfaceReturnMapId is 0 or 1)
            throw new InvalidDataException("A surface relocation needs an available return location.");
        var destination = (PendingRelocation)p.PendingRelocation switch
        {
            PendingRelocation.Surface => p.SurfaceReturnMapId,
            PendingRelocation.Yipe => MapCatalog.StartingMapId,
            PendingRelocation.Hell => 117,
            PendingRelocation.StoneCircle => 21,
            _ => l.MapId,
        };
        if (!catalog.Contains(destination)) throw new InvalidDataException("The relocation destination is unavailable.");
        if (changes.Count > (catalog.Ids.Count + 3) * Map.TileCount)
            throw new InvalidDataException("Too many persistent map changes.");
        var changed = new HashSet<(int, MapPosition)>();
        foreach (var change in changes)
            if (!catalog.Contains(change.MapId) || !Map.Contains(change.Position) || !changed.Add((change.MapId, change.Position)))
                throw new InvalidDataException("Invalid or duplicate persistent map change.");
        var seen = new HashSet<ushort>();
        foreach (var placement in defeated)
            if (placement >= MonsterPlacementCount || !seen.Add(placement))
                throw new InvalidDataException("Invalid or duplicate defeated placement.");
    }

    private static bool ReadFlag(BinaryReader reader) => reader.ReadByte() switch
    {
        0 => false, 1 => true, _ => throw new InvalidDataException("Invalid boolean flag in save."),
    };

    // Version 1 stores all 65 scalar properties and seven fixed-size arrays in Player
    // declaration order. Keep this layout stable; a schema change needs a new version.
    private static void WritePlayer(BinaryWriter writer, Player p)
    {
        var name = Utf8.GetBytes(p.Name);
        writer.Write((ushort)name.Length);
        writer.Write(name);
        writer.Write(p.Dexterity);
        writer.Write(p.Intelligence);
        writer.Write(p.Strength);
        writer.Write(p.Health);
        writer.Write(p.MaximumHealth);
        writer.Write(p.SpellPoints);
        writer.Write(p.MaximumSpellPoints);
        writer.Write(p.AbilityPoints);
        writer.Write(p.MaximumAbilityPoints);
        writer.Write(p.Level);
        writer.Write(p.Experience);
        writer.Write(p.Gold);
        writer.Write(p.WeaponRank);
        writer.Write(p.ArmorRank);
        writer.Write(p.BootsRank);
        writer.Write(p.GlovesRank);
        writer.Write(p.HelmetRank);
        writer.Write(p.RingRank);
        writer.Write(p.NecklaceRank);
        writer.Write(p.Difficulty);
        writer.Write(p.CombatStyle);
        writer.Write(p.Specialty);
        writer.Write(p.SillyMode);
        writer.Write(p.EquipmentUpgradeCount);
        writer.Write(p.LootRollPenalty);
        writer.Write(p.RestorationUses);
        writer.Write(p.DeathCount);
        writer.Write(p.QuestScore);
        writer.Write(p.QuestProgressTotal);
        writer.Write(p.GatekeeperState);
        writer.Write(p.StampsFound);
        writer.Write(p.PrincessesFound);
        writer.Write(p.PrincessMysteryUses);
        writer.Write(p.MiscellaneousShopStage);
        writer.Write(p.Value06a4);
        writer.Write(p.HasLuckyRabbitFoot);
        writer.Write(p.HasWaterproofShoeSpray);
        writer.Write(p.HasRainbowKey);
        writer.Write(p.HasUnderworldKey);
        writer.Write(p.HasTreeClimbingShoes);
        writer.Write((byte)p.SpecialAbility);
        writer.Write(p.SpecialAbilityDuration);
        writer.Write(p.EnrageBaseDexterity);
        writer.Write(p.EnrageBaseIntelligence);
        writer.Write(p.EnrageBaseStrength);
        foreach (var value in p.Inventory) writer.Write(value);
        foreach (var value in p.InventoryValues) writer.Write(value);
        foreach (var value in p.QuestFlags) writer.Write(value);
        foreach (var value in p.OpenedChests) writer.Write(value);
        foreach (var value in p.Spells) writer.Write(value);
        foreach (var value in p.EncounteredMonsterTypes) writer.Write(value);
        foreach (var value in p.MonsterDefeats) writer.Write(value);
        writer.Write(p.MonsterDefeatHistoryComplete);
        writer.Write(p.AlternateMapSet);
        writer.Write(p.HasBonusHealingRing);
        writer.Write(p.CurrentMapId);
        writer.Write(p.StatCapMode);
        writer.Write(p.SurfaceReturnMapId);
        writer.Write(p.SurfaceReturnRegionX);
        writer.Write(p.SurfaceReturnRegionY);
        writer.Write(p.RandomEncounterThreshold);
        writer.Write(p.SavedRandomEncounterThreshold);
        writer.Write(p.MonsterRepellentSteps);
        writer.Write(p.PendingRelocation);
        writer.Write(p.EnteredHellThroughSecretPassage);
        writer.Write((short)p.HellGatekeeperState);
        writer.Write(p.BridgeTreasureAvailable);
        writer.Write(p.HealingWaterAvailable);
        writer.Write(p.CaveCoinsCollected);
        writer.Write(p.SnowSearchCount);
        writer.Write(p.HasDefeatedWormoid);
    }

    private static Player ReadPlayer(BinaryReader reader)
    {
        var p = new Player();
        var nameLength = reader.ReadUInt16();
        var name = reader.ReadBytes(nameLength);
        if (name.Length != nameLength) throw new EndOfStreamException();
        p.Name = Utf8.GetString(name);
        p.Dexterity = reader.ReadInt32();
        p.Intelligence = reader.ReadInt32();
        p.Strength = reader.ReadInt32();
        p.Health = reader.ReadInt32();
        p.MaximumHealth = reader.ReadInt32();
        p.SpellPoints = reader.ReadInt32();
        p.MaximumSpellPoints = reader.ReadInt32();
        p.AbilityPoints = reader.ReadInt32();
        p.MaximumAbilityPoints = reader.ReadInt32();
        p.Level = reader.ReadInt32();
        p.Experience = reader.ReadInt32();
        p.Gold = reader.ReadInt32();
        p.WeaponRank = reader.ReadInt32();
        p.ArmorRank = reader.ReadInt32();
        p.BootsRank = reader.ReadUInt16();
        p.GlovesRank = reader.ReadUInt16();
        p.HelmetRank = reader.ReadUInt16();
        p.RingRank = reader.ReadUInt16();
        p.NecklaceRank = reader.ReadUInt16();
        p.Difficulty = reader.ReadInt32();
        p.CombatStyle = reader.ReadInt32();
        p.Specialty = reader.ReadInt32();
        p.SillyMode = ReadFlag(reader);
        p.EquipmentUpgradeCount = reader.ReadInt32();
        p.LootRollPenalty = reader.ReadInt32();
        p.RestorationUses = reader.ReadInt32();
        p.DeathCount = reader.ReadInt32();
        p.QuestScore = reader.ReadInt32();
        p.QuestProgressTotal = reader.ReadInt32();
        p.GatekeeperState = reader.ReadUInt16();
        p.StampsFound = reader.ReadUInt16();
        p.PrincessesFound = reader.ReadUInt16();
        p.PrincessMysteryUses = reader.ReadUInt16();
        p.MiscellaneousShopStage = reader.ReadUInt16();
        p.Value06a4 = reader.ReadUInt16();
        p.HasLuckyRabbitFoot = ReadFlag(reader);
        p.HasWaterproofShoeSpray = ReadFlag(reader);
        p.HasRainbowKey = ReadFlag(reader);
        p.HasUnderworldKey = ReadFlag(reader);
        p.HasTreeClimbingShoes = ReadFlag(reader);
        p.SpecialAbility = (SpecialAbility)reader.ReadByte();
        p.SpecialAbilityDuration = reader.ReadInt32();
        p.EnrageBaseDexterity = reader.ReadInt32();
        p.EnrageBaseIntelligence = reader.ReadInt32();
        p.EnrageBaseStrength = reader.ReadInt32();
        for (var i = 0; i < p.Inventory.Length; i++) p.Inventory[i] = reader.ReadUInt16();
        for (var i = 0; i < p.InventoryValues.Length; i++) p.InventoryValues[i] = reader.ReadUInt16();
        for (var i = 0; i < p.QuestFlags.Length; i++) p.QuestFlags[i] = ReadFlag(reader);
        for (var i = 0; i < p.OpenedChests.Length; i++) p.OpenedChests[i] = reader.ReadByte();
        for (var i = 0; i < p.Spells.Length; i++) p.Spells[i] = reader.ReadUInt16();
        for (var i = 0; i < p.EncounteredMonsterTypes.Length; i++) p.EncounteredMonsterTypes[i] = ReadFlag(reader);
        for (var i = 0; i < p.MonsterDefeats.Length; i++) p.MonsterDefeats[i] = reader.ReadInt32();
        p.MonsterDefeatHistoryComplete = ReadFlag(reader);
        p.AlternateMapSet = ReadFlag(reader);
        p.HasBonusHealingRing = ReadFlag(reader);
        p.CurrentMapId = reader.ReadInt32();
        p.StatCapMode = reader.ReadInt32();
        p.SurfaceReturnMapId = reader.ReadInt16();
        p.SurfaceReturnRegionX = reader.ReadInt16();
        p.SurfaceReturnRegionY = reader.ReadInt16();
        p.RandomEncounterThreshold = reader.ReadUInt16();
        p.SavedRandomEncounterThreshold = reader.ReadUInt16();
        p.MonsterRepellentSteps = reader.ReadUInt16();
        p.PendingRelocation = reader.ReadUInt16();
        p.EnteredHellThroughSecretPassage = ReadFlag(reader);
        p.HellGatekeeperState = (HellGatekeeperState)reader.ReadInt16();
        p.BridgeTreasureAvailable = ReadFlag(reader);
        p.HealingWaterAvailable = ReadFlag(reader);
        p.CaveCoinsCollected = ReadFlag(reader);
        p.SnowSearchCount = reader.ReadUInt16();
        p.HasDefeatedWormoid = ReadFlag(reader);
        return p;
    }
}
