#!/usr/bin/env python3
"""Generate a lightweight, original E-2D-inspired OBJ asset for Hegemonia."""

from math import cos, pi, sin
from pathlib import Path


PROJECT = Path(__file__).resolve().parents[2]
OUTPUT = PROJECT / "Assets" / "Art" / "Unidades" / "GrummanE2D"
OUTPUT.mkdir(parents=True, exist_ok=True)

vertices_by_material = {"Hull": [], "Radome": [], "Glass": [], "Propeller": [], "Rubber": []}
faces_by_material = {name: [] for name in vertices_by_material}


def add_mesh(material, vertices, faces):
    target_vertices = vertices_by_material[material]
    target_faces = faces_by_material[material]
    offset = len(target_vertices)
    target_vertices.extend(vertices)
    target_faces.extend(tuple(index + offset for index in face) for face in faces)


def ellipsoid(material, center, scale, segments=20, rings=12):
    cx, cy, cz = center
    sx, sy, sz = scale
    vertices = [(cx, cy + sy, cz)]
    for row in range(1, rings):
        phi = pi * row / rings
        ring_radius = sin(phi)
        y = cy + sy * cos(phi)
        for column in range(segments):
            theta = 2.0 * pi * column / segments
            vertices.append((cx + sx * ring_radius * cos(theta), y, cz + sz * ring_radius * sin(theta)))
    bottom = len(vertices)
    vertices.append((cx, cy - sy, cz))
    faces = []
    first_ring = 1
    for column in range(segments):
        faces.append((0, first_ring + column, first_ring + (column + 1) % segments))
    for row in range(rings - 2):
        upper = 1 + row * segments
        lower = upper + segments
        for column in range(segments):
            next_column = (column + 1) % segments
            faces.append((upper + column, lower + column, lower + next_column, upper + next_column))
    last_ring = 1 + (rings - 2) * segments
    for column in range(segments):
        faces.append((last_ring + column, bottom, last_ring + (column + 1) % segments))
    add_mesh(material, vertices, faces)


def fuselage():
    # Nose points toward +Z. The E-2D has a narrow nose, broad cockpit, and
    # a deep rear fuselage to support its high wing and radar assembly.
    rings = [
        (20.0, 0.05, 0.05, 1.00),
        (18.8, 0.48, 0.58, 1.05),
        (16.0, 1.05, 1.10, 1.05),
        (10.0, 1.35, 1.45, 0.95),
        (2.0, 1.50, 1.55, 0.95),
        (-8.0, 1.38, 1.40, 0.95),
        (-15.0, 1.02, 1.10, 0.90),
        (-19.0, 0.48, 0.56, 0.85),
        (-20.0, 0.04, 0.06, 0.82),
    ]
    segments = 24
    vertices = []
    for z, radius_x, radius_y, center_y in rings:
        for column in range(segments):
            angle = 2.0 * pi * column / segments
            vertices.append((radius_x * cos(angle), center_y + radius_y * sin(angle), z))
    faces = []
    for row in range(len(rings) - 1):
        upper = row * segments
        lower = upper + segments
        for column in range(segments):
            next_column = (column + 1) % segments
            faces.append((upper + column, lower + column, lower + next_column, upper + next_column))
    faces.append(tuple(range(segments - 1, -1, -1)))
    start = (len(rings) - 1) * segments
    faces.append(tuple(start + column for column in range(segments)))
    add_mesh("Hull", vertices, faces)


def planform(material, outline, y_top, y_bottom):
    count = len(outline)
    vertices = [(x, y_top, z) for x, z in outline] + [(x, y_bottom, z) for x, z in outline]
    faces = [tuple(range(count)), tuple(range(count, count * 2))[::-1]]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, count + index, count + next_index, next_index))
    add_mesh(material, vertices, faces)


def vertical_fin(material, outline_yz, x_center, thickness):
    half = thickness * 0.5
    vertices = [(x_center - half, y, z) for y, z in outline_yz]
    vertices += [(x_center + half, y, z) for y, z in outline_yz]
    count = len(outline_yz)
    faces = [tuple(range(count)), tuple(range(count, count * 2))[::-1]]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, next_index, count + next_index, count + index))
    add_mesh(material, vertices, faces)


def box(material, center, size, angle_z=0.0):
    cx, cy, cz = center
    sx, sy, sz = (component * 0.5 for component in size)
    base = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
            (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz)]
    c, s = cos(angle_z), sin(angle_z)
    vertices = [(cx + x * c - y * s, cy + x * s + y * c, cz + z) for x, y, z in base]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5),
             (2, 3, 7, 6), (3, 0, 4, 7)]
    add_mesh(material, vertices, faces)


def wheel(center, radius=0.58, thickness=0.3, segments=16):
    # A dark torus facing across the aircraft's X axis.
    cx, cy, cz = center
    minor = radius * 0.28
    major = radius - minor
    vertices = []
    faces = []
    for ring in range(8):
        theta = 2 * pi * ring / 8
        for column in range(segments):
            phi = 2 * pi * column / segments
            radial = major + minor * cos(phi)
            vertices.append((cx + (thickness * 0.5 + minor * sin(phi)) * cos(theta),
                             cy + radial * cos(theta), cz + radial * sin(theta)))
    for ring in range(8):
        next_ring = (ring + 1) % 8
        for column in range(segments):
            next_column = (column + 1) % segments
            a = ring * segments + column
            b = next_ring * segments + column
            c = next_ring * segments + next_column
            d = ring * segments + next_column
            faces.append((a, b, c, d))
    add_mesh("Rubber", vertices, faces)


def write_mtl():
    materials = {
        "Hull": ((0.42, 0.49, 0.53), (0.18, 0.18, 0.18), 72),
        "Radome": ((0.31, 0.39, 0.43), (0.10, 0.10, 0.10), 48),
        "Glass": ((0.035, 0.075, 0.095), (0.22, 0.22, 0.22), 96),
        "Propeller": ((0.10, 0.13, 0.14), (0.16, 0.16, 0.16), 36),
        "Rubber": ((0.025, 0.03, 0.032), (0.05, 0.05, 0.05), 12),
    }
    with (OUTPUT / "GrummanE2D.mtl").open("w", encoding="utf-8", newline="\n") as stream:
        for name, (diffuse, specular, gloss) in materials.items():
            stream.write(f"newmtl {name}\n")
            stream.write("Ka 0.08 0.08 0.08\n")
            stream.write(f"Kd {diffuse[0]:.4f} {diffuse[1]:.4f} {diffuse[2]:.4f}\n")
            stream.write(f"Ks {specular[0]:.4f} {specular[1]:.4f} {specular[2]:.4f}\n")
            stream.write(f"Ns {gloss}\nd 1.0\nillum 2\n\n")


def write_obj():
    with (OUTPUT / "GrummanE2D.obj").open("w", encoding="utf-8", newline="\n") as stream:
        stream.write("# Original lightweight E-2D Hawkeye-inspired aircraft model for Hegemonia Mundial.\n")
        stream.write("# Nose points toward +Z; Unity uses +Y as up.\n")
        stream.write("mtllib GrummanE2D.mtl\n")
        offset = 1
        for material, vertices in vertices_by_material.items():
            if not vertices:
                continue
            stream.write(f"\no GrummanE2D_{material}\nusemtl {material}\ns 1\n")
            for x, y, z in vertices:
                stream.write(f"v {x:.5f} {y:.5f} {z:.5f}\n")
            for face in faces_by_material[material]:
                stream.write("f " + " ".join(str(index + offset) for index in face) + "\n")
            offset += len(vertices)


def build():
    fuselage()

    # High, tapered wing and tail surfaces.
    planform("Hull", [(-15.0, -2.0), (-12.2, 2.2), (12.2, 2.2), (15.0, -2.0),
                       (14.3, -4.8), (2.2, -5.7), (-2.2, -5.7), (-14.3, -4.8)], 4.35, 3.85)
    planform("Hull", [(-6.3, -14.5), (-5.2, -13.0), (5.2, -13.0), (6.3, -14.5),
                       (5.8, -17.2), (-5.8, -17.2)], 3.2, 2.85)
    vertical_fin("Hull", [(0.9, -18.8), (5.7, -17.2), (4.0, -13.8), (1.2, -13.8)], 0, 0.55)

    # Twin turboprop nacelles, spinner hubs, and four static blades per prop.
    for side in (-1, 1):
        x = side * 7.4
        ellipsoid("Hull", (x, 3.85, -0.15), (1.25, 1.05, 3.1), 20, 12)
        ellipsoid("Propeller", (x, 3.85, 2.4), (0.52, 0.52, 0.65), 16, 10)
        for blade in range(4):
            angle = blade * pi / 2 + pi / 8
            box("Propeller", (x + cos(angle) * 1.25, 3.85 + sin(angle) * 1.25, 2.65),
                (1.85, 0.22, 0.12), angle)

        # Main gear strut and wheel.
        box("Hull", (side * 4.4, 0.45, -2.1), (0.24, 2.8, 0.25))
        wheel((side * 4.4, -1.05, -2.1), 0.58, 0.3)

    # Signature elevated radar rotodome with a short support mast.
    box("Hull", (0, 2.8, -1.0), (0.36, 2.2, 0.45))
    ellipsoid("Radome", (0, 7.25, -1.0), (5.7, 0.44, 1.35), 28, 12)

    # Cockpit glazing, nose wheel, and propeller hub fairings.
    ellipsoid("Glass", (-0.48, 1.72, 17.0), (0.43, 0.33, 1.15), 16, 10)
    ellipsoid("Glass", (0.48, 1.72, 17.0), (0.43, 0.33, 1.15), 16, 10)
    box("Hull", (0, -0.35, 17.4), (0.20, 1.4, 0.22))
    wheel((0, -1.28, 16.7), 0.46, 0.26)

    write_mtl()
    write_obj()
    print(f"Generated {OUTPUT / 'GrummanE2D.obj'}")


if __name__ == "__main__":
    build()
