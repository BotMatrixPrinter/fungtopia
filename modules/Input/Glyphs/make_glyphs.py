#!/usr/bin/env python3
"""Writes the Input module's button icons (64x64 SVG) next to this script.

Run: python3 modules/Input/Glyphs/make_glyphs.py
Letters are drawn as round-capped strokes (Godot's SVG loader does not draw <text>).
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
INK = "#4a3b32"
CREAM = "#fff8ec"
CAP_SHADE = "#d9c8b0"
WHITE = "#ffffff"
HILITE = "#ffb648"
DIM = "#8d8794"

# Stroke letters in a 10 x 14 box (x right, y down).
LETTERS = {
    "A": "M0,14 L5,0 L10,14 M2,9 H8",
    "B": "M0,7 H5.5 A3.5,3.5 0 0 0 5.5,0 H0 V14 H6 A3.5,3.5 0 0 0 6,7",
    "C": "M10,2.5 A5.5,7 0 1 0 10,11.5",
    "D": "M0,0 V14 H3.5 A6.5,7 0 0 0 3.5,0 Z",
    "E": "M10,0 H0 V14 H10 M0,7 H7.5",
    "F": "M10,0 H0 V14 M0,7 H7.5",
    "L": "M0,0 V14 H9",
    "M": "M0,14 V0 L5,8 L10,0 V14",
    "Q": "M5,0 A5,7 0 1 0 5,14 A5,7 0 1 0 5,0 M6,10 L10,14",
    "R": "M0,14 V0 H5.5 A3.5,3.5 0 0 1 5.5,7 H0 M5,7 L10,14",
    "S": "M9.5,2.5 C8,-0.5 0.5,-0.5 0.5,3.5 C0.5,7 9.5,6.5 9.5,10.5 C9.5,14.5 1.5,14.5 0.5,11.5",
    "T": "M0,0 H10 M5,0 V14",
    "W": "M0,0 L2.5,14 L5,5 L7.5,14 L10,0",
    "X": "M0,0 L10,14 M10,0 L0,14",
    "Y": "M0,0 L5,7 L10,0 M5,7 V14",
    "Z": "M0,0 H10 L0,14 H10",
    "1": "M2,3 L6,0 V14 M2,14 H10",
    "2": "M0.5,3.5 C1,-1 9.5,-1 9.5,4 C9.5,7 0.5,10 0,14 H10",
    "3": "M0.5,2 C3,-0.8 9.5,-0.5 9.5,3.5 C9.5,6 7,7 4,7 C7,7 10,8 10,10.5 C10,14.8 2,14.8 0,12",
    "+": "M5,2 V12 M0,7 H10",
    "-": "M0,7 H10",
}


def text(s, cx, cy, height, color, weight=2.6):
    """Stroke letters of `s`, `height` px tall, centered on (cx, cy)."""
    k = height / 14.0
    gap = 3.5
    width = len(s) * 10 + (len(s) - 1) * gap
    x0 = cx - width * k / 2
    y0 = cy - height / 2
    parts = []
    for i, ch in enumerate(s):
        x = x0 + i * (10 + gap) * k
        parts.append(
            f'<path transform="translate({x:.2f},{y0:.2f}) scale({k:.4f})" d="{LETTERS[ch]}" '
            f'fill="none" stroke="{color}" stroke-width="{weight / k:.2f}" '
            f'stroke-linecap="round" stroke-linejoin="round"/>')
    return "\n".join(parts)


def svg(body):
    return ('<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">\n'
            + body + "\n</svg>\n")


def keycap(inner, x=4, y=4, w=56, h=52):
    return (f'<rect x="{x}" y="{y + 4}" width="{w}" height="{h}" rx="12" fill="{INK}"/>\n'
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="12" fill="{CAP_SHADE}" stroke="{INK}" stroke-width="3"/>\n'
            f'<rect x="{x + 5}" y="{y + 3}" width="{w - 10}" height="{h - 12}" rx="9" fill="{CREAM}"/>\n'
            + inner)


def arrow(cx, cy, direction, size, color):
    """A rounded triangle pointing up/down/left/right."""
    s = size
    pts = {
        "up": [(0, -s), (s, s * 0.7), (-s, s * 0.7)],
        "down": [(0, s), (s, -s * 0.7), (-s, -s * 0.7)],
        "left": [(-s, 0), (s * 0.7, s), (s * 0.7, -s)],
        "right": [(s, 0), (-s * 0.7, s), (-s * 0.7, -s)],
    }[direction]
    d = "M" + " L".join(f"{cx + x:.1f},{cy + y:.1f}" for x, y in pts) + " Z"
    return f'<path d="{d}" fill="{color}" stroke="{color}" stroke-width="3" stroke-linejoin="round"/>'


def keyboard():
    g = {}
    for ch in "EQFMR123":
        g[f"key_{ch.lower()}"] = keycap(text(ch, 32, 27, 22, INK, 3.2))
    g["key_esc"] = keycap(text("ESC", 32, 27, 14, INK, 2.6))
    g["key_enter"] = keycap(
        f'<path d="M43,17 V32 H21 M28,25 L21,32 L28,39" fill="none" stroke="{INK}" stroke-width="4" '
        'stroke-linecap="round" stroke-linejoin="round"/>')
    g["key_space"] = keycap(
        f'<path d="M15,24 V33 H49 V24" fill="none" stroke="{INK}" stroke-width="4" '
        'stroke-linecap="round" stroke-linejoin="round"/>')
    g["key_shift"] = keycap(
        f'<path d="M32,13 L47,29 H39 V40 H25 V29 H17 Z" fill="none" stroke="{INK}" stroke-width="3.5" '
        'stroke-linejoin="round"/>')
    for d in ("up", "down", "left", "right"):
        g[f"key_{d}"] = keycap(arrow(32, 27, d, 10, INK))
    for ch in "WASD":
        g[f"key_{ch.lower()}"] = keycap(text(ch, 32, 27, 22, INK, 3.2))
    # Small four-key clusters: W A S D, and the arrow keys.
    def mini(x, y, inner):
        return keycap(inner, x, y, 19, 18).replace('rx="12"', 'rx="5"').replace('rx="9"', 'rx="4"')
    def cluster(top, left, mid, right):
        return (mini(22.5, 7, top) + mini(3, 32, left) + mini(22.5, 32, mid) + mini(42, 32, right))
    g["key_wasd"] = cluster(text("W", 32, 14.5, 8, INK, 1.8), text("A", 12.5, 39.5, 8, INK, 1.8),
                            text("S", 32, 39.5, 8, INK, 1.8), text("D", 51.5, 39.5, 8, INK, 1.8))
    g["key_arrows"] = cluster(arrow(32, 14.5, "up", 3.5, INK), arrow(12.5, 39.5, "left", 3.5, INK),
                              arrow(32, 39.5, "down", 3.5, INK), arrow(51.5, 39.5, "right", 3.5, INK))
    return g


def mouse(left=False, right=False, wheel=None, move=False):
    body = (f'<rect x="17" y="6" width="30" height="50" rx="15" fill="{CREAM}" stroke="{INK}" stroke-width="3"/>\n'
            f'<path d="M17,26 H47 M32,6 V26" stroke="{INK}" stroke-width="3" fill="none"/>\n')
    if left:
        body += f'<path d="M32,7.5 V24.5 H18.5 V21 A13.5,13.5 0 0 1 32,7.5 Z" fill="{HILITE}"/>\n'
    if right:
        body += f'<path d="M32,7.5 V24.5 H45.5 V21 A13.5,13.5 0 0 0 32,7.5 Z" fill="{HILITE}"/>\n'
    body += (f'<rect x="28.5" y="11" width="7" height="12" rx="3.5" fill="{HILITE if wheel else WHITE}" '
             f'stroke="{INK}" stroke-width="2.5"/>\n')
    if wheel:
        body += arrow(32, 40, wheel, 6, INK) + "\n"
    if move:
        body = body.replace('x="17" y="6" width="30" height="50"', 'x="21" y="12" width="22" height="38"')
        body = body.replace('rx="15"', 'rx="11"').replace("M17,26 H47 M32,6 V26", "M21,27 H43 M32,12 V27")
        body = body.replace('x="28.5" y="11" width="7" height="12"', 'x="29.5" y="15" width="5" height="9"')
        body += (arrow(32, 5.5, "up", 3.5, INK) + arrow(32, 58.5, "down", 3.5, INK)
                 + arrow(9, 31, "left", 3.5, INK) + arrow(55, 31, "right", 3.5, INK))
    return body


def face(fill, inner, ring=INK):
    return (f'<circle cx="32" cy="35" r="26" fill="{ring}"/>\n'
            f'<circle cx="32" cy="32" r="26" fill="{fill}" stroke="{ring}" stroke-width="3"/>\n'
            f'<circle cx="32" cy="32" r="20" fill="none" stroke="{WHITE}" stroke-opacity="0.25" stroke-width="3"/>\n'
            + inner)


def shoulder(fill, label, trigger=False):
    if trigger:
        shape = "M10,56 V24 C10,10 20,6 32,6 C44,6 54,10 54,24 V56 Z"
        cy = 34
    else:
        shape = "M6,48 V26 C6,18 12,14 20,14 H44 C52,14 58,18 58,26 V48 Z"
        cy = 32
    return (f'<path d="{shape}" transform="translate(0,3)" fill="{INK}"/>\n'
            f'<path d="{shape}" fill="{fill}" stroke="{INK}" stroke-width="3" stroke-linejoin="round"/>\n'
            + text(label, 32, cy, 15 if len(label) > 1 else 18, WHITE, 2.8))


def stick(fill, label):
    return (f'<circle cx="32" cy="32" r="27" fill="{DIM}" stroke="{INK}" stroke-width="3"/>\n'
            f'<circle cx="32" cy="35" r="18" fill="{INK}"/>\n'
            f'<circle cx="32" cy="31" r="18" fill="{fill}" stroke="{INK}" stroke-width="3"/>\n'
            f'<circle cx="32" cy="31" r="12" fill="none" stroke="{WHITE}" stroke-opacity="0.3" stroke-width="2.5"/>\n'
            + text(label, 32, 31, 12, WHITE, 2.6))


def dpad(direction):
    arm = "M24,6 H40 V24 H58 V40 H40 V58 H24 V40 H6 V24 H24 Z"
    lit = {
        "up": "M25.5,7.5 H38.5 V25.5 L32,32 L25.5,25.5 Z",
        "down": "M25.5,56.5 H38.5 V38.5 L32,32 L25.5,38.5 Z",
        "left": "M7.5,25.5 V38.5 H25.5 L32,32 L25.5,25.5 Z",
        "right": "M56.5,25.5 V38.5 H38.5 L32,32 L38.5,25.5 Z",
    }[direction]
    tip = {"up": (32, 15), "down": (32, 49), "left": (15, 32), "right": (49, 32)}[direction]
    return (f'<path d="{arm}" transform="translate(0,3)" fill="{INK}" stroke="{INK}" stroke-width="3" stroke-linejoin="round"/>\n'
            f'<path d="{arm}" fill="{DIM}" stroke="{INK}" stroke-width="3" stroke-linejoin="round"/>\n'
            f'<path d="{lit}" fill="{HILITE}" stroke-linejoin="round"/>\n'
            + arrow(tip[0], tip[1], direction, 4.5, INK))


def small_button(fill, inner, pill=False):
    if pill:
        return (f'<rect x="8" y="19" width="48" height="30" rx="15" fill="{INK}"/>\n'
                f'<rect x="8" y="16" width="48" height="30" rx="15" fill="{fill}" stroke="{INK}" stroke-width="3"/>\n'
                + inner)
    return face(fill, inner)


def lines(cx, cy, w, gap, color):
    return "".join(f'<path d="M{cx - w / 2},{cy + i * gap} H{cx + w / 2}" stroke="{color}" stroke-width="3.5" '
                   'stroke-linecap="round"/>' for i in (-1, 0, 1))


def gamepads():
    g = {}
    dark = "#3b3a44"
    # Xbox: colored face buttons with white letters.
    for label, color in (("A", "#5bb33f"), ("B", "#e0473c"), ("X", "#3a86d8"), ("Y", "#efb71e")):
        g[f"xbox_{label.lower()}"] = face(color, text(label, 32, 32, 22, WHITE, 3.6))
    for label in ("LB", "RB"):
        g[f"xbox_{label.lower()}"] = shoulder(dark, label)
    for label in ("LT", "RT"):
        g[f"xbox_{label.lower()}"] = shoulder(dark, label, trigger=True)
    g["xbox_view"] = small_button(dark,
        f'<rect x="20" y="20" width="16" height="13" rx="2.5" fill="none" stroke="{WHITE}" stroke-width="3"/>'
        f'<rect x="28" y="28" width="16" height="13" rx="2.5" fill="{dark}" stroke="{WHITE}" stroke-width="3"/>')
    g["xbox_menu"] = small_button(dark, lines(32, 32, 20, 7, WHITE))

    # PlayStation: dark buttons with the colored symbols.
    ps = "#2f2c3c"
    g["ps_cross"] = face(ps, f'<path d="M22,22 L42,42 M42,22 L22,42" stroke="#86b4f0" stroke-width="5" stroke-linecap="round"/>')
    g["ps_circle"] = face(ps, f'<circle cx="32" cy="32" r="11.5" fill="none" stroke="#f07878" stroke-width="5"/>')
    g["ps_square"] = face(ps, f'<rect x="21.5" y="21.5" width="21" height="21" rx="2" fill="none" stroke="#ee95d6" stroke-width="5"/>')
    g["ps_triangle"] = face(ps, f'<path d="M32,19 L44.5,40.5 H19.5 Z" fill="none" stroke="#4fcfae" stroke-width="5" stroke-linejoin="round"/>')
    for label in ("L1", "R1"):
        g[f"ps_{label.lower()}"] = shoulder(ps, label)
    for label in ("L2", "R2"):
        g[f"ps_{label.lower()}"] = shoulder(ps, label, trigger=True)
    g["ps_share"] = small_button(ps,
        f'<path d="M32,40 V22 M25,28 L32,21 L39,28" fill="none" stroke="{WHITE}" stroke-width="3.5" '
        'stroke-linecap="round" stroke-linejoin="round"/>'
        f'<path d="M22,34 V40 H42 V34" fill="none" stroke="{WHITE}" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>',
        pill=True).replace('y="16"', 'y="16"')
    g["ps_options"] = small_button(ps, lines(32, 31, 22, 7, WHITE), pill=True)

    # Nintendo: light gray buttons with dark letters, A right, B bottom, X top, Y left.
    ns = "#e9e6ee"
    nsd = "#43414c"
    for label in "ABXY":
        g[f"ns_{label.lower()}"] = face(ns, text(label, 32, 32, 22, nsd, 3.6))
    for label in ("L", "R"):
        g[f"ns_{label.lower()}"] = shoulder(nsd, label)
    for label in ("ZL", "ZR"):
        g[f"ns_{label.lower()}"] = shoulder(nsd, label, trigger=True)
    g["ns_minus"] = face(nsd, text("-", 32, 32, 22, WHITE, 3.6))
    g["ns_plus"] = face(nsd, text("+", 32, 32, 22, WHITE, 3.6))

    for family, fill in (("xbox", dark), ("ps", ps), ("ns", nsd)):
        g[f"{family}_ls"] = stick(fill, "L")
        g[f"{family}_rs"] = stick(fill, "R")
        for d in ("up", "down", "left", "right"):
            g[f"{family}_dpad_{d}"] = dpad(d)
    return g


def main():
    glyphs = keyboard()
    glyphs["mouse_left"] = mouse(left=True)
    glyphs["mouse_right"] = mouse(right=True)
    glyphs["mouse_wheel_up"] = mouse(wheel="up")
    glyphs["mouse_wheel_down"] = mouse(wheel="down")
    glyphs["mouse_move"] = mouse(move=True)
    glyphs.update(gamepads())
    for old in os.listdir(HERE):
        if old.endswith(".svg"):
            os.remove(os.path.join(HERE, old))
    for name, body in sorted(glyphs.items()):
        with open(os.path.join(HERE, name + ".svg"), "w") as f:
            f.write(svg(body))
    print(f"wrote {len(glyphs)} glyphs to {HERE}")


if __name__ == "__main__":
    main()
