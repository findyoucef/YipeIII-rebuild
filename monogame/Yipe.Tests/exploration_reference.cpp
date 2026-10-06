// Calls the reference map/action/timer rules; does not simulate the translated code.
#include "yipe/map.hpp"
#include "yipe/exploration.hpp"
#include <iostream>
#include <locale>
using namespace yipe;
namespace {
void state(const Player& p);
void visibility(const MapVisibility& v) {
    for(bool b:v.visible)std::cout<<b;std::cout<<',';for(bool b:v.expanded)std::cout<<b;
}
}
int main() {
    std::cout.imbue(std::locale::classic());
    for(int id=1;id<=134;++id) {
        auto prototype=original_map(id);if(!prototype)continue;
        for(int x=0;x<13;++x)for(int y=0;y<12;++y) {
            auto map=*prototype;MapVisibility v;initialize_map_visibility(v,map,{x,y});
            std::cout<<"visibility|"<<id<<','<<x<<','<<y<<'|';visibility(v);
            map.restore_tile({x,y},0x27);reveal_map_from(v,map,{(x+2)%13,(y+3)%12});std::cout<<'|';visibility(v);std::cout<<'\n';
        }
    }
    for(auto seed:{0u,1u,0xffffffffu})for(int id:{3,6,17,18,27,33,69,87,109})for(int tile=0;tile<=0x71;++tile)for(int variant=0;variant<4;++variant) {
        Map::Tiles tiles;tiles.fill(static_cast<std::uint16_t>(tile));Map map(id,0,tiles);Player p;
        p.level=variant==0?1:20;p.health=variant==0?1:variant==1?3:100;p.maximum_health=100;
        p.difficulty=variant%3+1;p.intelligence=25;p.has_bonus_healing_ring=variant==3;
        p.cave_coins_collected=variant>1;p.bridge_treasure_available=variant<2;p.healing_water_available=variant!=2;
        p.snow_search_count=variant;p.gatekeeper_state=variant;
        MsvcRandom r(seed);auto result=perform_other_action(p,map,{5,5},r);
        std::cout<<"other|"<<seed<<','<<id<<','<<tile<<','<<variant<<','<<result.message_resource_id<<','<<result.sound_id<<','<<result.message<<','<<map.tile({3,7})<<','<<map.tile({4,7})<<','<<r.state();state(p);
    }
    for(int id=0;id<=134;++id)for(int variant=0;variant<2;++variant) {
        Player p;p.level=variant?11:10;p.cave_coins_collected=variant!=0;p.inventory_values[11]=variant;p.has_defeated_wormoid=variant!=0;
        std::cout<<"hint|"<<id<<','<<variant<<','<<map_movement_hint(p,id)<<'\n';
    }
    for(int solved=0;solved<2;++solved)for(int uses:{0,1,5})for(auto code:{"","hhhhhh","HHHHHH","hhhhhhh"}) {
        Player p;p.has_bonus_healing_ring=solved!=0;p.restoration_uses=uses;auto r=enter_secret_tree_code(p,code);
        std::cout<<"tree|"<<solved<<','<<uses<<','<<code<<','<<int(r);state(p);
    }
    { Player p;p.maximum_spell_points=500;p.spell_points=0;p.maximum_ability_points=50;p.ability_points=0;ExplorationTimer timer;
      auto a=*original_map(5);auto b=*original_map(57);
      for(int i=0;i<760;++i){auto& m=i/190%2==0?a:b;MapPosition pos=i%76>=40&&i%76<=65?MapPosition{7,8}:MapPosition{5,5};
        auto tick=timer.advance(p,m,pos);std::cout<<"timer|"<<i<<','<<m.id()<<','<<tick<<','<<m.tile({7,8});state(p);}
    }
    for(int steps:{0,1,2,32767,32768,65535})for(int saved:{0,85,100,101,32767,32768,65535})for(int difficulty:{1,2,3}) {
        Player p;p.monster_repellent_steps=steps;p.saved_random_encounter_threshold=saved;p.random_encounter_threshold=125;p.difficulty=difficulty;
        for(int i=0;i<3;++i){auto ended=advance_repellent_timer(p);std::cout<<"repel|"<<steps<<','<<saved<<','<<difficulty<<','<<i<<','<<ended;state(p);}
    }
    for(int id=1;id<=134;++id) {
        auto h=timed_hazard_for_map(id);if(!h)continue;
        for(auto seed:{0u,1u,0xffffffffu})for(int tile:{0x24,0x2a,0x3c,0x27,0x28,0x29,0x53,0x2b,0x3d,2})for(int rider=0;rider<3;++rider)for(int revealed=0;revealed<2;++revealed) {
            Map::Tiles tiles;tiles.fill(0x27);Map m(id,0,tiles);MapVisibility v;if(revealed)initialize_map_visibility(v,m,{5,5});
            m.restore_tile(h->first,tile);m.restore_tile(h->second,tile);Player p;p.maximum_health=100;p.health=1;p.level=5;
            auto pos=rider==0?MapPosition{5,5}:rider==1?h->first:h->second;std::optional<RoamingMonster> roaming=RoamingMonster{1,h->first};MsvcRandom r(seed);
            auto result=advance_timed_hazard(m,v,p,pos,roaming,r);
            std::cout<<"hazard|"<<id<<','<<seed<<','<<tile<<','<<rider<<','<<revealed<<','<<result.changed<<','<<result.fell<<','<<result.sound_id<<','<<result.damage<<','<<bool(roaming)<<','<<m.tile(h->first)<<','<<m.tile(h->second)<<','<<r.state();state(p);
        }
    }
    for(int direction=0;direction<=6;++direction)for(auto seed:{0u,1u,0xffffffffu})for(int blocked=0;blocked<2;++blocked)for(int rider=0;rider<2;++rider) {
        Map::Tiles tiles;tiles.fill(0x0c);MapEvents events;events.overlay={{5,5},direction};Map m(74,5,tiles,events);MapVisibility v;initialize_map_visibility(v,m,{5,5});
        MsvcRandom r(seed);MapPosition pos=rider?MapPosition{5,5}:MapPosition{1,1};
        for(int i=0;i<40;++i){auto carried=m.advance_overlay(v,pos,r,blocked?std::optional<MapPosition>{{5,4}}:std::nullopt);
            std::cout<<"overlay|"<<direction<<','<<seed<<','<<blocked<<','<<rider<<','<<i<<','<<carried<<','<<pos.x<<','<<pos.y<<','<<m.events().overlay.position.x<<','<<m.events().overlay.position.y<<','<<m.events().overlay.value<<','<<r.state()<<'\n';}
    }
}

namespace {
void state(const yipe::Player& p) {
    std::cout << '|' << p.name;
    std::cout << ',' << +p.dexterity;
    std::cout << ',' << +p.intelligence;
    std::cout << ',' << +p.strength;
    std::cout << ',' << +p.health;
    std::cout << ',' << +p.maximum_health;
    std::cout << ',' << +p.spell_points;
    std::cout << ',' << +p.maximum_spell_points;
    std::cout << ',' << +p.ability_points;
    std::cout << ',' << +p.maximum_ability_points;
    std::cout << ',' << +p.level;
    std::cout << ',' << +p.experience;
    std::cout << ',' << +p.gold;
    std::cout << ',' << +p.weapon_rank;
    std::cout << ',' << +p.armor_rank;
    std::cout << ',' << +p.boots_rank;
    std::cout << ',' << +p.gloves_rank;
    std::cout << ',' << +p.helmet_rank;
    std::cout << ',' << +p.ring_rank;
    std::cout << ',' << +p.necklace_rank;
    std::cout << ',' << +p.difficulty;
    std::cout << ',' << +p.combat_style;
    std::cout << ',' << +p.specialty;
    std::cout << ',' << +p.silly_mode;
    std::cout << ',' << +p.equipment_upgrade_count;
    std::cout << ',' << +p.loot_roll_penalty;
    std::cout << ',' << +p.restoration_uses;
    std::cout << ',' << +p.death_count;
    std::cout << ',' << +p.quest_score;
    std::cout << ',' << +p.quest_progress_total;
    std::cout << ',' << +p.gatekeeper_state;
    std::cout << ',' << +p.stamps_found;
    std::cout << ',' << +p.princesses_found;
    std::cout << ',' << +p.princess_mystery_uses;
    std::cout << ',' << +p.miscellaneous_shop_stage;
    std::cout << ',' << +p.value_06a4;
    std::cout << ',' << +p.has_lucky_rabbit_foot;
    std::cout << ',' << +p.has_waterproof_shoe_spray;
    std::cout << ',' << +p.has_rainbow_key;
    std::cout << ',' << +p.has_underworld_key;
    std::cout << ',' << +p.has_tree_climbing_shoes;
    std::cout << ',' << static_cast<int>(p.special_ability);
    std::cout << ',' << +p.special_ability_duration;
    std::cout << ',' << +p.enrage_base_dexterity;
    std::cout << ',' << +p.enrage_base_intelligence;
    std::cout << ',' << +p.enrage_base_strength;
    std::cout << ',' << +p.monster_defeat_history_complete;
    std::cout << ',' << +p.alternate_map_set;
    std::cout << ',' << +p.has_bonus_healing_ring;
    std::cout << ',' << +p.current_map_id;
    std::cout << ',' << +p.stat_cap_mode;
    std::cout << ',' << +p.surface_return_map_id;
    std::cout << ',' << +p.surface_return_region_x;
    std::cout << ',' << +p.surface_return_region_y;
    std::cout << ',' << +p.random_encounter_threshold;
    std::cout << ',' << +p.saved_random_encounter_threshold;
    std::cout << ',' << +p.monster_repellent_steps;
    std::cout << ',' << +p.pending_relocation;
    std::cout << ',' << +p.entered_hell_through_secret_passage;
    std::cout << ',' << static_cast<int>(p.hell_gatekeeper_state);
    std::cout << ',' << +p.bridge_treasure_available;
    std::cout << ',' << +p.healing_water_available;
    std::cout << ',' << +p.cave_coins_collected;
    std::cout << ',' << +p.snow_search_count;
    std::cout << ',' << +p.has_defeated_wormoid;
    for (const auto value : p.inventory) std::cout << ',' << +value;
    for (const auto value : p.inventory_values) std::cout << ',' << +value;
    for (const auto value : p.quest_flags) std::cout << ',' << +value;
    for (const auto value : p.opened_chests) std::cout << ',' << +value;
    for (const auto value : p.spells) std::cout << ',' << +value;
    for (const auto value : p.encountered_monster_types) std::cout << ',' << +value;
    for (const auto value : p.monster_defeats) std::cout << ',' << +value;
    std::cout << ',' << p.maximum_stat() << '\n';
}
}
