#!/usr/bin/env python3
"""Writes the Wardrobe's own prompt icons (Page Up, Page Down: the tab keys, which the Input
module has no icon for) next to this script, drawn like the Input module's keycaps.

Run: python3 modules/Wardrobe/Godot/Glyphs/make_glyphs.py
"""
import importlib.util
import sys
import os

HERE = os.path.dirname(os.path.abspath(__file__))
INPUT = os.path.join(HERE, "..", "..", "..", "Input", "Glyphs", "make_glyphs.py")

sys.dont_write_bytecode = True  # leave nothing in the Input module
spec = importlib.util.spec_from_file_location("input_glyphs", INPUT)
g = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g)

g.LETTERS.setdefault("P", "M0,14 V0 H5.5 A3.5,3.5 0 0 1 5.5,7 H0")
g.LETTERS.setdefault("G", "M10,2.5 A5.5,7 0 1 0 10,11.5 V8 H6")


def page_key(direction):
    return g.keycap(g.text("PG", 32, 17, 10, g.INK, 2.4) + "\n" + g.arrow(32, 34, direction, 9, g.INK))


def main():
    for name, direction in (("key_pgup", "up"), ("key_pgdn", "down")):
        with open(os.path.join(HERE, name + ".svg"), "w") as f:
            f.write(g.svg(page_key(direction)))
    print(f"wrote 2 glyphs to {HERE}")


if __name__ == "__main__":
    main()
