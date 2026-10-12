from pathlib import Path
import subprocess,json,hashlib,wave,array,math
root=Path.cwd(); assets=root/'monogame/Yipe.Desktop/Content/Audio'; work=root/'build/earth-remaster'; dest=assets/'Ambience'
inputs=[dest/'breeding-ground.wav',work/'dripping-water.flac',assets/'Footsteps/sand-0.wav']
for suffix,water in [('',.48),('-deep',.9)]:
 output=dest/f'earth-cave{suffix}.wav'
 filters=f'[0:a]atrim=duration=60,highpass=f=55,lowpass=f=1100,volume=0.5[air];[1:a]atrim=duration=60,highpass=f=100,lowpass=f=3800,volume={water}[water];[2:a]atrim=start=0.03,highpass=f=230,lowpass=f=2000,volume=0.2,asplit=3[g1][g2][g3];[g1]adelay=11730:all=1[g1d];[g2]adelay=32810:all=1[g2d];[g3]adelay=47400:all=1[g3d];[air][water][g1d][g2d][g3d]amix=inputs=5:normalize=0:duration=longest,atrim=duration=60,volume=2,alimiter=limit=0.85:level=0,asplit=3[head][body][tail];[head]atrim=0:2,asetpts=PTS-STARTPTS[h];[body]atrim=2:58,asetpts=PTS-STARTPTS[b];[tail]atrim=58:60,asetpts=PTS-STARTPTS[t];[t][h]acrossfade=d=2:c1=tri:c2=tri[seam];[b][seam]concat=n=2:v=0:a=1[out]'
 subprocess.run(['ffmpeg','-v','error','-y','-i',str(inputs[0]),'-stream_loop','-1','-i',str(inputs[1]),'-i',str(inputs[2]),'-filter_complex',filters,'-map','[out]','-ar','22050','-ac','2','-c:a','pcm_s16le',str(output)],check=True)
sources=[{'title':'Loopable Dungeon Ambience','author':'JaggedStone','source':'https://opengameart.org/content/loopable-dungeon-ambience','input':'breeding-ground.wav','sha256':hashlib.sha256(inputs[0].read_bytes()).hexdigest()}, {'title':'Dripping water loop','author':'Independent.nu (submitted by qubodup)','source':'https://opengameart.org/content/dripping-water-loop','download':'https://opengameart.org/sites/default/files/atmosbasement.mp3_.flac','sha256':hashlib.sha256(inputs[1].read_bytes()).hexdigest()}, {'title':"Fantozzi’s footsteps: grass/sand, stone",'author':'Fantozzi (sliced by qubodup)','source':'https://opengameart.org/content/fantozzis-footsteps-grasssand-stone','input':'../Footsteps/sand-0.wav','sha256':hashlib.sha256(inputs[2].read_bytes()).hexdigest()}]
for source in sources:source['license']='CC0-1.0';source['license_url']='https://creativecommons.org/publicdomain/zero/1.0/'
outputs=[]
for p in [dest/'earth-cave.wav',dest/'earth-cave-deep.wav']:
 with wave.open(str(p)) as w:
  samples=array.array('h',w.readframes(w.getnframes()));outputs.append({'file':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'duration':len(samples)/2/22050,'rms':math.sqrt(sum(v*v for v in samples)/len(samples))/32768,'peak':max(abs(v) for v in samples)/32768,'seam_delta':max(abs(samples[c]-samples[-2+c]) for c in range(2))/32768})
manifest={'sources':sources,'processing':'58-second stereo 22050 Hz PCM16 loops. Existing cave air low-passed to 1100 Hz at half gain; dripping-water recording filtered 100–3800 Hz, gain 0.48 upper / 0.90 deep; quiet granular sample at three irregular offsets, no generated oscillators/noise. Final gain 2.0 with a 0.85 peak ceiling; two-second wrap crossfade. Mixed via build/earth-remaster/mix_audio.py; no peak normalization.','outputs':outputs,'downloaded_utc':'2026-10-09'}
(dest/'earth-cave-sources.json').write_text(json.dumps(manifest,indent=2)+'\n')
(dest/'EARTH-CAVE-CREDITS.txt').write_text('Earth Cave ambience — upper and deep\n\n'+'\n\n'.join(x['title']+' by '+x['author']+'\n'+x['source']+'\nCC0 1.0: '+x['license_url'] for x in sources)+'\n\n'+manifest['processing']+'\nLicense text: CC0-1.0.txt.\n')
print(json.dumps(outputs,indent=2))
