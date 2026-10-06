// Independent map oracle. Link to src/map.cpp, src/player.cpp, src/random.cpp and
// src/generated/original_maps.cpp. See README.md for reproduction and trace format.
#include "yipe/map.hpp"
#include "yipe/player.hpp"
#include <array>
#include <iostream>
#include <locale>
#include <stdexcept>

namespace {
constexpr std::array directions{
    yipe::MapPosition{-1,0}, yipe::MapPosition{1,0}, yipe::MapPosition{0,-1}, yipe::MapPosition{0,1},
    yipe::MapPosition{-1,-1}, yipe::MapPosition{1,-1}, yipe::MapPosition{-1,1}, yipe::MapPosition{1,1},
};
void point(yipe::MapPosition p) { std::cout << ',' << p.x << ',' << p.y; }
void event(yipe::MapEvent e) { point(e.position); std::cout << ',' << e.value; }
void map_state(const yipe::Map& map) {
    std::cout << '|' << map.id() << ',' << map.environment();
    for (int x=0; x<13; ++x) for (int y=0; y<12; ++y) std::cout << ',' << map.tile({x,y});
    const auto& e=map.events();
    event(e.message); event(e.overlay);
    for (auto v:e.depth) event(v);
    for (auto v:e.linked) event(v);
    point(e.princess.position);
    std::cout << ',' << e.princess.portrait_variant << ',' << e.princess.rescue_index;
    point(e.pursuer.position);
    std::cout << ',' << e.pursuer.type_id << ',' << e.pursuer.upgraded_type_id << ',' << e.pursuer.upgrade_level;
    for (int level:{0,1,5,6,12,30,100}) std::cout << ',' << e.pursuer.type_for_level(level);
    for (auto v:e.chests) point(v);
    point(map.linked_destination().value_or(yipe::MapPosition{-1,-1}));
    point(yipe::surface_return_position(map).value_or(yipe::MapPosition{-1,-1}));
    std::cout << '\n';
}
void location(const yipe::WorldLocation& l) {
    point(l.position);
    std::cout << ',' << l.region_x << ',' << l.region_y << ',' << l.map_layer
              << ',' << l.alternate_map_set << ',' << l.map_id;
}
void player_location(const yipe::Player& p) {
    std::cout << ',' << p.pending_relocation << ',' << p.current_map_id << ',' << p.alternate_map_set
              << ',' << p.entered_hell_through_secret_passage << ',' << static_cast<int>(p.hell_gatekeeper_state)
              << ',' << p.surface_return_map_id << ',' << p.surface_return_region_x << ',' << p.surface_return_region_y;
}
yipe::MovementState gear(int mask) { return {(mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0}; }
void move(const yipe::Map& map,yipe::MapPosition from,yipe::MapPosition delta,int mask) {
    const auto result=yipe::try_move(map,from,delta.x,delta.y,gear(mask));
    std::cout << '|' << result.original_code << ',' << from.x << ',' << from.y << ',' << result.position_changed << '\n';
}
}

int main() {
    std::cout.imbue(std::locale::classic());
    for(int id=-1;id<=134;++id) {
        std::cout << "catalog|" << id;
        const auto map=yipe::original_map(id);
        if(map) map_state(*map); else std::cout << "|missing\n";
    }
    for(int id=-1;id<=134;++id) {
        std::cout << "chests|" << id;
        for(auto p:yipe::original_chests_for_map(id)) point(p);
        std::cout << '\n';
    }
    for(int id=1;id<=133;++id) {
        const auto map=yipe::original_map(id);
        if(!map) continue;
        for(int mask:{0,15}) for(int x=0;x<13;++x) for(int y=0;y<12;++y) for(auto delta:directions) {
            std::cout << "moves|" << id << ',' << mask << ',' << x << ',' << y << ',' << delta.x << ',' << delta.y;
            move(*map,{x,y},delta,mask);
        }
    }
    for(int raw=0;raw<=128;++raw) for(int mask=0;mask<16;++mask) for(int variant=0;variant<6;++variant) {
        const int tile=raw==128?65535:raw;
        yipe::Map::Tiles tiles{};
        tiles.fill(2);
        const yipe::MapPosition target=variant==1||variant==3?yipe::MapPosition{0,5}:yipe::MapPosition{6,5};
        const yipe::MapPosition from{target.x==0?1:5,5};
        const yipe::MapPosition delta{target.x==0?-1:1,0};
        tiles[static_cast<std::size_t>(target.x*12+target.y)]=static_cast<std::uint16_t>(tile);
        yipe::MapEvents events;
        if(variant==2) events.depth[0]={target,0x3a};
        if(variant==3||variant==4) events.depth[0]={target,0x3b};
        if(variant==4||variant==5) events.overlay={target,variant==4?1:0};
        const yipe::Map map(0,0,tiles,events);
        std::cout << "tiles|" << tile << ',' << mask << ',' << variant;
        move(map,from,delta,mask);
    }
    for(int id=1;id<=133;++id) {
        auto map=yipe::original_map(id);
        if(!map) continue;
        for(int attempt=0;attempt<2;++attempt) {
            const bool toggled=map->toggle_linked_tiles();
            std::cout << "mutations|" << id << ",toggle," << attempt << ',' << toggled;
            map_state(*map);
        }
        map->open_door({5,5},{6,5});
        std::cout << "mutations|" << id << ",door"; map_state(*map);
        for(int slot=0;slot<4;++slot) {
            map->remove_chest(static_cast<std::size_t>(slot));
            std::cout << "mutations|" << id << ",chest," << slot; map_state(*map);
        }
    }
    for(int id=1;id<=133;++id) for(auto p:std::array{yipe::MapPosition{5,5},yipe::MapPosition{6,5},
        yipe::MapPosition{2,2},yipe::MapPosition{-1,-1},yipe::MapPosition{13,12}}) for(int op=0;op<2;++op) {
        auto map=yipe::original_map(id);
        if(!map) continue;
        if(op==0) map->relocate_overlay_after_combat(p); else map->restore_overlay_for_saved_position(p);
        std::cout << "overlays|" << id << ',' << p.x << ',' << p.y << ',' << op;
        event(map->events().overlay); std::cout << '\n';
    }
    constexpr std::array positions{yipe::MapPosition{0,0},yipe::MapPosition{12,11},yipe::MapPosition{0,5},
        yipe::MapPosition{12,5},yipe::MapPosition{5,0},yipe::MapPosition{5,11},yipe::MapPosition{5,5},
        yipe::MapPosition{0,11},yipe::MapPosition{12,0}};
    for(int rx=-4;rx<=1;++rx) for(int ry=-3;ry<=2;++ry) for(int layer=0;layer<3;++layer)
        for(bool alternate:{false,true}) for(auto p:positions) for(int op:{-3,-2,-1,0,1,14,15,16,17}) {
            yipe::WorldLocation l{p,rx,ry,layer,alternate,999};
            bool changed=true;
            if(op==-3) changed=yipe::cross_map_edge(l);
            else if(op==-2) yipe::ascend_to_surface(l);
            else if(op==-1) yipe::return_to_surface(l);
            else changed=yipe::change_map_depth(l,op);
            std::cout << "world|" << rx << ',' << ry << ',' << layer << ',' << alternate
                << ',' << p.x << ',' << p.y << ',' << op << '|' << changed;
            location(l); std::cout << '\n';
        }
    for(int code:{0,1,2,3,4,5,65535}) for(int gate:{-2,-1,0,1}) for(int return_map:{-1,1,17,133}) {
        yipe::Player p;
        p.pending_relocation=static_cast<std::uint16_t>(code);
        p.hell_gatekeeper_state=static_cast<yipe::HellGatekeeperState>(gate);
        p.surface_return_map_id=static_cast<std::int16_t>(return_map);
        p.surface_return_region_x=-4; p.surface_return_region_y=2;
        p.current_map_id=77; p.alternate_map_set=true;
        yipe::WorldLocation l{{8,9},1,-2,2,true,88};
        int result=-1;
        try { result=static_cast<int>(yipe::apply_pending_relocation(p,l)); }
        catch(const std::runtime_error&) { }
        std::cout << "relocations|" << code << ',' << gate << ',' << return_map << '|' << result;
        location(l); player_location(p); std::cout << '\n';
    }
    for(int gate:{-2,-1,0,1}) for(bool secret:{false,true}) {
        yipe::Player p;
        p.hell_gatekeeper_state=static_cast<yipe::HellGatekeeperState>(gate);
        yipe::WorldLocation l;
        yipe::enter_hell(p,l,secret);
        std::cout << "hell|" << gate << ',' << secret << '|';
        location(l); player_location(p); std::cout << '\n';
    }
}
