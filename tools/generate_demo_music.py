"""Generate the original 120 BPM Clockwork Neon demo; Python standard library only."""
from array import array
from pathlib import Path
import math
import sys
import wave

RATE = 44100
DURATION = 20
samples = [0.0] * (RATE * DURATION)


def tone(start, duration, midi, gain, bass=False):
    frequency = 440 * 2 ** ((midi - 69) / 12)
    for i in range(int(duration * RATE)):
        t = i / RATE
        envelope = min(1, t / .006) * min(1, (duration - t) / .04)
        envelope *= math.exp(-t * (3 if bass else 6))
        signal = math.sin(2 * math.pi * frequency * t)
        signal += .22 * math.sin(4 * math.pi * frequency * t)
        index = int(start * RATE) + i
        if index < len(samples):
            samples[index] += gain * envelope * signal


# Four intro beats, then eight bars. Notes share the chart's half-second beat grid.
roots = [48, 44, 51, 46]
melody = [72, 75, 79, 75, 74, 77, 81, 77]
for beat in range(36):
    t = beat * .5
    tone(t, .09, 84 if beat % 4 == 0 else 79, .10)
    if beat >= 4:
        root = roots[((beat - 4) // 4) % len(roots)]
        tone(t, .42, root, .25, True)
        tone(t, .22, melody[(beat - 4) % len(melody)], .16)
        tone(t + .25, .20, root + 19, .08)
tone(18, 1.5, 48, .22, True)
tone(18, 1.5, 72, .12)
pcm = array('h', (round(max(-1, min(1, sample)) * 32767) for sample in samples))
if sys.byteorder != 'little':
    pcm.byteswap()
destination = Path(__file__).resolve().parents[1] / 'Assets/Resources/Music/clockwork_neon.wav'
destination.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(destination), 'wb') as wav:
    wav.setnchannels(1)
    wav.setsampwidth(2)
    wav.setframerate(RATE)
    wav.writeframes(pcm.tobytes())
print(f'Generated {destination.name}: {DURATION} s, 120 BPM, chart zero at 2 s')
