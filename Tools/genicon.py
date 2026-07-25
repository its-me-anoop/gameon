#!/usr/bin/env python3
"""Generates the Gravitile app icon (1024x1024 PNG, no alpha).

Design: a small tiled world lit from the upper right, its climate bands running
around a *tilted* axis, with the bright axis shaft cutting through both poles.
The tilt is the point — it is what the game is about, and it is what makes the
icon read as this app rather than as any other planet.

Palette matches WorldPalette.ember. Pure stdlib plus a zlib PNG writer, so the
icon is regenerable and has nothing to install.

Usage:
    python3 Tools/genicon.py Gravitile/Resources/Assets.xcassets/AppIcon.appiconset/icon-1024.png
"""
import math
import struct
import sys
import zlib

SIZE = 1024

# sRGB 0-255, from WorldPalette.ember.
SPACE_TOP = (11, 13, 22)
SPACE_BOTTOM = (24, 27, 42)
GLOW = (58, 48, 78)
CRUST = (58, 44, 33)
FROZEN = (206, 219, 233)
TUNDRA = (150, 176, 190)
TEMPERATE = (104, 158, 116)
ARID = (216, 158, 62)
MOLTEN = (206, 96, 44)
AXIS = (239, 148, 51)
AXIS_HI = (255, 197, 128)
STAR = (255, 233, 178)

# The world's tilt, in degrees clockwise from vertical.
TILT = 22.0
# Direction the star lights it from, in screen space.
LIGHT = (0.52, -0.66, 0.54)


def lerp(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def normalize(v):
    length = math.sqrt(sum(component * component for component in v))
    return tuple(component / length for component in v)


def coverage(distance, soft=1.4):
    """Antialiased inside-ness from a signed distance in pixels."""
    return max(0.0, min(1.0, 0.5 - distance / soft))


def hex_seam(x, y, size):
    """Distance from a point to the nearest edge of a pointy-top hex grid.

    Positive inside a cell, zero on the seam between cells.
    """
    # Axial coordinates, then cube-round to the containing cell.
    q = (math.sqrt(3) / 3 * x - 1 / 3 * y) / size
    r = (2 / 3 * y) / size
    cube = (q, -q - r, r)
    rounded = [round(component) for component in cube]
    diffs = [abs(rounded[i] - cube[i]) for i in range(3)]
    if diffs[0] > diffs[1] and diffs[0] > diffs[2]:
        rounded[0] = -rounded[1] - rounded[2]
    elif diffs[1] > diffs[2]:
        rounded[1] = -rounded[0] - rounded[2]
    else:
        rounded[2] = -rounded[0] - rounded[1]

    center_x = size * (math.sqrt(3) * rounded[0] + math.sqrt(3) / 2 * rounded[2])
    center_y = size * (3 / 2 * rounded[2])
    dx, dy = x - center_x, y - center_y

    # A hexagon is the intersection of three slabs, so the distance to its
    # boundary is the inradius minus the largest slab projection.
    # Pointy-top cells have edge normals at 0, 60 and 120 degrees. Using the
    # flat-top set here is what turns a honeycomb into a field of triangles.
    inradius = size * math.sqrt(3) / 2
    projection = max(
        abs(dx * math.cos(angle) + dy * math.sin(angle))
        for angle in (0.0, math.pi / 3, 2 * math.pi / 3)
    )
    return inradius - projection


def capsule_distance(px, py, ax, ay, bx, by, radius):
    """Signed distance to a capsule running from (ax, ay) to (bx, by)."""
    vx, vy = bx - ax, by - ay
    wx, wy = px - ax, py - ay
    length_squared = vx * vx + vy * vy
    t = 0.0 if length_squared == 0 else max(0.0, min(1.0, (wx * vx + wy * vy) / length_squared))
    return math.hypot(wx - vx * t, wy - vy * t) - radius


def main(out_path):
    center = SIZE / 2
    planet_radius = SIZE * 0.375
    light = normalize(LIGHT)
    tilt = math.radians(TILT)
    # Axis in screen space, leaning right at the top.
    axis = (math.sin(tilt), -math.cos(tilt), 0.0)
    axis_end = planet_radius * 1.30
    star_x, star_y = center + SIZE * 0.395, center - SIZE * 0.415

    rows = []
    for y in range(SIZE):
        row = bytearray()
        for x in range(SIZE):
            dx, dy = x - center, y - center
            distance = math.hypot(dx, dy)

            # Sky: a vertical gradient with a warm bloom where the star sits.
            color = lerp(SPACE_TOP, SPACE_BOTTOM, y / SIZE)
            bloom = math.exp(
                -((x - star_x) ** 2 + (y - star_y) ** 2) / (2 * (SIZE * 0.20) ** 2)
            )
            color = lerp(color, GLOW, bloom * 0.85)
            star_core = coverage(math.hypot(x - star_x, y - star_y) - SIZE * 0.024, soft=7)
            color = lerp(color, STAR, star_core * 0.9)

            # The world.
            inside = coverage(distance - planet_radius)
            if inside > 0:
                # Reconstruct the sphere's normal from the projected disc.
                nz = math.sqrt(max(0.0, planet_radius**2 - distance**2)) / planet_radius
                normal = (dx / planet_radius, dy / planet_radius, nz)

                # Latitude about the tilted axis picks the climate band.
                latitude = abs(sum(normal[i] * axis[i] for i in range(3)))
                if latitude > 0.90:
                    ground = FROZEN
                elif latitude > 0.76:
                    ground = lerp(TUNDRA, FROZEN, (latitude - 0.76) / 0.14)
                elif latitude > 0.52:
                    ground = lerp(TEMPERATE, TUNDRA, (latitude - 0.52) / 0.24)
                elif latitude > 0.22:
                    ground = lerp(ARID, TEMPERATE, (latitude - 0.22) / 0.30)
                else:
                    ground = lerp(MOLTEN, ARID, latitude / 0.22)

                # Tiles: the hex grid is walked in a frame that leans with the
                # axis, so seams follow the world instead of the screen.
                warp = 1.0 / max(0.45, nz)
                ux = (dx * math.cos(tilt) + dy * math.sin(tilt)) * warp
                uy = (-dx * math.sin(tilt) + dy * math.cos(tilt)) * warp
                seam = hex_seam(ux, uy, SIZE * 0.082)
                ground = lerp(CRUST, ground, coverage(-seam + 4.5, soft=3.0))

                shade = sum(normal[i] * light[i] for i in range(3))
                shade = 0.30 + 0.85 * max(0.0, shade)
                lit = tuple(min(255.0, channel * shade) for channel in ground)
                # A cool rim where the world meets the sky.
                rim = coverage(abs(distance - planet_radius) - 3.0, soft=5)
                lit = lerp(lit, FROZEN, rim * 0.16)
                color = lerp(color, lit, inside)

            # The axis, drawn last so it reads over the world.
            shaft = capsule_distance(
                x, y,
                center - axis[0] * axis_end, center - axis[1] * axis_end,
                center + axis[0] * axis_end, center + axis[1] * axis_end,
                SIZE * 0.016,
            )
            color = lerp(color, AXIS, coverage(shaft))
            for direction in (1, -1):
                cap = math.hypot(
                    x - (center + direction * axis[0] * axis_end),
                    y - (center + direction * axis[1] * axis_end),
                ) - SIZE * 0.034
                color = lerp(color, AXIS_HI, coverage(cap))

            row += bytes(int(max(0, min(255, round(channel)))) for channel in color)
        rows.append(row)

    raw = b"".join(b"\x00" + bytes(row) for row in rows)

    def chunk(tag, data):
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        )

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9))
    png += chunk(b"IEND", b"")

    with open(out_path, "wb") as handle:
        handle.write(png)
    print(f"wrote {out_path} ({len(png) // 1024} KB)")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "icon-1024.png")
