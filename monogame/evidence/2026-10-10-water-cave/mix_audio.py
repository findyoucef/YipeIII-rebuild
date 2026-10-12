from pathlib import Path
import array, subprocess, wave, math, hashlib, json
root = Path(__file__).resolve().parents[3]
dest = root / 'monogame/Yipe.Desktop/Content/Audio/Ambience'
rate, channels = 22050, 2

def read(name, filters):
    raw = subprocess.check_output(['ffmpeg', '-v', 'error', '-stream_loop', '-1', '-i', str(dest/name),
        '-t', '60', '-af', filters, '-ar', str(rate), '-ac', str(channels), '-f', 'f32le', '-'])
    samples = array.array('f'); samples.frombytes(raw); return samples

cave = read('breeding-ground.wav', 'highpass=f=90,lowpass=f=2800')
water = read('maynard-river.wav', 'highpass=f=140,lowpass=f=2200,aecho=0.8:0.8:89|173:0.14|0.08')
mix = array.array('f', (cave[i] * .85 + water[i] * .22 for i in range(min(len(cave), len(water)))))
# A two-second wrap crossfade joins the existing recorded textures without a pop.
wrap = 2 * rate * channels
loop = array.array('f', mix[wrap:-wrap])
for i in range(wrap):
    weight = i / (wrap - 1)
    loop.append(mix[-wrap+i] * (1-weight) + mix[i] * weight)
rms = math.sqrt(sum(x*x for x in loop) / len(loop))
gain = min(.075 / rms, .65 / max(abs(x) for x in loop))
samples = array.array('h', (round(x*gain*32767) for x in loop))
out = dest/'water-cave.wav'
with wave.open(str(out), 'wb') as wav:
    wav.setnchannels(channels); wav.setsampwidth(2); wav.setframerate(rate); wav.writeframes(samples.tobytes())
h = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
record = {'date':'2026-10-10', 'sources':[
    {'title':'Loopable Dungeon Ambience','author':'JaggedStone','source':'https://opengameart.org/content/loopable-dungeon-ambience','license':'CC0-1.0','input':'breeding-ground.wav','input_sha256':h(dest/'breeding-ground.wav'),'original_credit_record':'sources.json'},
    {'title':'Park ambiences — river','author':'Thimras','source':'https://opengameart.org/content/park-ambiences','license':'CC0-1.0','input':'maynard-river.wav','input_sha256':h(dest/'maynard-river.wav'),'original_credit_record':'maynard-sources.json'}],
    'processing':'Recorded cave air/drips filtered 90–2800 Hz at0.85 gain, recorded river filtered140–2200 Hz at0.22 gain with quiet89/173ms echoes. Two-second wrap crossfade. RMS target0.075 and peak cap0.65. No synthesized sound. Stereo PCM16 22050Hz.',
    'output':{'file':out.name,'duration_seconds':len(samples)/channels/rate,'rms':math.sqrt(sum((x/32768)**2 for x in samples)/len(samples)),'peak':max(abs(x) for x in samples)/32768,'seam_delta':max(abs(samples[c]-samples[-channels+c])/32768 for c in range(channels)),'sha256':h(out)}}
(dest/'water-cave-sources.json').write_text(json.dumps(record,indent=2)+'\n')
(dest/'WATER-CAVE-CREDITS.txt').write_text('Water Cave ambience\n\nLoopable Dungeon Ambience, by JaggedStone\nhttps://opengameart.org/content/loopable-dungeon-ambience\nOriginal provenance: sources.json\n\nPark ambiences — river, by Thimras\nhttps://opengameart.org/content/park-ambiences\nOriginal provenance: maynard-sources.json\n\nBoth recordings: CC0 1.0 Universal\nhttps://creativecommons.org/publicdomain/zero/1.0/\n\nQuiet filtered recording mix with short echoes and wrap crossfade.\nProcessing and hashes: water-cave-sources.json\n')
print(json.dumps(record['output']))
