# Geometry helpers for the Stunt Car build (Blender 5.x).
# Every generator returns plain vertex / face lists so parts can be merged
# into one MeshBuilder with a "role" (palette slot) per face.
import bpy
import bmesh
import math
from mathutils import Vector, Matrix

# Roles become cells of the colour palette texture at the end of the build.
ROLES = [
    "PaintPrimary", "PaintSecondary", "Carbon", "Interior", "Chrome", "EngineBlock",
    "EngineAccent", "Metal", "Cage", "Rim", "RimLip", "Tire", "Accent", "BrakeDisc",
    "Grille", "LightFront", "LightRear", "Screen", "Suit", "Gloves", "Helmet", "Visor",
    "Seat", "DecalBase", "DecalText",
]

# Temporary viewport colours used while modelling (replaced by palette materials later).
PREVIEW = {
    "PaintPrimary": (1.0, 0.48, 0.0), "PaintSecondary": (0.01, 0.01, 0.01),
    "Carbon": (0.02, 0.02, 0.025), "Interior": (0.03, 0.03, 0.03), "Chrome": (0.8, 0.8, 0.8),
    "EngineBlock": (0.05, 0.05, 0.06), "EngineAccent": (0.7, 0.7, 0.7), "Metal": (0.1, 0.1, 0.12),
    "Cage": (0.01, 0.01, 0.01), "Rim": (0.01, 0.01, 0.01), "RimLip": (0.8, 0.8, 0.8),
    "Tire": (0.01, 0.01, 0.01), "Accent": (1.0, 0.5, 0.0), "BrakeDisc": (0.3, 0.3, 0.3),
    "Grille": (0.005, 0.005, 0.005), "LightFront": (1, 1, 1), "LightRear": (1, 0, 0),
    "Screen": (1.0, 0.5, 0.0), "Suit": (0.01, 0.01, 0.01), "Gloves": (0.01, 0.01, 0.01),
    "Helmet": (1.0, 0.48, 0.0), "Visor": (0.5, 0.3, 0.02), "Seat": (0.02, 0.02, 0.02),
    "DecalBase": (0.9, 0.9, 0.9), "DecalText": (0.01, 0.01, 0.01),
}


def role_mat(role):
    name = "ROLE_" + role
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        c = PREVIEW.get(role, (0.5, 0.5, 0.5))
        m.diffuse_color = (*c, 1.0)
        try:
            m.use_nodes = True
        except Exception:
            pass
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (*c, 1.0)
            metal = role in ("Chrome", "EngineAccent", "RimLip", "BrakeDisc", "Metal")
            bsdf.inputs["Metallic"].default_value = 1.0 if metal else 0.0
            bsdf.inputs["Roughness"].default_value = 0.25 if metal else 0.35
            if role in ("LightFront", "LightRear", "Screen"):
                bsdf.inputs["Emission Color"].default_value = (*c, 1.0)
                bsdf.inputs["Emission Strength"].default_value = 4.0
        m.use_backface_culling = True
    return m


def coll(name, parent=None):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(c)
    return c


def clear_collection(c):
    for ob in list(c.objects):
        data = ob.data
        bpy.data.objects.remove(ob, do_unlink=True)
        if data is not None and data.users == 0:
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
            elif isinstance(data, bpy.types.Curve):
                bpy.data.curves.remove(data)


# ---------------------------------------------------------------- builder
class MB:
    """Accumulates geometry; each face carries a role (material slot)."""

    def __init__(self):
        self.bm = bmesh.new()
        self.roles = []

    def slot(self, role):
        if role not in self.roles:
            self.roles.append(role)
        return self.roles.index(role)

    def add(self, verts, faces, roles, matrix=None, recalc=False, weld=0.0):
        """Returns the new faces aligned with `faces` (None where a face was degenerate)."""
        bm = self.bm
        if matrix is not None:
            verts = [matrix @ Vector(v) for v in verts]
        bvs = [bm.verts.new(Vector(v)) for v in verts]
        new_faces = []
        for i, f in enumerate(faces):
            try:
                face = bm.faces.new([bvs[j] for j in f])
            except ValueError:
                new_faces.append(None)
                continue
            r = roles if isinstance(roles, str) else roles[i]
            face.material_index = self.slot(r)
            new_faces.append(face)
        if weld > 0:
            bmesh.ops.remove_doubles(bm, verts=bvs, dist=weld)
            new_faces = [f if (f is not None and f.is_valid) else None for f in new_faces]
        valid = [f for f in new_faces if f is not None]
        if recalc and valid:
            bmesh.ops.recalc_face_normals(bm, faces=valid)
        return new_faces

    def mirror_x(self):
        bmesh.ops.scale(self.bm, vec=Vector((-1, 1, 1)), verts=list(self.bm.verts))
        bmesh.ops.reverse_faces(self.bm, faces=list(self.bm.faces))

    def add_bm(self, src, roles, matrix=None, recalc=False):
        """Copy a temporary bmesh in. roles: str, or dict material_index -> role."""
        idx = {v: i for i, v in enumerate(src.verts)}
        verts = [v.co.copy() for v in src.verts]
        faces = [[idx[v] for v in f.verts] for f in src.faces]
        if isinstance(roles, dict):
            rl = [roles.get(f.material_index, roles.get(0)) for f in src.faces]
        else:
            rl = roles
        out = self.add(verts, faces, rl, matrix, recalc)
        src.free()
        return out

    def weld_all(self, dist=1e-4):
        bmesh.ops.remove_doubles(self.bm, verts=list(self.bm.verts), dist=dist)

    def to_object(self, name, collection, sharp_deg=35.0, origin=None):
        bm = self.bm
        if origin is not None:
            bmesh.ops.translate(bm, verts=list(bm.verts), vec=-Vector(origin))
        bm.normal_update()
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for r in self.roles:
            me.materials.append(role_mat(r))
        ob = bpy.data.objects.new(name, me)
        collection.objects.link(ob)
        if origin is not None:
            ob.location = Vector(origin)
        me.shade_smooth()
        me.set_sharp_from_angle(angle=math.radians(sharp_deg))
        return ob


# ---------------------------------------------------------------- profiles / lofts
def mirror_ring(half):
    """half: list of (x, z) from bottom centre (x=0) round to the other centre point (x=0).
    Returns full closed ring of (x, z) and, for each ring segment, the index of the half segment."""
    ring = list(half) + [(-x, z) for (x, z) in reversed(half[1:-1])]
    nh = len(half) - 1
    seg_map = list(range(nh)) + list(reversed(range(nh)))
    return ring, seg_map


def loft(rings, closed=True, cap0=False, cap1=False):
    """rings: list of equal-length point lists. Winding is outward for rings that run
    bottom -> +x -> top -> -x while sections advance along +Y."""
    n = len(rings[0])
    verts = [Vector(p) for ring in rings for p in ring]
    faces, seg = [], []
    m = n if closed else n - 1
    for s in range(len(rings) - 1):
        a, b = s * n, (s + 1) * n
        for i in range(m):
            j = (i + 1) % n
            faces.append((a + i, b + i, b + j, a + j))
            seg.append(i)
    if cap0:
        faces.append(tuple(range(n)))
        seg.append(-1)
    if cap1:
        last = (len(rings) - 1) * n
        faces.append(tuple(reversed(range(last, last + n))))
        seg.append(-2)
    return verts, faces, seg


def revolve(profile, segs, a0=0.0, a1=None, closed_profile=False):
    """Lathe around the X axis. profile: list of (r, x). Full circle unless a1 is given.
    Outward winding when the profile runs inner side -> tread -> outer side."""
    full = a1 is None
    if full:
        a1 = 2 * math.pi
    n = len(profile)
    rings = segs if full else segs + 1
    verts = []
    for k in range(rings):
        t = a0 + (a1 - a0) * k / segs
        c, s = math.cos(t), math.sin(t)
        for r, x in profile:
            verts.append(Vector((x, r * c, r * s)))
    faces, seg_i, ring_k = [], [], []
    m = n if closed_profile else n - 1
    for k in range(segs):
        k2 = (k + 1) % rings
        for i in range(m):
            j = (i + 1) % n
            faces.append((k * n + i, k2 * n + i, k2 * n + j, k * n + j))
            seg_i.append(i)
            ring_k.append(k)
    return verts, faces, seg_i, ring_k


# ---------------------------------------------------------------- paths / tubes
def fillet(points, radius, segs=4):
    """Round the corners of a polyline. radius / segs may be single values or per-corner lists."""
    pts = [Vector(p) for p in points]
    out = [pts[0]]
    radii = radius if isinstance(radius, (list, tuple)) else None
    seg_list = segs if isinstance(segs, (list, tuple)) else None
    for i in range(1, len(pts) - 1):
        if radii is not None:
            radius = radii[i - 1]
        if seg_list is not None:
            segs = seg_list[i - 1]
        p0, p1, p2 = pts[i - 1], pts[i], pts[i + 1]
        d0, d2 = p0 - p1, p2 - p1
        l0, l2 = d0.length, d2.length
        d0.normalize()
        d2.normalize()
        ang = d0.angle(d2)
        if ang > math.pi - 1e-3 or radius <= 0:
            out.append(p1)
            continue
        t = min(radius / math.tan(ang / 2), l0 * 0.5, l2 * 0.5)
        a, b = p1 + d0 * t, p1 + d2 * t
        for s in range(segs + 1):
            u = s / segs
            out.append((1 - u) ** 2 * a + 2 * (1 - u) * u * p1 + u * u * b)
    out.append(pts[-1])
    return out


def spline(points, samples=4):
    P = [Vector(p) for p in points]
    P = [P[0] + (P[0] - P[1])] + P + [P[-1] + (P[-1] - P[-2])]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for s in range(samples):
            t = s / samples
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
                              + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(P[-2])
    return out


def simplify(path, min_angle_deg=5.0):
    """Drop interior points where the path barely turns (straight runs), keep the bends."""
    pts = [Vector(p) for p in path]
    out = [pts[0]]
    lim = math.radians(min_angle_deg)
    for i in range(1, len(pts) - 1):
        a, b = pts[i] - out[-1], pts[i + 1] - pts[i]
        if a.length < 1e-6 or b.length < 1e-6:
            continue
        if a.angle(b) > lim:
            out.append(pts[i])
    out.append(pts[-1])
    return out


def tube(path, radius, sides=8, cap0=True, cap1=True, closed=False, twist=0.0, up=None):
    path = [Vector(p) for p in path]
    N = len(path)
    T = []
    for i in range(N):
        if closed:
            d = (path[(i + 1) % N] - path[i]).normalized() + (path[i] - path[i - 1]).normalized()
        elif i == 0:
            d = path[1] - path[0]
        elif i == N - 1:
            d = path[-1] - path[-2]
        else:
            d = (path[i + 1] - path[i]).normalized() + (path[i] - path[i - 1]).normalized()
        T.append(d.normalized())
    ref = Vector(up) if up else (Vector((0, 0, 1)) if abs(T[0].z) < 0.9 else Vector((1, 0, 0)))
    nrm = T[0].cross(ref).normalized()
    frames = []
    for i in range(N):
        if i > 0:
            axis = T[i - 1].cross(T[i])
            if axis.length > 1e-8:
                nrm = Matrix.Rotation(T[i - 1].angle(T[i]), 3, axis.normalized()) @ nrm
        nrm = (nrm - T[i] * nrm.dot(T[i])).normalized()
        frames.append((nrm.copy(), T[i].cross(nrm)))
    verts = []
    for i in range(N):
        r = radius[i] if isinstance(radius, (list, tuple)) else radius
        n_, b_ = frames[i]
        for k in range(sides):
            t = 2 * math.pi * k / sides + twist
            verts.append(path[i] + (n_ * math.cos(t) + b_ * math.sin(t)) * r)
    faces = []
    for i in range(N - 1 + (1 if closed else 0)):
        i2 = (i + 1) % N
        for k in range(sides):
            k2 = (k + 1) % sides
            faces.append((i * sides + k, i * sides + k2, i2 * sides + k2, i2 * sides + k))
    if not closed:
        if cap0:
            faces.append(tuple(reversed(range(sides))))
        if cap1:
            faces.append(tuple(range((N - 1) * sides, N * sides)))
    return verts, faces


def helix(p0, p1, coil_r, turns, per_turn=8):
    p0, p1 = Vector(p0), Vector(p1)
    ax = (p1 - p0)
    L = ax.length
    ax.normalize()
    ref = Vector((1, 0, 0)) if abs(ax.x) < 0.9 else Vector((0, 1, 0))
    u = ax.cross(ref).normalized()
    v = ax.cross(u)
    n = int(turns * per_turn)
    pts = []
    for i in range(n + 1):
        t = i / n
        a = 2 * math.pi * turns * t
        pts.append(p0 + ax * (L * t) + (u * math.cos(a) + v * math.sin(a)) * coil_r)
    return pts


# ---------------------------------------------------------------- bmesh primitives
def _place(bm, center=(0, 0, 0), rot=None):
    M = Matrix.Translation(Vector(center))
    if rot is not None:
        M = M @ (rot.to_matrix().to_4x4() if hasattr(rot, "to_matrix") else rot.to_4x4())
    bmesh.ops.transform(bm, matrix=M, verts=list(bm.verts))
    return bm


def box(size, center=(0, 0, 0), rot=None, bevel=0.0, segs=1):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=list(bm.verts))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges) + list(bm.verts), offset=bevel, segments=segs,
                        profile=0.5, affect='EDGES', clamp_overlap=True)
    return _place(bm, center, rot)


def hexa(corners, bevel=0.0, segs=1):
    """corners: 8 points, bottom quad (ccw seen from above) then matching top quad."""
    bm = bmesh.new()
    vs = [bm.verts.new(Vector(c)) for c in corners]
    for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges) + list(bm.verts), offset=bevel, segments=segs,
                        profile=0.5, affect='EDGES', clamp_overlap=True)
    return bm


def cyl(r1, depth, sides=12, r2=None, center=(0, 0, 0), axis='Z', rot=None, caps=True, bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=sides, radius1=r1,
                          radius2=r1 if r2 is None else r2, depth=depth)
    if bevel > 0 and caps:
        rim = [e for e in bm.edges if not all(len(l.face.verts) == 4 for l in e.link_loops)]
        bmesh.ops.bevel(bm, geom=rim, offset=bevel, segments=1, profile=0.5, affect='EDGES',
                        clamp_overlap=True)
    if axis == 'X':
        bmesh.ops.rotate(bm, verts=list(bm.verts), matrix=Matrix.Rotation(math.pi / 2, 3, 'Y'))
    elif axis == 'Y':
        bmesh.ops.rotate(bm, verts=list(bm.verts), matrix=Matrix.Rotation(-math.pi / 2, 3, 'X'))
    return _place(bm, center, rot)


def sphere(r, u=16, v=10, center=(0, 0, 0), scale=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=list(bm.verts))
    return _place(bm, center)


def disc(center, normal, radius, sides=24, up=(0, 0, 1)):
    """Flat n-gon facing `normal`."""
    c, n = Vector(center), Vector(normal).normalized()
    u = Vector(up)
    u = (u - n * u.dot(n)).normalized()
    v = n.cross(u)
    verts = [c + (u * math.cos(2 * math.pi * k / sides) + v * math.sin(2 * math.pi * k / sides)) * radius
             for k in range(sides)]
    # counter-clockwise around n
    face = tuple(range(sides)) if u.cross(v).dot(n) > 0 else tuple(reversed(range(sides)))
    return verts, [face]


def quad(center, normal, w, h, up=(0, 0, 1)):
    c, n = Vector(center), Vector(normal).normalized()
    u = Vector(up)
    u = (u - n * u.dot(n)).normalized()
    r = u.cross(n)
    verts = [c - r * w / 2 - u * h / 2, c + r * w / 2 - u * h / 2, c + r * w / 2 + u * h / 2, c - r * w / 2 + u * h / 2]
    face = (0, 1, 2, 3) if (verts[1] - verts[0]).cross(verts[2] - verts[0]).dot(n) > 0 else (3, 2, 1, 0)
    return verts, [face]


def extrude_poly(poly, axis, a0, a1):
    """Closed prism: 2D polygon in the plane normal to `axis`, spanning a0..a1 along it.
    axis 'X': poly is (y, z); 'Y': (x, z); 'Z': (x, y)."""
    def p3(p, a):
        if axis == 'X':
            return Vector((a, p[0], p[1]))
        if axis == 'Y':
            return Vector((p[0], a, p[1]))
        return Vector((p[0], p[1], a))
    n = len(poly)
    verts = [p3(p, a0) for p in poly] + [p3(p, a1) for p in poly]
    faces = [tuple(range(n)), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    return verts, faces  # add with recalc=True


def orient_outward(verts, faces, center):
    """Flip faces of an open surface so they face away from `center`."""
    c = Vector(center)
    out = []
    for f in faces:
        p = [Vector(verts[i]) for i in f]
        nrm = (p[1] - p[0]).cross(p[2] - p[0])
        mid = sum(p, Vector()) / len(p)
        out.append(tuple(f) if nrm.dot(mid - c) >= 0 else tuple(reversed(f)))
    return out


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)
