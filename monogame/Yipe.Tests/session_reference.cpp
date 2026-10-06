// Calls the supplied C++ core. The small action sequence follows app/main.cpp's
// door/switch and blocked restoration/fire/teleport dispatch; it is not a full app replay.
#include "yipe/map.hpp"
#include "yipe/player.hpp"
#include "yipe/random.hpp"
#include <cstdint>
#include <iostream>
#include <locale>

namespace {
void map_state(const yipe::Map& map) {
    for (int x=0;x<13;++x) for (int y=0;y<12;++y) std::cout << ',' << map.tile({x,y});
    for (auto p:map.events().chests) std::cout << ',' << p.x << ',' << p.y;
    std::cout << '\n';
}
void profile(yipe::Player& p,int variant,int id) {
    p.strength=variant%2==0?39:40;
    constexpr std::uint16_t flags[]{0,1,4,5,32767,32768,65535};
    p.gatekeeper_state=flags[variant%7]; p.stamps_found=flags[variant%7];
    if(id<=128) p.opened_chests[static_cast<std::size_t>(id-1)]=static_cast<std::uint8_t>(variant);
    p.healing_water_available=false;
}
bool supported(int code) {
    return code==0xc||code==0x12||code==0x13||code==0x14||code==0x17||code==0x1a||code==0x1b;
}
}
int main() {
    std::cout.imbue(std::locale::classic());
    for(int id=1;id<=134;++id) {
        if(!yipe::original_map(id)) continue;
        // Every entry cell, plus every chest bit combination and signed quest boundaries.
        for(int sample=0;sample<172;++sample) {
            auto map=*yipe::original_map(id); yipe::Player player;
            const int variant=sample<156?0:sample-156;
            const yipe::MapPosition entry=sample<156?yipe::MapPosition{sample/12,sample%12}:yipe::MapPosition{5,5};
            profile(player,variant,id);
            yipe::initialize_map_actions(player,map,entry);
            std::cout << "entry|" << id << ',' << sample << ',' << player.healing_water_available;
            map_state(map);
        }
    }
    for(int id=1;id<=134;++id) {
        if(!yipe::original_map(id)) continue;
        for(int mask:{0,15}) for(int x=0;x<13;++x) for(int y=0;y<12;++y) {
            auto map=*yipe::original_map(id); yipe::Player p; yipe::MsvcRandom random(0xffffffffu);
            const yipe::MapPosition from{x==0?1:x-1,y}; const int dx=x==0?-1:1;
            yipe::initialize_map_actions(p,map,from); map.restore_overlay_for_saved_position(from);
            auto candidate=from;
            const yipe::MovementState gear{mask!=0,mask!=0,mask!=0,mask!=0};
            if(!supported(yipe::try_move(map,candidate,dx,0,gear).original_code)) continue;
            p.health=1; p.spell_points=0; p.ability_points=0;
            for(int step=0;step<3;++step) {
                auto position=from;
                const auto result=yipe::try_move(map,position,dx,0,gear);
                bool restored=false; int damage=0;
                if(result.position_changed) {
                    if(result.original_code==0x12) map.open_door(from,position);
                    else if(result.original_code==0x1a) static_cast<void>(map.toggle_linked_tiles());
                } else if(result.original_code==0xc) restored=yipe::use_restoration_tile(p);
                else if(result.original_code==0x17) damage=yipe::apply_fire_wall(p,random);
                else if(result.original_code==0x1b) {
                    if(auto target=map.linked_destination()) position=*target;
                }
                std::cout << "action|" << id << ',' << mask << ',' << x << ',' << y << ',' << step
                    << ',' << result.original_code << ',' << position.x << ',' << position.y << ',' << (position!=from)
                    << ',' << restored << ',' << damage << ',' << p.health << ',' << p.spell_points << ',' << p.ability_points
                    << ',' << p.restoration_uses << ',' << random.state();
                map_state(map);
            }
        }
    }
    for(int difficulty:{0,1,2,3,7}) for(int uses=0;uses<9;++uses) {
        yipe::Player p; p.difficulty=difficulty; p.restoration_uses=uses;
        p.health=1; p.spell_points=0; p.ability_points=0;
        const bool restored=yipe::use_restoration_tile(p);
        std::cout << "restore|" << difficulty << ',' << uses << ',' << restored << ',' << p.health
            << ',' << p.spell_points << ',' << p.ability_points << ',' << p.restoration_uses << '\n';
    }
    for(auto seed:{0u,1u,0xffffffffu}) for(int difficulty:{0,1,2,3})
        for(int level:{0,1,10,100}) for(int health:{1,2,13}) {
            yipe::Player p; p.difficulty=difficulty; p.level=level; p.health=health;
            yipe::MsvcRandom random(seed);
            const int damage=yipe::apply_fire_wall(p,random);
            std::cout << "fire|" << seed << ',' << difficulty << ',' << level << ',' << health
                << ',' << damage << ',' << p.health << ',' << random.state() << '\n';
        }
}
