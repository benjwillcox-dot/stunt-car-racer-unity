# Rebuilds both cars, writes the palette textures and the FBX straight into the game's
# Assets/Models/StuntCar (both liveries share one mesh, so only the yellow car is exported).
# Open StuntCar.blend, load this file in Blender's Text Editor and press Run Script.
import bpy
import os
import sys
import importlib

ROOT = bpy.path.abspath("//")
BUILD_DIR = os.path.join(ROOT, "blender_scripts")
UNITY_DIR = os.path.normpath(os.path.join(ROOT, "..", "..", "Assets", "Models", "StuntCar"))
if BUILD_DIR not in sys.path:
    sys.path.insert(0, BUILD_DIR)

g = {"BUILD_DIR": BUILD_DIR, "__name__": "build_car"}
with open(os.path.join(BUILD_DIR, "build_car.py"), encoding="utf-8") as fh:
    exec(compile(fh.read(), "build_car.py", "exec"), g)
root_y, parts = g["build"]()

import schemes as S
importlib.reload(S)
for ob in parts.values():
    S.bake_role_uvs(ob)
mats = {sch: S.make_material(sch, S.make_palette_images(sch, os.path.join(UNITY_DIR, "Textures")))
        for sch in ("Yellow", "White")}
S.apply_scheme(parts.values(), mats["Yellow"])
root_w, w_parts = S.duplicate_car(root_y, "Y_", "W_", "StuntCar_White", "StuntCar_White", (3.2, 0, 0))
S.apply_scheme(w_parts, mats["White"])
for m in list(bpy.data.materials):
    if m.name.startswith("ROLE_") and m.users == 0:
        bpy.data.materials.remove(m)

S.export_fbx(root_y, "Y_", os.path.join(UNITY_DIR, "StuntCar.fbx"))
print("Stunt car rebuilt and exported to " + UNITY_DIR)
