from pathlib import Path
import array, subprocess, wave, math, hashlib, json
root=Path('.').resolve(); dest=root/'monogame/Yipe.Desktop/Content/Audio/Ambience'
air=dest/'breeding-ground.wav'; crack=root/'build/ice-remaster/ice-crack-ecfike.mp3'
def read(p, filt):
    b=subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-af',filt,'-ar','22050','-ac','2','-f','f32le','-'])
    a=array.array('f'); a.frombytes(b); return a
a=read(air,'highpass=f=55,lowpass=f=680')
b=read(air,'asetrate=16537.5,aresample=22050,highpass=f=45,lowpass=f=190')
g=read(crack,'asetrate=22050,aresample=44100,highpass=f=70,lowpass=f=1100,aecho=0.65:0.6:230|510:0.26|0.16')
n=60*22050*2
bed=array.array('f',(a[i%len(a)]*.55+b[i%len(b)]*.6 for i in range(n)))
rms=math.sqrt(sum(x*x for x in bed)/len(bed)); bed=array.array('f',(x*.042/rms for x in bed)); peak=max(abs(x) for x in g)
for seconds,level in [(9,.12),(31,.095),(46,.13)]:
 start=seconds*22050*2
 for i,x in enumerate(g):
  if start+i<len(bed): bed[start+i]+=x/peak*level
# Wrap the tail into the head, retaining a two-second continuous crossfade.
k=2*22050*2
seam=array.array('f',(bed[-k+i]*(1-i/k)+bed[i]*(i/k) for i in range(k)))
loop=bed[k:-k]+seam
samples=array.array('h',(round(max(-.75,min(.75,x))*32767) for x in loop))
out=dest/'final-cave.wav'
with wave.open(str(out),'wb') as w:
 w.setnchannels(2); w.setsampwidth(2); w.setframerate(22050); w.writeframes(samples.tobytes())
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
record={'date':'2026-10-11','sources':[{'title':'Loopable Dungeon Ambience','author':'JaggedStone','source':'https://opengameart.org/content/loopable-dungeon-ambience','license':'CC0-1.0','input':'breeding-ground.wav','input_sha256':h(air),'original_credit_record':'sources.json'},{'title':'Ice Crack 5.wav','author':'ecfike','source':'https://freesound.org/people/ecfike/sounds/177217/','license':'CC0-1.0','input':'Public HQ MP3 preview; reused from Ice Cave source','input_sha256':h(crack),'original_credit_record':'ice-sources.json'}], 'processing':'58-second stereo PCM16 22050Hz loop. Recorded dungeon air filtered 55–680Hz, with a 75%-speed 45–190Hz rumble layer; combined bed RMS 0.042. Recorded crack slowed to half speed and filtered 70–1100Hz with 230/510ms echoes for distant stone creaks at 9,31,46 seconds, peak 0.12/0.095/0.13. Two-second wrap crossfade. No synthesized audio. Reproducible mix_audio.py retained with evidence.', 'output':{'file':out.name,'duration_seconds':len(samples)/44100,'rms':math.sqrt(sum((x/32768)**2 for x in samples)/len(samples)),'peak':max(abs(x) for x in samples)/32768,'seam_delta':max(abs(samples[-2+j]-samples[j]) for j in range(2))/32768,'sha256':h(out)}}
(dest/'final-cave-sources.json').write_text(json.dumps(record,indent=2)+'\n')
(dest/'FINAL-CAVE-CREDITS.txt').write_text('Final Cave ambience\n\nLoopable Dungeon Ambience, by JaggedStone\nhttps://opengameart.org/content/loopable-dungeon-ambience\nReused breeding-ground.wav; provenance in sources.json.\n\nIce Crack 5.wav, by ecfike\nhttps://freesound.org/people/ecfike/sounds/177217/\nReused public HQ preview from Ice Cave; provenance in ice-sources.json.\n\nBoth recordings: CC0 1.0 Universal\nhttps://creativecommons.org/publicdomain/zero/1.0/\n\nFiltered and slowed, with quiet echoes and a loop crossfade.\nProcessing and hashes: final-cave-sources.json.\n')
print(json.dumps(record['output']))
