from pathlib import Path
import array, subprocess, wave, math, hashlib, json
root=Path('.').resolve(); work=root/'build/ice-remaster'; dest=root/'monogame/Yipe.Desktop/Content/Audio/Ambience'
wind=dest/'maynard-night.wav'; crack=work/'ice-crack-ecfike.mp3'
def read(p, filt):
    result=subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-af',filt,'-ar','22050','-ac','2','-f','f32le','-'])
    data=array.array('f');data.frombytes(result);return data
bed=read(wind,'highpass=f=120,lowpass=f=2100'); grain=read(crack,'highpass=f=150,lowpass=f=5000,aecho=0.7:0.6:140|290:0.2|0.12')
rms=math.sqrt(sum(x*x for x in bed)/len(bed));gain=.055/rms
bed=array.array('f',(x*gain for x in bed));peak=max(abs(x) for x in grain)
for seconds, level in [(13,.18),(39,.14)]:
    start=seconds*22050*2
    for i,x in enumerate(grain):
        if start+i<len(bed):bed[start+i]+=x/peak*level
peak=max(abs(x) for x in bed);scale=min(1,.7/peak)
samples=array.array('h',(round(max(-1,min(1,x*scale))*32767) for x in bed))
out=dest/'ice-cave.wav'
with wave.open(str(out),'wb') as wav:
    wav.setnchannels(2);wav.setsampwidth(2);wav.setframerate(22050);wav.writeframes(samples.tobytes())
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
record={'date':'2026-10-10','sources':[{'title':'Park ambiences — wind','author':'Thimras','source':'https://opengameart.org/content/park-ambiences','license':'CC0-1.0','input':'maynard-night.wav','input_sha256':h(wind),'original_credit_record':'maynard-sources.json'},{'title':'Ice Crack 5.wav','author':'ecfike','source':'https://freesound.org/people/ecfike/sounds/177217/','download':'https://cdn.freesound.org/previews/177/177217_2337290-hq.mp3','format':'Public HQ MP3 preview of the CC0 recording','license':'CC0-1.0','source_sha256':h(crack)}], 'processing':'58-second recorded wind loop; 120–2100 Hz filter and 0.055 RMS. Recorded ice crack filtered 150–5000 Hz with quiet 140/290ms echoes, peak 0.18/0.14 at 13/39 seconds. Stereo PCM16 22050Hz. No synthesized audio. Reproducible recipe retained with evidence.','output':{'file':out.name,'duration_seconds':len(samples)/2/22050,'rms':math.sqrt(sum((x/32768)**2 for x in samples)/len(samples)),'peak':max(abs(x) for x in samples)/32768,'sha256':h(out)}}
(dest/'ice-sources.json').write_text(json.dumps(record,indent=2)+'\n')
(dest/'ICE-CREDITS.txt').write_text('Ice Cave ambience\n\nPark ambiences — wind, by Thimras\nhttps://opengameart.org/content/park-ambiences\nReused processed maynard-night.wav; original provenance in maynard-sources.json.\n\nIce Crack 5.wav, by ecfike\nhttps://freesound.org/people/ecfike/sounds/177217/\nPublic HQ preview used.\n\nBoth recordings: CC0 1.0 Universal\nhttps://creativecommons.org/publicdomain/zero/1.0/\n\nFiltered and mixed with restrained echo; processing and hashes in ice-sources.json.\n')
print(json.dumps(record['output']))
