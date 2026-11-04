#!/usr/bin/env python3
"""Assemble the trailer with short cross-dissolves and synchronized game audio."""
import json
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
capture = Path(sys.argv[1] if len(sys.argv) > 1 else root / "build/trailer-20261020-v3").resolve()
output = Path(sys.argv[2] if len(sys.argv) > 2 else root / "monogame/evidence/2026-10-20-trailer").resolve()
output.mkdir(parents=True, exist_ok=True)
audio = root / "monogame/Yipe.Desktop/Content/Audio"


def run(args):
    subprocess.run([str(arg) for arg in args], check=True)


def probe(path):
    return json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-show_streams", "-show_format", "-of", "json", str(path)
    ], text=True))


shots = json.loads((capture / "shots.json").read_text())
transition_frames = 20  # One third of a second at 60 fps.
timeline_frames = 0
source_duration = 0
for index, shot in enumerate(shots):
    info = probe(capture / (shot["Name"] + ".mp4"))
    video = info["streams"][0]
    if (video["width"], video["height"], video["r_frame_rate"]) != (1280, 720, "60/1"):
        raise ValueError(f"Unexpected capture format: {shot['Name']}")
    if abs(float(info["format"]["duration"]) - shot["Seconds"]) > 1 / 60:
        raise ValueError(f"Unexpected capture duration: {shot['Name']}")
    if index:
        timeline_frames -= transition_frames
    shot["Start"] = timeline_frames / 60
    timeline_frames += round(shot["Seconds"] * 60)
    source_duration += shot["Seconds"]
if source_duration != 45:
    raise ValueError(f"Expected 45 seconds of source footage; got {source_duration}")
duration = timeline_frames / 60
transitions = [{"from": previous["Name"], "to": current["Name"], "at": current["Start"],
                "duration": transition_frames / 60, "type": "cross-dissolve"}
               for previous, current in zip(shots, shots[1:])]
starts = {shot["Name"]: shot["Start"] for shot in shots}

# Audio is mixed separately, with the existing battle's recorded impact/counter timestamps.
# All source ambience and footstep recordings are CC0; the music attribution is on the endcard.
clips = []


def cue(path, start, length, gain, fade=0):
    clips.append({"source": str(path.relative_to(root)), "at": start, "length": length,
                  "gain": gain, "fade": fade})


for shot, name, gain in [
    ("02-maynard", "maynard-day", .28),
    ("03-overworld", "maynard-day", .22),
    ("04-snow", "maynard-night", .35),
    ("05-ice", "ice-cave", .45),
    ("06-raft", "water-cave", .6),
    ("10-night", "maynard-night", .3),
]:
    seconds = next(s["Seconds"] for s in shots if s["Name"] == shot)
    cue(audio / f"Ambience/{name}.wav", starts[shot], seconds, gain, .25)

for shot in shots:
    for step, footfall in enumerate(json.loads((capture / (shot["Name"] + "-footsteps.json")).read_text())):
        cue(audio / f"Footsteps/{footfall['Surface']}-{step % 3}.wav",
            shot["Start"] + footfall["At"], .4, .45)

for hit in json.loads((capture / "combat-cues.json").read_text()):
    if hit["Sound"] == 0:
        continue  # A missed attack has no hit sound in the game.
    source = next((root / "assets/embedded/audio").glob(f"{hit['Sound']}_*.wav"))
    cue(source, starts["07-fire-battle"] + hit["At"], .6, 2)

command = ["ffmpeg", "-hide_banner", "-loglevel", "warning", "-y",
           "-i", audio / "Music/celtic-impulse.ogg"]
filters = [f"[0:a]aresample=48000,atrim=duration={duration},asetpts=PTS-STARTPTS,"
           f"afade=t=in:d=1,afade=t=out:st={duration-3}:d=3[music]"]
labels = ["[music]"]
for i, clip in enumerate(clips, 1):
    command += ["-i", root / clip["source"]]
    chain = f"[{i}:a]aresample=48000,atrim=duration={clip['length']},asetpts=PTS-STARTPTS,volume={clip['gain']}"
    if clip["fade"]:
        chain += f",afade=t=in:d={clip['fade']},afade=t=out:st={clip['length']-clip['fade']}:d={clip['fade']}"
    chain += f",adelay={round(clip['at']*1000)}:all=1[a{i}]"
    filters.append(chain)
    labels.append(f"[a{i}]")
filters.append("".join(labels) + f"amix=inputs={len(labels)}:normalize=0:duration=longest,atrim=duration={duration},asetpts=N/SR/TB[mix]")
mix = capture / "trailer-mix.wav"
run(command + ["-filter_complex", ";".join(filters), "-map", "[mix]", "-ar", "48000", "-ac", "2", "-c:a", "pcm_f32le", mix])

# Two-pass loudness normalization preserves a consistent review volume with true-peak headroom.
measurement = subprocess.run([
    "ffmpeg", "-hide_banner", "-i", str(mix), "-af",
    "loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json", "-f", "null", "-"
], check=True, capture_output=True, text=True).stderr
levels, _ = json.JSONDecoder().raw_decode(measurement[measurement.rindex("{"):])
normalize = ("loudnorm=I=-16:TP=-1.5:LRA=11:linear=true:"
             f"measured_I={levels['input_i']}:measured_TP={levels['input_tp']}:"
             f"measured_LRA={levels['input_lra']}:measured_thresh={levels['input_thresh']}:"
             f"offset={levels['target_offset']}")
command = ["ffmpeg", "-hide_banner", "-loglevel", "warning", "-y", "-filter_complex_threads", "2"]
video_filters = []
for i, shot in enumerate(shots):
    command += ["-i", capture / (shot["Name"] + ".mp4")]
    video_filters.append(f"[{i}:v]settb=1/60,setpts=PTS-STARTPTS,format=yuv444p[v{i}]")
previous = "v0"
for i, shot in enumerate(shots[1:], 1):
    video_filters.append(f"[{previous}][v{i}]xfade=transition=fade:duration={transition_frames/60:.9f}:"
                         f"offset={shot['Start']:.9f}[fade{i}]")
    previous = f"fade{i}"
video_filters.append(f"[{previous}]scale=1920:1080:flags=neighbor[video]")
video = output / "yipe-iii-trailer-v3.mp4"
run(command + ["-i", mix, "-filter_complex", ";".join(video_filters),
     "-map", "[video]", "-map", f"{len(shots)}:a:0", "-af", normalize,
     "-c:v", "libx264", "-preset", "slow", "-crf", "17", "-pix_fmt", "yuv420p",
     "-c:a", "aac", "-b:a", "256k", "-ar", "48000", "-ac", "2", "-t", str(duration),
     "-r", "60", "-fps_mode", "cfr", "-frames:v", str(timeline_frames),
     "-movflags", "+faststart", "-metadata", "title=Quest of Yipe! III - Trailer v3",
     "-metadata", "artist=Icosium Labs",
     "-metadata", "comment=Celtic Impulse by Kevin MacLeod (incompetech.com), CC BY 4.0; excerpt edited and mixed. https://creativecommons.org/licenses/by/4.0/",
     video])
run(["ffmpeg", "-hide_banner", "-loglevel", "warning", "-y", "-ss", "39", "-i", video,
     "-frames:v", "1", "-update", "1", output / "poster.png"])
(output / "timeline.json").write_text(json.dumps({"shots": shots, "transitions": transitions, "audio_cues": clips}, indent=2) + "\n")
(output / "probe.json").write_text(json.dumps(probe(video), indent=2) + "\n")
print(f"Exported {video} ({duration}s, 1920x1080, 60 fps)")
