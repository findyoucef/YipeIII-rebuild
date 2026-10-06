// Independent P1 oracle: calls the supplied C++ save readers/writer and generator.
// Startup ordering follows app/main.cpp::run_game/apply_pending_relocation.
// Legacy input builder and full-player formatter retain the existing C++ test conventions.
#include "yipe/save.hpp"
#include "yipe/legacy_save.hpp"
#include "yipe/random.hpp"
#include <fstream>
#include <iostream>
#include <locale>
#include <limits>
#include <stdexcept>
using namespace yipe;
namespace {
void append_u16(std::vector<std::uint8_t>& b,std::uint16_t v) { b.push_back(v&255); b.push_back(v>>8); }
void append_i32(std::vector<std::uint8_t>& b,std::int32_t v) {
    auto u=static_cast<std::uint32_t>(v); for(int i=0;i<4;++i) b.push_back((u>>(i*8))&255);
}
std::vector<std::uint8_t> legacy_save_bytes(const std::string& name,
                                          std::int32_t wormoid_defeats = 1034,
                                          std::int32_t ability = 2,
                                          std::uint16_t shop_stage = 2,
                                          std::uint16_t value_06a4 = 0) {
    std::vector<std::uint8_t> bytes;
    if (name.size() < 0xff) {
        bytes.push_back(static_cast<std::uint8_t>(name.size()));
    } else {
        bytes.push_back(0xff);
        append_u16(bytes, static_cast<std::uint16_t>(name.size()));
    }
    bytes.insert(bytes.end(), name.begin(), name.end());

    for (const std::int32_t value : std::array<std::int32_t, 13>{
             13, 12, 11, 7, 13, 4, 9, 2, 6, 8, 123, 6, 7}) {
        append_i32(bytes, value);
    }
    append_i32(bytes, 533);
    for (const std::uint16_t value : std::array<std::uint16_t, 5>{1, 2, 3, 4, 5}) {
        append_u16(bytes, value);
    }
    append_i32(bytes, 2);
    append_u16(bytes, 9);
    append_i32(bytes, 3);
    append_u16(bytes, 1);
    append_u16(bytes, 2);
    append_i32(bytes, ability);
    append_i32(bytes, 55);

    for (const bool value : std::array{false, true, true, false, true, true, true}) {
        append_i32(bytes, value ? 1 : 0);
    }
    for (std::size_t index = 0; index < 512; ++index) {
        append_i32(bytes, index == 7 ? 1 : 0);
        append_i32(bytes, index == 8 ? 1 : 0);
    }
    for (std::uint16_t index = 0; index < 15; ++index) {
        append_u16(bytes, static_cast<std::uint16_t>(index + 1));
    }
    for (std::size_t index = 0; index < 10; ++index) {
        append_i32(bytes, index == 4 ? 1 : 0);
        append_u16(bytes, index < 2 ? 1 : 0);
    }
    for (std::size_t index = 0; index < 30; ++index) {
        append_u16(bytes, index == 0 ? 5 : index == 29 ? 9 : 0);
        append_u16(bytes, static_cast<std::uint16_t>(index + 10));
        append_i32(bytes, index == 29 ? 1 : 0);
    }
    for (std::size_t index = 0; index < 50; ++index) {
        std::uint16_t value = 0;
        switch (index) {
        case 2: value = 2; break;
        case 3: value = 1; break;
        case 4: value = 0xfffc; break;
        case 5: value = 0xffff; break;
        case 6: value = 125; break;
        case 8: value = 4; break;
        case 9: value = 11; break;
        case 11: value = 1; break;
        case 14: value = 1; break;
        case 15: value = 3; break;
        case 16: value = 85; break;
        case 17: value = 22; break;
        case 18: value = 7; break;
        case 19: value = 0xffff; break;
        case 20: value = shop_stage; break;
        case 26: value = 2; break;
        case 27: value = 1; break;
        case 30: value = 3; break;
        case 31: value = 1; break;
        case 35: value = value_06a4; break;
        default: break;
        }
        append_u16(bytes, value);
        append_i32(bytes, index == 34 ? wormoid_defeats
                                     : static_cast<std::int32_t>(1000 + index));
        append_i32(bytes, index == 4 || index == 36 || index == 38 ? 1 : 0);
    }
    for (const std::uint16_t value : std::array<std::uint16_t, 5>{2, 8, 0, 1, 0}) {
        append_u16(bytes, value);
    }
    append_i32(bytes, 797);
    append_i32(bytes, 4321);
    append_i32(bytes, 1);
    return bytes;
}

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
void profile(Player& p,int variant) {
    p.name="Reference-"+std::to_string(variant);
    p.dexterity=static_cast<int>(106 + variant);
    p.intelligence=static_cast<int>(107 + variant);
    p.strength=static_cast<int>(108 + variant);
    p.health=static_cast<int>(109 + variant);
    p.maximum_health=static_cast<int>(110 + variant);
    p.spell_points=static_cast<int>(111 + variant);
    p.maximum_spell_points=static_cast<int>(112 + variant);
    p.ability_points=static_cast<int>(113 + variant);
    p.maximum_ability_points=static_cast<int>(114 + variant);
    p.level=static_cast<int>(115 + variant);
    p.experience=static_cast<int>(116 + variant);
    p.gold=static_cast<int>(117 + variant);
    p.weapon_rank=static_cast<int>(118 + variant);
    p.armor_rank=static_cast<int>(119 + variant);
    p.boots_rank=static_cast<std::uint16_t>(120 + variant);
    p.gloves_rank=static_cast<std::uint16_t>(121 + variant);
    p.helmet_rank=static_cast<std::uint16_t>(122 + variant);
    p.ring_rank=static_cast<std::uint16_t>(123 + variant);
    p.necklace_rank=static_cast<std::uint16_t>(124 + variant);
    p.difficulty=static_cast<int>(125 + variant);
    p.combat_style=static_cast<int>(126 + variant);
    p.specialty=static_cast<int>(127 + variant);
    p.silly_mode=(variant % 2 == 0);
    p.equipment_upgrade_count=static_cast<int>(129 + variant);
    p.loot_roll_penalty=static_cast<int>(130 + variant);
    p.restoration_uses=static_cast<int>(131 + variant);
    p.death_count=static_cast<int>(132 + variant);
    p.quest_score=static_cast<int>(133 + variant);
    p.quest_progress_total=static_cast<int>(134 + variant);
    p.gatekeeper_state=static_cast<std::uint16_t>(135 + variant);
    p.stamps_found=static_cast<std::uint16_t>(136 + variant);
    p.princesses_found=static_cast<std::uint16_t>(137 + variant);
    p.princess_mystery_uses=static_cast<std::uint16_t>(138 + variant);
    p.miscellaneous_shop_stage=static_cast<std::uint16_t>(139 + variant);
    p.value_06a4=static_cast<std::uint16_t>(140 + variant);
    p.has_lucky_rabbit_foot=(variant % 2 == 0);
    p.has_waterproof_shoe_spray=(variant % 2 == 0);
    p.has_rainbow_key=(variant % 2 == 0);
    p.has_underworld_key=(variant % 2 == 0);
    p.has_tree_climbing_shoes=(variant % 2 == 0);
    p.special_ability_duration=static_cast<int>(146 + variant);
    p.enrage_base_dexterity=static_cast<int>(147 + variant);
    p.enrage_base_intelligence=static_cast<int>(148 + variant);
    p.enrage_base_strength=static_cast<int>(149 + variant);
    p.monster_defeat_history_complete=(variant % 2 == 0);
    p.alternate_map_set=(variant % 2 == 0);
    p.has_bonus_healing_ring=(variant % 2 == 0);
    p.current_map_id=static_cast<int>(153 + variant);
    p.stat_cap_mode=static_cast<int>(154 + variant);
    p.surface_return_map_id=static_cast<std::int16_t>(155 + variant);
    p.surface_return_region_x=static_cast<std::int16_t>(156 + variant);
    p.surface_return_region_y=static_cast<std::int16_t>(157 + variant);
    p.random_encounter_threshold=static_cast<std::uint16_t>(158 + variant);
    p.saved_random_encounter_threshold=static_cast<std::uint16_t>(159 + variant);
    p.monster_repellent_steps=static_cast<std::uint16_t>(160 + variant);
    p.pending_relocation=static_cast<std::uint16_t>(161 + variant);
    p.entered_hell_through_secret_passage=(variant % 2 == 0);
    p.bridge_treasure_available=(variant % 2 == 0);
    p.healing_water_available=(variant % 2 == 0);
    p.cave_coins_collected=(variant % 2 == 0);
    p.snow_search_count=static_cast<std::uint16_t>(166 + variant);
    p.has_defeated_wormoid=(variant % 2 == 0);
    p.level=12; p.difficulty=variant%3+1; p.specialty=variant%4;
    p.health=4; p.maximum_health=200; p.spell_points=7; p.maximum_spell_points=150;
    p.ability_points=2; p.maximum_ability_points=12; p.intelligence=17;
    p.special_ability=static_cast<SpecialAbility>(variant%5);
    p.hell_gatekeeper_state=static_cast<HellGatekeeperState>(variant%4-2);
    p.miscellaneous_shop_stage=variant%5; p.pending_relocation=variant%5;
    p.surface_return_map_id=21; p.surface_return_region_x=-2; p.surface_return_region_y=-1;
    for(std::size_t i=0;i<p.inventory.size();++i) {
        p.inventory[i]=static_cast<std::uint16_t>((i+1)*1777+variant);
        p.inventory_values[i]=static_cast<std::uint16_t>(65535-i*47-variant);
        p.quest_flags[i]=(i+variant)%2==0;
    }
    p.inventory[0]=1;
    for(std::size_t i=0;i<p.opened_chests.size();++i) p.opened_chests[i]=(i+variant)%16;
    for(std::size_t i=0;i<p.spells.size();++i) p.spells[i]=static_cast<std::uint16_t>(i+variant);
    for(std::size_t i=0;i<p.encountered_monster_types.size();++i) p.encountered_monster_types[i]=(i+variant)%2==0;
    for(std::size_t i=0;i<p.monster_defeats.size();++i) p.monster_defeats[i]=i%2==0
        ? std::numeric_limits<std::int32_t>::min()+static_cast<int>(i) : std::numeric_limits<std::int32_t>::max()-static_cast<int>(i);
}

Map load(const WorldLocation& l,MsvcRandom& r) {
    if(l.map_id>=118&&l.map_id<=120) return generate_original_map(l.map_id,l.position,r);
    return original_map(l.map_id).value();
}
Map reopen(GameState& g) {
    MsvcRandom r(g.random_state);
    auto map=load(g.location,r);
    auto relocation=apply_pending_relocation(g.player,g.location);
    if(relocation!=PendingRelocation::none) {
        map=load(g.location,r);
        if(relocation==PendingRelocation::surface) if(auto p=surface_return_position(map)) g.location.position=*p;
    }
    g.player.current_map_id=g.location.map_id; g.player.alternate_map_set=g.location.alternate_map_set;
    initialize_map_actions(g.player,map,g.location.position);
    for(auto c:g.map_tile_changes) if(c.map_id==map.id()) map.restore_tile(c.position,c.tile);
    if(relocation==PendingRelocation::none) map.restore_overlay_for_saved_position(g.location.position);
    g.random_state=r.state(); return map;
}
void map_state(const Map& m) {
    std::cout<<m.id()<<','<<m.environment();
    for(int x=0;x<13;++x) for(int y=0;y<12;++y) std::cout<<','<<m.tile({x,y});
    for(auto p:m.events().chests) std::cout<<','<<p.x<<','<<p.y;
    std::cout<<'\n';
}
void snapshot(const GameState& g,const Map& m) {
    state(g.player);
    auto l=g.location;
    std::cout<<l.position.x<<','<<l.position.y<<','<<l.region_x<<','<<l.region_y<<','<<l.map_layer<<','
        <<l.alternate_map_set<<','<<l.map_id<<','<<g.random_state<<'\n';
    std::cout<<g.map_tile_changes.size();
    for(auto c:g.map_tile_changes) std::cout<<','<<c.map_id<<','<<c.position.x<<','<<c.position.y<<','<<c.tile;
    std::cout<<'\n'<<g.defeated_monster_placements.size();
    for(auto d:g.defeated_monster_placements) std::cout<<','<<d;
    std::cout<<'\n'; map_state(m);
}
void expected(GameState g,const std::filesystem::path& path) {
    std::ofstream out(path); auto old=std::cout.rdbuf(out.rdbuf());
    for(int cycle=0;cycle<4;++cycle) {
        auto map=reopen(g); snapshot(g,map);
        MsvcRandom r(g.random_state); g.player.inventory[0]=1;
        static_cast<void>(g.player.use_inventory_item(0,r,true,false,g.location.map_id,g.location.alternate_map_set));
        g.random_state=r.state(); snapshot(g,map);
    }
    std::cout.rdbuf(old);
}
}
int main(int argc,char** argv) {
    if(argc!=2) return 2;
    std::cout.imbue(std::locale::classic());
    const std::filesystem::path dir(argv[1]); std::filesystem::create_directories(dir);
    for(int sample=0;sample<20;++sample) {
        GameState g; profile(g.player,sample);
        if(sample<5) { g.location.map_id=61; g.location.region_x=-2; g.location.region_y=-1; g.location.map_layer=1; }
        else { g.location.map_id=118+(sample-5)%3; g.location.region_x=g.location.map_id-121; g.location.region_y=-1; g.location.alternate_map_set=true; }
        g.location.position={1+sample%11,1+sample%10};
        g.player.current_map_id=g.location.map_id; g.player.alternate_map_set=g.location.alternate_map_set;
        g.random_state=sample%2==0?2566201570u:0xffffffffu;
        g.map_tile_changes={{17,{6,5},0x27},{g.location.map_id,{4,4},0x27}};
        g.defeated_monster_placements={0,5,185};
        auto path=dir/("native-"+std::to_string(sample)+".y3r"); save_game_atomic(path,g);
        expected(load_game(path),path.string()+".txt");
    }
    for(int sample=0;sample<4;++sample) {
        auto bytes=legacy_save_bytes(sample==1?std::string(300,'L'):"Original-"+std::to_string(sample),
            sample==2?std::numeric_limits<std::int32_t>::max():1034,sample, sample,5);
        auto path=dir/("original-"+std::to_string(sample)+".yp3");
        { std::ofstream file(path,std::ios::binary); file.write(reinterpret_cast<const char*>(bytes.data()),bytes.size()); }
        auto legacy=load_legacy_save(path); if(!legacy.validation_passed) throw std::runtime_error("bad legacy fixture");
        expected(legacy.game,path.string()+".txt");
    }
    std::ofstream generated(dir/"generated.txt"); auto old=std::cout.rdbuf(generated.rdbuf());
    for(auto seed:{0u,1u,2u,0x7fffffffu,0x80000000u,0xffffffffu,0xdeadbeefu,2566201570u})
        for(int id=118;id<=120;++id) for(int x=1;x<=11;++x) for(int y=1;y<=10;++y) {
            MsvcRandom r(seed); auto m=generate_original_map(id,{x,y},r);
            std::cout<<seed<<','<<id<<','<<x<<','<<y<<','<<r.state()<<'|'; map_state(m);
        }
    std::cout.rdbuf(old);
}
