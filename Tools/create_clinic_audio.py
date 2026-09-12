#!/usr/bin/env python3
"""Compose original clinic music and action sounds; no samples or external recordings.

Deterministic, 32 kHz mono PCM. Wrapped note tails make the 16-bar score loop
seamlessly. This script and its output are project-owned original compositions.
"""
import array
import hashlib
import json
import math
from pathlib import Path
import random
import wave

RATE = 32000
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Unity/OrbitOrchard/Assets/IdleClinic/Resources/ClinicAudio"
OUT.mkdir(parents=True, exist_ok=True)
manifest = {}


def write(name, data):
    peak = max(abs(v) for v in data)
    if peak > .92:
        data = [v * .92 / peak for v in data]
    pcm = array.array("h", (round(max(-1, min(1, v)) * 32767) for v in data))
    path = OUT / (name + ".wav")
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(pcm.tobytes())
    manifest[name] = {"seconds": len(data) / RATE, "peak": max(abs(v) for v in data),
                      "rms": math.sqrt(sum(v*v for v in data)/len(data)),
                      "seamDelta": abs(data[-1]-data[0]),
                      "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}


def note(data, start, duration, midi, gain, timbre="keys", wrap=False):
    f = 440 * 2 ** ((midi-69)/12)
    for i in range(int(duration * RATE)):
        t = i / RATE
        phase = 2 * math.pi * f * t
        if timbre == "bass":
            tone = math.sin(phase) + .12 * math.sin(2*phase)
            envelope = min(1, t/.028) * math.exp(-t*2.3) * min(1, (duration-t)/.09)
        else:
            tone = math.sin(phase) + .23*math.sin(2*phase)*math.exp(-t*4) + .09*math.sin(3*phase)*math.exp(-t*8)
            envelope = min(1, t/.006) * math.exp(-t*2.6) * min(1, (duration-t)/.08)
        index = round(start*RATE) + i
        if wrap:
            index %= len(data)
        elif index >= len(data):
            break
        data[index] += tone * envelope * gain


# "Morning Rounds": E-flat major, 80 BPM, a sparse 16-bar miniature.
beat = .75
music = [0.] * round(64 * beat * RATE)
chords = [(51,58,62,67), (48,55,58,63), (56,60,63,67), (58,62,65,68)]
melodies = [[79,77,74,70], [75,74,70,67], [72,75,79,77], [74,70,72,74],
            [79,82,79,77], [75,70,74,75], [79,75,72,67], [70,74,77,74]]
for bar in range(16):
    chord = chords[bar % 4]
    origin = bar*4*beat
    for n in (0,2):
        note(music, origin+n*beat, 1.8, chord[0]-12, .10, "bass", True)
    for n, pitch in enumerate(chord[1:]):
        note(music, origin+(.5+n*.5)*beat, 2.4, pitch, .045, wrap=True)
        note(music, origin+(2.5+n*.25)*beat, 1.8, pitch+12, .025, wrap=True)
    for n, pitch in enumerate(melodies[bar % 8]):
        if (bar+n) % 5 == 3:
            continue
        start = origin + (n*.875 + (.125 if bar%2 else 0))*beat
        note(music, start, 2.4, pitch, .072 if bar < 8 else .058, wrap=True)
        note(music, start+.1875, 1.8, pitch, .013, wrap=True)
write("morning-rounds", music)

def chime(name, notes, spacing=.065, gain=.18):
    data = [0.] * int((len(notes)*spacing+.55)*RATE)
    for i, pitch in enumerate(notes):
        note(data, i*spacing, .5, pitch, gain)
    write(name, data)

chime("payment", [79,86], .055, .18)
chime("collect", [75,79,82,87], .055, .18)
chime("care", [70,75,79], .09, .13)
chime("consultation", [67,74,79], .09, .11)
chime("pharmacy", [82,79,87], .06, .10)
chime("upgrade", [63,67,70,75], .085, .16)
chime("complete", [70,75,79,82,87], .10, .16)
chime("tap", [79], gain=.09)

rng = random.Random(93217)
def noise_sound(name, duration, decay, gain, frequency):
    data, filtered = [], 0.
    for i in range(int(duration*RATE)):
        t = i/RATE
        filtered = .92*filtered + .08*rng.uniform(-1,1)
        envelope = min(1,t/.008)*math.exp(-t*decay)*min(1,(duration-t)/.02)
        data.append(gain*envelope*(filtered+.2*math.sin(2*math.pi*frequency*t)))
    write(name, data)

noise_sound("footstep", .14, 32, .28, 145)
noise_sound("door", .48, 5.5, .20, 92)
noise_sound("treatment", .24, 14, .15, 370)
noise_sound("construction", .45, 9, .35, 180)
noise_sound("taxi", .65, 3, .15, 74)
(OUT / "composition.json").write_text(json.dumps({"title":"Morning Rounds", "author":"Original project composition",
    "bpm":80, "bars":16, "source":"Tools/create_clinic_audio.py", "assets":manifest}, indent=2)+"\n")
print(json.dumps(manifest, indent=2))
