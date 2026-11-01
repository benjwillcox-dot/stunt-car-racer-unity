# Builds the stunt car (scheme-neutral geometry, one "role" per face).
# Axes: Blender -Y is the car's front, +Z up, 1 unit = 1 m, origin on the ground
# midway between the axles. Run from Blender with BUILD_DIR set to this folder.
import bpy
import bmesh
import math
import sys
import importlib
from mathutils import Vector, Matrix, Euler

if BUILD_DIR not in sys.path:  # noqa: F821  (injected by the runner)
    sys.path.insert(0, BUILD_DIR)  # noqa: F821
import scr_lib as L
importlib.reload(L)

rad = math.radians
T = Matrix.Translation

# ------------------------------------------------------------------ layout
FRONT_AXLE_Y, FRONT_TRACK = -1.35, 0.80
REAR_AXLE_Y, REAR_TRACK = 1.25, 0.82
FRONT_WHEEL = dict(R=0.34, W=0.26, Rr=0.22, segs=26)
REAR_WHEEL = dict(R=0.42, W=0.40, Rr=0.25, segs=30)
ENGINE_Y = -0.60
EXHAUST_X, EXHAUST_Z, MERGE_Y = 0.56, 0.32, 0.0  # side-pipe axis and where the headers merge


def NOSE(y):
    """Compress everything ahead of the front axle (shorter, chunkier nose)."""
    return y if y >= FRONT_AXLE_Y else FRONT_AXLE_Y + (y - FRONT_AXLE_Y) * 0.8


# ------------------------------------------------------------------ body shell
def body_half(w, zb, zt, kind="closed", crown=0.016, floor=0.24):
    """Half cross-section (x >= 0) as [(x, z), role-of-next-segment]."""
    h = zt - zb
    ch, chx = min(0.07, 0.22 * h), min(0.07, 0.18 * w)
    sh = min(0.08, 0.25 * h)
    z2, z3 = zb + ch, zt - sh
    ps0, ps1 = z2 + 0.70 * (z3 - z2), z2 + 0.78 * (z3 - z2)
    k = math.sqrt(w / 0.48)
    s_in, s_out = 0.05 * k, 0.17 * k
    rx = 0.08

    def top(x):
        return zt + crown * (1 - (x / w) ** 2)

    pts = [((0, zb), "Carbon"), ((w - chx, zb), "Carbon"), ((w, z2), "PaintPrimary"),
           ((w, ps0), "PaintSecondary"), ((w, ps1), "PaintPrimary")]
    for a in (0, 30, 60):
        x = w - rx + rx * math.cos(rad(a))
        pts.append(((x, z3 + (top(x) - z3) * math.sin(rad(a))), "PaintPrimary"))
    pts.append(((w - rx, top(w - rx)), "PaintPrimary"))
    if kind == "closed":
        pts += [((s_out, top(s_out)), "PaintSecondary"), ((s_in, top(s_in)), "PaintPrimary"),
                ((0, top(0)), None)]
    else:  # cockpit tub: rim, inner wall, floor
        xi = w - rx - 0.045
        pts += [((w - rx - 0.03, top(w - rx) - 0.012), "Interior"),
                ((xi, top(w - rx) - 0.05), "Interior"), ((xi, floor + 0.04), "Interior"),
                ((xi - 0.04, floor), "Interior"), ((0, floor), None)]
    return pts


def body_loft(mb, sections, kind, cap0_role=None, cap1_role=None):
    rings, roles = [], None
    for (y, w, zb, zt) in sections:
        half = body_half(w, zb, zt, kind)
        ring, seg_map = L.mirror_ring([p for p, _ in half])
        rings.append([(x, y, z) for (x, z) in ring])
        roles = [half[s][1] for s in seg_map]
    verts, faces, seg = L.loft(rings, closed=True, cap0=cap0_role is not None, cap1=cap1_role is not None)
    face_roles = [roles[s] if s >= 0 else (cap0_role if s == -1 else cap1_role) for s in seg]
    mb.add(verts, faces, face_roles)


def build_shell(mb):
    front = [(-2.14, 0.27, 0.215, 0.345), (-2.11, 0.315, 0.195, 0.375), (-2.04, 0.345, 0.18, 0.405),
             (-1.85, 0.375, 0.172, 0.445), (-1.60, 0.40, 0.17, 0.49), (-1.35, 0.42, 0.17, 0.53),
             (-1.05, 0.44, 0.17, 0.57), (-0.70, 0.46, 0.175, 0.61), (-0.35, 0.473, 0.178, 0.64),
             (0.00, 0.48, 0.18, 0.665)]
    cockpit = [(0.00, 0.48, 0.18, 0.665), (0.43, 0.48, 0.185, 0.668), (0.86, 0.48, 0.19, 0.67)]
    rear = [(0.86, 0.48, 0.19, 0.67), (1.10, 0.48, 0.195, 0.665), (1.40, 0.475, 0.205, 0.65),
            (1.65, 0.465, 0.215, 0.63), (1.85, 0.45, 0.228, 0.605), (1.97, 0.43, 0.24, 0.585),
            (2.03, 0.40, 0.258, 0.56), (2.05, 0.37, 0.275, 0.54)]
    front = [(NOSE(y), w, zb, zt) for (y, w, zb, zt) in front]
    body_loft(mb, front, "closed", cap0_role="PaintPrimary", cap1_role="Interior")
    body_loft(mb, cockpit, "cockpit")
    body_loft(mb, rear, "closed", cap0_role="Interior", cap1_role="Carbon")
    mb.weld_all(1e-4)

    # headrest fairing behind the driver
    rings = []
    for (y, hw, top) in [(0.875, 0.15, 0.985), (0.95, 0.165, 0.975), (1.15, 0.16, 0.92),
                         (1.40, 0.135, 0.82), (1.62, 0.10, 0.72), (1.78, 0.065, 0.655)]:
        rx2, rz = 0.6 * hw, 0.09
        half = [(0, 0.60), (hw, 0.60), (hw, top - rz)]
        for a in (30, 60):
            half.append((hw - rx2 + rx2 * math.cos(rad(a)), top - rz + rz * math.sin(rad(a))))
        half += [(hw - rx2, top), (0, top + 0.005)]
        ring, _ = L.mirror_ring(half)
        rings.append([(x, y, z) for (x, z) in ring])
    v, f, seg = L.loft(rings, cap0=True, cap1=True)
    mb.add(v, f, ["Seat" if s == -1 else "PaintPrimary" for s in seg])

    # nose: grille and LED strips
    v, f = L.quad((0, NOSE(-2.1415), 0.262), (0, -1, 0), 0.30, 0.055)
    mb.add(v, f, "Grille")
    for s in (1, -1):
        mb.add_bm(L.box((0.11, 0.008, 0.02), (s * 0.155, NOSE(-2.142), 0.318)), "LightFront")
    # tail LED bar
    mb.add_bm(L.box((0.50, 0.012, 0.028), (0, 2.05, 0.47), bevel=0.004), "LightRear")

    # front splitter + struts
    v, f = L.extrude_poly([(-0.43, NOSE(-2.25)), (0.43, NOSE(-2.25)), (0.40, NOSE(-1.84)), (-0.40, NOSE(-1.84))], 'Z', 0.150, 0.172)
    mb.add(v, f, "Carbon", recalc=True)
    for s in (1, -1):
        mb.add_bm(L.box((0.03, 0.06, 0.06), (s * 0.22, NOSE(-2.10), 0.20)), "Carbon")
    # rear diffuser plate + fins
    v, f = L.extrude_poly([(1.62, 0.192), (2.07, 0.255), (2.07, 0.27), (1.62, 0.207)], 'X', -0.36, 0.36)
    mb.add(v, f, "Carbon", recalc=True)
    for x in (-0.22, 0.0, 0.22):
        v, f = L.extrude_poly([(1.64, 0.20), (2.06, 0.262), (2.06, 0.30), (1.64, 0.30)], 'X', x - 0.006, x + 0.006)
        mb.add(v, f, "Carbon", recalc=True)

    # fuel filler
    mb.add_bm(L.cyl(0.045, 0.02, 12, center=(0.27, 1.42, 0.65)), "Chrome")

    # side number roundels
    for s in (1, -1):
        v, f = L.disc((s * 0.4835, 0.40, 0.475), (s, 0, 0), 0.105, 24)
        mb.add(v, f, "DecalBase")
        add_number(mb, "7", s, (s * 0.486, 0.40, 0.475), 0.15)


def add_number(mb, text, side, center, size):
    cu = bpy.data.curves.new("tmp_num", 'FONT')
    cu.body = text
    cu.size = size
    cu.align_x, cu.align_y = 'CENTER', 'CENTER'
    cu.resolution_u = 2
    ob = bpy.data.objects.new("tmp_num", cu)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob)
    bpy.data.curves.remove(cu)
    # text X -> along body, text Y -> up, text normal -> outward
    if side > 0:
        R = Matrix(((0, 0, 1), (1, 0, 0), (0, 1, 0)))
    else:
        R = Matrix(((0, 0, -1), (-1, 0, 0), (0, 1, 0)))
    M = T(center) @ R.to_4x4()
    verts = [M @ v.co for v in me.vertices]
    faces = [tuple(p.vertices) for p in me.polygons]
    bpy.data.meshes.remove(me)
    mb.add(verts, faces, "DecalText")


# ------------------------------------------------------------------ chrome push bar, cage, chassis
def push_bar(mb, yb, ym, mount_x, upright_z0, strut_y=None):
    """Chrome tubular bumper: a main bar whose ends return into the body at y = ym, two uprights
    and a lower rail at y = yb. strut_y adds short struts from the uprights back into the body."""
    path = L.fillet([(mount_x, ym, 0.38), (0.47, yb, 0.38), (-0.47, yb, 0.38), (-mount_x, ym, 0.38)], 0.10, 3)
    v, f = L.tube(path, 0.022, 8)
    mb.add(v, f, "Chrome")
    for s in (1, -1):
        v, f = L.tube([(s * 0.22, yb, upright_z0), (s * 0.22, yb, 0.40)], 0.02, 8)
        mb.add(v, f, "Chrome")
        if strut_y is not None:
            v, f = L.tube([(s * 0.22, yb, 0.31), (s * 0.22, strut_y, 0.31)], 0.016, 8, cap0=False, cap1=False)
            mb.add(v, f, "Chrome")
    # rail ends are buried in the uprights
    v, f = L.tube([(0.22, yb, 0.24), (-0.22, yb, 0.24)], 0.018, 8, cap0=False, cap1=False)
    mb.add(v, f, "Chrome")


def build_pushbar(mb):
    push_bar(mb, NOSE(-2.245), NOSE(-1.98), 0.30, 0.165)    # front: uprights stand on the splitter
    push_bar(mb, 2.14, 1.93, 0.34, 0.20, strut_y=2.03)      # rear: struts into the tail panel


def build_cage(mb):
    R, S = 0.022, 8
    hoop = L.fillet([(0.43, 0.89, 0.62), (0.41, 0.89, 1.40), (-0.41, 0.89, 1.40), (-0.43, 0.89, 0.62)], 0.14, 4)
    v, f = L.tube(hoop, R, S)
    mb.add(v, f, "Cage")
    corner = {1: hoop[1 + 2], -1: hoop[1 + 5 + 2]}  # bezier mid-points of the two top corners
    fronts = {}
    for s in (1, -1):
        side = L.fillet([(s * 0.43, 0.03, 0.62), (s * 0.39, 0.24, 1.30), corner[s]], 0.12, 4)
        fronts[s] = side[1 + 2]
        v, f = L.tube(side, R, S)
        mb.add(v, f, "Cage")
        # door bar, bowed outward
        door = L.spline([(s * 0.418, 0.092, 0.82), (s * 0.465, 0.49, 0.80), (s * 0.425, 0.89, 0.82)], 2)
        v, f = L.tube(door, R, S)
        mb.add(v, f, "Cage")
        # rear brace down to the deck
        v, f = L.tube([corner[s] + Vector((0, 0.01, 0)), (s * 0.28, 1.62, 0.60)], R, S)
        mb.add(v, f, "Cage")
    v, f = L.tube([fronts[1], fronts[-1]], R, S)
    mb.add(v, f, "Cage")


def coilover(mb, p0, p1, turns=3.0, coil_r=0.034):
    v, f = L.tube([p0, p1], 0.017, 8)
    mb.add(v, f, "Metal")
    a = Vector(p0).lerp(Vector(p1), 0.12)
    b = Vector(p0).lerp(Vector(p1), 0.85)
    v, f = L.tube(L.helix(a, b, coil_r, turns, 5), 0.007, 4)
    mb.add(v, f, "Accent")


def build_chassis(mb):
    y = FRONT_AXLE_Y
    for s in (1, -1):
        up, lo = Vector((s * 0.655, y, 0.45)), Vector((s * 0.665, y, 0.23))
        for p in ((s * 0.38, y - 0.21, 0.47), (s * 0.40, y + 0.21, 0.47)):
            v, f = L.tube([p, up], 0.016, 6)
            mb.add(v, f, "Metal")
        for p in ((s * 0.38, y - 0.21, 0.24), (s * 0.40, y + 0.21, 0.24)):
            v, f = L.tube([p, lo], 0.016, 6)
            mb.add(v, f, "Metal")
        mb.add_bm(L.box((0.05, 0.08, 0.30), (s * 0.655, y, 0.34), bevel=0.01), "Metal")
        v, f = L.tube([(s * 0.38, y - 0.12, 0.33), (s * 0.645, y - 0.12, 0.34)], 0.011, 6)
        mb.add(v, f, "Metal")
        coilover(mb, (s * 0.60, y, 0.255), (s * 0.395, y, 0.52))

    y = REAR_AXLE_Y
    for s in (1, -1):
        v, f = L.tube([(s * 0.30, y, 0.42), (s * 0.70, y, 0.42)], 0.045, 10)
        mb.add(v, f, "Metal")
        mb.add_bm(L.cyl(0.07, 0.03, 12, center=(s * 0.55, y, 0.42), axis='X'), "Metal")
        v, f = L.tube([(s * 0.58, y, 0.37), (s * 0.47, 0.75, 0.30)], 0.016, 6)
        mb.add(v, f, "Metal")
        v, f = L.tube([(s * 0.58, y, 0.47), (s * 0.47, 0.80, 0.52)], 0.016, 6)
        mb.add(v, f, "Metal")
        coilover(mb, (s * 0.60, 1.16, 0.43), (s * 0.43, 1.00, 0.64), turns=3.5)


def build_sidepipes(mb):
    """Flanged 4-into-1 merge collector, side pipe, and a tip turned out away from the body."""
    for s in (1, -1):
        x0, z0 = s * EXHAUST_X, EXHAUST_Z
        # flange the header bundle plugs into
        mb.add_bm(L.cyl(0.07, 0.022, 12, center=(x0, MERGE_Y, z0), axis='Y'), "Chrome")
        # collector cone -> straight run -> turn-out bend (dx is measured outward from the pipe axis)
        turn = L.spline([(0, 0.44), (0.006, 0.52), (0.04, 0.60), (0.105, 0.655), (0.15, 0.675)], 2)
        path = [Vector((x0, MERGE_Y + 0.005, z0)), Vector((x0, MERGE_Y + 0.17, z0))]
        path += [Vector((s * (EXHAUST_X + p.x), p.y, z0)) for p in turn]
        radii = [0.062, 0.05] + [0.05] * len(turn)
        # flared chrome tip continuing along the final direction
        d = (path[-1] - path[-2]).normalized()
        end = path[-1] + d * 0.075
        path += [path[-1] + d * 0.004, end]
        radii += [0.058, 0.058]
        v, f = L.tube(path, radii, 10)
        mb.add(v, f, "Chrome")
        v, f = L.disc(end + d * 0.0008, d, 0.047, 10)
        mb.add(v, f, "Grille")
        # hangers to the body side
        for y in (MERGE_Y + 0.10, 0.36):
            mb.add_bm(L.box((0.055, 0.022, 0.018), (s * 0.505, y, z0)), "Metal")


def build_wing(mb):
    c, aoa = 0.38, rad(12)
    foil = [(0, 0), (0.02, 0.012), (0.10, 0.02), (0.30, 0.024), (0.60, 0.02), (0.90, 0.008), (1.0, 0.0),
            (0.90, -0.012), (0.60, -0.035), (0.30, -0.045), (0.10, -0.035), (0.02, -0.018)]

    def wing_ring(x, y0, z0, chord, ang, scale_v=1.0):
        out = []
        for (u, vv) in foil:
            u2, v2 = u * chord, vv * chord / 0.38 * scale_v
            out.append((x, y0 + u2 * math.cos(ang) - v2 * math.sin(ang), z0 + u2 * math.sin(ang) + v2 * math.cos(ang)))
        return out

    span = 0.625
    v, f, _ = L.loft([wing_ring(-span, 1.70, 1.0, c, aoa), wing_ring(span, 1.70, 1.0, c, aoa)], cap0=True, cap1=True)
    mb.add(v, f, "Carbon", recalc=True)
    fy = 1.70 + 0.34 * math.cos(aoa)
    fz = 1.0 + 0.34 * math.sin(aoa) + 0.05
    v, f, _ = L.loft([wing_ring(-span, fy, fz, 0.13, rad(32)), wing_ring(span, fy, fz, 0.13, rad(32))], cap0=True, cap1=True)
    mb.add(v, f, "PaintPrimary", recalc=True)
    for s in (1, -1):
        v, f = L.extrude_poly([(1.64, 0.90), (2.13, 0.95), (2.13, 1.22), (1.70, 1.12)], 'X', s * 0.624, s * 0.638)
        mb.add(v, f, "PaintSecondary", recalc=True)
        v, f = L.extrude_poly([(1.66, 0.60), (1.84, 0.60), (1.93, 1.0), (1.76, 1.0)], 'X', s * 0.24 - 0.008, s * 0.24 + 0.008)
        mb.add(v, f, "Carbon", recalc=True)


def build_seat(mb):
    mb.add_bm(L.box((0.40, 0.42, 0.07), (0, 0.50, 0.28)), "Seat")
    rot = Euler((rad(-10.4), 0, 0))
    mb.add_bm(L.box((0.42, 0.07, 0.64), (0, 0.745, 0.60), rot=rot, bevel=0.02), "Seat")
    for s in (1, -1):
        mb.add_bm(L.box((0.05, 0.16, 0.48), (s * 0.215, 0.70, 0.56), rot=rot), "Seat")


# ------------------------------------------------------------------ engine
def build_engine(mb):
    EY = ENGINE_Y
    mb.add_bm(L.box((0.42, 0.80, 0.30), (0, EY, 0.58), bevel=0.02), "EngineBlock")
    mb.add_bm(L.box((0.26, 0.05, 0.22), (0, -1.02, 0.62), bevel=0.012), "Chrome")
    for s in (1, -1):
        M = T((s * 0.15, EY, 0.77)) @ Matrix.Rotation(s * rad(28), 4, 'Y')
        mb.add_bm(L.box((0.15, 0.74, 0.10), bevel=0.012), "EngineBlock", matrix=M)
        mb.add_bm(L.box((0.125, 0.68, 0.05), (0, 0, 0.07), bevel=0.012), "EngineAccent", matrix=M)
        for fx in (-0.035, 0.0, 0.035):
            mb.add_bm(L.box((0.008, 0.56, 0.018), (fx, 0, 0.102)), "EngineBlock", matrix=M)
        mb.add_bm(L.cyl(0.018, 0.05, 8, center=(0, 0.27, 0.11)), "Chrome", matrix=M)
    mb.add_bm(L.box((0.20, 0.62, 0.10), (0, EY, 0.83), bevel=0.015), "EngineBlock")
    # supercharger, ribs, snout, pulleys, belt
    mb.add_bm(L.box((0.30, 0.56, 0.17), (0, EY - 0.02, 0.955), bevel=0.045, segs=2), "EngineAccent")
    for s in (1, -1):
        for z in (0.915, 0.955, 0.995):
            mb.add_bm(L.box((0.012, 0.44, 0.012), (s * 0.152, EY - 0.02, z)), "EngineBlock")
    mb.add_bm(L.cyl(0.03, 0.16, 10, center=(0, -0.98, 0.95), axis='Y'), "Chrome")
    mb.add_bm(L.cyl(0.075, 0.045, 12, center=(0, -1.07, 0.95), axis='Y'), "Chrome")
    mb.add_bm(L.cyl(0.06, 0.045, 12, center=(0, -1.07, 0.66), axis='Y'), "Chrome")
    pts = []
    for (cz, r) in ((0.95, 0.083), (0.66, 0.068)):
        pts += [(r * math.cos(2 * math.pi * k / 12), cz + r * math.sin(2 * math.pi * k / 12)) for k in range(12)]
    hull = convex_hull(pts)
    v, f = L.tube([(x, -1.07, z) for (x, z) in hull], 0.016, 4, closed=True, twist=math.pi / 4)
    mb.add(v, f, "Tire")
    # bug-catcher scoop: folded loft = recessed grille -> inner wall -> chrome lip -> body
    def scoop_ring(y, hw, zb, zt, c=0.03):
        pts = [(0, zb), (hw - c, zb), (hw, zb + c), (hw, zt - c), (hw - c, zt), (0, zt),
               (-hw + c, zt), (-hw, zt - c), (-hw, zb + c), (-hw + c, zb)]
        return [(x, y, z) for (x, z) in pts]
    d = 0.016
    rings = [scoop_ring(-0.75, 0.12 - d, 1.035 + d, 1.20 - d), scoop_ring(-0.815, 0.12 - d, 1.035 + d, 1.20 - d),
             scoop_ring(-0.815, 0.12 + d, 1.035 - d, 1.20 + d), scoop_ring(-0.795, 0.12 + d, 1.035 - d, 1.20 + d),
             scoop_ring(-0.79, 0.12, 1.035, 1.20), scoop_ring(-0.62, 0.118, 1.035, 1.16), scoop_ring(-0.47, 0.115, 1.035, 1.10)]
    v, f, seg = L.loft(rings, cap0=True, cap1=True)
    n = len(rings[0])
    roles = []
    for i, sg in enumerate(seg):
        span = i // n if sg >= 0 else None
        roles.append("Grille" if sg == -1 else "PaintSecondary" if sg == -2 else
                     "Grille" if span == 0 else "Chrome" if span in (1, 2, 3) else "PaintSecondary")
    mb.add(v, f, roles)
    # distributor
    mb.add_bm(L.cyl(0.04, 0.07, 8, center=(0, -0.20, 0.86)), "EngineBlock")
    mb.add_bm(L.cyl(0.045, 0.035, 8, r2=0.035, center=(0, -0.20, 0.91), bevel=0.004), "Accent")
    # headers: each pipe drops down the body side, bends rearward into its lane of a 2x2 bundle
    # and runs straight into the merge flange. The front pipe takes a bottom lane so the later
    # pipes can drop in above / beside it without crossing.
    lanes = [(0.024, -0.024), (-0.024, -0.024), (0.024, 0.024), (-0.024, 0.024)]
    for s in (1, -1):
        for i, dy in enumerate((-0.27, -0.09, 0.09, 0.27)):
            y = EY + dy + (0.02 if s < 0 else 0.0)
            ox, oz = lanes[i]
            lx, lz = EXHAUST_X + ox, EXHAUST_Z + oz
            # out of the head, over the shoulder, straight down the side, round the bottom bend, back
            poly = [(0.215, y, 0.738), (0.36, y + 0.005, 0.715), (lx, y + 0.03, 0.62),
                    (lx, y + 0.09, lz), (lx, MERGE_Y - 0.04, lz)]
            path = L.fillet([(s * px, py, pz) for (px, py, pz) in poly], [0.05, 0.06, 0.075], [1, 3, 5])
            path.append(Vector((s * (EXHAUST_X + ox * 0.55), MERGE_Y + 0.06, EXHAUST_Z + oz * 0.55)))
            v, f = L.tube(path, 0.021, 8, cap0=False, cap1=False)
            mb.add(v, f, "Chrome")


def convex_hull(points):
    pts = sorted(set(points))

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


# ------------------------------------------------------------------ wheels
def build_wheel(R, W, Rr, segs, spokes=6):
    mb = L.MB()
    hw = W / 2
    Rt = R - 0.012  # tread groove depth; blocks extrude back out to R
    mid = Rr + 0.45 * (R - Rr)
    prof = [((Rr + 0.004, -hw + 0.022), "Tire"), ((R - 0.035, -hw - 0.002), "Tire"),
            ((Rt - 0.006, -hw + 0.022), "Tire"), ((Rt, -hw + 0.05), "Tire"), ((Rt, 0.0), "Tire"),
            ((Rt, hw - 0.05), "Tire"), ((Rt - 0.006, hw - 0.022), "Tire"), ((R - 0.035, hw - 0.004), "Tire"),
            ((R - 0.055, hw), "Accent"), ((R - 0.07, hw + 0.002), "Tire"), ((mid, hw + 0.004), "Tire"),
            ((Rr + 0.004, hw - 0.022), None)]
    v, f, seg_i, ring_k = L.revolve([p for p, _ in prof], segs)
    faces = mb.add(v, f, [prof[i][1] for i in seg_i])
    blocks = [fc for fc, i, k in zip(faces, seg_i, ring_k) if fc and i in (3, 4) and (k + i) % 2 == 0]
    res = bmesh.ops.extrude_discrete_faces(mb.bm, faces=blocks)
    for fc in res["faces"]:
        for vt in fc.verts:
            radial = Vector((0, vt.co.y, vt.co.z))
            vt.co += radial.normalized() * (R - Rt)

    # rim barrel with a polished deep-dish lip
    barrel = [((Rr - 0.03, hw - 0.055), "RimLip"), ((Rr - 0.012, hw - 0.012), "RimLip"), ((Rr + 0.018, hw - 0.004), "RimLip"),
              ((Rr + 0.006, hw - 0.03), "Rim"), ((Rr + 0.012, -hw + 0.012), "Rim"), ((Rr - 0.02, -hw + 0.018), "Rim")]
    v, f, seg_i, _ = L.revolve([p for p, _ in barrel], segs, closed_profile=True)
    mb.add(v, f, [barrel[i][1] for i in seg_i], recalc=True)

    # spokes
    a_hub = hw - 0.085
    secs = [(0.055, a_hub + 0.01, 0.05, 0.03), ((Rr - 0.03) * 0.6, hw - 0.07, 0.04, 0.025), (Rr - 0.026, hw - 0.058, 0.032, 0.022)]
    for n in range(spokes):
        t = 2 * math.pi * n / spokes + math.pi / 2
        rdir, tdir = Vector((0, math.cos(t), math.sin(t))), Vector((0, -math.sin(t), math.cos(t)))
        rings = []
        for (r, a, w, th) in secs:
            c = Vector((a, 0, 0)) + rdir * r
            rings.append([c + tdir * (w / 2) + Vector((th / 2, 0, 0)), c - tdir * (w / 2) + Vector((th / 2, 0, 0)),
                          c - tdir * (w / 2) - Vector((th / 2, 0, 0)), c + tdir * (w / 2) - Vector((th / 2, 0, 0))])
        v, f, _ = L.loft(rings)  # open ends: buried in the hub and the barrel
        mid = Vector((secs[1][1], 0, 0)) + rdir * secs[1][0]
        mb.add(v, L.orient_outward(v, f, mid), "Rim")
    mb.add_bm(L.cyl(0.075, 0.035, 12, center=(a_hub - 0.0025, 0, 0), axis='X'), "Rim")
    mb.add_bm(L.cyl(0.035, 0.03, 10, r2=0.026, center=(hw - 0.06, 0, 0), axis='X'), "RimLip")
    for n in range(5):
        t = 2 * math.pi * n / 5
        c = Vector((0, 0.052 * math.cos(t), 0.052 * math.sin(t)))
        v, f = L.tube([c + Vector((hw - 0.075, 0, 0)), c + Vector((hw - 0.055, 0, 0))], 0.011, 6, cap0=False)
        mb.add(v, f, "Chrome")  # lug nut, back end sits inside the hub
    # brake disc + hat
    d0, d1 = -hw + 0.075, -hw + 0.10
    disc = [(Rr - 0.04, d0), (Rr - 0.04, d1), (0.09, d1)]  # rim edge + outer face only
    v, f, _, _ = L.revolve(disc, 16)
    mb.add(v, f, "BrakeDisc")
    hat_len = (a_hub - 0.02) - d1
    mb.add_bm(L.cyl(0.09, hat_len, 12, center=(d1 + hat_len / 2, 0, 0), axis='X', caps=False), "Metal")
    return mb


def build_caliper(R, W, Rr, a0, a1):
    mb = L.MB()
    hw = W / 2
    prof = [(Rr - 0.068, -hw + 0.062), (Rr - 0.022, -hw + 0.062), (Rr - 0.022, -hw + 0.113), (Rr - 0.068, -hw + 0.113)]
    v, f, _, _ = L.revolve(prof, 6, a0=a0, a1=a1, closed_profile=True)
    n, last = len(prof), 6 * len(prof)
    f += [tuple(range(n)), tuple(range(last, last + n))]
    mb.add(v, f, "Accent", recalc=True)
    return mb


# ------------------------------------------------------------------ driver + steering wheel
HELMET = Vector((0, 0.64, 1.04))
WHEEL_HUB = Vector((0, 0.16, 0.72))
WHEEL_TILT = rad(25)


def build_driver(mb):
    hip, sho = Vector((0, 0.55, 0.34)), Vector((0, 0.64, 0.86))
    rings = []
    for t, ax, ay in [(0, 0.16, 0.11), (0.35, 0.17, 0.115), (0.7, 0.205, 0.12), (0.92, 0.215, 0.115), (1.0, 0.19, 0.10), (1.06, 0.10, 0.07)]:
        c = hip.lerp(sho, t)
        rings.append([c + Vector((ax * math.cos(2 * math.pi * k / 10), ay * math.sin(2 * math.pi * k / 10), 0)) for k in range(10)])
    v, f, _ = L.loft(rings, cap0=True, cap1=True)
    mb.add(v, f, "Suit", recalc=True)
    mb.add_bm(L.cyl(0.055, 0.12, 10, center=(0, 0.64, 0.92)), "Suit")
    for s in (1, -1):
        hand = WHEEL_HUB + Vector((s * 0.15, 0.02, 0.01))
        arm = L.spline([(s * 0.19, 0.64, 0.82), (s * 0.27, 0.42, 0.64), hand + Vector((0, 0.04, 0))], 3)
        n = len(arm)
        v, f = L.tube(arm, [0.052 - 0.012 * i / (n - 1) for i in range(n)], 6)
        mb.add(v, f, "Suit")
        mb.add_bm(L.sphere(0.042, 8, 6, center=hand), "Gloves")
        leg = L.spline([(s * 0.09, 0.50, 0.33), (s * 0.11, 0.14, 0.43), (s * 0.11, -0.06, 0.30)], 3)
        n = len(leg)
        v, f = L.tube(leg, [0.068 - 0.02 * i / (n - 1) for i in range(n)], 6)
        mb.add(v, f, "Suit")

    # helmet with a centre stripe
    bm = L.sphere(0.14, 12, 10, center=HELMET, scale=(1, 1.08, 1))
    for fc in bm.faces:
        c = fc.calc_center_median()
        fc.material_index = 1 if (abs(c.x) < 0.03 and c.z > HELMET.z - 0.02) else 0
    mb.add_bm(bm, {0: "Helmet", 1: "PaintSecondary"})
    # visor
    verts, nu, nv = [], 9, 4
    for j in range(nv):
        el = rad(-18 + 40 * j / (nv - 1))
        for i in range(nu):
            az = rad(-65 + 130 * i / (nu - 1))
            r = 0.1445
            verts.append(HELMET + Vector((r * math.cos(el) * math.sin(az), -r * math.cos(el) * math.cos(az) * 1.08, r * math.sin(el))))
    faces = [(j * nu + i, j * nu + i + 1, (j + 1) * nu + i + 1, (j + 1) * nu + i) for j in range(nv - 1) for i in range(nu - 1)]
    mb.add(verts, L.orient_outward(verts, faces, HELMET), "Visor")


def build_steering_wheel():
    mb = L.MB()
    ring = []
    for k in range(14):
        t = 2 * math.pi * k / 14
        ring.append((0.15 * math.cos(t), 0, max(0.15 * math.sin(t), -0.115)))
    v, f = L.tube(ring, 0.017, 6, closed=True)
    mb.add(v, f, "Carbon")
    for s in (1, -1):
        mb.add_bm(L.box((0.10, 0.02, 0.03), (s * 0.09, 0, 0), bevel=0.005), "Carbon")
    mb.add_bm(L.box((0.03, 0.02, 0.08), (0, 0, -0.07), bevel=0.005), "Carbon")
    mb.add_bm(L.box((0.11, 0.035, 0.08), (0, -0.005, 0), bevel=0.01), "Carbon")
    v, f = L.quad((0, 0.0131, 0.008), (0, 1, 0), 0.07, 0.035)
    mb.add(v, f, "Screen")
    mb.add_bm(L.cyl(0.022, 0.20, 8, center=(0, -0.11, 0), axis='Y'), "Metal")
    return mb


# ------------------------------------------------------------------ assembly
def build(prefix="Y_", root_name="StuntCar_Yellow", coll_name="StuntCar_Yellow"):
    col = L.coll(coll_name)
    L.clear_collection(col)
    root = bpy.data.objects.new(root_name, None)
    root.empty_display_type = 'ARROWS'
    root.empty_display_size = 0.6
    col.objects.link(root)
    parts = {}

    mb = L.MB()
    build_shell(mb)
    build_pushbar(mb)
    build_cage(mb)
    build_chassis(mb)
    build_sidepipes(mb)
    build_wing(mb)
    build_seat(mb)
    parts["Body"] = mb.to_object(prefix + "Body", col, 35)

    mb = L.MB()
    build_engine(mb)
    parts["Engine"] = mb.to_object(prefix + "Engine", col, 35, origin=(0, ENGINE_Y, 0.75))

    mb = L.MB()
    build_driver(mb)
    parts["Driver"] = mb.to_object(prefix + "Driver", col, 50, origin=(0, 0.6, 0.6))

    mb = build_steering_wheel()
    sw = mb.to_object(prefix + "SteeringWheel", col, 40)
    sw.location = WHEEL_HUB
    sw.rotation_euler = (WHEEL_TILT, 0, 0)
    parts["SteeringWheel"] = sw

    for tag, y, track, spec in (("F", FRONT_AXLE_Y, FRONT_TRACK, FRONT_WHEEL), ("R", REAR_AXLE_Y, REAR_TRACK, REAR_WHEEL)):
        for side, s in (("L", 1), ("R", -1)):
            mb = build_wheel(spec["R"], spec["W"], spec["Rr"], spec["segs"])
            if s < 0:
                mb.mirror_x()
            w = mb.to_object(prefix + "Wheel_" + tag + side, col, 40)
            w.location = (s * track, y, spec["R"])
            parts["Wheel_" + tag + side] = w
            a0, a1 = (rad(-10), rad(40)) if tag == "F" else (rad(140), rad(190))
            mb = build_caliper(spec["R"], spec["W"], spec["Rr"], a0, a1)
            if s < 0:
                mb.mirror_x()
            c = mb.to_object(prefix + "Caliper_" + tag + side, col, 40)
            c.location = (s * track, y, spec["R"])
            parts["Caliper_" + tag + side] = c

    for ob in parts.values():
        ob.parent = root
    return root, parts
