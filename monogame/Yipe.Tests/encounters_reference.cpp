#include "yipe/encounter.hpp"
#include <iostream>
#include <locale>
using namespace yipe;
namespace {
void roaming(const std::optional<RoamingMonster>& r){if(r)std::cout<<r->type_id<<','<<r->position.x<<','<<r->position.y;else std::cout<<"0,-1,-1";}
void pursuer(const std::optional<PursuingMonster>& p){if(p)std::cout<<p->type_id<<','<<p->position.x<<','<<p->position.y<<','<<p->origin.x<<','<<p->origin.y;else std::cout<<"0,-1,-1,-1,-1";}
}
int main(){std::cout.imbue(std::locale::classic());
 for(int level=1;level<=50;++level)for(int difficulty=1;difficulty<=3;++difficulty)for(int id=1;id<=134;++id)for(auto p:original_monsters_for_map(id,difficulty,level)){
  int coverage=0;for(int x=0;x<13;++x)for(int y=0;y<12;++y)if(monster_placement_contains(p,{x,y}))coverage+=x*12+y+1;
  std::cout<<"placement|"<<level<<','<<difficulty<<','<<id<<','<<p.placement_index<<','<<p.type_id<<','<<p.position.x<<','<<p.position.y<<','<<p.slot<<','<<p.encounter_size<<','<<p.companion_type_1<<','<<p.companion_type_2<<','<<p.respawns_after_map_reload<<','<<p.occupies_two_by_two_tiles<<','<<coverage<<'\n';
 }
 for(int env=-1;env<=8;++env)for(int type=0;type<=42;++type)for(int alternate=0;alternate<2;++alternate)std::cout<<"allowed|"<<env<<','<<type<<','<<alternate<<','<<roaming_monster_allowed(env,type,alternate!=0)<<'\n';
 for(auto seed:{0u,1u,0xffffffffu})for(int id=1;id<=134;++id){auto original=original_map(id);if(!original)continue;for(int mode=0;mode<4;++mode){auto map=*original;Player p;p.level=mode==0?1:mode==1?8:mode==2?25:45;p.difficulty=mode%3+1;p.maximum_health=100;p.health=mode==0?1:100;p.alternate_map_set=mode==3;p.encountered_monster_types.fill(mode>1);auto placements=original_monsters_for_map(id,p.difficulty,p.level);WorldLocation l;l.map_id=id;l.position={5,5};MapVisibility v;initialize_map_visibility(v,map,l.position);auto q=pursuing_monster_for_map(map,p.level);std::optional<RoamingMonster> r;if(mode==1)r=RoamingMonster{3,{2,2}};MsvcRandom random(seed);
   std::cout<<"initial|"<<seed<<','<<id<<','<<mode<<'|';pursuer(q);std::cout<<'\n';
   for(int i=0;i<12;++i){if(i==6){l.position={10,8};reveal_map_from(v,map,l.position);}auto update=update_roaming_monster(r,map,p,l,map.tile(l.position),placements,v,{mode==0?20:mode==1?24:33},random,q?std::optional<MapPosition>{q->position}:std::nullopt);auto move=q?move_pursuing_monster(*q,map,l.position,placements,r,v,random):RoamingMonsterMove::stayed;
    std::cout<<"update|"<<seed<<','<<id<<','<<mode<<','<<i<<','<<int(update)<<','<<int(move)<<','<<random.state()<<'|';roaming(r);std::cout<<'|';pursuer(q);std::cout<<'\n';if(update==RoamingMonsterUpdate::encountered_player)r.reset();
   }
 }}
 for(auto seed:{0u,1u,0xffffffffu})for(int type=1;type<=41;++type)for(int tile=0;tile<=0x71;++tile){Map::Tiles tiles;tiles.fill(static_cast<std::uint16_t>(tile));Map map(69,1,tiles);RoamingMonster m{type,{5,5}};MsvcRandom random(seed);for(int i=0;i<3;++i){auto result=move_roaming_monster(m,map,{7,7},{},random,MapPosition{6,6});std::cout<<"terrain|"<<seed<<','<<type<<','<<tile<<','<<i<<','<<int(result)<<','<<m.position.x<<','<<m.position.y<<','<<random.state()<<'\n';}}
}
