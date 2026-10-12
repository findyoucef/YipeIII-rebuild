from pathlib import Path
import subprocess,wave,array,math,json,hashlib
work=Path('build/overworld-remaster');dest=Path('monogame/Yipe.Desktop/Content/Audio/Ambience')
def run(args): subprocess.run(['ffmpeg','-v','error','-y']+args,check=True)
def stats(path):
 with wave.open(str(path)) as w:
  a=array.array('h',w.readframes(w.getnframes()));c=w.getnchannels();r=w.getframerate()
 return dict(duration_seconds=len(a)/c/r,channels=c,sample_rate=r,rms=math.sqrt(sum(x*x for x in a)/len(a))/32768,peak=max(abs(x) for x in a)/32768,sha256=hashlib.sha256(path.read_bytes()).hexdigest())
run(['-i',str(work/'crickets-source.mp3'),'-filter_complex','[0:a]atrim=duration=11,highpass=f=550,lowpass=f=6500,asplit=3[h][b][t];[h]atrim=0:0.5,asetpts=PTS-STARTPTS[head];[b]atrim=0.5:10.5,asetpts=PTS-STARTPTS[body];[t]atrim=10.5:11,asetpts=PTS-STARTPTS[tail];[tail][head]acrossfade=d=0.5:c1=tri:c2=tri[seam];[body][seam]concat=n=2:v=0:a=1[out]','-map','[out]','-ar','22050','-ac','2','-c:a','pcm_s16le',str(work/'crickets-loop.wav')])
s=stats(work/'crickets-loop.wav');gain=min(.065/s['rms'],.75/s['peak'])
run(['-i',str(work/'crickets-loop.wav'),'-af',f'volume={gain}','-c:a','pcm_s16le',str(dest/'outdoor-night.wav')])
manifest={'sources':[{'title':'Crickets Ambient Noise - loopable','author':'Wolfgang_ / Ted Kerr','source':'https://opengameart.org/content/crickets-ambient-noise-loopable','download':'https://opengameart.org/sites/default/files/crickets_1.mp3','license':'CC0-1.0','sha256':hashlib.sha256((work/'crickets-source.mp3').read_bytes()).hexdigest()},*json.loads((dest/'maynard-sources.json').read_text())['sources']], 'processing':'outdoor-birds.wav is the existing 28-second filtered bird-only recording from the Maynard recipe (280–7500Hz, two-second wrapped crossfade, PCM16 stereo 22050Hz). outdoor-night.wav: cricket clip trimmed to 11s, filtered 550–6500Hz, 0.5-second wrapped crossfade, RMS target 0.065, peak cap 0.75, PCM16 stereo 22050Hz. Reuses maynard-night.wav as the habitat wind bed and maynard-river.wav for positional water. No synthesized sounds added.', 'outputs':[{'file':n,**stats(dest/n)} for n in ['outdoor-birds.wav','outdoor-night.wav']]}
(dest/'outdoor-sources.json').write_text(json.dumps(manifest,indent=2)+'\n')
(dest/'OUTDOOR-CREDITS.txt').write_text('Overworld nature ambience\n\nCrickets Ambient Noise - loopable — Wolfgang_ / Ted Kerr\nhttps://opengameart.org/content/crickets-ambient-noise-loopable\nCC0 1.0 https://creativecommons.org/publicdomain/zero/1.0/\n\nDaytime birds — isaiah658, Ambient Bird Sounds\nhttps://opengameart.org/content/ambient-bird-sounds\nCC0 1.0\n\nWind and positional river — Thimras, Park ambiences\nhttps://opengameart.org/content/park-ambiences\nCC0 1.0. Existing Maynard recordings; see MAYNARD-CREDITS.txt.\n\nAdapted by icosium labs: filtering, level matching, loop crossfades and habitat mixing. See outdoor-sources.json for provenance and output hashes.\n')
print(json.dumps(manifest['outputs'],indent=2))
