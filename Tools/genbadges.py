#!/usr/bin/env python3
"""Generates 512x512 achievement badge PNGs: a rounded tile on navy field,
tinted per achievement, with a simple glyph (text) rendered as blocky
5x7 pixel-font — consistent with the app's tile aesthetic. Stdlib only.
Usage: python3 Tools/genbadges.py /tmp/gravitile-badges
"""
import math
import struct
import sys
import zlib
from pathlib import Path

SIZE = 512

# 5x7 pixel font for the glyphs we need (digits + a few letters/symbols)
FONT = {
    "0": ["01110","10001","10011","10101","11001","10001","01110"],
    "1": ["00100","01100","00100","00100","00100","00100","01110"],
    "2": ["01110","10001","00001","00010","00100","01000","11111"],
    "3": ["11110","00001","00001","01110","00001","00001","11110"],
    "4": ["00010","00110","01010","10010","11111","00010","00010"],
    "5": ["11111","10000","11110","00001","00001","10001","01110"],
    "6": ["00110","01000","10000","11110","10001","10001","01110"],
    "7": ["11111","00001","00010","00100","01000","01000","01000"],
    "8": ["01110","10001","10001","01110","10001","10001","01110"],
    "9": ["01110","10001","10001","01111","00001","00010","01100"],
    "K": ["10001","10010","10100","11000","10100","10010","10001"],
    "M": ["10001","11011","10101","10101","10001","10001","10001"],
    "X": ["10001","10001","01010","00100","01010","10001","10001"],
    "!": ["00100","00100","00100","00100","00100","00000","00100"],
    "*": ["00000","10101","01110","11111","01110","10101","00000"],
    "~": ["00000","00000","01000","10101","00010","00000","00000"],
}

BADGES = [
    ("first-merge",   "2",    (255, 143, 49)),   # orange
    ("first-cascade", "~",    (241, 107, 51)),   # tangerine
    ("cascade-x3",    "X3",   (241, 68, 69)),    # vermilion
    ("tile-256",      "256",  (191, 28, 127)),   # magenta
    ("tile-512",      "512",  (146, 43, 168)),   # purple
    ("tile-1024",     "1K",   (97, 62, 190)),    # violet
    ("tile-2048",     "2K",   (35, 91, 200)),    # indigo
    ("streak-7",      "7!",   (255, 143, 49)),   # orange
    ("streak-30",     "30!",  (22, 176, 155)),   # teal
]

BG = (10, 13, 21)
TEXT = (16, 13, 8)


def rounded_dist(px, py, cx, cy, half, radius):
    dx, dy = abs(px - cx) - (half - radius), abs(py - cy) - (half - radius)
    ox, oy = max(dx, 0.0), max(dy, 0.0)
    return math.hypot(ox, oy) + min(max(dx, dy), 0.0) - radius


def write_png(path, rows):
    def chunk(tag, data):
        payload = tag + data
        return struct.pack(">I", len(data)) + payload + struct.pack(">I", zlib.crc32(payload))
    raw = b"".join(b"\x00" + bytes(r) for r in rows)
    png = (b"\x89PNG\r\n\x1a\n"
           + chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0))
           + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
    Path(path).write_bytes(png)


def glyph_pixels(text):
    cols = 0
    for ch in text:
        cols += 6
    cols -= 1
    grid = [[0] * cols for _ in range(7)]
    x = 0
    for ch in text:
        for r, row in enumerate(FONT[ch]):
            for c, bit in enumerate(row):
                if bit == "1":
                    grid[r][x + c] = 1
        x += 6
    return grid


def main(outdir):
    out = Path(outdir)
    out.mkdir(parents=True, exist_ok=True)
    for name, text, tile in BADGES:
        grid = glyph_pixels(text)
        gh, gw = len(grid), len(grid[0])
        cell = min(280 // gw, 300 // gh)
        ox = (SIZE - gw * cell) // 2
        oy = (SIZE - gh * cell) // 2
        hi = tuple(min(255, int(c * 1.25 + 20)) for c in tile)
        rows = []
        for y in range(SIZE):
            row = bytearray()
            for x in range(SIZE):
                d = rounded_dist(x, y, SIZE / 2, SIZE / 2, SIZE * 0.42, 60)
                if d < 0:
                    t = y / SIZE
                    color = tuple(int(hi[i] + (tile[i] - hi[i]) * t) for i in range(3))
                    gx, gy = (x - ox) // cell, (y - oy) // cell
                    if 0 <= gy < gh and 0 <= gx < gw and grid[gy][gx]:
                        color = TEXT
                else:
                    color = BG
                row += bytes(color)
            rows.append(row)
        write_png(out / f"{name}.png", rows)
        print(f"wrote {name}.png")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "/tmp/gravitile-badges")
