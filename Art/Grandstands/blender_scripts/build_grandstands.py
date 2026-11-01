# Grandstand kit for the Unity game, built from code (Blender 5.x).
#
# Three styles of stand, each as a 12 m bay that repeats along a side of the stadium and an
# end piece that closes the run off (the game mirrors it for the other end):
#   Main     - a modern main grandstand: lower and upper tiers with two floors of glazed
#              hospitality suites between them, under a white fabric canopy slung between big
#              cantilevered trusses (after the main stands at Austin and Silverstone)
#   Scaffold - a temporary stand on a galvanised scaffold frame, wrapped in banners, with a row
#              of peaked tent roofs (after the street-circuit and harbour-front stands)
#   Terrace  - a classic concrete terrace under a sloping roof on slim green columns, with press
#              boxes along the back and a clock tower at the end (after the old main stands at
#              Monza and Interlagos)
#
# Every module is an empty with four child meshes:
#   <Module>_Palette   the structure; every face's UVs point at one cell of the 8x8 palette
#   <Module>_Crowd     seat rows for the game's crowd texture: U = metres / 12, and V's whole
#                      part numbers the row (risers and treads separately) so the game can give
#                      each its own strip of the texture; V's fraction runs 0.02..0.98
#   <Module>_Ads       sponsors' boards: U's whole part numbers the board and its fraction and V
#                      run 0..1 across it; the game picks an advert for each board
#   <Module>_Collider  a simple solid for physics, never drawn
#
# Geometry is written in Unity's axes for the module: x along the stand (left to right as seen
# from the track), y up, z away from the track, with the stand's front edge on z = 0 and a bay
# running from x = 0 to 12. B() turns that into Blender's axes, and the FBX export maps them
# straight back.
import bpy
import bmesh
import math
import os
from mathutils import Vector

V = Vector
BAY = 12.0         # width of one repeating bay
CROWD_TILE = 12.0  # metres of stand per repeat of the crowd texture (24 people)
GRID, CELL = 8, 32

# role: (albedo sRGB hex, metallic, smoothness, emission sRGB hex or None)
PALETTE = {
    "Concrete": ("C6C3BA", 0.0, 0.15, None),
    "ConcreteDark": ("8E8B84", 0.0, 0.12, None),
    "Step": ("AAA79F", 0.0, 0.15, None),
    "Nosing": ("F2C200", 0.0, 0.30, None),
    "SteelWhite": ("E9EBEC", 0.30, 0.55, None),
    "SteelGrey": ("9EA4AA", 0.85, 0.45, None),
    "SteelDark": ("2C3035", 0.50, 0.45, None),
    "SteelGreen": ("1F5A3C", 0.35, 0.50, None),
    "Membrane": ("F4F3EE", 0.0, 0.25, None),
    "Canvas": ("D9CFB8", 0.0, 0.20, None),
    "RoofSheet": ("B3B9BF", 0.70, 0.40, None),
    "Glass": ("38566E", 0.25, 0.92, "FFC27A"),
    "Mullion": ("1E2125", 0.60, 0.55, None),
    "Railing": ("D9DCDF", 0.80, 0.60, None),
    "Banner": ("1E2A5E", 0.0, 0.30, None),
    "Accent": ("D7141A", 0.10, 0.55, None),
    "Accent2": ("F2B705", 0.10, 0.55, None),
    "Brick": ("8C4A36", 0.0, 0.10, None),
    "Opening": ("0E0F11", 0.0, 0.05, None),
    "Lamp": ("FFFFFF", 0.0, 0.80, "FFF2DD"),
    "ClockFace": ("F1EFE6", 0.0, 0.60, "FFF6E0"),
}
ROLES = list(PALETTE)


def cell_uv(role):
    i = ROLES.index(role)
    return ((i % GRID + 0.5) / GRID, 1 - (i // GRID + 0.5) / GRID)


def B(p):
    """Unity module axes (x along, y up, z back) to Blender's."""
    return V((-p[0], -p[2], p[1]))


# ------------------------------------------------------------------ mesh building
class Part:
    """One mesh of a module. Faces never share vertices (flat shading), except surface()."""

    def __init__(self):
        self.v, self.uv, self.f, self.smooth = [], [], [], []

    def poly(self, pts, normal, role=None, uvs=None, smooth=False):
        """A face through pts, turned so its front faces `normal` (Unity axes)."""
        bp = [B(p) for p in pts]
        e = V((0.0, 0.0, 0.0))
        for i in range(1, len(bp) - 1):
            e += (bp[i] - bp[0]).cross(bp[i + 1] - bp[0])
        if e.length < 1e-9:
            return
        order = list(range(len(bp)))
        if e.dot(B(normal)) < 0:
            order.reverse()
        uvs = uvs or [cell_uv(role)] * len(pts)
        i0 = len(self.v)
        for i in order:
            self.v.append(bp[i])
            self.uv.append(tuple(uvs[i]))
        self.f.append(tuple(range(i0, i0 + len(order))))
        self.smooth.append(smooth)

    def quad(self, a, b, c, d, normal, role=None, uvs=None, both=False):
        self.poly([a, b, c, d], normal, role, uvs)
        if both:
            self.poly([a, b, c, d], [-n for n in normal], role, uvs)

    def face_x(self, x, y0, y1, z0, z1, sign, role):
        self.quad((x, y0, z0), (x, y1, z0), (x, y1, z1), (x, y0, z1), (sign, 0, 0), role)

    def face_y(self, y, x0, x1, z0, z1, sign, role):
        self.quad((x0, y, z0), (x0, y, z1), (x1, y, z1), (x1, y, z0), (0, sign, 0), role)

    def face_z(self, z, x0, x1, y0, y1, sign, role, both=False):
        self.quad((x0, y0, z), (x0, y1, z), (x1, y1, z), (x1, y0, z), (0, 0, sign), role, both=both)

    def abox(self, x0, x1, y0, y1, z0, z1, role, skip=()):
        """Axis-aligned box; skip names faces to leave out ('+x', '-y', ...)."""
        if '+x' not in skip:
            self.face_x(x1, y0, y1, z0, z1, 1, role)
        if '-x' not in skip:
            self.face_x(x0, y0, y1, z0, z1, -1, role)
        if '+y' not in skip:
            self.face_y(y1, x0, x1, z0, z1, 1, role)
        if '-y' not in skip:
            self.face_y(y0, x0, x1, z0, z1, -1, role)
        if '+z' not in skip:
            self.face_z(z1, x0, x1, y0, y1, 1, role)
        if '-z' not in skip:
            self.face_z(z0, x0, x1, y0, y1, -1, role)

    def box(self, c, size, axes, role, skip=()):
        """Box centred on c with edges along the given (unit) axes."""
        c = V(c)
        X, Y, Z = (V(a) * (s / 2) for a, s in zip(axes, size))
        for key, n, u, w in (('+x', X, Y, Z), ('-x', -X, Y, Z), ('+y', Y, Z, X), ('-y', -Y, Z, X),
                             ('+z', Z, X, Y), ('-z', -Z, X, Y)):
            if key in skip:
                continue
            p = c + n
            self.quad(p - u - w, p + u - w, p + u + w, p - u + w, n, role)

    def beam(self, a, b, t, role, t2=None, ends=False):
        """Beam from a to b, t wide and t2 (default t) deep; open ends unless asked."""
        a, b = V(a), V(b)
        d = b - a
        length = d.length
        if length < 1e-4:
            return
        d /= length
        up = V((0, 1, 0)) if abs(d.y) < 0.95 else V((0, 0, 1))
        x = up.cross(d).normalized()
        y = d.cross(x)
        self.box((a + b) / 2, (t, t2 or t, length), (x, y, d), role, () if ends else ('+z', '-z'))

    def surface(self, rows, up, role, both=True):
        """Smooth-shaded sheet through a grid of points (rows of equal length), front facing
        roughly `up`; with both, a second copy faces the other way."""
        uv = cell_uv(role)
        nc = len(rows[0])
        for flip in ((False, True) if both else (False,)):
            base = len(self.v)
            for r in rows:
                for p in r:
                    self.v.append(B(p))
                    self.uv.append(uv)
            want = B(up) * (-1 if flip else 1)
            for i in range(len(rows) - 1):
                for j in range(nc - 1):
                    q = [base + i * nc + j, base + i * nc + j + 1, base + (i + 1) * nc + j + 1, base + (i + 1) * nc + j]
                    pa, pb, pc = self.v[q[0]], self.v[q[1]], self.v[q[2]]
                    if (pb - pa).cross(pc - pa).dot(want) < 0:
                        q.reverse()
                    self.f.append(tuple(q))
                    self.smooth.append(True)

    def board(self, index, bl, right, up, normal):
        bl, r, u = V(bl), V(right), V(up)
        uvs = [(index + 0.005, 0.0), (index + 0.005, 1.0), (index + 0.995, 1.0), (index + 0.995, 0.0)]
        self.quad(bl, bl + u, bl + u + r, bl + r, normal, uvs=uvs)

    def prism(self, profile, x0, x1):
        """Solid: a polygon of (z, y) points swept from x0 to x1."""
        n = len(profile)
        area = sum(profile[i][0] * profile[(i + 1) % n][1] - profile[(i + 1) % n][0] * profile[i][1]
                   for i in range(n))
        for i in range(n):
            (za, ya), (zb, yb) = profile[i], profile[(i + 1) % n]
            dz, dy = zb - za, yb - ya
            nz, ny = (dy, -dz) if area > 0 else (-dy, dz)
            self.quad((x0, ya, za), (x1, ya, za), (x1, yb, zb), (x0, yb, zb), (0, ny, nz), "Concrete")
        self.poly([(x1, y, z) for z, y in profile], (1, 0, 0), "Concrete")
        self.poly([(x0, y, z) for z, y in profile], (-1, 0, 0), "Concrete")

    def box_solid(self, x0, x1, y0, y1, z0, z1):
        self.abox(x0, x1, y0, y1, z0, z1, "Concrete")


class Module:
    def __init__(self, name):
        self.name = name
        self.pal, self.crowd, self.ads, self.col = Part(), Part(), Part(), Part()
        self.boards = 0
        self.rows = 0

    def board(self, bl, right, up, normal=(0, 0, -1)):
        self.ads.board(self.boards, bl, right, up, normal)
        self.boards += 1


def cut(span, xa, xb):
    a, b = span
    out = []
    if xa > a:
        out.append((a, min(b, xa)))
    if xb < b:
        out.append((max(a, xb), b))
    return [s for s in out if s[1] - s[0] > 1e-3]


def seating(m, x0, x1, y0, z0, rise, tread, rows, aisles, gaps=(), aisle_role="Step"):
    """Rows of seats from x0 to x1: row k's riser stands at depth z0 + k * tread, from height
    y0 + k * rise. Aisles are stepped, one step per row; gaps are (xa, xb, k0, k1) openings."""
    for k in range(rows):
        y, z = y0 + k * rise, z0 + k * tread
        yt = y + rise
        rid, tid = m.rows, m.rows + 1
        m.rows += 2
        spans = [(x0, x1)]
        for (xa, xb, k0, k1) in gaps:
            if k0 <= k <= k1:
                spans = [s for sp in spans for s in cut(sp, xa, xb)]
        for (xa, xb) in spans:
            ua, ub = xa / CROWD_TILE, xb / CROWD_TILE
            m.crowd.quad((xa, y, z), (xa, yt, z), (xb, yt, z), (xb, y, z), (0, 0, -1),
                         uvs=[(ua, rid + 0.02), (ua, rid + 0.98), (ub, rid + 0.98), (ub, rid + 0.02)])
            m.crowd.quad((xa, yt, z), (xa, yt, z + tread), (xb, yt, z + tread), (xb, yt, z), (0, 1, 0),
                         uvs=[(ua, tid + 0.98), (ua, tid + 0.02), (ub, tid + 0.02), (ub, tid + 0.98)])
        for (xa, xb) in aisles:
            m.pal.face_z(z, xa, xb, y, yt - 0.07, -1, aisle_role)
            m.pal.face_z(z, xa, xb, yt - 0.07, yt, -1, "Nosing")
            m.pal.face_y(yt, xa, xb, z, z + tread, 1, aisle_role)


def vomitory(m, xa, xb, y0, z0, rise, tread, k0, k1):
    """A tunnel mouth cut into rows k0..k1: floor, stepped side walls, dark opening, rails."""
    yf = y0 + k0 * rise
    za, zb = z0 + k0 * tread, z0 + (k1 + 1) * tread
    yb = y0 + (k1 + 1) * rise
    m.pal.face_y(yf, xa, xb, za, zb, 1, "Concrete")
    m.pal.face_z(zb, xa, xb, yf, yb, -1, "Opening")
    for k in range(k0, k1 + 1):
        z, yt = z0 + k * tread, y0 + (k + 1) * rise
        m.pal.face_x(xa, yf, yt, z, z + tread, 1, "Concrete")
        m.pal.face_x(xb, yf, yt, z, z + tread, -1, "Concrete")
    for x in (xa, xb):
        m.pal.beam((x, y0 + (k0 + 1) * rise + 0.95, za), (x, yb + 0.95, zb), 0.07, "Railing")
    m.pal.beam((xa, yb + 1.0, zb + 0.06), (xb, yb + 1.0, zb + 0.06), 0.07, "Railing")


def rail(m, x0, x1, y, z, posts, role="Railing", height=1.0, mid=True):
    """A guard rail along x: posts at the given x positions, a top rail and a mid rail."""
    for x in posts:
        m.pal.beam((x, y, z), (x, y + height, z), 0.06, role)
    m.pal.beam((x0, y + height, z), (x1, y + height, z), 0.07, role)
    if mid:
        m.pal.beam((x0, y + height * 0.5, z), (x1, y + height * 0.5, z), 0.045, role)


def end_wall(m, xa, xb, steps, role="Concrete"):
    """A solid end wall from xa to xb: one block per (z0, z1, top) step of the profile."""
    for i, (z0, z1, top) in enumerate(steps):
        last = i == len(steps) - 1
        m.pal.abox(xa, xb, 0, top, z0, z1, role, skip=('-y',) if last else ('-y', '+z'))


# ------------------------------------------------------------------ Main grandstand
def build_main(end):
    m = Module("Main_End" if end else "Main_Bay")
    pal = m.pal
    W = 6.0 if end else BAY                      # front and back walls run this far
    inner = 5.4 if end else BAY                  # floors and suites stop at the end wall
    seats_to = 5.4 if end else 11.4
    aisles = [(0.0, 0.6)] if end else [(0.0, 0.6), (11.4, 12.0)]
    sides = ('-x', '+x') if not end else ('-x',)
    P, WALK = 3.2, 1.2
    R1, T1, N1 = 0.6, 0.85, 14
    Y2, Z2 = 19.0, 15.4
    R2, T2, N2 = 0.74, 0.82, 12
    top_y1, top_z1 = P + N1 * R1, WALK + N1 * T1
    top_y2, top_z2 = Y2 + N2 * R2, Z2 + N2 * T2
    BACK, PAR = 25.6, top_y2 + 1.1

    # Front wall, its row of boards, coping and rail; the walkway behind it.
    pal.face_z(0, 0, W, 0, 1.6, -1, "ConcreteDark")
    pal.face_z(0, 0, W, 1.6, 3.05, -1, "SteelDark")
    for x in range(0, int(W), 6):
        m.board((x + 0.05, 1.58, -0.02), (5.9, 0, 0), (0, 1.47, 0))
    pal.abox(0, W, 3.05, 3.25, -0.08, 0.3, "SteelWhite", skip=sides)
    pal.face_y(P, 0, inner, 0.3, WALK, 1, "Concrete")
    rail(m, 0, inner, 3.25, 0.1, [x + 1.0 for x in range(0, int(inner), 2)])

    # Lower tier, with a vomitory in the middle of each bay.
    gaps = [] if end else [(4.8, 7.2, 4, 7)]
    seating(m, 0.6, seats_to, P, WALK, R1, T1, N1, aisles, gaps)
    if not end:
        vomitory(m, 4.8, 7.2, P, WALK, R1, T1, 4, 7)

    # Concourse along the top of the lower tier, then two floors of glazed suites, leaning out.
    pal.face_y(top_y1, 0, inner, top_z1, 14.4, 1, "Concrete")
    rail(m, 0, inner, top_y1, top_z1 + 0.1, [x + 1.0 for x in range(0, int(inner), 3)])
    for y0, y1 in ((top_y1, 14.9), (15.3, 18.4)):
        pal.quad((0, y0, 14.4), (0, y1, 14.0), (inner, y1, 14.0), (inner, y0, 14.4), (0, 0.12, -1), "Glass")
        for x in range(0, int(inner) + 1, 2):
            if x < inner:
                pal.beam((x, y0, 14.38), (x, y1, 13.98), 0.12, "Mullion", t2=0.2)
    pal.abox(0, inner, 14.9, 15.3, 13.6, 14.45, "SteelWhite", skip=sides)

    # Upper tier, behind a fascia of boards.
    pal.abox(0, inner, 18.3, 20.0, 13.95, 14.6, "SteelWhite", skip=sides + ('+z',))
    for x in range(0, int(inner), 6):
        m.board((x + 0.05, 18.42, 13.93), (min(5.9, inner - x - 0.1), 0, 0), (0, 1.47, 0))
    pal.face_y(Y2, 0, inner, 14.6, Z2, 1, "Concrete")
    seating(m, 0.6, seats_to, Y2, Z2, R2, T2, N2, aisles)
    pal.face_z(top_z2, 0, inner, top_y2, PAR, -1, "Concrete")
    pal.face_y(PAR, 0, W, top_z2, BACK, 1, "Concrete")

    # Back wall: concrete base, the suites' rear glazing, cladding, and fins up its face.
    pal.face_z(BACK, 0, W, 0, top_y1, 1, "ConcreteDark")
    pal.face_z(BACK, 0, W, top_y1, 18.4, 1, "Glass")
    pal.face_z(BACK, 0, W, 18.4, PAR, 1, "SteelWhite")
    if not end:
        pal.face_z(BACK + 0.01, 4.0, 8.0, 0, 3.0, 1, "Opening")
    for x in (1, 3, 5, 7, 9, 11):
        if x < W:
            pal.abox(x - 0.12, x + 0.12, top_y1, PAR, BACK, BACK + 0.5, "SteelWhite", skip=('-z', '-y'))

    # Canopy: a cantilevered truss over each bay's middle (the end's over its end wall), and
    # white fabric slung between them, highest along the trusses.
    def y_top(s):
        return 32.0 + 3.0 * s + 0.8 * math.sin(math.pi * s)

    def y_bot(s):
        return y_top(s) - (3.0 * (1 - s) + 0.5 * s)

    def z_at(s):
        return 27.0 - 25.0 * s

    tx = 6.0
    S = [i / 8 for i in range(9)]
    top = [V((tx, y_top(s), z_at(s))) for s in S]
    bot = [V((tx, y_bot(s), z_at(s))) for s in S]
    pal.abox(tx - 0.45, tx + 0.45, 0, y_top(0) + 0.3, 26.55, 27.45, "SteelWhite", skip=('-y',))
    for i in range(8):
        pal.beam(top[i], top[i + 1], 0.42, "SteelWhite")
        pal.beam(bot[i], bot[i + 1], 0.34, "SteelWhite")
        pal.beam(top[i], bot[i + 1], 0.18, "SteelWhite") if i % 2 == 0 else pal.beam(bot[i], top[i + 1], 0.18, "SteelWhite")
    for i in range(1, 9):
        pal.beam(bot[i], top[i], 0.2, "SteelWhite")
    pal.beam((tx, 22.0, 27.0), (tx, y_bot(0.12), z_at(0.12)), 0.3, "SteelWhite")
    for s in (0.0, 1.0):
        pal.beam((0, y_top(s), z_at(s)), (W, y_top(s), z_at(s)), 0.36, "SteelWhite")
    xs = [x * 1.5 for x in range(int(W / 1.5) + 1)]
    sag = 2.2
    rows = []
    for i in range(13):
        s = i / 12
        rows.append([(x, y_top(s) + 0.22 - sag * math.sin(math.pi * s) ** 0.8 * ((x - tx) / 6.0) ** 2, z_at(s))
                     for x in xs])
    pal.surface(rows, (0, 1, 0), "Membrane")
    # Boards hung under the canopy's front edge, and floodlights under the truss.
    pal.abox(0, W, 33.0, 34.5, 1.88, 2.0, "SteelDark", skip=('-z',) + sides)
    for x in range(0, int(W), 6):
        m.board((x + 0.05, 33.02, 1.87), (5.9, 0, 0), (0, 1.47, 0))
    for s in (0.3, 0.55, 0.8):
        pal.abox(tx - 0.3, tx + 0.3, y_bot(s) - 0.55, y_bot(s) - 0.2, z_at(s) - 0.4, z_at(s) + 0.4, "Lamp")

    profile = [(0, 0), (0, P), (WALK, P), (top_z1, top_y1), (13.95, top_y1), (13.95, 20.0), (Z2, Y2),
               (top_z2, top_y2), (top_z2, PAR), (BACK, PAR), (BACK, 0)]
    roof = [(z_at(0), y_top(0) + 0.3), (z_at(1), y_top(1) + 0.3), (z_at(1), y_bot(1)), (z_at(0), y_bot(0) + 1.5)]
    m.col.prism(profile, 0, W)
    m.col.prism(roof, 0, W)

    if end:
        # Solid end wall stepping up with the rows, and a stair and lift tower beyond it.
        steps = [(0.02, WALK, P + 1.05)]
        steps += [(WALK + k * T1, WALK + (k + 1) * T1, P + (k + 1) * R1 + 1.0) for k in range(N1)]
        steps += [(top_z1, Z2, 20.0)]
        steps += [(Z2 + k * T2, Z2 + (k + 1) * T2, Y2 + (k + 1) * R2 + 1.0) for k in range(N2)]
        steps += [(top_z2, BACK - 0.02, PAR)]
        end_wall(m, 5.4, 6.0, steps)
        pal.abox(6.0, 10.0, 0, 30.5, 14.0, 26.1, "ConcreteDark", skip=('-y', '+x', '+y'))
        pal.face_x(10.0, 0, 30.5, 14.0, 26.1, 1, "Glass")
        for k in range(1, 9):
            y = 3.4 * k
            pal.abox(10.0, 10.15, y - 0.15, y + 0.15, 14.0, 26.1, "SteelWhite", skip=('-x',))
        for z in (16.0, 18.0, 20.0, 22.0, 24.0):
            pal.beam((10.06, 0, z), (10.06, 30.5, z), 0.1, "Mullion")
        pal.face_x(10.17, 0, 3.0, 17.5, 21.5, 1, "Opening")
        pal.abox(9.2, 10.0, 0, 31.6, 13.7, 14.0, "Accent", skip=('-y', '+z'))
        pal.abox(5.9, 10.1, 30.5, 30.9, 13.9, 26.2, "SteelWhite")
        m.col.box_solid(6.0, 10.1, 0, 30.9, 13.9, 26.2)
    return m


# ------------------------------------------------------------------ Scaffold stand
def build_scaffold(end):
    m = Module("Scaffold_End" if end else "Scaffold_Bay")
    pal = m.pal
    W = 6.0 if end else BAY
    seats_to = 5.4 if end else 11.4
    aisles = [(0.0, 0.6), (5.4, 6.0)] if end else [(0.0, 0.6), (11.4, 12.0)]
    sides = ('-x',) if end else ('-x', '+x')
    P, WALK = 2.6, 0.9
    R, T, N = 0.62, 0.8, 24
    top_y, top_z = P + N * R, WALK + N * T
    BACK = 21.3

    def deck(z):  # underside of the seating deck at depth z
        return P - 0.12 + (z - WALK) * (top_y - P) / (top_z - WALK)

    # Hoarding wrapped in a banner with boards on it, a guard rail and the front walkway.
    pal.face_z(0, 0, W, 0, P, -1, "Banner")
    for x in range(0, int(W), 6):
        m.board((x + 0.2, 0.95, -0.02), (5.6, 0, 0), (0, 1.4, 0))
    pal.abox(0, W, P, P + 0.1, -0.06, 0.12, "SteelGrey", skip=sides)
    rail(m, 0, W, P + 0.1, 0.05, [x * 2.4 for x in range(int(W / 2.4) + 1) if x * 2.4 < W])
    pal.face_y(P, 0, W, 0.12, WALK, 1, "SteelGrey")

    seating(m, 0.6, seats_to, P, WALK, R, T, N, aisles, aisle_role="SteelGrey")

    # Rear walkway and guard; the deck's underside.
    pal.face_y(top_y, 0, W, top_z, BACK, 1, "SteelGrey")
    pal.face_y(top_y - 0.12, 0, W, top_z, BACK, -1, "SteelDark")
    pal.face_z(BACK - 0.05, 0, W, top_y, top_y + 0.15, -1, "SteelGrey")
    pal.beam((0, top_y + 1.1, BACK - 0.05), (W, top_y + 1.1, BACK - 0.05), 0.07, "Railing")
    pal.beam((0, top_y + 0.6, BACK - 0.05), (W, top_y + 0.6, BACK - 0.05), 0.045, "Railing")
    pal.quad((0, deck(WALK), WALK), (W, deck(WALK), WALK), (W, deck(top_z), top_z), (0, deck(top_z), top_z),
             (0, -(top_z - WALK), top_y - P), "SteelDark")

    # The scaffold: standards on a 2.4 m grid, ledgers and braces on the back, a banner over its
    # top half.
    xs = [x * 2.4 for x in range(5) if x * 2.4 < W]
    for x in xs:
        for z in (2.4, 7.2, 12.0, 16.8):
            pal.beam((x, 0, z), (x, deck(z), z), 0.1, "SteelGrey")
        pal.beam((x, 0, BACK), (x, top_y + 1.1, BACK), 0.1, "SteelGrey")
        pal.beam((x, 0.3, 2.4), (x, 0.3, BACK), 0.07, "SteelGrey")
    lifts = [0.2, 3.6, 7.0, 10.4, 13.8]
    for y in lifts:
        pal.beam((0, y, BACK), (W, y, BACK), 0.08, "SteelGrey")
    for i, x in enumerate(xs):
        if i % 2 == 0:
            for j in range(3):
                y0, y1 = lifts[j], lifts[j + 1]
                a, b = (x, y0, BACK), (x + 2.4, y1, BACK)
                pal.beam(a, b, 0.07, "SteelGrey") if j % 2 == 0 else pal.beam((x, y1, BACK), (x + 2.4, y0, BACK), 0.07, "SteelGrey")
    for za, zb in ((2.4, 7.2), (12.0, 16.8)):
        pal.beam((0, 0.3, za), (0, deck(zb) - 0.2, zb), 0.07, "SteelGrey")
    pal.face_z(BACK + 0.07, 0, W, 9.0, top_y + 1.0, 1, "Banner", both=True)

    # Peaked tent roofs, each 6 m across, on masts behind the stand with ties out to the front.
    ye, yr, zf, zb = 21.6, 23.4, 8.5, BACK + 0.3
    tents = [0.0] if end else [0.0, 6.0]
    for x0 in tents:
        xm, x1 = x0 + 3.0, x0 + 6.0
        for slope, n in ((((x0, ye, zf), (xm, yr, zf), (xm, yr, zb), (x0, ye, zb)), (-1.8, 3, 0)),
                         (((xm, yr, zf), (x1, ye, zf), (x1, ye, zb), (xm, yr, zb)), (1.8, 3, 0))):
            pal.quad(*slope, n, "Membrane")
            pal.quad(*slope, [-c for c in n], "Canvas")
        for z, n in ((zf, -1), (zb, 1)):
            pal.poly([(x0, ye, z), (xm, yr, z), (x1, ye, z)], (0, 0, n), "Membrane")
            pal.poly([(x0, ye, z), (xm, yr, z), (x1, ye, z)], (0, 0, -n), "Canvas")
        pal.face_z(zf, x0, x1, ye - 1.55, ye, -1, "Accent")
        pal.face_z(zf + 0.01, x0, x1, ye - 1.55, ye, 1, "Accent")
        m.board((x0 + 0.1, ye - 1.5, zf - 0.02), (5.8, 0, 0), (0, 1.45, 0))
        pal.beam((xm, yr - 0.1, zb), (xm, yr - 0.1, zf), 0.12, "SteelGrey")
        pal.beam((x0, ye, zf - 0.05), (xm, yr, zf - 0.05), 0.1, "SteelGrey")
        pal.beam((xm, yr, zf - 0.05), (x1, ye, zf - 0.05), 0.1, "SteelGrey")
        for z in (12.0, 17.0):
            pal.abox(xm - 0.2, xm + 0.2, yr - 0.6, yr - 0.25, z - 0.3, z + 0.3, "Lamp")
    for x in sorted({x for x0 in tents for x in (x0, x0 + 6.0)}):
        pal.beam((x, ye - 0.12, zb), (x, ye - 0.12, zf), 0.16, "SteelGrey")
        pal.beam((x, 0, zb), (x, 25.0, zb), 0.2, "SteelGrey")
        pal.beam((x, 25.0, zb), (x, ye, zf), 0.06, "SteelGrey")
        pal.beam((x, 25.0, zb), (x, ye, (zf + zb) / 2), 0.06, "SteelGrey")
        # A pennant on top, streaming out behind the stand.
        pal.beam((x, 25.0, zb), (x, 26.6, zb), 0.05, "SteelGrey")
        flag = [(x, 26.55, zb), (x, 25.75, zb), (x, 26.15, zb + 1.6)]
        pal.poly(flag, (1, 0, 0), "Accent2")
        pal.poly(flag, (-1, 0, 0), "Accent2")

    profile = [(0, 0), (0, P), (WALK, P), (top_z, top_y), (BACK, top_y), (BACK, 0)]
    m.col.prism(profile, 0, W)
    m.col.prism([(zf, ye - 0.2), (zb, ye - 0.2), (zb, yr + 0.1), (zf, yr + 0.1)], 0, W)

    if end:
        # Banner-wrapped side with the frame outside it, a stepped guard rail, and a stair tower.
        def profile_top(z):
            if z < WALK:
                return P
            if z >= top_z:
                return top_y
            return P + (int((z - WALK) / T) + 1) * R

        pal.face_x(6.0, 0, P, 0, WALK, 1, "Banner")
        for k in range(N):
            pal.face_x(6.0, 0, P + (k + 1) * R, WALK + k * T, WALK + (k + 1) * T, 1, "Banner")
        pal.face_x(6.0, 0, top_y, top_z, BACK, 1, "Banner")
        zs = [0.1, 2.4, 7.2, 12.0, 16.8, BACK]
        for z in zs:
            pal.beam((6.1, 0, z), (6.1, profile_top(z) - 0.05, z), 0.1, "SteelGrey")
        for y in lifts[:-1]:
            z_from = next(z for z in [i * 0.4 for i in range(60)] if profile_top(z) > y + 0.3)
            pal.beam((6.1, y + 0.3, z_from), (6.1, y + 0.3, BACK), 0.08, "SteelGrey")
        for za, zb2 in zip(zs[1:], zs[2:]):
            pal.beam((6.1, 0.3, za), (6.1, min(profile_top(za), profile_top(zb2)) - 0.3, zb2), 0.07, "SteelGrey")
        pal.beam((5.95, P + 1.1, WALK), (5.95, top_y + 1.1, top_z), 0.07, "Railing")
        pal.beam((5.95, top_y + 1.1, top_z), (5.95, top_y + 1.1, BACK), 0.07, "Railing")
        for k in range(0, N + 1, 4):
            y = P + k * R
            pal.beam((5.95, y, WALK + k * T + 0.1), (5.95, y + 1.1, WALK + k * T + 0.1), 0.06, "Railing")

        # Switchback stair: four flights between landings at the back and front of the tower.
        tx0, tx1, zf0, zf1, zb0, zb1 = 6.3, 8.7, 13.5, 15.5, 19.3, 21.3
        for x in (tx0, tx1):
            for z in (zf0, zb1):
                pal.beam((x, 0, z), (x, top_y + 1.1, z), 0.1, "SteelGrey")
        lv = [top_y * j / 4 for j in range(5)]
        for j in range(4):
            front_first = j % 2 == 0
            za, zc = (zb0, zf1) if front_first else (zf1, zb0)
            xa = (tx0, 7.4) if j % 2 == 0 else (7.6, tx1)
            xm = (xa[0] + xa[1]) / 2
            pal.beam((xm, lv[j], za), (xm, lv[j + 1], zc), xa[1] - xa[0], "SteelGrey", t2=0.12, ends=True)
            pal.beam((xa[0] if j % 2 == 0 else xa[1], lv[j] + 1.0, za), (xa[0] if j % 2 == 0 else xa[1], lv[j + 1] + 1.0, zc), 0.05, "Railing")
            land = (zf0, zf1) if front_first else (zb0, zb1)
            pal.abox(tx0, tx1, lv[j + 1] - 0.12, lv[j + 1], land[0], land[1], "SteelGrey")
        pal.abox(6.0, tx0, top_y - 0.12, top_y, zb0, zb1, "SteelGrey")
        m.col.box_solid(6.0, tx1, 0, top_y + 1.1, zf0, zb1)
    return m


# ------------------------------------------------------------------ Classic terrace
def build_terrace(end):
    m = Module("Terrace_End" if end else "Terrace_Bay")
    pal = m.pal
    W = 6.0 if end else BAY
    inner = 5.4 if end else BAY
    seats_to = 5.4 if end else 11.4
    aisles = [(0.0, 0.6)] if end else [(0.0, 0.6), (11.4, 12.0)]
    sides = ('-x',) if end else ('-x', '+x')
    P, WALK = 2.4, 1.0
    R, T, N = 0.58, 0.85, 22
    top_y, top_z = P + N * R, WALK + N * T
    BACKF, BACK = 20.5, 20.9
    RW = W + 0.4 if end else W   # the roof overhangs the end wall

    def yu(z):  # underside of the roof, rising towards the track
        return 19.4 + (BACK - z) / (BACK - 4.0) * 1.6

    pal.face_z(0, 0, W, 0, 0.85, -1, "Concrete")
    pal.face_z(0, 0, W, 0.85, P, -1, "SteelGreen")
    for x in range(0, int(W), 6):
        m.board((x + 0.05, 0.86, -0.02), (5.9, 0, 0), (0, 1.47, 0))
    pal.abox(0, W, P, P + 0.15, -0.08, 0.3, "Concrete", skip=sides)
    rail(m, 0, W, P + 0.15, 0.1, [x * 1.5 + 0.75 for x in range(int(W / 1.5))], role="SteelGreen", mid=False)
    pal.face_y(P, 0, inner, 0.3, WALK, 1, "Concrete")

    gaps = [] if end else [(4.8, 7.2, 6, 9)]
    seating(m, 0.6, seats_to, P, WALK, R, T, N, aisles, gaps)
    if not end:
        vomitory(m, 4.8, 7.2, P, WALK, R, T, 6, 9)

    # Rear walkway and the back wall, with a row of press boxes looking out over the stand.
    pal.face_y(top_y, 0, inner, top_z, BACKF, 1, "Concrete")
    pal.face_z(BACKF, 0, inner, top_y, 16.2, -1, "Brick")
    pal.face_z(BACKF, 0, inner, 16.2, 18.4, -1, "Glass")
    pal.face_z(BACKF, 0, inner, 18.4, yu(BACKF), -1, "Brick")
    pal.abox(0, inner, 16.05, 16.2, BACKF - 0.3, BACKF, "Concrete", skip=('+z',) + sides)
    for i in range(int(inner / 1.5) + 1):
        x = i * 1.5
        if x < inner:
            pal.abox(x - 0.06, x + 0.06, 16.2, 18.4, BACKF - 0.12, BACKF, "SteelWhite", skip=('+z', '-y', '+y'))
    pal.face_z(BACK, 0, W, 0, yu(BACK), 1, "Brick")
    for x in (1.5, 4.5, 7.5, 10.5):
        if x < W:
            for y0 in (5.6, 11.0):
                pal.face_z(BACK + 0.01, x - 0.6, x + 0.6, y0, y0 + 1.8, 1, "Opening")

    # The roof: corrugated sheet on girders, a fascia of boards along its front edge, and slim
    # columns standing in the aisles.
    zr0, zr1 = 4.0, BACK + 0.3
    pal.quad((0, yu(zr1) + 0.45, zr1), (0, yu(zr0) + 0.45, zr0), (RW, yu(zr0) + 0.45, zr0), (RW, yu(zr1) + 0.45, zr1),
             (0, 1, 0.1), "RoofSheet")
    pal.quad((0, yu(zr1), zr1), (0, yu(zr0), zr0), (RW, yu(zr0), zr0), (RW, yu(zr1), zr1), (0, -1, -0.1), "SteelWhite")
    pal.abox(0, RW, yu(zr0) - 1.1, yu(zr0) + 0.65, zr0 - 0.15, zr0 + 0.05, "SteelGreen", skip=('-x', '+x') if not end else ('-x',))
    for x in range(0, int(W), 6):
        m.board((x + 0.05, yu(zr0) - 1.0, zr0 - 0.16), (5.9, 0, 0), (0, 1.47, 0))
    pal.face_z(zr1, 0, RW, yu(zr1) - 0.3, yu(zr1) + 0.45, 1, "SteelGreen")
    if end:
        pal.quad((RW, yu(zr1), zr1), (RW, yu(zr1) + 0.45, zr1), (RW, yu(zr0) + 0.45, zr0), (RW, yu(zr0), zr0),
                 (1, 0, 0), "SteelGreen")
    for gx in ((0.0, 6.0) if end else (0.0,)):
        pal.beam((gx, yu(zr0 + 0.05) - 0.35, zr0 + 0.05), (gx, yu(BACKF) - 0.35, BACKF), 0.3, "SteelGreen", t2=0.7)
        zc = 7.0
        base = P + (int((zc - WALK) / T) + 1) * R + (1.0 if gx > 0 else 0.0)
        pal.abox(gx - 0.18, gx + 0.18, base, yu(zc) - 0.7, zc - 0.18, zc + 0.18, "SteelGreen", skip=('-y',))
        for zk in (4.6, 9.4):
            pal.beam((gx, yu(zc) - 3.0, zc), (gx, yu(zk) - 0.7, zk), 0.16, "SteelGreen")
    for z in (6.0, 10.0, 14.0, 18.0):
        pal.beam((0, yu(z) - 0.12, z), (RW, yu(z) - 0.12, z), 0.22, "SteelGreen")
    for x in ((3.0,) if end else (3.0, 9.0)):
        for z in (8.0, 14.0):
            pal.abox(x - 0.3, x + 0.3, yu(z) - 0.72, yu(z) - 0.35, z - 0.6, z + 0.6, "Lamp")

    profile = [(0, 0), (0, P), (WALK, P), (top_z, top_y), (BACK, top_y), (BACK, 0)]
    m.col.prism(profile, 0, W)
    m.col.prism([(zr0, yu(zr0) - 0.2), (zr0, yu(zr0) + 0.5), (zr1, yu(zr1) + 0.5), (zr1, yu(zr1) - 0.2)], 0, RW)

    if end:
        # End wall, then a brick clock tower standing beside the stand.
        steps = [(0.02, WALK, P + 1.1)]
        steps += [(WALK + k * T, WALK + (k + 1) * T, P + (k + 1) * R + 1.0) for k in range(N)]
        steps += [(top_z, BACK - 0.02, yu(top_z) - 0.02)]
        end_wall(m, 5.4, 6.0, steps)
        cx0, cx1, cz0, cz1, ch = 6.0, 9.6, 9.0, 20.9, 29.0
        pal.abox(cx0, cx1, 0, ch, cz0, cz1, "Brick", skip=('-y', '+y'))
        pal.abox(cx0 - 0.2, cx1 + 0.2, ch, ch + 0.6, cz0 - 0.2, cz1 + 0.2, "Concrete")
        apex = (7.8, ch + 4.2, (cz0 + cz1) / 2)
        c = [(cx0 - 0.2, ch + 0.6, cz0 - 0.2), (cx1 + 0.2, ch + 0.6, cz0 - 0.2), (cx1 + 0.2, ch + 0.6, cz1 + 0.2), (cx0 - 0.2, ch + 0.6, cz1 + 0.2)]
        mid = V(((cx0 + cx1) / 2, ch + 0.6, (cz0 + cz1) / 2))
        for i in range(4):
            a, b = V(c[i]), V(c[(i + 1) % 4])
            pal.poly([a, b, apex], ((a + b) / 2 - mid) + V((0, 1.5, 0)), "SteelGreen")
        clock_y = ch - 2.6
        pal.face_z(cz0 - 0.01, 6.6, 9.0, clock_y - 1.2, clock_y + 1.2, -1, "ClockFace")
        pal.abox(6.5, 9.1, clock_y - 1.3, clock_y + 1.3, cz0 - 0.03, cz0 - 0.005, "SteelGreen", skip=('-z', '+z'))
        pal.beam((7.8, clock_y, cz0 - 0.06), (7.8, clock_y + 0.95, cz0 - 0.06), 0.09, "Mullion", t2=0.04)
        pal.beam((7.8, clock_y, cz0 - 0.07), (8.42, clock_y - 0.36, cz0 - 0.07), 0.12, "Mullion", t2=0.04)
        pal.face_x(cx1 + 0.01, clock_y - 1.2, clock_y + 1.2, 13.75, 16.15, 1, "ClockFace")
        pal.beam((cx1 + 0.05, clock_y, 14.95), (cx1 + 0.05, clock_y + 0.95, 14.95), 0.09, "Mullion", t2=0.04)
        pal.beam((cx1 + 0.06, clock_y, 14.95), (cx1 + 0.06, clock_y - 0.4, 15.5), 0.12, "Mullion", t2=0.04)
        pal.face_z(cz0 - 0.01, 7.4, 8.2, 4.0, 19.0, -1, "Glass")
        pal.face_z(cz0 - 0.01, 7.2, 8.4, 0, 2.6, -1, "Opening")
        m.col.box_solid(cx0, cx1, 0, ch + 0.6, cz0, cz1)
    return m


STYLES = (("Main", build_main), ("Scaffold", build_scaffold), ("Terrace", build_terrace))


# ------------------------------------------------------------------ Blender objects
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
        if isinstance(data, bpy.types.Mesh) and data.users == 0:
            bpy.data.meshes.remove(data)


def to_object(name, part, mat, collection, parent, triangulate=False):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in part.v], [], part.f)
    uvl = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        poly.use_smooth = part.smooth[poly.index]
        for li in poly.loop_indices:
            uvl.data[li].uv = part.uv[me.loops[li].vertex_index]
    if triangulate:
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
        bm.to_mesh(me)
        bm.free()
    if mat:
        me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    collection.objects.link(ob)
    ob.parent = parent
    return ob


def hex_rgb(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def make_palette_images(out_dir):
    size = GRID * CELL
    imgs = {}
    os.makedirs(out_dir, exist_ok=True)
    for kind in ("Albedo", "MetallicSmoothness", "Emission"):
        name = "Grandstand_" + kind
        old = bpy.data.images.get(name)
        if old:
            bpy.data.images.remove(old)
        img = bpy.data.images.new(name, size, size, alpha=(kind == "MetallicSmoothness"), float_buffer=False)
        # colour space / alpha first: changing them later regenerates (blanks) the buffer
        img.colorspace_settings.name = "Non-Color" if kind == "MetallicSmoothness" else "sRGB"
        if kind == "MetallicSmoothness":
            img.alpha_mode = 'CHANNEL_PACKED'
        px = [0.0] * (size * size * 4)
        for i, role in enumerate(ROLES):
            alb, met, smooth, emi = PALETTE[role]
            if kind == "Albedo":
                c = (*hex_rgb(alb), 1.0)
            elif kind == "MetallicSmoothness":
                c = (met, 0.0, 0.0, smooth)
            else:
                c = (*(hex_rgb(emi) if emi else (0, 0, 0)), 1.0)
            col, row = i % GRID, i // GRID
            y0 = size - (row + 1) * CELL  # image rows start at the bottom
            for y in range(y0, y0 + CELL):
                base = (y * size + col * CELL) * 4
                px[base:base + CELL * 4] = c * CELL
        img.pixels = px
        path = os.path.join(out_dir, name + ".png")
        img.filepath_raw = path
        img.file_format = 'PNG'
        img.save()
        img.source = 'FILE'
        img.filepath = path
        img.reload()
        imgs[kind] = img
    return imgs


def node_material(name, build):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (500, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (150, 0)
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    build(nt, bsdf)
    m.use_backface_culling = True
    return m


def image_node(nt, img, y, closest=True):
    n = nt.nodes.new("ShaderNodeTexImage")
    n.image = img
    n.interpolation = 'Closest' if closest else 'Linear'
    n.location = (-600, y)
    return n


def make_palette_material(imgs, emission=0.08):
    def build(nt, bsdf):
        a = image_node(nt, imgs["Albedo"], 300)
        nt.links.new(a.outputs["Color"], bsdf.inputs["Base Color"])
        ms = image_node(nt, imgs["MetallicSmoothness"], 0)
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(ms.outputs["Color"], sep.inputs["Color"])
        nt.links.new(sep.outputs["Red"], bsdf.inputs["Metallic"])
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation = 'SUBTRACT'
        inv.inputs[0].default_value = 1.0
        nt.links.new(ms.outputs["Alpha"], inv.inputs[1])
        nt.links.new(inv.outputs["Value"], bsdf.inputs["Roughness"])
        e = image_node(nt, imgs["Emission"], -320)
        nt.links.new(e.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = emission
    return node_material("Grandstand", build)


def make_flat_material(name, rgb):
    def build(nt, bsdf):
        bsdf.inputs["Base Color"].default_value = (*rgb, 1)
        bsdf.inputs["Roughness"].default_value = 0.8
    return node_material(name, build)


def build_kit(collection, palette_mat, crowd_mat, ads_mat, col_mat):
    """Builds every module as an empty with its child meshes. Returns the empties."""
    # The preview leaves the kit out of the view layer; it has to be in it to hide and select.
    for lc in bpy.context.view_layer.layer_collection.children:
        if lc.collection == collection:
            lc.exclude = False
    clear_collection(collection)
    roots = []
    for i, (style, fn) in enumerate(STYLES):
        for end in (False, True):
            m = fn(end)
            root = bpy.data.objects.new(m.name, None)
            root.empty_display_type = 'ARROWS'
            root.empty_display_size = 3
            # Laid out for viewing: each style's end piece sits where it would in the game.
            root.location = (-40.0 * i - (BAY if end else 0.0), 0, 0)
            collection.objects.link(root)
            to_object(m.name + "_Palette", m.pal, palette_mat, collection, root)
            to_object(m.name + "_Crowd", m.crowd, crowd_mat, collection, root)
            to_object(m.name + "_Ads", m.ads, ads_mat, collection, root)
            c = to_object(m.name + "_Collider", m.col, col_mat, collection, root, triangulate=True)
            c.display_type = 'WIRE'
            c.hide_render = True
            c.hide_set(True)
            roots.append(root)
    return roots


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def export_fbx(roots, path):
    bpy.ops.object.select_all(action='DESELECT')
    hidden = []
    for r in roots:
        r.select_set(True)
        for ch in r.children:
            if ch.hide_get():
                hidden.append(ch)
                ch.hide_set(False)
            ch.select_set(True)
    bpy.context.view_layer.objects.active = roots[0]
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'},
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
            axis_forward='-Z', axis_up='Y', bake_space_transform=True,
            use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False,
            add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)
    finally:
        for ch in hidden:
            ch.hide_set(True)
    return path


def rebuild(art_dir, preview_textures=None):
    """Builds the kit, writes the palette and the FBX into the game's Assets/Models/Grandstands."""
    unity_dir = os.path.normpath(os.path.join(art_dir, "..", "..", "Assets", "Models", "Grandstands"))
    imgs = make_palette_images(os.path.join(unity_dir, "Textures"))
    palette_mat = make_palette_material(imgs)
    tex = preview_textures or os.path.join(art_dir, "preview_textures")

    def textured(name, file, closest):
        path = os.path.join(tex, file)
        if not os.path.exists(path):
            return make_flat_material(name, (0.3, 0.3, 0.35))
        img = bpy.data.images.load(path, check_existing=True)

        def build(nt, bsdf):
            n = image_node(nt, img, 0, closest)
            nt.links.new(n.outputs["Color"], bsdf.inputs["Base Color"])
            bsdf.inputs["Roughness"].default_value = 0.85
        return node_material(name, build)

    # Previews only: the game uses its own crowd and advert materials on these meshes.
    crowd_mat = textured("Crowd (preview)", "crowd.png", False)
    ads_mat = textured("Adverts (preview)", "adverts.png", False)
    col_mat = make_flat_material("Collider", (0.9, 0.2, 0.9))
    roots = build_kit(coll("Grandstands"), palette_mat, crowd_mat, ads_mat, col_mat)
    export_fbx(roots, os.path.join(unity_dir, "Grandstands.fbx"))
    stats = {}
    for r in roots:
        stats[r.name] = {ch.name[len(r.name) + 1:]: tri_count(ch) for ch in r.children}
    return roots, stats


if __name__ == "__main__":
    here = bpy.path.abspath("//") if bpy.data.filepath else ART_DIR  # noqa: F821 (set by a runner)
    rebuild(os.path.normpath(here))
