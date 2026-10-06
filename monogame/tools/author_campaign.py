#!/usr/bin/env python3
"""Historical partial route authoring example; use verify_campaign.py for the full retained completion."""
import argparse
from pathlib import Path
from campaign_driver import Campaign, ROOT

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("output", type=Path)
parser.add_argument("--farm-trips", type=int, default=100)
parser.add_argument("--golem-trips", type=int, default=500)
parser.add_argument("--prefix", type=Path, help="Reconstruct a prior earned prefix from inputs, never saves")
args = parser.parse_args()
game = Campaign(args.output, ROOT / "build/monogame-reference/campaign-reference")
try:
    if args.prefix:
        game.replay(args.prefix)
    else:
        game.command("spell 0")
        game.command("walk 3666666669")
        game.command("walk 888")
        game.checkpoint("king-first-stamp")
        game.command("walk 1444444447")
        game.expect(17, 1, 2)
        for trip in range(args.farm_trips):
            game.rest()
            game.command("walk 833366666688")
            game.expect(57, 10, 2)
            game.rest()
            game.command("spell 0")
            game.expect(17, 1, 2)
            if (trip + 1) % 50 == 0:
                game.checkpoint(f"bat-trip-{trip + 1}")
        # Spend only the gold just earned through the original equipment dialogs.
        def buy_equipment(title, rank):
            sold = False
            bought = False
            def choose(actual, text, buttons):
                nonlocal sold, bought
                if actual != title: raise RuntimeError(f"Unexpected dialog {actual}: {text}")
                if "Sell anyway?" in text: return 1
                if not sold: sold = True; return 3
                if bought: return 0
                if f"(rank {rank})" in text: bought = True; return 1
                return 2
            return choose
        game.dialog_handler = buy_equipment("Weapon Shop", 16)
        game.walk_to(17, 8, 4, interact=True)
        game.dialog_handler = buy_equipment("Armor Shop", 15)
        game.walk_to(17, 8, 6, interact=True)
        game.dialog_handler = None
        if game.state['equipment'][:2] != [16, 15]: raise RuntimeError("Equipment upgrade was not earned")
        game.checkpoint("town-upgrades")
        learned = False
        def learn_stink(title, text, buttons):
            global learned
            if title != "Magic Shop": raise RuntimeError(f"Unexpected dialog {title}")
            if learned: return 0
            learned = True
            return 3
        game.dialog_handler = learn_stink
        game.walk_to(17, 11, 10, interact=True)
        game.dialog_handler = None
        if game.state['spells'][4] != 1: raise RuntimeError("Stinky Smell was not learned")
        game.rest()
        game.walk_to(58, 11, 9)
        game.rest()
        game.checkpoint("golem-route")
        for trip in range(args.golem_trips):
            game.walk_to(64, 9, 1)
            game.walk_to(58, 11, 9)
            game.rest()
            if (trip + 1) % 50 == 0:
                game.checkpoint(f"golem-trip-{trip + 1}")
        game.command("spell 0")
        game.dialog_handler = lambda title, text, buttons: 3 if title == "Magic Shop" and "Available spell:" in text else 0
        game.walk_to(17, 11, 10, interact=True)
        game.dialog_handler = None
        game.command("heal 5 low")
        if game.state['spells'][7]: game.command("combat 7 low")
        elif game.state['spells'][6]: game.command("combat 6 low")
        game.rest()
        game.checkpoint("advanced-spells")
        for title, tile, rank in [("Armor Shop", 7, 28), ("Weapon Shop", 6, 25)]:
            game.dialog_handler = buy_equipment(title, rank)
            m = game.maps[27]
            x, y = next((x, y) for x in range(13) for y in range(12) if m['tiles_by_x'][x][y] == tile)
            game.walk_to(27, x, y, interact=True)
            game.dialog_handler = None
        game.checkpoint("ervyn-upgrades")
        # Earn the two remaining earth/stone-side rescues before harder elemental caves.
        for map_id, x, y in [(116, 1, 1), (106, 1, 10)]:
            game.walk_to(map_id, x, y)
            game.rest()
            game.checkpoint(f"rescue-{map_id}")
            game.command("spell 0")
    def buy_items(map_id, name, count):
        remaining = count
        visits = 0
        def choose(title, text, buttons):
            nonlocal remaining, visits
            visits += 1
            if title not in ["Fred's Miscellaneous Shop", "Maynard Supplies Shop", "Quizilog Random Items Shop", "Outback Shop"]:
                raise RuntimeError(f"Unexpected shop {title}")
            if visits > count + 30: raise RuntimeError(f"Shop never offered {name}")
            if remaining == 0: return 0
            if text.startswith(name + "\n"):
                remaining -= 1
                return 1
            return 2
        game.dialog_handler = choose
        m = game.maps[map_id]
        x, y = next((x, y) for x in range(13) for y in range(12) if m['tiles_by_x'][x][y] == 1)
        game.walk_to(map_id, x, y, interact=True)
        game.dialog_handler = None

    game.command("combat off")
    if game.state['map'] == 117:
        game.command("walk " + "8" * game.state['y'], [("Gatekeeper", 1)])
        game.command("walk 8")
    game.rest()
    # Carry healing supplies while reserving SP for Full Heal.
    buy_items(17, "Juicy Cheeseburger", 10)
    for map_id, x, y in [(99, 8, 1), (88, 9, 5), (86, 1, 1)]:
        game.walk_to(map_id, x, y)
        game.rest()
        game.checkpoint(f"rescue-{map_id}")
        game.command("spell 0")
    game.checkpoint("authoring-prefix")
    print("AUTHORING PREFIX ONLY: all progression earned, full completion not yet attempted.", flush=True)
finally:
    game.close()
