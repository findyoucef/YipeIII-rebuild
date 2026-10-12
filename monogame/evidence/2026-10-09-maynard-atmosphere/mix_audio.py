from pathlib import Path
import subprocess,json,hashlib,wave,array,math
work=Path('build/maynard-atmosphere');dest=Path('monogame/Yipe.Desktop/Content/Audio/Ambience')
def stats(path):
 with wave.open(str(path)) as w:
  a=array.array('h',w.readframes(w.getnframes()));channels=w.getnchannels()
  return {'duration_seconds':len(a)/channels/w.getframerate(),'channels':channels,'sample_rate':w.getframerate(),'rms':math.sqrt(sum(x*x for x in a)/len(a))/32768,'peak':max(abs(x) for x in a)/32768,'seam_delta':max(abs(a[c]-a[-channels+c]) for c in range(channels))/32768}
def ff(inputs,filters,out,channels=2):
 cmd=['ffmpeg','-v','error','-y']
 for path,loop in inputs:
  if loop:cmd+=['-stream_loop','-1']
  cmd+=['-i',str(path)]
 subprocess.run(cmd+['-filter_complex',filters,'-map','[out]','-ar','22050','-ac',str(channels),'-c:a','pcm_s16le',str(out)],check=True)
def wrap(duration):
 return f'asplit=3[head][body][tail];[head]atrim=0:2,asetpts=PTS-STARTPTS[h];[body]atrim=2:{duration-2},asetpts=PTS-STARTPTS[b];[tail]atrim={duration-2}:{duration},asetpts=PTS-STARTPTS[t];[t][h]acrossfade=d=2:c1=tri:c2=tri[seam];[b][seam]concat=n=2:v=0:a=1[out]'
def normalize(path,target):
 data=stats(path);gain=min(target/data['rms'],.8/data['peak'])
 tmp=work/'normalized.wav'
 ff([(path,False)],f'[0:a]volume={gain}[out]',tmp,data['channels']);path.write_bytes(tmp.read_bytes());return gain
ff([(work/'birds-source.ogg',False)],'[0:a]atrim=duration=30,highpass=f=280,lowpass=f=7500,'+wrap(30),work/'birds-loop.wav')
ff([(work/'wind-source.wav',False)],'[0:a]atrim=start=20:duration=60,asetpts=PTS-STARTPTS,highpass=f=220,lowpass=f=4200,'+wrap(60),work/'wind-loop.wav')
normalize(work/'birds-loop.wav',.09);normalize(work/'wind-loop.wav',.08)
ff([(work/'birds-loop.wav',True),(work/'wind-loop.wav',True)],'[0:a]atrim=duration=60[birds];[1:a]atrim=duration=60,volume=0.45[wind];[birds][wind]amix=inputs=2:normalize=0,'+wrap(60),dest/'maynard-day.wav')
ff([(work/'wind-loop.wav',False)],'[0:a]anull[out]',dest/'maynard-night.wav')
ff([(work/'river-source.wav',False)],'[0:a]atrim=start=30:duration=60,asetpts=PTS-STARTPTS,highpass=f=85,lowpass=f=5200,'+wrap(60),dest/'maynard-river.wav',1)
gains={name:normalize(dest/name,rms) for name,rms in [('maynard-day.wav',.10),('maynard-night.wav',.08),('maynard-river.wav',.14)]}
sources=[{'title':'Ambient Bird Sounds','author':'isaiah658','source':'https://opengameart.org/content/ambient-bird-sounds','download':'https://opengameart.org/sites/default/files/birds-isaiah658_0.ogg','local':'birds-source.ogg'}, {'title':'Park ambiences — wind','author':'Thimras','source':'https://opengameart.org/content/park-ambiences','download':'https://opengameart.org/sites/default/files/park_ambience_wind.wav','local':'wind-source.wav'}, {'title':'Park ambiences — river','author':'Thimras','source':'https://opengameart.org/content/park-ambiences','download':'https://opengameart.org/sites/default/files/park_ambience_river.wav','local':'river-source.wav'}]
for src in sources:
 src.update(license='CC0-1.0',license_url='https://creativecommons.org/publicdomain/zero/1.0/',sha256=hashlib.sha256((work/src.pop('local')).read_bytes()).hexdigest())
outputs=[{'file':name,**stats(dest/name),'normalization_gain':gains[name],'sha256':hashlib.sha256((dest/name).read_bytes()).hexdigest()} for name in gains]
manifest={'downloaded_utc':'2026-10-09','sources':sources,'processing':'Recorded sources only. Bird recording filtered 280–7500 Hz and looped to 28s with a 2s wrap crossfade; wind seconds20–80 filtered 220–4200 Hz and looped to58s. Bird and wind intermediates level-matched to0.09/0.08 RMS with0.8 peak cap. Day mixes birds with wind at0.45 gain and wraps to58s; night uses wind alone. River seconds30–90 filtered85–5200 Hz, converted to mono for positional panning and wrapped to58s. Final day/night/river RMS targets0.10/0.08/0.14, all peak-capped at0.8. All outputs PCM16 22050Hz; no synthesized audio. Reproducible recipe in evidence mix_audio.py.','outputs':outputs}
(dest/'maynard-sources.json').write_text(json.dumps(manifest,indent=2)+'\n')
(dest/'MAYNARD-CREDITS.txt').write_text('Maynard outdoor ambience and river\n\n'+'\n\n'.join(src['title']+' — '+src['author']+'\n'+src['source']+'\nCC0 1.0: '+src['license_url'] for src in sources)+'\n\nAdapted by icosium labs: filtering, level matching, mixing, resampling and loop crossfades. Recorded sources only. See maynard-sources.json for provenance and hashes.\n')
print(json.dumps(outputs,indent=2))
