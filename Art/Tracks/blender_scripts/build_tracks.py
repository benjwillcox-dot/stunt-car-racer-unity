# 3D previews of the game's tracks (Blender 5.x): the banked deck with its kerbs, gaps and
# scaffolding, built from tracks.json, the tracks sampled every 2 m exactly as Track.cs samples
# them (Art/Tracks/exporter writes it). Each track is its own collection, laid out where it sits
# in the game, so they can be looked at one at a time. build_all() builds them; render_all()
# renders each one side on, beside its biggest drop and riding over the top of it into renders/;
# contact_sheets() puts each kind of view for every track on one sheet.
import bpy
import bmesh
import json
import math
import os
from mathutils import Vector

HALF_WIDTH, THICKNESS, KERB = 4.0, 0.6, 0.7


def B(x, y, z):
    """Unity axes to Blender's, the way the FBX export maps them."""
    return Vector((-x, -z, y))


def material(name, rgb, rough=0.6, metal=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    return m


def mats():
    return [material("Track asphalt", (0.05, 0.05, 0.055), 0.7),
            material("Track kerb red", (0.6, 0.03, 0.02), 0.5),
            material("Track kerb white", (0.85, 0.85, 0.82), 0.5),
            material("Track side", (0.3, 0.3, 0.32), 0.6),
            material("Track scaffold", (0.55, 0.57, 0.6), 0.4, 0.6)]


def build_track(t, materials):
    """One track's deck and scaffolding, in a collection named after it."""
    name = t["name"]
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.data.collections["Tracks"].children.link(col)
    for ob in list(col.objects):
        me = ob.data
        bpy.data.objects.remove(ob, do_unlink=True)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)

    S = t["samples"]
    n = len(S)
    centre = [B(s[0], s[1], s[2]) for s in S]
    right = [B(s[3], s[4], s[5]) for s in S]
    gap = [s[7] for s in S]
    up = []
    for i in range(n):
        fwd = (centre[(i + 1) % n] - centre[i - 1]).normalized()
        up.append(right[i].cross(fwd).normalized() if right[i].cross(fwd).z > 0 else fwd.cross(right[i]).normalized())

    bm = bmesh.new()
    # Across the deck, left edge to right: kerb, asphalt, kerb; then the sides and underside.
    across = [-HALF_WIDTH, -HALF_WIDTH + KERB, HALF_WIDTH - KERB, HALF_WIDTH]
    rows = []
    for i in range(n):
        top = [bm.verts.new(centre[i] + right[i] * a) for a in across]
        bot = [bm.verts.new(centre[i] + right[i] * across[0] - up[i] * THICKNESS),
               bm.verts.new(centre[i] + right[i] * across[3] - up[i] * THICKNESS)]
        rows.append(top + bot)
    for i in range(n):
        if gap[i]:
            continue
        a, b = rows[i], rows[(i + 1) % n]
        kerb = 1 if (i // 1) % 2 == 0 else 2
        faces = [((a[0], a[1], b[1], b[0]), kerb), ((a[1], a[2], b[2], b[1]), 0), ((a[2], a[3], b[3], b[2]), 3 - kerb),
                 ((a[3], a[5], b[5], b[3]), 3), ((a[5], a[4], b[4], b[5]), 3), ((a[4], a[0], b[0], b[4]), 3)]
        for verts, m in faces:
            try:
                f = bm.faces.new(verts)
                f.material_index = m
            except ValueError:
                pass
    # Close the deck's ends at each gap.
    for i in range(n):
        if gap[i] and not gap[i - 1]:
            r = rows[i]
            bm.faces.new((r[0], r[4], r[5], r[3], r[2], r[1])).material_index = 3
        if gap[i]:
            r = rows[(i + 1) % n]
            if not gap[(i + 1) % n]:
                bm.faces.new((r[0], r[1], r[2], r[3], r[5], r[4])).material_index = 3
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name + " deck")
    bm.to_mesh(me)
    bm.free()
    for m in materials[:4]:
        me.materials.append(m)
    deck = bpy.data.objects.new(name + " deck", me)
    col.objects.link(deck)

    # Scaffolding: a pair of legs every 6 m, as in the game, with ties between them.
    bm = bmesh.new()

    def leg(p0, p1, w):
        d = p1 - p0
        if d.length < 0.2:
            return
        mat = d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        geom = bmesh.ops.create_cube(bm, size=1.0)
        vs = geom["verts"]
        bmesh.ops.scale(bm, vec=(w, w, d.length), verts=vs)
        bmesh.ops.transform(bm, matrix=mat, verts=vs)
        bmesh.ops.translate(bm, vec=(p0 + p1) / 2, verts=vs)

    for i in range(0, n, 3):
        if gap[i] or gap[i - 1]:
            continue
        for a in (-HALF_WIDTH + 0.5, HALF_WIDTH - 0.5):
            top = centre[i] + right[i] * a - up[i] * THICKNESS
            if top.z > 1.0:
                leg(Vector((top.x, top.y, 0)), top, 0.32)
        tl = centre[i] + right[i] * (-HALF_WIDTH + 0.5) - up[i] * THICKNESS
        tr = centre[i] + right[i] * (HALF_WIDTH - 0.5) - up[i] * THICKNESS
        h = 3.0
        while h < min(tl.z, tr.z) - 0.6:
            leg(Vector((tl.x, tl.y, h)), Vector((tr.x, tr.y, h)), 0.14)
            h += 3.0
    me = bpy.data.meshes.new(name + " scaffolding")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(materials[4])
    sc = bpy.data.objects.new(name + " scaffolding", me)
    col.objects.link(sc)
    return col


def setup_scene():
    sc = bpy.context.scene
    if "Tracks" not in bpy.data.collections:
        sc.collection.children.link(bpy.data.collections.new("Tracks"))
    stage = bpy.data.collections.get("Stage")
    if stage is None:
        stage = bpy.data.collections.new("Stage")
        sc.collection.children.link(stage)
    for ob in list(stage.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for name in ("Cube", "Light", "Camera"):
        ob = bpy.data.objects.get(name)
        if ob:
            ob.hide_render = True
            ob.hide_set(True)
    me = bpy.data.meshes.get("Stage ground") or bpy.data.meshes.new("Stage ground")
    if not me.polygons:
        s = 1500
        me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
        me.materials.append(material("Stage grass", (0.12, 0.25, 0.07), 0.9))
    stage.objects.link(bpy.data.objects.new("Stage ground", me))
    sun = bpy.data.lights.get("Stage sun") or bpy.data.lights.new("Stage sun", 'SUN')
    sun.energy = 4.0
    sun.angle = math.radians(2)
    ob = bpy.data.objects.new("Stage sun", sun)
    ob.rotation_euler = (math.radians(45), 0, math.radians(-35))
    stage.objects.link(ob)
    cam = bpy.data.cameras.get("Stage camera") or bpy.data.cameras.new("Stage camera")
    cam.clip_end = 4000
    camob = bpy.data.objects.new("Stage camera", cam)
    stage.objects.link(camob)
    sc.camera = camob
    world = sc.world or bpy.data.worlds.new("World")
    sc.world = world
    try:
        world.use_nodes = True
    except Exception:
        pass
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.5, 0.64, 0.85, 1)
    bg.inputs["Strength"].default_value = 1.0
    r = sc.render
    r.engine = 'BLENDER_EEVEE'
    r.resolution_x, r.resolution_y = 1600, 900
    r.image_settings.file_format = 'JPEG'
    r.image_settings.quality = 88
    try:
        sc.eevee.taa_render_samples = 24
    except Exception:
        pass
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Medium High Contrast'
    return camob


def build_all(json_path):
    with open(json_path, encoding="utf-8") as fh:
        tracks = json.load(fh)
    setup_scene()
    materials = mats()
    for t in tracks:
        build_track(t, materials)
    return tracks


def show_only(name):
    for lc in bpy.context.view_layer.layer_collection.children["Tracks"].children:
        lc.exclude = lc.name != name


def look_at(ob, target):
    d = Vector(target) - ob.location
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


def biggest_drop(t):
    """Where the deck falls furthest within 120 m: (sample index at the crest, metres fallen)."""
    S = t["samples"]
    n = len(S)
    best, at = 0.0, 0
    for i in range(n):
        if S[i][7]:
            continue
        low = min(S[(i + k) % n][1] for k in range(0, 60))
        if S[i][1] - low > best + 0.01:
            best, at = S[i][1] - low, i
    # back up to the crest itself
    while S[at - 1][1] >= S[at][1] and not S[at - 1][7]:
        at -= 1
    return at, best


def shoot(path, loc, target, lens):
    cam = bpy.context.scene.camera
    cam.data.lens = lens
    cam.location = Vector(loc)
    look_at(cam, target)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def render_track(t, out_dir, views=None):
    """Views of one track: side on, beside its biggest drop, and riding over the top of it."""
    show_only(t["name"])
    S = t["samples"]
    n = len(S)
    pts = [B(s[0], s[1], s[2]) for s in S]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), 0))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    mid = (lo + hi) / 2
    slug = t["name"].lower().replace(" ", "_")
    out = []
    # Side on, low down, across the track's long way, so the climbs and drops read.
    if hi.x - lo.x >= hi.y - lo.y:
        side, span = Vector((0.35, -1, 0)).normalized(), hi.x - lo.x
    else:
        side, span = Vector((-1, -0.35, 0)).normalized(), hi.y - lo.y
    if views is None or "side" in views:
        out.append(shoot(os.path.join(out_dir, slug + "_side.jpg"), mid + side * span * 0.95 + Vector((0, 0, span * 0.16)),
                         mid + Vector((0, 0, 8)), 38))
    # Beside the biggest drop, at the foot of it.
    i, fall = biggest_drop(t)
    j = min(range(i, i + 60), key=lambda k: S[k % n][1])
    m = (i + j) // 2
    pm = pts[m % n]
    r = B(*S[m % n][3:6])
    r.z = 0
    r.normalize()

    # Stand off to whichever side has no other part of the track in the way, looking at the drop.
    def clearance(p):
        return min((Vector((q.x - p.x, q.y - p.y)).length for q in pts), default=0.0)

    spots = [pm + side * r * 60 for side in (1, -1)]
    spot = max(spots, key=lambda p: min(clearance(pm.lerp(p, f)) for f in (0.35, 0.65, 1.0)))
    if views is None or "drop" in views:
        out.append(shoot(os.path.join(out_dir, slug + "_drop.jpg"), Vector((spot.x, spot.y, 6)),
                         pm + Vector((0, 0, fall * 0.2)), 26))
    # Riding: over the crest and tipping into the plunge, looking down it.
    p = pts[(i + 12) % n]
    ahead = pts[(i + 40) % n]
    if views is None or "ride" in views:
        out.append(shoot(os.path.join(out_dir, slug + "_ride.jpg"), p + Vector((0, 0, 2.2)), ahead + Vector((0, 0, -1.0)), 20))
    return out


def contact_sheets(out_dir, order):
    """all_side.jpg, all_drop.jpg and all_ride.jpg: every track's view of each kind, 3 across."""
    import numpy as np
    w, h, cols = 533, 300, 3
    rows = (len(order) + cols - 1) // cols
    out = []
    for view in ("side", "drop", "ride"):
        sheet = np.zeros((rows * h, cols * w, 4), dtype=np.float32)
        sheet[..., 3] = 1
        for k, name in enumerate(order):
            img = bpy.data.images.load(os.path.join(out_dir, f"{name.lower().replace(' ', '_')}_{view}.jpg"))
            img.scale(w, h)
            px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
            r, c = k // cols, k % cols
            sheet[(rows - 1 - r) * h:(rows - r) * h, c * w:(c + 1) * w] = px   # image rows start at the bottom
            bpy.data.images.remove(img)
        im = bpy.data.images.new("sheet_" + view, cols * w, rows * h)
        im.pixels = sheet.ravel()
        im.filepath_raw = os.path.join(out_dir, f"all_{view}.jpg")
        im.file_format = 'JPEG'
        im.save()
        bpy.data.images.remove(im)
        out.append(os.path.join(out_dir, f"all_{view}.jpg"))
    return out


def render_all(out_dir, names=None, views=None):
    """Renders the tracks already built in the scene (see build_all)."""
    os.makedirs(out_dir, exist_ok=True)
    tracks = [c.name for c in bpy.data.collections["Tracks"].children]
    with open(os.path.join(os.path.dirname(out_dir), "tracks.json"), encoding="utf-8") as fh:
        data = {t["name"]: t for t in json.load(fh)}
    out = []
    for name in tracks:
        if names and name not in names:
            continue
        out += render_track(data[name], out_dir, views)
    for lc in bpy.context.view_layer.layer_collection.children["Tracks"].children:
        lc.exclude = False
    return out
