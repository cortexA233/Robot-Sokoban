"""Rebuild original station SFX. Python standard library only, no source samples."""
import math
import random
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'Assets/Resources/audio/sfx'
RATE = 44100


def write(name, duration, frequency, end_frequency, noise=0, notes=None):
    rng = random.Random(700 + len(name))
    samples = []
    for i in range(round(duration * RATE)):
        t = i / RATE
        u = t / duration
        envelope = min(1, t / .008) * (1 - u) ** 2
        phase = 2 * math.pi * (frequency * t + (end_frequency - frequency) * t * t / (2 * duration))
        signal = .8 * math.sin(phase) + .2 * math.sin(phase * 2)
        if notes:
            signal = 0
            for start, pitch in notes:
                age = t - start
                if age >= 0:
                    signal += math.sin(2 * math.pi * pitch * age) * min(1, age / .008) * math.exp(-age * 9) / len(notes)
            envelope = min(1, (duration - t) / .05)
        signal = signal * (1 - noise) + rng.uniform(-1, 1) * noise
        samples.append(round(max(-1, min(1, signal * envelope * .42)) * 32767))
    ROOT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(ROOT / (name + '.wav')), 'wb') as output:
        output.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        output.writeframes(struct.pack('<' + 'h' * len(samples), *samples))


if __name__ == '__main__':
    write('Move', .09, 160, 100, .18)
    write('Push', .18, 110, 65, .3)
    write('Blocked', .14, 140, 95, .08)
    write('Land', .19, 180, 55, .22)
    write('PowerOn', .32, 360, 820)
    write('PowerOff', .28, 560, 180)
    write('Complete', .8, 0, 0, notes=[(0, 523.25), (.12, 659.25), (.24, 783.99)])
    write('Click', .055, 900, 600)
