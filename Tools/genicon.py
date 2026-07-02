#!/usr/bin/env python3
"""Generates the Gravitile app icon (1024x1024 PNG, no alpha).

Design: a 3x3 grid of cell wells on the navy board field, with one heat-orange
tile mid-tumble (rotated, offset) — the game's identity in one glyph.
Matches Theme.swift's OKLCH-derived palette. Pure stdlib + zlib PNG writer.
Usage: python3 Tools/genicon.py Gravitile/Resources/Assets.xcassets/AppIcon.appiconset/icon-1024.png
"""
import math
import struct
import sys
import zlib

SIZE = 1024

# Palette (sRGB 0-255) from Theme.swift
BG_TOP = (10, 13, 21)        # deepened board navy for icon contrast
BG_BOTTOM = (23, 30, 43)     # oklch(20% 0.018 260)-ish
WELL = (36, 42, 53)          # cellWell
TILE = (255, 143, 49)        # oklch(76% 0.17 55) — the "16" tile orange
TILE_HI = (255, 178, 92)     # highlight edge


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def rounded_rect_dist(px, py, cx, cy, hw, hh, radius):
    """Signed distance to a rounded rectangle centered at (cx, cy)."""
    dx = abs(px - cx) - (hw - radius)
    dy = abs(py - cy) - (hh - radius)
    ox, oy = max(dx, 0.0), max(dy, 0.0)
    outside = math.hypot(ox, oy)
    inside = min(max(dx, dy), 0.0)
    return outside + inside - radius


def rotate(px, py, cx, cy, angle):
    s, c = math.sin(angle), math.cos(angle)
    dx, dy = px - cx, py - cy
    return cx + dx * c - dy * s, cy + dx * s + dy * c


def coverage(dist, soft=1.5):
    """Anti-aliased coverage from a signed distance."""
    return max(0.0, min(1.0, 0.5 - dist / soft))


def main(out_path):
    rows = [bytearray() for _ in range(SIZE)]

    # Grid geometry: 3x3 wells, generous margins.
    margin = 150
    gap = 34
    cell = (SIZE - 2 * margin - 2 * gap) / 3
    radius_well = 44
    # The tumbling tile replaces the top-right well, rotated 14° and nudged.
    tile_row, tile_col = 0, 2
    tile_angle = math.radians(14)
    tile_dx, tile_dy = -26, 30

    centers = []
    for r in range(3):
        for c in range(3):
            cx = margin + cell / 2 + c * (cell + gap)
            cy = margin + cell / 2 + r * (cell + gap)
            centers.append((r, c, cx, cy))

    for y in range(SIZE):
        row = rows[y]
        t = y / SIZE
        base = lerp(BG_TOP, BG_BOTTOM, t)
        for x in range(SIZE):
            color = base

            for r, c, cx, cy in centers:
                if (r, c) == (tile_row, tile_col):
                    continue
                d = rounded_rect_dist(x, y, cx, cy, cell / 2, cell / 2, radius_well)
                a = coverage(d)
                if a > 0:
                    color = lerp(color, WELL, a)
                    break

            # Tumbling tile: drawn last, on top, with soft drop shadow.
            _, _, tcx, tcy = next(z for z in centers if (z[0], z[1]) == (tile_row, tile_col))
            tcx += tile_dx
            tcy += tile_dy
            rx, ry = rotate(x, y, tcx, tcy, -tile_angle)
            d_shadow = rounded_rect_dist(rx - 10, ry - 16, tcx, tcy, cell / 2, cell / 2, radius_well)
            a_shadow = coverage(d_shadow, soft=26.0) * 0.42
            if a_shadow > 0:
                color = lerp(color, (0, 0, 0), a_shadow)
            d_tile = rounded_rect_dist(rx, ry, tcx, tcy, cell / 2, cell / 2, radius_well)
            a_tile = coverage(d_tile)
            if a_tile > 0:
                # Vertical-ish gradient across the rotated tile.
                tt = max(0.0, min(1.0, (ry - (tcy - cell / 2)) / cell))
                tile_color = lerp(TILE_HI, TILE, tt)
                color = lerp(color, tile_color, a_tile)

            row += bytes(color)

    # Write PNG (8-bit RGB).
    def chunk(tag, data):
        payload = tag + data
        return struct.pack(">I", len(data)) + payload + struct.pack(">I", zlib.crc32(payload))

    raw = b"".join(b"\x00" + bytes(row) for row in rows)
    png = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )
    with open(out_path, "wb") as f:
        f.write(png)
    print(f"Wrote {out_path}")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "icon-1024.png")
