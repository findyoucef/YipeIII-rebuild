#include "yipe/quest.hpp"

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <stdexcept>

namespace yipe {
namespace {

constexpr std::array<std::string_view, 8> greetings{
    "Welcome",
    "Hi",
    "Hello",
    "Hello again",
    "Howdy",
    "Good morning",
    "Afternoon",
    "Good evening",
};

constexpr std::array<std::string_view, 34> messages{
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
};

constexpr std::array<std::string_view, 34> footers{
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
};

std::string reward_message(std::string_view text, int amount) {
    std::string result(text);
    if (const std::size_t marker = result.find("xx"); marker != std::string::npos) {
        result.replace(marker, 2, std::to_string(amount));
    }
    return result;
}

int select_message(Player& player, MsvcRandom& random) {
    const int stamps = static_cast<std::int16_t>(player.stamps_found);
    const int princesses = static_cast<std::int16_t>(player.princesses_found);
    const int gatekeeper = static_cast<std::int16_t>(player.gatekeeper_state);

    if (!player.quest_flags[0]) {
        player.quest_flags[0] = true;
        return 0;
    }
    if (!player.quest_flags[1]) {
        player.quest_flags[1] = true;
        return 1;
    }
    if (!player.quest_flags[2] && stamps == 1) {
        player.quest_flags[2] = true;
        player.gold += 500;
        return 2;
    }
    if (!player.quest_flags[2] && stamps == 2) {
        player.quest_flags[2] = true;
        player.gold += 500;
        return 28;
    }
    if (!player.quest_flags[5] && princesses > 2) {
        player.quest_flags[5] = true;
        return 5;
    }
    if (!player.quest_flags[6] && stamps > 2) {
        player.gold += 1000;
        player.quest_flags[6] = true;
        return 6;
    }
    if (!player.quest_flags[8] && princesses > 9) {
        player.has_lucky_rabbit_foot = true;
        player.quest_flags[8] = true;
        return 8;
    }

    if (stamps == 0) {
        const int value = random.next();
        if (value % 10 < 2 && player.quest_flags[3]) {
            return 24;
        }
        if (value % 10 < 6 && player.quest_flags[3]) {
            return 11;
        }
        player.quest_flags[3] = true;
        return 3;
    }

    if (stamps < 3) {
        const int value = random.next() % 12;
        const int accessory_total = player.boots_rank + player.gloves_rank
            + player.helmet_rank + player.ring_rank;
        if (accessory_total < 4 && player.quest_progress_total > 50 && value < 2) {
            return 25;
        }
        if (stamps == 1 && value < 6) {
            return 10;
        }
        if (stamps == 2 && value < 5) {
            return 26;
        }
        return value < 9 ? 4 : 23;
    }

    if (stamps == 4) {
        if (!player.has_waterproof_shoe_spray) {
            return 16;
        }
        if (!player.quest_flags[7]) {
            player.quest_flags[7] = true;
            return 15;
        }
    } else if (stamps > 4) {
        if (stamps == 5) {
            const int value = random.next();
            if (value % 10 < 6 && player.quest_flags[7]) {
                return 12;
            }
            player.quest_flags[7] = true;
            return 9;
        }
        if (stamps == 6) {
            if (!player.quest_flags[9]) {
                player.quest_flags[9] = true;
                player.gold += 5000;
                return 18;
            }
            if (gatekeeper == 1) {
                if (player.quest_flags[12] && random.next() % 10 < 5) {
                    return 29;
                }
                player.quest_flags[12] = true;
                return 17;
            }
        }
        if (gatekeeper == 2) {
            if (!player.quest_flags[10] && princesses < 20) {
                player.quest_score += 1500;
                if (player.weapon_rank < 38) {
                    ++player.weapon_rank;
                }
                player.quest_flags[10] = true;
                return 19;
            }
            if (player.quest_flags[10] && princesses < 20) {
                if (random.next() % 10 < 6) {
                    if (player.inventory_values[13] == 0) {
                        return 30;
                    }
                    if (player.inventory_values[2] == 0) {
                        return 31;
                    }
                }
                return 21;
            }
        } else if (gatekeeper > 2 && !player.quest_flags[11]) {
            player.weapon_rank = 38;
            player.armor_rank = 30;
            player.quest_flags[11] = true;
            return 20;
        }
        if (player.quest_flags[13] && random.next() % 10 < 6) {
            return 32 + (random.next() & 1);
        }
        return 14;
    }

    const int value = random.next();
    if (value % 10 < 5) {
        return 13;
    }
    if (value % 10 == 7 && stamps > 2) {
        return 27;
    }
    return 7;
}

} // namespace

PrincessReward rescue_princess(Player& player, std::size_t rescue_index,
                               PrincessGift gift, MsvcRandom& random) {
    if (rescue_index >= player.inventory_values.size()) {
        throw std::out_of_range("princess rescue index is outside the inventory value table");
    }
    if (gift < PrincessGift::gold || gift > PrincessGift::mystery) {
        throw std::invalid_argument("unknown princess gift");
    }
    if (player.inventory_values[rescue_index] != 0) {
        return {};
    }

    player.inventory_values[rescue_index] = 1;
    ++player.princesses_found;
    switch (gift) {
    case PrincessGift::gold: {
        const int level_amount = player.level * 3;
        const int amount = random.next() % level_amount + level_amount + 275
            + random.next() % 100;
        player.gold += amount;
        return {true, 1100, amount,
                reward_message("The Princess gives you xx gold pieces!", amount)};
    }
    case PrincessGift::experience: {
        const int amount = player.level * 4 + 125 + random.next() % 25;
        player.experience += amount;
        return {true, 1101, amount,
                reward_message("The Princess rewards you with xx experience points!",
                               amount)};
    }
    case PrincessGift::kiss:
        player.ability_points = player.maximum_ability_points;
        player.health = player.maximum_health;
        player.spell_points = player.maximum_spell_points;
        return {true, 1102, 0,
                "The Princess gives you a polite kiss on the cheek.  You feel healed "
                "from her gentle touch."};
    case PrincessGift::mystery:
        break;
    }

    ++player.princess_mystery_uses;
    const int choice_roll = random.next();
    int threshold = 0;
    const auto signed_mystery_uses =
        static_cast<std::int16_t>(player.princess_mystery_uses);
    if (signed_mystery_uses > 2) {
        threshold += signed_mystery_uses * 10;
    }
    if (player.level > 15) {
        threshold += player.level;
    }
    if (player.level > 29) {
        threshold += player.level;
    }
    const int condition_roll = random.next();
    int choice = choice_roll % 10;
    if (condition_roll % 97 + 25 < threshold && !player.has_rainbow_key) {
        choice = 10;
    }
    switch (choice) {
    case 0:
    case 1:
    case 7: {
        const int amount = player.level < 20 ? 300 : 500;
        player.gold += amount;
        return {true, 1103, amount,
                reward_message("The Princess gives you xx gold pieces and wishes "
                               "you luck in your journey.", amount)};
    }
    case 2:
    case 9:
        player.maximum_health += 2;
        return {true, 1104, 2,
                "The Princess looks at you and winks her eye.  You notice your "
                "maximum health has gone up a little."};
    case 3:
    case 6:
    case 8:
        player.health = player.maximum_health;
        player.spell_points = player.maximum_spell_points;
        return {true, 1105, 0,
                "The Princess makes some kind of motion with her hand.  Your health "
                "and spell points have been healed."};
    case 4:
        if (!player.has_rainbow_key) {
            ++player.princess_mystery_uses;
            return {true, 1106, 0,
                    "The Princess tells you that one of her sisters carries a key.  "
                    "Ask her for the Mystery gift and she shall give it to you."};
        }
        break;
    case 5:
        if (player.ring_rank < 4) {
            ++player.ring_rank;
            return {true, 1107, 1,
                    "The Princess takes off her shiny ring and gives it to you."};
        }
        break;
    default:
        player.has_rainbow_key = true;
        return {true, 1108, 0,
                "The Princess nods at you and opens up a small pouch she is carrying.  "
                "From it she takes a rainbow key and hands it to you."};
    }

    player.gold += 500;
    return {true, 1103, 500,
            reward_message("The Princess gives you xx gold pieces and wishes you luck "
                           "in your journey.", 500)};
}

bool complete_gatekeeper_finale(Player& player) {
    const auto rescued = std::count_if(
        player.inventory_values.begin(), player.inventory_values.end(),
        [](std::uint16_t value) { return value > 0; });
    if (player.gatekeeper_state != 2 || rescued != 20) {
        return false;
    }
    player.gatekeeper_state = 3;
    return true;
}

KingConversation talk_to_king(Player& player, MsvcRandom& random, int local_hour) {
    int greeting_index = random.next() % 6;
    if (greeting_index == 5) {
        if (local_hour >= 12 && local_hour < 17) {
            greeting_index = 6;
        } else if (local_hour < 2 || local_hour >= 17) {
            greeting_index = 7;
        }
    }

    const bool first_conversation = !player.quest_flags[0];
    const int message_index = select_message(player, random);
    if (first_conversation) {
        greeting_index = 0;
    }

    const std::size_t greeting = static_cast<std::size_t>(greeting_index);
    const std::size_t message = static_cast<std::size_t>(message_index);
    return {
        721 + greeting_index,
        731 + message_index,
        766 + message_index,
        std::string(greetings[greeting]) + ", " + player.name,
        messages[message],
        footers[message],
    };
}

} // namespace yipe
