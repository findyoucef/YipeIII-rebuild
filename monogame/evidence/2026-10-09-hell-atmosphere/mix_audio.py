from pathlib import Path
import subprocess, json, hashlib, wave, array, math
assets=Path('monogame/Yipe.Desktop/Content/Audio/Ambience')
inputs=[Path('build/fire-cave/audio/LavaLoop.wav'),Path('build/fire-cave/audio/fire.wav'),Path('build/breeding-grounds/dungeon-ambient-source.ogg')]
filters='[0:a]asplit=2[lava][bass];[lava]atrim=duration=60,highpass=f=60,lowpass=f=1700,volume=0.70[flow];[bass]asetrate=33075,aresample=44100,atrim=duration=60,highpass=f=45,lowpass=f=240,volume=0.85[rumble];[1:a]atrim=duration=60,highpass=f=180,lowpass=f=3200,volume=0.24[fire];[2:a]atrim=duration=60,highpass=f=60,lowpass=f=500,volume=0.65[air];[flow][rumble][fire][air]amix=inputs=4:normalize=0:duration=shortest,alimiter=limit=0.85:level=0,asplit=3[head][body][tail];[head]atrim=0:2,asetpts=PTS-STARTPTS[h];[body]atrim=2:58,asetpts=PTS-STARTPTS[b];[tail]atrim=58:60,asetpts=PTS-STARTPTS[t];[t][h]acrossfade=d=2:c1=tri:c2=tri[seam];[b][seam]concat=n=2:v=0:a=1[out]'
cmd=['ffmpeg','-v','error','-y']
for p in inputs:cmd += ['-stream_loop','-1','-i',str(p)]
output=assets/'hell.wav'
subprocess.run(cmd+['-filter_complex',filters,'-map','[out]','-ar','22050','-ac','2','-c:a','pcm_s16le',str(output)],check=True)
sources=json.loads((assets/'fire-cave-sources.json').read_text())['sources']
cave=json.loads((assets/'sources.json').read_text());sources.append({k:cave[k] for k in ['title','author','source','download','license','license_url']});sources[-1]['sha256']=cave['source_sha256']
for src,p in zip(sources,inputs):assert src['sha256']==hashlib.sha256(p.read_bytes()).hexdigest()
with wave.open(str(output)) as w:
 samples=array.array('h',w.readframes(w.getnframes()))
 metrics={'duration_seconds':w.getnframes()/w.getframerate(),'sample_rate':w.getframerate(),'channels':w.getnchannels(),'rms':math.sqrt(sum(v*v for v in samples)/len(samples))/32768,'peak':max(abs(v) for v in samples)/32768,'seam_delta':max(abs(samples[c]-samples[-2+c]) for c in range(2))/32768}
manifest={'output':'hell.wav','sources':sources,'processing':'58-second stereo PCM16 22050 Hz loop. Lava flow filtered 60–1700 Hz at gain 0.70; lava pitched down to 75% speed and filtered 45–240 Hz at gain 0.85; fire crackle filtered 180–3200 Hz at gain 0.24; cavern air filtered 60–500 Hz at gain 0.65. No synthesized oscillators or noise. 0.85 peak ceiling; two-second wrap crossfade. Existing verified source recordings reused; no new downloads.','ffmpeg_filter':filters,'output_sha256':hashlib.sha256(output.read_bytes()).hexdigest(),'metrics':metrics}
(assets/'hell-sources.json').write_text(json.dumps(manifest,indent=2)+'\n')
(assets/'HELL-CREDITS.txt').write_text('Yipe Hell ambience\n\n'+'\n\n'.join(x['title']+' — '+x['author']+'\n'+x['source']+'\n'+x['license']+': '+x['license_url'] for x in sources)+'\n\nAdapted by icosium labs for Yipe III: mixing, filtering, pitch adjustment, resampling and a wrap crossfade. The adapted mix is distributed under CC BY 3.0. Source authors do not endorse this adaptation.\nSee hell-sources.json for processing and source/output hashes.\n')
print(json.dumps(metrics,indent=2))
