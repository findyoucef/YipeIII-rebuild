import pathlib,shutil,subprocess,json
root=pathlib.Path.cwd()
source=next((root/'build/npc-animation/focused-3').glob('yipe-*/npcs'))
before=next(p for p in (root/'build/npc-animation/before').glob('yipe-*/npcs') if (p/'king-dialogue.png').exists())
dest=root/'monogame/evidence/2026-10-11-npc-animation'
frames=root/'build/npc-animation/video-frames';frames.mkdir(exist_ok=True)
segments=[];i=0
for actor in ['king','teacher','princess-pink','princess-purple','princess-blue']:
 for suffix in ['world','talk','reward' if actor=='king' else 'trained' if actor=='teacher' else 'rescued']:
  start=i/8
  for p in sorted(source.glob(f'{actor}-{suffix}-[0-9][0-9].png')):
   out=frames/f'{i:04d}.png'
   if not out.exists(): out.hardlink_to(p)
   i+=1
  segments.append({'actor':actor,'scene':suffix,'start':start,'end':i/8})
for p in source.glob('*.png'):
 if p.stem[-2:].isdigit(): continue
 shutil.copy2(p,dest/p.name)
for p in before.glob('*.png'):
 if not p.name.startswith(('princess-purple','princess-blue')): shutil.copy2(p,dest/(p.stem+'-before.png'))
(dest/'video-scenes.json').write_text(json.dumps(segments,indent=2)+'\n')
subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y','-framerate','8','-i',str(frames/'%04d.png'),'-vf','fps=24','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(dest/'npc-review.mp4')],check=True)
print('Saved',i,'frames,',i/8,'seconds to',dest/'npc-review.mp4')
