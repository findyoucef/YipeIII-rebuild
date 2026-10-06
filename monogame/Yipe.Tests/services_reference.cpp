// Independent oracle for the translated shops, exploration spells and casino rules.
#include "yipe/shop.hpp"
#include "yipe/spell.hpp"
#include "yipe/casino.hpp"
#include <iostream>
#include <locale>
#include <limits>
using namespace yipe;
namespace {
void state(const Player& p);
constexpr std::uint32_t seeds[]{0u,1u,2u,0x7fffffffu,0x80000000u,0xffffffffu,0xdeadbeefu,2566201570u};
void result(int status,int amount,std::uint32_t rng,const Player& p) {
    std::cout << status << ',' << amount << ',' << rng; state(p);
}
}
int main() {
    std::cout.imbue(std::locale::classic());
    for(int map:{17,27,8,12}) for(int stage=0;stage<5;++stage) for(int key=0;key<2;++key)
        for(int difficulty=1;difficulty<=3;++difficulty) {
            Player base; base.miscellaneous_shop_stage=stage; base.has_rainbow_key=key!=0; base.difficulty=difficulty;
            auto stock=miscellaneous_shop_stock(base,map);
            for(auto item:stock) for(int mode=0;mode<5;++mode) {
                auto p=base; p.gold=mode==0?0:mode==1?item.price:99999;
                if(mode==3) p.inventory.fill(1);
                if(mode==4) p.helmet_rank=p.gloves_rank=p.ring_rank=p.boots_rank=p.necklace_rank=10;
                auto r=purchase_shop_item(p,item);
                std::cout<<"shop|"<<map<<','<<stage<<','<<key<<','<<difficulty<<','<<mode<<','<<item.name<<','<<item.price<<','<<int(item.kind)<<','<<item.type<<','<<item.tier<<'|';
                result(int(r.status),int(r.inventory_slot),1,p);
            }
        }
    for(int stage=0;stage<5;++stage) for(int level:{25,26}) for(int difficulty:{1,2,3}) for(int strength:{30,31,32,33}) {
        Player p; p.miscellaneous_shop_stage=stage; p.level=level; p.difficulty=difficulty; p.strength=strength;
        auto notice=enter_miscellaneous_shop(p); std::cout<<"enter|"; result(notice,0,1,p);
    }
    for(auto kind:{EquipmentKind::weapon,EquipmentKind::armor}) {
        for(int map:{17,27,8,12}) for(auto item:equipment_shop_stock(kind,map))
            std::cout<<"stock|"<<int(kind)<<','<<map<<','<<item.rank<<','<<item.price<<'\n';
        for(int owned:{1,2,15}) for(int rank=-1;rank<=43;++rank) for(int offset:{-1,0,1}) {
            Player p; p.weapon_rank=p.armor_rank=owned; p.gold=equipment_price(kind,rank)+offset;
            auto confirmation=equipment_sale_requires_confirmation(p,kind); auto sale=equipment_sale_value(p,kind);
            auto buy=purchase_equipment(p,kind,rank); std::cout<<"equip|"<<int(kind)<<','<<owned<<','<<rank<<','<<offset<<','<<confirmation<<','<<sale<<'|';result(int(buy),0,1,p);
            auto sold=sell_equipment(p,kind);std::cout<<"sell|";result(int(sold),0,1,p);
        }
    }
    for(auto seed:seeds) for(int level:{1,17,18,36}) for(int service=0;service<6;++service) for(int health:{0,1,50,51}) for(int rich=0;rich<2;++rich) {
        Player p;p.level=level;p.maximum_health=p.maximum_spell_points=50;p.health=p.spell_points=health;p.gold=rich?1000:0;
        MsvcRandom random(seed);auto r=service<4?purchase_healing(p,service,random):purchase_spell_point_healing(p,service==5,random);
        std::cout<<"heal|"<<seed<<','<<level<<','<<service<<','<<health<<','<<rich<<'|';result(int(r.status),r.amount,random.state(),p);
    }
    for(int difficulty=1;difficulty<=3;++difficulty) for(int known=0;known<=12;++known) for(int level:{1,7,17,40,50}) for(int gold:{0,100,101,1000,1001}) {
        Player p;p.spells.fill(0);for(int i=0;i<known;++i)p.spells[i]=1;p.difficulty=difficulty;p.level=level;p.gold=gold;
        auto offer=next_spell_offer(p);std::cout<<"learn|"<<difficulty<<','<<known<<','<<level<<','<<gold<<','<<(offer?int(offer->index):-1)<<','<<(offer?offer->required_level:0)<<'|';
        auto r=learn_next_spell(p);result(int(r),0,1,p);
    }
    for(int environment=-1;environment<=8;++environment)for(int ability=0;ability<=4;++ability)for(int gold:{0,149,150,250,400}) {
        Player p;p.special_ability=static_cast<SpecialAbility>(ability);p.gold=gold;auto offer=ability_offer_for_environment(environment);
        std::cout<<"train|"<<environment<<','<<ability<<','<<gold<<','<<(offer?int(offer->ability):0)<<'|';auto r=train_ability(p,environment);result(int(r),0,1,p);
    }
    for(auto seed:seeds)for(int spell=0;spell<12;++spell)for(int specialty=0;specialty<4;++specialty)for(int power=1;power<=2;++power)for(int variant=0;variant<5;++variant) {
        Player p;p.specialty=specialty;p.level=17;p.intelligence=25;p.health=3;p.maximum_health=100;p.ability_points=1;p.maximum_ability_points=10;
        p.has_bonus_healing_ring=variant==4;p.monster_repellent_steps=65530;p.random_encounter_threshold=variant==4?125:85;
        p.spells[spell]=variant==0?0:1;p.spell_points=variant==1?0:500;
        WorldLocation l;l.map_layer=variant==2?0:1;l.alternate_map_set=variant==3;
        MsvcRandom random(seed);auto r=cast_exploration_spell(p,l,spell,static_cast<SpellPower>(power),random);
        std::cout<<"spell|"<<seed<<','<<spell<<','<<specialty<<','<<power<<','<<variant<<','<<spell_point_cost(p,spell,static_cast<SpellPower>(power))<<','<<r.repellent_duration<<','<<r.location_changed<<'|';
        result(int(r.status),r.healing,random.state(),p);
    }
    for(auto seed:seeds) {
        MsvcRandom r(seed);SlotMachineState slots{1000,0};
        for(int i=0;i<100;++i) {
            if(i%3==0)static_cast<void>(insert_slot_credits(slots));auto s=spin_slots(slots,r);
            std::cout<<"slots|"<<seed<<','<<i<<','<<s.spun<<','<<s.payout<<','<<s.symbols[0]<<','<<s.symbols[1]<<','<<s.symbols[2]<<','<<slots.gold<<','<<slots.credits<<','<<r.state()<<'\n';
            if(i%7==0)collect_slot_credits(slots);
        }
        for(int selected=0;selected<5;++selected)for(int bet:{0,1,100,250}) {
            CasinoWager w{1000,bet};auto race=race_dots(w,selected,r);
            std::cout<<"race|"<<seed<<','<<selected<<','<<bet<<','<<race.raced<<','<<race.winner<<','<<race.payout<<','<<race.final_positions[0]<<','<<race.final_positions[1]<<','<<race.final_positions[2]<<','<<w.gold<<','<<w.bet<<','<<r.state()<<'\n';
        }
        for(int aim=16;aim<=80;++aim)for(int i=0;i<3;++i) {
            auto dart=throw_dart(aim,i,r);std::cout<<"dart|"<<seed<<','<<aim<<','<<i<<','<<dart.final_y<<','<<dart.score<<','<<r.state()<<'\n';
        }
    }
    for(int gold:{0,10,100,1000})for(int bet:{0,249,250})for(int amount:{-1,0,1,10,250,251}) {
        CasinoWager w{gold,bet};auto s=place_casino_wager(w,amount);std::cout<<"wager|"<<gold<<','<<bet<<','<<amount<<','<<int(s)<<','<<w.gold<<','<<w.bet;
        clear_casino_wager(w);std::cout<<','<<w.gold<<','<<w.bet<<'\n';
    }
    for(int aim=10;aim<=90;++aim)std::cout<<"aim|"<<aim<<','<<adjust_dart_aim(aim,false)<<','<<adjust_dart_aim(aim,true)<<'\n';
    for(int total=0;total<=180;++total)std::cout<<"payout|"<<total<<','<<dart_round_payout({total,0,0},250)<<'\n';
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
