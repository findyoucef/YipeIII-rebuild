#include "yipe/quest.hpp"
#include "yipe/map.hpp"
#include <iostream>
#include <locale>
using namespace yipe;
namespace { void state(const Player& p); }
int main(){std::cout.imbue(std::locale::classic());
 for(auto seed:{0u,1u,0xffffffffu})for(int stamps=0;stamps<=6;++stamps)for(int princesses:{0,3,10,19,20})for(int gate=0;gate<=3;++gate)for(int mode=0;mode<3;++mode){
  Player p;p.stamps_found=stamps;p.princesses_found=princesses;p.gatekeeper_state=gate;p.quest_progress_total=60;p.has_waterproof_shoe_spray=mode>0;if(mode==1){p.quest_flags[0]=true;p.quest_flags[1]=true;}else if(mode==2)p.quest_flags.fill(true);
  MsvcRandom r(seed);for(int i=0;i<8;++i){auto k=talk_to_king(p,r,i*3);std::cout<<"king|"<<seed<<','<<stamps<<','<<princesses<<','<<gate<<','<<mode<<','<<i<<','<<k.greeting_resource_id<<','<<k.message_resource_id<<','<<k.footer_resource_id<<','<<k.greeting<<','<<k.message<<','<<k.footer<<','<<r.state();state(p);}
 }
 for(auto seed:{0u,1u,0xffffffffu})for(int level:{1,15,30,45})for(int gift=1;gift<=4;++gift)for(int mode=0;mode<6;++mode)for(int index:{0,1,2,13,19,29}){
  Player p;p.level=level;p.maximum_health=100;p.health=3;p.maximum_spell_points=80;p.spell_points=1;p.maximum_ability_points=10;p.ability_points=2;p.has_rainbow_key=mode==1;p.ring_rank=mode;p.princess_mystery_uses=mode*5;p.princesses_found=mode==5?65535:0;MsvcRandom r(seed);
  for(int i=0;i<2;++i){auto v=rescue_princess(p,index,static_cast<PrincessGift>(gift),r);std::cout<<"rescue|"<<seed<<','<<level<<','<<gift<<','<<mode<<','<<index<<','<<i<<','<<v.rescued<<','<<v.message_resource_id<<','<<v.amount<<','<<v.message<<','<<r.state();state(p);}
 }
 for(auto seed:{0u,1u,0xffffffffu})for(int id=1;id<=134;++id){auto original=original_map(id);if(!original)continue;for(int slot=0;slot<4;++slot){auto map=*original;Player p;p.level=id%40+1;p.difficulty=id%3+1;MsvcRandom r(seed);auto pos=map.events().chests[slot];
  for(int i=0;i<2;++i){auto v=open_chest(p,map,pos,slot,r);std::cout<<"chest|"<<seed<<','<<id<<','<<slot<<','<<i<<','<<v.opened<<','<<v.experience<<','<<v.gold<<','<<int(v.loot.equipment_upgrade)<<','<<v.loot.item_id<<','<<v.loot.inventory_full<<','<<v.loot.waterproof_shoe_spray<<','<<map.events().chests[slot].x<<','<<map.events().chests[slot].y<<','<<r.state();state(p);}
 }}
 for(auto seed:{0u,1u,0xffffffffu})for(int level:{1,2,12,31,45})for(int deaths=0;deaths<13;++deaths)for(int monster:{1,5,41})for(int mode=0;mode<4;++mode){Player p;p.level=level;p.death_count=deaths;p.gold=mode*20;p.health=0;p.strength=mode==0?10:40;p.dexterity=p.strength;p.armor_rank=mode*3;p.boots_rank=p.gloves_rank=p.ring_rank=mode;p.gatekeeper_state=mode==3?1:0;WorldLocation l;l.map_id=57;l.map_layer=1;l.region_x=0;l.region_y=0;l.position={5,8};MsvcRandom r(seed);auto d=resolve_combat_defeat(p,l,monster,r);
  std::cout<<"death|"<<seed<<','<<level<<','<<deaths<<','<<monster<<','<<mode<<','<<d.message_resource_id<<','<<d.message<<','<<r.state()<<'|'<<l.map_id<<','<<l.map_layer<<','<<l.region_x<<','<<l.region_y<<','<<l.position.x<<','<<l.position.y<<','<<l.alternate_map_set;state(p);
 }
 for(int difficulty=1;difficulty<=3;++difficulty)for(int level:{1,25,50})for(int gold:{0,1,9,10,99,1000,10000}){Player p;p.difficulty=difficulty;p.level=level;p.gold=gold;p.maximum_health=100;p.health=1;auto quote=gatekeeper_recovery_fee(p);auto fee=pay_gatekeeper_recovery(p);std::cout<<"fee|"<<difficulty<<','<<level<<','<<gold<<','<<quote<<','<<fee;state(p);}
 for(int gate=0;gate<=3;++gate)for(int rescued=0;rescued<=30;++rescued){Player p;p.gatekeeper_state=gate;for(int i=0;i<rescued;++i)p.inventory_values[i]=1;auto ended=complete_gatekeeper_finale(p);std::cout<<"finale|"<<gate<<','<<rescued<<','<<ended;state(p);}
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
} // namespace
