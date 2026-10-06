// Independent driver of the supplied C++ encounter/spell/loot implementation.
#include "yipe/encounter.hpp"
#include "yipe/spell.hpp"
#include <iostream>
#include <locale>
using namespace yipe;
namespace {
void state(const Player& p);
void reward(const CombatReward& r){std::cout<<r.experience<<','<<r.gold<<','<<int(r.equipment_upgrade)<<','<<r.underworld_key;}
void encounter(const CombatEncounter& e){
    std::cout<<'|'<<e.selected_target<<','<<e.map_environment<<',';reward(e.pending_reward);
    for(const auto& m:e.monsters)std::cout<<'|'<<m.type_id<<','<<m.name<<','<<m.bitmap_resource_id<<','<<m.health<<','<<m.maximum_health<<','<<m.special_rating<<','<<m.rank<<','<<m.attack_rating<<','<<m.defense_rating<<','<<m.gatekeeper_empowered;
}
void turn(const CombatTurn& t){std::cout<<'|'<<t.player_damage<<','<<t.monster_damage<<','<<int(t.outcome)<<',';for(int x:t.monster_damage_by_slot)std::cout<<x<<',';reward(t.reward);std::cout<<','<<t.level_up.leveled_up<<','<<t.level_up.health_increase<<','<<t.level_up.spell_point_increase<<','<<int(t.level_up.stat_increase);}
Player profile(int level,int difficulty,int mode){Player p;p.level=level;p.experience=level==1?0:level*level*30;p.difficulty=difficulty;p.strength=6+level;p.dexterity=5+level;p.intelligence=4+level;p.health=p.maximum_health=12+level*4;p.spell_points=p.maximum_spell_points=30+level*3;p.ability_points=p.maximum_ability_points=15;p.weapon_rank=2+level/2;p.armor_rank=2+level/2;p.helmet_rank=mode+1;p.gloves_rank=mode;p.boots_rank=mode;p.ring_rank=mode;p.necklace_rank=mode;p.special_ability=static_cast<SpecialAbility>(mode);p.specialty=mode%4;p.has_lucky_rabbit_foot=mode==4;p.has_bonus_healing_ring=mode==3;p.spells.fill(1);return p;}
}
int main(){std::cout.imbue(std::locale::classic());
 for(int id=1;id<=41;++id){auto d=*original_monster_definition(id);std::cout<<"definition|"<<id<<','<<d.name<<','<<d.string_resource_id<<','<<d.bitmap_resource_id<<','<<d.health_base<<','<<d.rank_and_health_range<<','<<d.attack_base<<','<<d.defense_base<<','<<d.special_rating<<','<<d.damage_minimum<<','<<d.damage_maximum<<'\n';}
 for(int rank=-1;rank<=44;++rank){auto [a,b]=original_weapon_damage_range(rank);std::cout<<"weapon|"<<rank<<','<<a<<','<<b<<'\n';}
 for(auto seed:{0u,1u,0xffffffffu})for(int id=1;id<=41;++id)for(int count=0;count<=3;++count)for(int mode=0;mode<5;++mode){
  int level=mode==0?1:mode==1?4:mode==2?10:mode==3?25:45;int difficulty=mode%3+1;auto p=profile(level,difficulty,mode);MsvcRandom r(seed);
  EncounterSetup setup{level,difficulty,mode==2?1:0,mode==2?2:0,mode==4?1:0,id-34};p.gatekeeper_state=setup.gatekeeper_state;
  auto e=start_monster_encounter(id,count,r,setup);
  std::cout<<"start|"<<seed<<','<<id<<','<<count<<','<<mode<<','<<player_attack_equipment_rating(p)<<','<<player_defense_equipment_rating(p)<<','<<r.state();encounter(e);state(p);
  for(int i=0;i<16;++i){CombatTurn t;if(i==0&&mode>0)t=resolve_special_ability_turn(p,e,r);else if(i%3==1&&mode>1)t=resolve_spell_turn(p,e,mode==2?2:mode==3?6:7,i%2?SpellPower::high:SpellPower::low,r).turn;else t=resolve_attack_turn(p,e,r);
   std::cout<<"fight|"<<seed<<','<<id<<','<<count<<','<<mode<<','<<i<<','<<r.state();turn(t);encounter(e);state(p);if(t.outcome!=CombatOutcome::ongoing)break;
  }
 }
 for(auto seed:{0u,1u,0xffffffffu})for(int spell=0;spell<12;++spell)for(int power=1;power<=2;++power)for(int count=1;count<=3;++count)for(int mode=0;mode<5;++mode){
  auto p=profile(30,2,mode);p.health=15;p.spells[spell]=mode==0?0:1;if(mode==1)p.spell_points=0;MsvcRandom r(seed);auto e=start_monster_encounter(mode==4?35:4,count,r,30);e.selected_target=count-1;
  auto s=resolve_spell_turn(p,e,spell,static_cast<SpellPower>(power),r);std::cout<<"spell|"<<seed<<','<<spell<<','<<power<<','<<count<<','<<mode<<','<<int(s.status)<<','<<s.healing<<',';for(int x:s.damage_by_slot)std::cout<<x<<',';std::cout<<r.state();turn(s.turn);encounter(e);state(p);
 }
 for(auto seed:{0u,1u,0xffffffffu})for(int id=1;id<=41;++id)for(int mode=0;mode<5;++mode){auto p=profile(mode==0?1:40,mode%3+1,mode);p.health=mode==0?2:mode==1?6:mode==2?20:100;MsvcRandom r(seed);auto e=start_monster_encounter(id,3,r,p.level);auto f=try_flee(p,e,r);
  std::cout<<"flee|"<<seed<<','<<id<<','<<mode<<','<<f.escaped<<','<<f.monster_damage<<','<<f.player_defeated<<',';for(int x:f.monster_damage_by_slot)std::cout<<x<<',';std::cout<<r.state();encounter(e);state(p);
  if(p.health>0&&!f.escaped){auto t=resolve_monster_turn(p,e,r);std::cout<<"counter|"<<r.state();turn(t);encounter(e);state(p);}
 }
 for(auto seed:{0u,1u,0xffffffffu,0xdeadbeefu})for(int level:{1,15,25,50})for(int kind=0;kind<=4;++kind)for(int threshold:{-10,50,100})for(int mode=0;mode<5;++mode){
  auto p=profile(level,2,mode);p.weapon_rank=mode==0?2:mode==1?12:mode==2?14:mode==3?23:29;p.armor_rank=mode==4?26:25;p.equipment_upgrade_count=mode%3;p.loot_roll_penalty=mode==3?100:2;p.stat_cap_mode=mode%3;p.current_map_id=mode==4?0x63:0x11;if(mode==2)p.inventory.fill(1);MsvcRandom r(seed);
  for(int i=0;i<8;++i){auto loot=roll_treasure(p,threshold,kind,r);std::cout<<"loot|"<<seed<<','<<level<<','<<kind<<','<<threshold<<','<<mode<<','<<i<<','<<int(loot.equipment_upgrade)<<','<<loot.item_id<<','<<loot.inventory_full<<','<<loot.waterproof_shoe_spray<<','<<r.state();state(p);}
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
} // namespace
