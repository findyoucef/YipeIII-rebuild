namespace Yipe.Core;

public readonly record struct KingConversation(int GreetingResourceId, int MessageResourceId, int FooterResourceId, string Greeting, string Message, string Footer);
public enum PrincessGift { Gold = 1, Experience, Kiss, Mystery }
public readonly record struct PrincessReward(bool Rescued, int MessageResourceId, int Amount, string Message);
public readonly record struct ChestReward(bool Opened, int Experience, int Gold, LootReward Loot);
public readonly record struct CombatDefeatResult(int MessageResourceId, string Message);

public static class Quests
{
    private static readonly string[] Greetings = [
        "Welcome",
        "Hi",
        "Hello",
        "Hello again",
        "Howdy",
        "Good morning",
        "Afternoon",
        "Good evening",
    ];

    private static readonly string[] Messages = [
        "Thank you for coming to the aid of Yipe!  We are in desperate need of your help!  Strange monsters have been attacking the innocent people of Yipe!  Even worse, my collection of six rare stamps has been stolen!",
        "Last time I talked to you I got a little carried away.  I forgot to mention the other problem I have.  All of my daughters are missing!  I figure that some were kidnapped, others are lost, and some just ran away.  Unfortunately, I forget how many I have.",
        "Wow!  You have found one of my rare stamps!  I only need five more before my life can return to normal.  Have you tried searching in any of the other caves around the world?",
        "Thank you for trying to help the town.  I am totally unable to rule as King without my stamps.  Of course, it would also be nice to have all my daughters back.",
        "You've done well in the quests that I have given you.  Keep up your good work and I'm sure that Yipe! will return to its normal peaceful state.",
        "Thank you for returning me some of the princesses!  It sure is much more pleasant having my daughters running around the castle again!  One daughter has told me that there are many more still in need of rescuing.",
        "Three stamps!  You have returned half of my stamps to me!  This is cause for celebration!  I will organize a party tonight in your honor.  But, don't get too carried away, there are still more stamps to find!",
        "It seems that each main cave holds one of my rare stamps.  There must be more caves somewhere out in the world of Yipe!  Have you searched everywhere?  Oh how I long to have my precious stamps back!",
        "Thank you so much for saving around 10 of my daughters.  They tell me that there are still many more of their sisters around the world.  One princess suggested that I give you my lucky rabbit foot to help you out.",
        "You have returned to me five of my rare stamps!  There is only one stamp left out there!  I pray that it is not lost or too hard to find.  If you can't find it I don't think anyone can do it.",
        "The cave directly to the east of Yipe! is probably the next best place to look for another one of my rare stamps.  You probably are not strong enough to explore the rest of the world.",
        "I have heard rumors that a huge monster lives deep in the cave that is located right here in the town of Yipe!  Perhaps somewhere beyond this monster may be the answers to finding my stamps and daughters.",
        "I have been told that the entrance to the final cave is in the middle of a tall mountain range.  The only way to enter is through a portal that appears near a group of small rocks.",
        "I wish that I could follow you into the caves and seek revenge on these evil monsters myself.  However, I am old and not as strong as you are.  It is up to you to find the last few remaining stamps!",
        "You are truly a brave adventurer!  I thank you for everything that you have done for me and the entire city of Yipe!",
        "Ah ha... I see you found the water proof shoe spray.  Did you know that with the spray you will be able to cross over certain streams around the world?  I hear north of Yipe! there is another cave surrounding by water.",
        "Four stamps!  It is soooo great to have almost all of my stamps back.  Have you been checking all the treasure chests you find along the way?  You may need some of the items you find in the chests to be able to find the rest of my stamps.",
        "The Gatekeeper is responsible!  I should have known someone living in Hell must be evil!  Did you know there is more than one way to enter Hell?  One doesn't have to die to get there.",
        "Holy Cow!  You have returned all six stamps to me.  This is perhaps the best day of my life!  Well... it might be another good day if you could return all my daughters to me.",
        "Congratulations!  You have defeated the evil Gatekeeper!  I'm sure the world will soon be peaceful once again.  Now, if only you could find the rest of my daughters for me.",
        "All of my princesses are back and happily living in the castle!  You have also killed off the evil gatekeeper and returned all my stamps!  Our world is now peaceful, however take this weapon and armor as my thanks to you.",
        "Those princesses sure are hard to find.  Have you looked for secret walls?  Sailed all the rafts?  Looked behind every door?  Fallen through traps?  Who knows where they all might be.",
        "How strange!  There is a stamp stuck to the back of your armor.  You must have found one of my rare stamps somewhere in one of the caves.  I guess it ended up sticking to you before you had a chance to find it.",
        "If you are bored of fighting monsters and searching for stamps and princesses, perhaps you could spend some time at the Yipe! casino.  It is located just a short distance from my castle.",
        "One of the townsfolk recently escaped from the cave here in town.  He told me a huge monster at the very end of the cave was holding something small and square.",
        "It seems that only the largest of the monsters in this world are holding stamps.  You'll need more protection to kill these larger monsters.  Try equiping yourself with helmets, gloves, boots and magical rings.",
        "Two caves fully explored and two stamps found.  Only seems logical that the next cave will have another stamp of mine.  Have I ever shown you the current value of my stamps in the latest Stamp Price Guide?",
        "There is a post-it note on the King's throne.  A servant named Eric takes the note and walks it over to you.  The note says:",
        "Wow!  You have found two of my rare stamps!  You seem to have a great knack for finding rare stamps!  I only need four more before my life can return to normal.",
        "I hear that you have defeated the gatekeeper in the past.  Remember that each time you defeat him, he grows stronger.  Make sure to carry lots of healing potions before dealing with him again!",
        "One of my daughters told me that her younger sister is still missing.  She also tells me that this missing princess was very fond of water.  Perhaps she wandered north into the water cave?",
        "Still looking for princesses?  When I wanted to hide from the world I used to enter the stone cave, go north, go past a door and through a secret wall.  Maybe try looking there?",
        "Is there something I can help you with?  I'm really quite busy playing with my entire stamp collection.  Maybe you could go catch a bunny for my daughters to play with.",
        "An assistant to the King comes out and tells you that until a new problem arises in the world of Yipe! that the King has no need for your services.",
    ];

    private static readonly string[] Footers = [
        "Please, help me find my stamps!",
        "Find them, and I shall reward you!",
        "Here is 500 gold to reward you for your good deeds!",
        "I wish you luck my friend.",
        "We all owe you greatly for your good deeds.",
        "Princesses are well known for hiding in tricky spots.",
        "The King give you 1000 gold coins!",
        "The breeding grounds are not one of the main caves.",
        "The King gives you his Lucky Rabbit Foot.",
        "The King looks hopefully at you.",
        "You must first travel south, then east, and then north to reach the cave.",
        "I hope you don't give up.  The city is counting on you!",
        "Search and you shall find!",
        "I wish you luck!",
        "You have finished Yipe! III",
        "Prepare to get wet...",
        "I wish I could help you more myself.",
        "You might need to climb over a tree or cross a stream.",
        "Take this 5000 gold pieces as a token of my gratitude.",
        "The King gives you a better weapon!",
        "-The End",
        "Try walking into all the walls.  You might be able to go through some.",
        "Thanks for finding another rare stamp!",
        "My favorite game is darts!",
        "Something small and square sure sounds like one of my stamps!!",
        "You can purchase some of these things in some of the shops.",
        "This might be a good time to sneak out of the castle.",
        "'Organizing stamp collection.  Back in 5 minutes.'",
        "Here is 500 gold to reward you for your good deeds!",
        "Spell point healing potions can only be purchased in Quizilog.",
        "You don't need a boat to travel to some areas of the water cave.",
        "One time I even saw a princess blessing the water in that room.",
        "You have completed everything in Yipe! III",
        "I guess you'll have to wait for Yipe! IV...",
    ];

    private static readonly string[] DefeatMessages = [
        "Your character has run out of health points!  In the future, you will want to 'Run' away from monsters when you notice your health points (HP) start to run out.",
        "Your health has run out once again.  Make sure to purchase snacks in the Yipe! healing shop.  You may also have some items in your inventory that you could eat to regain your HP.",
        "Try purchasing the basic armor items in Fred's Miscellaneous shop.  These armor items will help protect you from being killed by the monsters.",
        "Maybe you should invest some money into increasing your strength and dexterity.  These skills will help you in combat.",
        "The monsters wish there were more players like you they could feed upon.  Perhaps you should run from monsters that look too difficult.",
        "Maybe you should go back to fighting little bunnies.",
        "You have died many times.  You may want to start a brand new game so that your character can start off with a full amount of gold.",
        "Make sure to go back to town and heal yourself before continuing to fight.",
        "The townspeople are starting to wonder if you're really cut out for saving them.",
        "I've seen bunnies with more combat skills than you!  Okay, maybe not that bad.  But you might want to consider running a little more often.",
        "Just in case you forgot, the idea is to not be killed while in combat.  It's quite okay to 'Run' away if you are low on health points.",
        "Hmm.  Are you feeling a little embarrassed about dying to this monster?  Hopefully nobody else is around to see!",
        "The more armor you have, the less likely you will be hit by a monster.  Thus, before you continue fighting you should buy all the basic items you can in Fred's Miscellaneous shop.",
        "It is very impressive that you have lasted this long in the world of Yipe! without a single death.  Don't let this first setback discourage you!",
        "\n\nThe Gatekeeper laughs at your feeble attempts to destroy him.  You are still stuck here in Yipe! Hell.",
        "Too bad there is not a high score category for deaths.  It looks like you're well on your way to achieving the all time high score in that category!",
        "The gatekeeper is going to make a fortune from healing you each time you die.  Perhaps you could invest your money into healing snacks instead.",
        "In order to start killing the more difficult monsters in the game you may want to consider raising your Dexterity and Strength.",
        "In order to help prevent so many deaths, you may want to try purchasing a more expensive suit of armor.  Check the Armor Shop in the town of Yipe!",
        "When your HP are running low during combat you could try casting a healing spell.  If you haven't learned one yet, visit Epiy in the town of Yipe!",
    ];

    private static int DefeatMessageIndex(Player player, int deathCount,
                             int monsterType, MsvcRandom random)
    {
        if (monsterType == 41)
        {
            return 14;
        }
        if (deathCount == 1)
        {
            return player.Level < 21 ? 0 : 13;
        }
        if (deathCount == 2)
        {
            return 1;
        }
        if (deathCount == 3 && player.Level == 1)
        {
            return 7;
        }
        if ((deathCount == 4 && player.ArmorRank < 5)
            || (deathCount == 5 && player.ArmorRank < 6))
        {
            return 18;
        }
        if (deathCount > 5 && player.Level < 3 && player.Gold < 25)
        {
            return 6;
        }

        bool hasBasicArmor = player.BootsRank > 0 && player.GlovesRank > 0
            && player.HelmetRank > 0 && player.RingRank > 0;
        if (!hasBasicArmor)
        {
            return deathCount < 7 ? 2 : 12;
        }
        if (deathCount >= 4 && (player.Dexterity <= 14 || player.Strength <= 14))
        {
            return 3;
        }
        if (deathCount >= 7 && player.Level >= 31
            && (player.Dexterity <= 29 || player.Strength <= 29))
        {
            return 17;
        }
        if (player.Level >= 12 && monsterType <= 3)
        {
            return 11;
        }
        if (deathCount < 6 || player.Level < 11)
        {
            return 4;
        }

        switch (random.Next() % 7)
        {
            case 0:
                return 5;
            case 1:
                return 8;
            case 2:
                return 9;
            case 3:
                return 15;
            case 4:
                return player.GatekeeperState == 0 ? 16 : 10;
            case 5:
                return 19;
            default:
                return 10;
        }
    }

    private static string RewardMessage(string text, int amount) => text.Replace("xx", amount.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static int SelectMessage(Player player, MsvcRandom random)
    {
        int stamps = unchecked((short)player.StampsFound);
        int princesses = unchecked((short)player.PrincessesFound);
        int gatekeeper = unchecked((short)player.GatekeeperState);

        if (!player.QuestFlags[0])
        {
            player.QuestFlags[0] = true;
            return 0;
        }
        if (!player.QuestFlags[1])
        {
            player.QuestFlags[1] = true;
            return 1;
        }
        if (!player.QuestFlags[2] && stamps == 1)
        {
            player.QuestFlags[2] = true;
            player.Gold += 500;
            return 2;
        }
        if (!player.QuestFlags[2] && stamps == 2)
        {
            player.QuestFlags[2] = true;
            player.Gold += 500;
            return 28;
        }
        if (!player.QuestFlags[5] && princesses > 2)
        {
            player.QuestFlags[5] = true;
            return 5;
        }
        if (!player.QuestFlags[6] && stamps > 2)
        {
            player.Gold += 1000;
            player.QuestFlags[6] = true;
            return 6;
        }
        if (!player.QuestFlags[8] && princesses > 9)
        {
            player.HasLuckyRabbitFoot = true;
            player.QuestFlags[8] = true;
            return 8;
        }

        if (stamps == 0)
        {
            int value = random.Next();
            if (value % 10 < 2 && player.QuestFlags[3])
            {
                return 24;
            }
            if (value % 10 < 6 && player.QuestFlags[3])
            {
                return 11;
            }
            player.QuestFlags[3] = true;
            return 3;
        }

        if (stamps < 3)
        {
            int value = random.Next() % 12;
            int accessoryTotal = player.BootsRank + player.GlovesRank
                + player.HelmetRank + player.RingRank;
            if (accessoryTotal < 4 && player.QuestProgressTotal > 50 && value < 2)
            {
                return 25;
            }
            if (stamps == 1 && value < 6)
            {
                return 10;
            }
            if (stamps == 2 && value < 5)
            {
                return 26;
            }
            return value < 9 ? 4 : 23;
        }

        if (stamps == 4)
        {
            if (!player.HasWaterproofShoeSpray)
            {
                return 16;
            }
            if (!player.QuestFlags[7])
            {
                player.QuestFlags[7] = true;
                return 15;
            }
        }
        else if (stamps > 4)
        {
            if (stamps == 5)
            {
                int value = random.Next();
                if (value % 10 < 6 && player.QuestFlags[7])
                {
                    return 12;
                }
                player.QuestFlags[7] = true;
                return 9;
            }
            if (stamps == 6)
            {
                if (!player.QuestFlags[9])
                {
                    player.QuestFlags[9] = true;
                    player.Gold += 5000;
                    return 18;
                }
                if (gatekeeper == 1)
                {
                    if (player.QuestFlags[12] && random.Next() % 10 < 5)
                    {
                        return 29;
                    }
                    player.QuestFlags[12] = true;
                    return 17;
                }
            }
            if (gatekeeper == 2)
            {
                if (!player.QuestFlags[10] && princesses < 20)
                {
                    player.QuestScore += 1500;
                    if (player.WeaponRank < 38)
                    {
                        ++player.WeaponRank;
                    }
                    player.QuestFlags[10] = true;
                    return 19;
                }
                if (player.QuestFlags[10] && princesses < 20)
                {
                    if (random.Next() % 10 < 6)
                    {
                        if (player.InventoryValues[13] == 0)
                        {
                            return 30;
                        }
                        if (player.InventoryValues[2] == 0)
                        {
                            return 31;
                        }
                    }
                    return 21;
                }
            }
            else if (gatekeeper > 2 && !player.QuestFlags[11])
            {
                player.WeaponRank = 38;
                player.ArmorRank = 30;
                player.QuestFlags[11] = true;
                return 20;
            }
            if (player.QuestFlags[13] && random.Next() % 10 < 6)
            {
                return 32 + (random.Next() & 1);
            }
            return 14;
        }

        int hintRoll = random.Next();
        if (hintRoll % 10 < 5)
        {
            return 13;
        }
        if (hintRoll % 10 == 7 && stamps > 2)
        {
            return 27;
        }
        return 7;
    }


    public static CombatDefeatResult ResolveCombatDefeat(Player player,
                                             WorldLocation location,
                                             int monsterType,
                                             MsvcRandom random)
    {
        int messageIndex = DefeatMessageIndex(
            player, player.DeathCount + 1, monsterType, random);
        player.Health = 1;
        ++player.DeathCount;
        location.EnterHell(player, false);
        return new(701 + messageIndex,
                DefeatMessages[messageIndex]);
    }

    public static int GatekeeperRecoveryFee(Player player)
    {
        int fee = 0;
        if (player.Difficulty == 1)
        {
            fee = Math.Min(player.Gold / 9, 150);
        }
        else if (player.Difficulty == 2)
        {
            fee = Math.Min(player.Gold / 7, player.Level + 260);
        }
        else if (player.Difficulty == 3)
        {
            fee = Math.Min(player.Gold / 5, player.Level + 375);
        }
        fee = Math.Max(fee, 10);
        return Math.Min(player.Gold, fee);
    }

    public static int PayGatekeeperRecovery(Player player)
    {
        int fee = GatekeeperRecoveryFee(player);
        player.Gold -= fee;
        player.Health = player.MaximumHealth;
        player.HellGatekeeperState = HellGatekeeperState.Paid;
        return fee;
    }

    public static PrincessReward RescuePrincess(Player player, int rescueIndex,
                                   PrincessGift gift, MsvcRandom random)
    {
        if (rescueIndex < 0 || rescueIndex >= player.InventoryValues.Length)
        {
            throw new ArgumentOutOfRangeException("princess rescue index is outside the inventory value table");
        }
        if (gift is < PrincessGift.Gold or > PrincessGift.Mystery)
        {
            throw new ArgumentException("unknown princess gift");
        }
        if (player.InventoryValues[rescueIndex] != 0)
        {
            return new(false, 0, 0, "");
        }

        player.InventoryValues[rescueIndex] = 1;
        player.PrincessesFound = unchecked((ushort)(player.PrincessesFound + 1));
        switch (gift)
        {
            case PrincessGift.Gold:
                {
                    int levelAmount = player.Level * 3;
                    int amount = random.Next() % levelAmount + levelAmount + 275
                        + random.Next() % 100;
                    player.Gold += amount;
                    return new(true, 1100, amount,
                            RewardMessage("The Princess gives you xx gold pieces!", amount));
                }
            case PrincessGift.Experience:
                {
                    int amount = player.Level * 4 + 125 + random.Next() % 25;
                    player.Experience += amount;
                    return new(true, 1101, amount,
                            RewardMessage("The Princess rewards you with xx experience points!",
                                           amount));
                }
            case PrincessGift.Kiss:
                player.AbilityPoints = player.MaximumAbilityPoints;
                player.Health = player.MaximumHealth;
                player.SpellPoints = player.MaximumSpellPoints;
                return new(true, 1102, 0,
                        "The Princess gives you a polite kiss on the cheek.  You feel healed " +
                        "from her gentle touch.");
            case PrincessGift.Mystery:
                break;
        }

        player.PrincessMysteryUses = unchecked((ushort)(player.PrincessMysteryUses + 1));
        int choiceRoll = random.Next();
        int threshold = 0;
        var signedMysteryUses =
            unchecked((short)player.PrincessMysteryUses);
        if (signedMysteryUses > 2)
        {
            threshold += signedMysteryUses * 10;
        }
        if (player.Level > 15)
        {
            threshold += player.Level;
        }
        if (player.Level > 29)
        {
            threshold += player.Level;
        }
        int conditionRoll = random.Next();
        int choice = choiceRoll % 10;
        if (conditionRoll % 97 + 25 < threshold && !player.HasRainbowKey)
        {
            choice = 10;
        }
        switch (choice)
        {
            case 0:
            case 1:
            case 7:
                {
                    int amount = player.Level < 20 ? 300 : 500;
                    player.Gold += amount;
                    return new(true, 1103, amount,
                            RewardMessage("The Princess gives you xx gold pieces and wishes " +
                            "you luck in your journey.", amount));
                }
            case 2:
            case 9:
                player.MaximumHealth += 2;
                return new(true, 1104, 2,
                        "The Princess looks at you and winks her eye.  You notice your " +
                        "maximum health has gone up a little.");
            case 3:
            case 6:
            case 8:
                player.Health = player.MaximumHealth;
                player.SpellPoints = player.MaximumSpellPoints;
                return new(true, 1105, 0,
                        "The Princess makes some kind of motion with her hand.  Your health " +
                        "and spell points have been healed.");
            case 4:
                if (!player.HasRainbowKey)
                {
                    player.PrincessMysteryUses = unchecked((ushort)(player.PrincessMysteryUses + 1));
                    return new(true, 1106, 0,
                            "The Princess tells you that one of her sisters carries a key.  " +
                        "Ask her for the Mystery gift and she shall give it to you.");
                }
                break;
            case 5:
                if (player.RingRank < 4)
                {
                    ++player.RingRank;
                    return new(true, 1107, 1,
                            "The Princess takes off her shiny ring and gives it to you.");
                }
                break;
            default:
                player.HasRainbowKey = true;
                return new(true, 1108, 0,
                        "The Princess nods at you and opens up a small pouch she is carrying.  " +
                        "From it she takes a rainbow key and hands it to you.");
        }

        player.Gold += 500;
        return new(true, 1103, 500,
                RewardMessage("The Princess gives you xx gold pieces and wishes you luck " +
                    "in your journey.", 500));
    }

    public static ChestReward OpenChest(Player player, Map map, MapPosition position,
                           int slot, MsvcRandom random)
    {
        if (slot < 0 || slot >= map.Events.Chests.Length) throw new ArgumentOutOfRangeException("invalid chest slot");
        if (map.Id < 1 || map.Id > 128 || !Map.Contains(position)
            || map.Events.Chests[slot] != position) return default;
        ref byte opened = ref player.OpenedChests[map.Id - 1];
        var mask = (byte)(1 << slot);
        if ((opened & mask) != 0) return default;
        map.RemoveChest(slot);
        opened |= mask;
        int chest = slot;
        int levelRoll = random.Next() % Math.Max(player.Level, 1);
        int experience = random.Next() % 12 + chest + levelRoll + 13
            + player.Difficulty * 2;
        int goldRoll = random.Next() % (chest + 30);
        int gold = experience + 25 + goldRoll + chest * 10
            - random.Next() % 8 + player.Difficulty;
        player.Experience += experience;
        player.Gold += gold;
        player.CurrentMapId = map.Id;
        int[] thresholds = [99, 78, 70, -1];
        return new(true, experience, gold,
                Combat.RollTreasure(player, thresholds[slot], chest + 1, random));
    }

    public static bool CompleteGatekeeperFinale(Player player)
    {
        if (player.GatekeeperState != 2 || player.RescuedPrincessCount != 20)
        {
            return false;
        }
        player.GatekeeperState = 3;
        return true;
    }

    public static KingConversation TalkToKing(Player player, MsvcRandom random, int localHour)
    {
        int greetingIndex = random.Next() % 6;
        if (greetingIndex == 5)
        {
            if (localHour >= 12 && localHour < 17)
            {
                greetingIndex = 6;
            }
            else if (localHour < 2 || localHour >= 17)
            {
                greetingIndex = 7;
            }
        }

        bool firstConversation = !player.QuestFlags[0];
        int messageIndex = SelectMessage(player, random);
        if (firstConversation)
        {
            greetingIndex = 0;
        }

        int greeting = greetingIndex;
        int message = messageIndex;
        return new(
            721 + greetingIndex,
            731 + messageIndex,
            766 + messageIndex,
            Greetings[greeting] + ", " + player.Name,
            Messages[message],
            Footers[message]
        );
    }

}
