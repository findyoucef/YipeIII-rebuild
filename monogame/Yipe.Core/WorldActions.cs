namespace Yipe.Core;

public readonly record struct OtherActionResult(int MessageResourceId, int SoundId, string Message);
public enum SecretTreeResult { IncorrectCode, RewardGranted, AlreadySolved }

public static class WorldActions
{
    private static readonly string[] OtherMessages = [
        "You find nothing of interest right here.",
        "Yummm!  That water sure tastes refreshing!",
        "You find nothing of interest underneath the bridge.",
        "You are standing in a narrow crack in the wall.  This must be some type of secret passage.",
        "You lie down and make a pretty snow angel in the cool winter snow.",
        "Looking under the bridge you find a small pile of gold!  You take a quick look to see that nobody is looking and you pocket the 600 gold pieces.",
        "Not only does the water taste great, but it heals some of your wounds!",
        "The water hole is temporarily dry.",
        "You take a moment to rest on the warm rock.",
        "The pretty flowers smell as good as they look.",
        "These flowers give off no distinctive smell, but they sure look nice.",
        "Upon looking at the skull closely, you decide you have no idea what type of animal it came from.",
        "The small plant you are standing on tickles your leg.",
        "You are standing on an old desert plant.  There is nothing of importance about it.",
        "A spike seems to be coming out of the wall right towards you!  You better move fast!",
        "There is an interesting crack in the ground here.  At the moment it doesn't seem big enough for you to fall into it.",
        "You don't notice anything out of the ordinary on the small stone path that you are standing on.",
        "",
        "",
        "",
        "",
        "The grounds shakes for a minute and suddenly part of the mountain range lowers into the ground before you!",
        "As you take a closer look at the flower, a bee pops out and stings you on the nose.  Ouch!!",
        "You notice that the sand feels soft under your feet.",
        "The sand feels warm from the hot sun.",
        "You pick up some sand and let it trickle through your fingers.",
        "Not finding anything of interest, you decide to sing a short song.",
        "",
        "You don't notice anything around you, however you do seem to feel a little safer standing on this road.",
        "This section of the road looks to be in good shape.  Some of the other sections don't seem to be as nice as this particular part.",
        "You have a nice view of the land from up here in this tree.  Sure is a pretty world.  Too bad there are so many monsters wandering around.",
        "From up in this tree it looks like something is moving down in the sand below you.  It's probably just a little sand bug.",
        "You get a nice warm feeling inside yourself when you look out upon the world of Yipe!  You feel pride in knowing that you're the one who saved the world!",
        "You are standing at the bottom of a mountain.  A few pebbles lay scattered around the sandy area below you.",
        "Some sand comes up onto the grass right here.  You don't find anything else interesting.",
        "You are standing right on top of a small plant in the middle of a grassy area.  You do not notice anything unusual about the plant.",
        "Below you is a sturdy rock that is halfway buried into the ground.  It looks just right for sitting on.",
        "You find nothing of interest in the grassy area that you are currently standing on.",
        "You have discovered a secret little path through the mountains right here.  There was no way to see this path from below.",
        "You find nothing of interest in this part of the cave.",
        "The cave floor below you feels a little chilly.  Other than that, there's nothing particularly interesting right here.",
        "You can't quite tell what seems so strange about the mountains right here.  Perhaps you should keep looking around this area.",
        "How did you get up on this mountain?!?  Hopefully you have a way to climb down from up here!",
        "The cave floor below you is surprisingly smooth.  Perhaps it has been worn down by the high number of monsters that have walked through here!",
        "Below you is a patch of snow.  Digging around in the snow you do not find anything of interest.",
        "The snow starts to melt away into the grass right here.",
        "You dig around in the snow for a few minutes and your work pays off.  Below the snow was a pile of around 400 gold coins!!",
        "After checking one more time in the snow, it looks like you missed a few gold coins the first time you checked.  You've found another 100 coins!",
        "At this point your hand is starting to get cold.  You should probably stop searching for gold coins.",
        "Ouch!!!  It looks like you've got frost bite!  The pain in your hand is just a little too much to keep searching.  You take a small amount of damange.",
        "As you take a moment to notice, you feel a slight breeze blowing throughout this cave.",
        "You are standing right on the entrance to an underground cave.",
        "While looking at the tally marks you notice the shape of the walls around you seem to be in the shape of a letter.",
        "Other than what you've already discovered, there is nothing else of interest here.",
    ];
    private static string OtherMessage(int id) => id switch
    {
        146 => "You pile up the coins scattered about the floor and put them into your pocket.  In all, it looks like you gained about 180 coins.",
        147 => "You search the floor again.  It looks like you've already gathered up all the loose gold.",
        >= 1002 and <= 1055 when OtherMessages[id - 1002].Length > 0 => OtherMessages[id - 1002],
        _ => throw new InvalidOperationException("Unknown Other action message."),
    };

    public static OtherActionResult Other(Player player, Map map, MapPosition position, MsvcRandom random)
    {
        var tile = map.Tile(position);
        int message = 1002;
        int sound = 0;
        switch (tile)
        {
            case 3: message = 1044; break;
            case 8:
            case 0x16:
            case 0x17:
            case 0x18:
            case 0x19:
            case 0x1a:
                message = 1018;
                if (map.Id != 0x11 && map.Id != 0x1b)
                {
                    int roll = random.Next() % 50;
                    message = roll < 29 ? 1018 : roll < 46 ? 1030 : 1031;
                }
                break;
            case 0x0b:
                message = 1004;
                if (map.Id == 0x21 && player.BridgeTreasureAvailable)
                {
                    player.BridgeTreasureAvailable = false;
                    player.Gold += 600;
                    player.Experience += random.Next() % 10 + 20;
                    message = 1007;
                }
                break;
            case 0x1b: message = 1036; break;
            case 0x1f: message = random.Next() % 100 < 71 ? 1046 : 1006; break;
            case 0x20: message = 1047; break;
            case 0x26:
                message = 1003;
                sound = 143;
                if (map.Id == 6)
                {
                    message = 1009;
                    sound = 0;
                    if (player.HealingWaterAvailable && player.Health < player.MaximumHealth)
                    {
                        if (random.Next() % 100 < player.Difficulty + 40)
                        {
                            player.HealingWaterAvailable = false;
                        }
                        int healing = random.Next() % 10 + player.Level
                            + (player.HasBonusHealingRing ? player.Intelligence : 0);
                        player.AdjustHealth(healing);
                        message = 1008;
                        sound = 143;
                    }
                }
                break;
            case 0x27: message = random.Next() % 100 < 21 ? 1042 : 1041; break;
            case 0x28: message = random.Next() % 100 < 26 ? 1045 : 1041; break;
            case 0x29:
            case 0x2a: message = 1017; break;
            case 0x2c: message = 1013; break;
            case 0x2d:
                {
                    int roll = random.Next() % 4;
                    message = roll == 0 ? 1002 : 1024 + roll;
                    break;
                }
            case 0x2f: message = 1015; break;
            case 0x32: message = random.Next() % 100 < 61 ? 1037 : 1014; break;
            case 0x33: message = random.Next() % 100 < 41 ? 1038 : 1010; break;
            case 0x34:
                message = 1011;
                if (player.Health > 3 && random.Next() % 100 > 79)
                {
                    if (!player.AdjustHealth(-2))
                    {
                        player.Health = 1;
                    }
                    message = 1024;
                    sound = 50;
                }
                break;
            case 0x35:
                message = 1012;
                if (map.Id == 0x12)
                {
                    map.RestoreTile(new(3, 7), 0x46);
                    map.RestoreTile(new(4, 7), 0x46);
                    message = 1023;
                    sound = 152;
                }
                break;
            case 0x36:
            case 0x39: message = 1053; break;
            case 0x3c: message = 1016; break;
            case 0x42:
                player.PendingRelocation = (ushort)PendingRelocation.Hell;
                return new(0, 0, "");
            case 0x43:
            case 0x44: message = 1005; break;
            case 0x45: message = 1040; break;
            case 0x46:
                if (map.Id == 3)
                {
                    player.PendingRelocation = (ushort)PendingRelocation.StoneCircle;
                    return new(0, 0, "");
                }
                if (map.Id == 0x12)
                {
                    message = 1043;
                }
                break;
            case 0x47:
                message = 1055;
                if (map.Id == 0x45)
                {
                    message = player.CaveCoinsCollected ? 147 : 146;
                    if (!player.CaveCoinsCollected)
                    {
                        player.CaveCoinsCollected = true;
                        player.Gold += 180;
                    }
                }
                break;
            case 0x49: message = map.Id == 0x57 ? 1054 : 1055; break;
            case 0x53: message = random.Next() % 100 < 31 ? 1052 : 1041; break;
            case 0x56:
            case 0x57:
                if (random.Next() % 100 < 76)
                {
                    int roll = random.Next() % 100;
                    if (roll >= 61 && tile == 0x57)
                    {
                        message = 1033;
                    }
                }
                else
                {
                    message = player.GatekeeperState < 3 ? 1032 : 1034;
                }
                break;
            case 0x65: message = 1035; break;
            case 0x69:
                if (player.SnowSearchCount == 0)
                {
                    player.SnowSearchCount = 1;
                    player.Gold += random.Next() % 10 + 401;
                    message = 1048;
                }
                else if (player.SnowSearchCount == 1)
                {
                    player.SnowSearchCount = 2;
                    player.Gold += random.Next() % 10 + 101;
                    message = 1049;
                }
                else if (player.SnowSearchCount == 2)
                {
                    player.SnowSearchCount = 3;
                    message = 1050;
                }
                else
                {
                    if (!player.AdjustHealth(-5 - random.Next() % 15))
                    {
                        player.Health = 1;
                    }
                    player.SnowSearchCount = 2;
                    message = 1051;
                    sound = 144;
                }
                break;
            default:
                {
                    int roll = random.Next() % 100;
                    if (roll >= 86 && tile == 2)
                    {
                        message = 1039;
                    }
                    else if (roll <= 9)
                    {
                        message = 1028;
                    }
                    break;
                }
        }
        return new(message, sound, OtherMessage(message));
    }

    public static string MovementHint(Player player, int mapId)
    {
        switch (mapId)
        {
            case 3:
                return "The three stones surrounding you start to glow.  Perhaps there is some " +
                        "'other' thing you are supposed to do here.";
            case 0x12:
                return "You notice something strange about the flowers and mountains around this area.";
            case 0x2c:
                return "You sense that this space seems different.  It seems like there was once a wall here.";
            case 0x2d:
                return "You notice that the wall next to you could be pushed open.  " +
                        "Unfortunately you do not have enough strength to do so.";
            case 0x39:
                return "You notice a crack in the wall to the north of you.";
            case 0x45:
                if (!player.CaveCoinsCollected)
                {
                    return "You find a small pile of coins on the floor.  " +
                        "Hit the 'Other' button if you would like to take them.";
                }
                break;
            case 0x48:
                if (player.Level <= 10)
                {
                    return "Suddenly you sense that you are not yet strong or powerful enough " +
                        "to survive in this dungeon.  Yet, you can try if you really want to.";
                }
                break;
            case 0x4a:
                if (player.InventoryValues[11] == 0)
                {
                    return "You hear the faint cry of a young female.";
                }
                break;
            case 0x57:
                return "You notice six tally marks etched into the strangely shaped wall in front of you.";
            case 0x5a:
                return "A message on the wall says:  A group of stones holds the entrance.";
            case 0x6d:
                // The original tests kill-count index 34 (Wormoid), not the Earth Giant.
                if (!player.HasDefeatedWormoid)
                {
                    return "You feel very small standing in front of this giant monster.";
                }
                break;
            default:
                break;
        }
        return "";
    }

    public static int ApplyFireWall(Player player, MsvcRandom random)
    {
        var difficultyRoll = random.Next(); var levelRoll = random.Next();
        var damage = difficultyRoll % Math.Max(player.Difficulty, 1) + 2 + levelRoll % Math.Max(player.Level, 1);
        if (!player.AdjustHealth(-damage)) player.Health = 1;
        return damage;
    }
    public static SecretTreeResult EnterTreeCode(Player p, string code)
    {
        if (p.HasBonusHealingRing) return SecretTreeResult.AlreadySolved;
        if (code != "hhhhhh") return SecretTreeResult.IncorrectCode;
        p.Gold += 1500; p.Experience += 250;
        if (p.RestorationUses > 0) p.RestorationUses--;
        p.HasBonusHealingRing = true;
        return SecretTreeResult.RewardGranted;
    }
}
