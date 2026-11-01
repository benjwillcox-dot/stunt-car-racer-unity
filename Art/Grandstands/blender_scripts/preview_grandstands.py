# Preview stadium for the grandstand kit: lays the modules out round a mock track the way the
# game does (a mirrored end, stretched bays, the other end), gives each row of crowd its own
# strip of the texture and each board an advert, and renders a few views into renders/.
# Lives in its own collection and is never exported. Run after build_grandstands.rebuild().
import bpy
import bmesh
import math
import os
import random
from mathutils import Vector, Matrix

BAY = 12.0
CROWD_ROWS = 8
AD_SLOTS = 16


def ad_rect(i):
    inset = 1.0 / 1024
    return ((i % 2) * 0.5 + inset, (i // 2) / 8 + inset, 0.5 - 2 * inset, 1 / 8 - 2 * inset)


class Run:
    """Accumulates the copies of one material's meshes for a whole stand."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def add(self, mesh, matrix, mirror, kind, rng, board_slots):
        src = bmesh.new()
        src.from_mesh(mesh)
        bmesh.ops.transform(src, matrix=matrix, verts=src.verts[:])
        if mirror:
            bmesh.ops.reverse_faces(src, faces=src.faces[:])
        uv_src = src.loops.layers.uv.active
        rows = {}
        boards = {}
        vmap = {}
        for v in src.verts:
            vmap[v] = self.bm.verts.new(v.co)
        for f in src.faces:
            nf = self.bm.faces.new([vmap[v] for v in f.verts])
            nf.smooth = f.smooth
            for l_src, l_dst in zip(f.loops, nf.loops):
                u, v = l_src[uv_src].uv
                if kind == "Crowd":
                    rid = math.floor(v)
                    if rid not in rows:
                        rows[rid] = (rng.randrange(CROWD_ROWS), rng.random())
                    strip, off = rows[rid]
                    l_dst[self.uv].uv = (u + off, (strip + (v - rid)) / CROWD_ROWS)
                elif kind == "Ads":
                    b = math.floor(u)
                    if b not in boards:
                        boards[b] = board_slots()
                    fu = u - b
                    if mirror:
                        fu = 1 - fu
                    x, y, w, h = ad_rect(boards[b])
                    l_dst[self.uv].uv = (x + fu * w, y + v * h)
                else:
                    l_dst[self.uv].uv = (u, v)
        src.free()

    def to_object(self, name, mat, collection):
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        me.materials.append(mat)
        ob = bpy.data.objects.new(name, me)
        collection.objects.link(ob)
        return ob


def place_stand(style, a, b, collection, rng, slots, section=None):
    """A stand of `style` with its front edge from a to b (Blender XY, ground level), facing the
    side of the line on your right as you walk from a to b. Mirrors the game's layout."""
    a, b = Vector((*a, 0)), Vector((*b, 0))
    along = (b - a).normalized()
    length = (b - a).length
    # The module's front faces Blender +Y and its bays run along -X; turn that onto the line.
    theta = math.atan2(-along.y, -along.x)
    base = Matrix.Translation(a) @ Matrix.Rotation(theta, 4, 'Z')
    n = max(1, round((length - 12) / BAY))
    s = (length - 12) / (n * BAY)
    places = [(style + "_End", 6.0, -1.0, True)]
    places += [(style + "_Bay", 6.0 + i * BAY * s, s, False) for i in range(n)]
    places += [(style + "_End", 6.0 + n * BAY * s, 1.0, False)]
    runs = {k: Run() for k in ("Palette", "Crowd", "Ads")}
    for mod, at, sx, mirror in places:
        m = base @ Matrix.Translation((-at, 0, 0)) @ Matrix.Diagonal((sx, 1, 1, 1))
        for kind, run in runs.items():
            ob = bpy.data.objects[mod + "_" + kind]
            run.add(ob.data, m, mirror, kind, rng, slots)
    name = section or style
    mats = {"Palette": bpy.data.materials["Grandstand"], "Crowd": bpy.data.materials["Crowd (preview)"],
            "Ads": bpy.data.materials["Adverts (preview)"]}
    return [run.to_object(name + " " + kind, mats[kind], collection) for kind, run in runs.items()]


def look_at(ob, target):
    d = Vector(target) - ob.location
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


def flat(name, rgb, rough=0.8, emit=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    if emit:
        b.inputs["Emission Color"].default_value = (*emit, 1)
        b.inputs["Emission Strength"].default_value = 3.0
    return m


def box(collection, name, c, size, mat):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts[:])
    bmesh.ops.translate(bm, vec=Vector(c), verts=bm.verts[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    collection.objects.link(ob)
    return ob


def setup(seed=3):
    sc = bpy.context.scene
    c = bpy.data.collections.get("Preview")
    if c is None:
        c = bpy.data.collections.new("Preview")
        sc.collection.children.link(c)
    for ob in list(c.objects):
        me = ob.data
        bpy.data.objects.remove(ob, do_unlink=True)
        if isinstance(me, bpy.types.Mesh) and me.users == 0:
            bpy.data.meshes.remove(me)
    # Keep the kit itself out of the renders; the stadium below is built from copies of it.
    kit = bpy.data.collections.get("Grandstands")
    if kit:
        bpy.context.view_layer.layer_collection.children["Grandstands"].exclude = True

    rng = random.Random(seed)
    counter = [0]

    def slots():
        counter[0] += 1
        return 0 if counter[0] % 7 == 0 else 2 + counter[0] % (AD_SLOTS - 2)

    # A mock deck footprint: 180 x 110 m, with the stands set back from it as in the game.
    x0, x1, y0, y1 = -90.0, 90.0, -55.0, 55.0
    place_stand("Main", (x1, y0 - 10), (x0, y0 - 10), c, rng, slots, "Main stand")
    place_stand("Terrace", (x0, y1 + 12), (x1, y1 + 12), c, rng, slots, "Far stand")
    for k, (za, zb) in enumerate(((y0 + 6, -4.0), (4.0, y1 - 6))):
        place_stand("Scaffold", (x0 - 12, za), (x0 - 12, zb), c, rng, slots, "Left stand %d" % k)
        place_stand("Scaffold", (x1 + 12, -za), (x1 + 12, -zb), c, rng, slots, "Right stand %d" % k)

    grass = flat("Preview grass", (0.16, 0.3, 0.09))
    road = flat("Preview road", (0.06, 0.06, 0.065), 0.6)
    steel = flat("Preview steel", (0.55, 0.57, 0.6), 0.4)
    car = flat("Preview car", (0.9, 0.55, 0.02), 0.3)
    box(c, "Preview ground", (0, 0, -0.05), (1200, 1200, 0.1), grass)
    # An elevated road round the footprint's edge, on scaffold legs.
    h, w = 5.0, 14.0
    for (cx, cy, sx, sy) in ((0, y0 + w / 2, x1 - x0, w), (0, y1 - w / 2, x1 - x0, w),
                             (x0 + w / 2, 0, w, y1 - y0 - 2 * w), (x1 - w / 2, 0, w, y1 - y0 - 2 * w)):
        box(c, "Preview deck", (cx, cy, h), (sx, sy, 0.5), road)
        nx, ny = max(1, int(sx / 12)), max(1, int(sy / 12))
        for i in range(nx + 1):
            for j in range(ny + 1):
                px = cx - sx / 2 + sx * i / nx
                py = cy - sy / 2 + sy * j / ny
                box(c, "Preview leg", (px, py, h / 2), (0.3, 0.3, h), steel)
    box(c, "Preview car", (-30, y0 + 7, h + 0.95), (4.2, 2.0, 1.2), car)

    sun_data = bpy.data.lights.get("PreviewSun") or bpy.data.lights.new("PreviewSun", 'SUN')
    sun_data.energy = 4.0
    sun_data.angle = math.radians(2)
    sun = bpy.data.objects.new("PreviewSun", sun_data)
    sun.rotation_euler = (math.radians(42), 0, math.radians(-30))
    c.objects.link(sun)

    world = sc.world or bpy.data.worlds.new("World")
    sc.world = world
    try:
        world.use_nodes = True
    except Exception:
        pass
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.5, 0.64, 0.85, 1)
    bg.inputs["Strength"].default_value = 1.0

    cam_data = bpy.data.cameras.get("PreviewCam") or bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    cam_data.clip_end = 3000
    c.objects.link(cam)
    sc.camera = cam

    r = sc.render
    r.engine = 'BLENDER_EEVEE'
    r.resolution_x, r.resolution_y = 1600, 900
    r.resolution_percentage = 100
    r.image_settings.file_format = 'JPEG'
    r.image_settings.quality = 88
    try:
        sc.eevee.taa_render_samples = 32
    except Exception:
        pass
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Medium High Contrast'
    return cam


def shoot(path, loc, target, lens=28):
    sc = bpy.context.scene
    cam = sc.camera
    cam.data.lens = lens
    cam.location = Vector(loc)
    look_at(cam, target)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


VIEWS = {
    "aerial": ((150, -190, 115), (0, -5, 0), 30),
    "main_trackside": ((25, -47, 7), (-5, -80, 16), 22),
    "scaffold_trackside": ((-72, -35, 7), (-112, 0, 10), 24),
    "terrace_trackside": ((45, 42, 7), (85, 75, 14), 24),
}


def render_all(out_dir, names=None):
    os.makedirs(out_dir, exist_ok=True)
    out = []
    for name, (loc, target, lens) in VIEWS.items():
        if names and name not in names:
            continue
        out.append(shoot(os.path.join(out_dir, name + ".jpg"), loc, target, lens))
    return out
