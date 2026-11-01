# Colour schemes as palette textures + one material per car, and FBX export for Unity.
# Every face's UVs point at the centre of its role's 32 px cell in an 8x8 palette, so a
# scheme is just three small textures: Albedo, MetallicSmoothness (R / A) and Emission.
import bpy
import os
import scr_lib as L

GRID, CELL = 8, 32

# role: (albedo sRGB hex, metallic, smoothness, emission sRGB hex or None)
SCHEMES = {
    "Yellow": {
        "PaintPrimary": ("FFB300", 0.15, 0.85, None), "PaintSecondary": ("0D0D0F", 0.10, 0.85, None),
        "Carbon": ("1C1E22", 0.25, 0.65, None), "Interior": ("232427", 0.0, 0.30, None),
        "Chrome": ("E6E8EB", 1.0, 0.92, None), "EngineBlock": ("2B2D31", 0.60, 0.50, None),
        "EngineAccent": ("C9CCD1", 1.0, 0.80, None), "Metal": ("44474D", 0.85, 0.50, None),
        "Cage": ("121214", 0.30, 0.60, None), "Rim": ("111113", 0.50, 0.75, None),
        "RimLip": ("DADDE1", 1.0, 0.90, None), "Tire": ("171717", 0.0, 0.20, None),
        "Accent": ("FFB300", 0.10, 0.80, None), "BrakeDisc": ("8C9096", 1.0, 0.55, None),
        "Grille": ("0A0A0B", 0.20, 0.25, None), "LightFront": ("F5F8FF", 0.0, 0.90, "FFFFFF"),
        "LightRear": ("B00000", 0.0, 0.90, "FF1E1E"), "Screen": ("1A1200", 0.0, 0.90, "FFB000"),
        "Suit": ("141416", 0.0, 0.30, None), "Gloves": ("FFB300", 0.0, 0.30, None),
        "Helmet": ("FFB300", 0.15, 0.90, None), "Visor": ("B8862B", 1.0, 0.95, None),
        "Seat": ("1E1F22", 0.0, 0.35, None), "DecalBase": ("F2F2F2", 0.0, 0.70, None),
        "DecalText": ("0D0D0F", 0.0, 0.70, None),
    },
    "White": {
        "PaintPrimary": ("F2F3F0", 0.20, 0.88, None), "PaintSecondary": ("00A39A", 0.35, 0.85, None),
        "Carbon": ("1A1C1F", 0.25, 0.65, None), "Interior": ("232427", 0.0, 0.30, None),
        "Chrome": ("A7ACB2", 1.0, 0.80, None), "EngineBlock": ("1F2023", 0.60, 0.50, None),
        "EngineAccent": ("00A39A", 0.80, 0.75, None), "Metal": ("3B3E44", 0.85, 0.50, None),
        "Cage": ("2A2D32", 0.60, 0.60, None), "Rim": ("EDEDE9", 0.20, 0.75, None),
        "RimLip": ("00A39A", 0.80, 0.85, None), "Tire": ("171717", 0.0, 0.20, None),
        "Accent": ("00C2B5", 0.20, 0.80, None), "BrakeDisc": ("8C9096", 1.0, 0.55, None),
        "Grille": ("0A0A0B", 0.20, 0.25, None), "LightFront": ("F5F8FF", 0.0, 0.90, "FFFFFF"),
        "LightRear": ("B00000", 0.0, 0.90, "FF1E1E"), "Screen": ("001A18", 0.0, 0.90, "00E5D4"),
        "Suit": ("009188", 0.0, 0.30, None), "Gloves": ("F2F3F0", 0.0, 0.30, None),
        "Helmet": ("F2F3F0", 0.20, 0.90, None), "Visor": ("1F8FA8", 1.0, 0.95, None),
        "Seat": ("1E1F22", 0.0, 0.35, None), "DecalBase": ("00A39A", 0.0, 0.70, None),
        "DecalText": ("F2F3F0", 0.0, 0.70, None),
    },
}


def hex_rgb(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def cell_uv(role):
    i = L.ROLES.index(role)
    col, row = i % GRID, i // GRID
    return ((col + 0.5) / GRID, 1 - (row + 0.5) / GRID)


def make_palette_images(scheme, out_dir):
    size = GRID * CELL
    pal = SCHEMES[scheme]
    imgs = {}
    for kind in ("Albedo", "MetallicSmoothness", "Emission"):
        name = "StuntCar_%s_%s" % (scheme, kind)
        old = bpy.data.images.get(name)
        if old:
            bpy.data.images.remove(old)
        img = bpy.data.images.new(name, size, size, alpha=(kind == "MetallicSmoothness"), float_buffer=False)
        # colour space / alpha first: changing them later regenerates (blanks) the buffer
        img.colorspace_settings.name = "Non-Color" if kind == "MetallicSmoothness" else "sRGB"
        if kind == "MetallicSmoothness":
            img.alpha_mode = 'CHANNEL_PACKED'
        px = [0.0] * (size * size * 4)
        for role in L.ROLES:
            alb, met, smooth, emi = pal[role]
            if kind == "Albedo":
                c = (*hex_rgb(alb), 1.0)
            elif kind == "MetallicSmoothness":
                c = (met, 0.0, 0.0, smooth)
            else:
                c = (*(hex_rgb(emi) if emi else (0, 0, 0)), 1.0)
            i = L.ROLES.index(role)
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
        # point the datablock at the saved file so the .blend references it
        img.source = 'FILE'
        img.filepath = path
        img.reload()
        imgs[kind] = img
    return imgs


def make_material(scheme, imgs):
    name = "StuntCar_" + scheme
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
    uv = nt.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "UVMap"
    uv.location = (-900, 0)

    def tex(img, y):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = img
        n.interpolation = 'Closest'
        n.location = (-600, y)
        nt.links.new(uv.outputs["UV"], n.inputs["Vector"])
        return n
    a = tex(imgs["Albedo"], 300)
    nt.links.new(a.outputs["Color"], bsdf.inputs["Base Color"])
    ms = tex(imgs["MetallicSmoothness"], 0)
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    sep.location = (-300, 50)
    nt.links.new(ms.outputs["Color"], sep.inputs["Color"])
    nt.links.new(sep.outputs["Red"], bsdf.inputs["Metallic"])
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    inv.location = (-300, -120)
    nt.links.new(ms.outputs["Alpha"], inv.inputs[1])
    nt.links.new(inv.outputs["Value"], bsdf.inputs["Roughness"])
    e = tex(imgs["Emission"], -320)
    nt.links.new(e.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 4.0
    m.use_backface_culling = True
    return m


def bake_role_uvs(ob):
    """Point every face's UVs at its role cell, then collapse to a single material slot."""
    me = ob.data
    roles = [ms.name[len("ROLE_"):] if ms and ms.name.startswith("ROLE_") else None for ms in me.materials]
    uvl = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        u, v = cell_uv(roles[poly.material_index])
        for li in poly.loop_indices:
            uvl.data[li].uv = (u, v)
    for poly in me.polygons:
        poly.material_index = 0
    me.materials.clear()


def apply_scheme(objs, mat):
    for ob in objs:
        if ob.type != 'MESH':
            continue
        ob.data.materials.clear()
        ob.data.materials.append(mat)


def duplicate_car(src_root, prefix_from, prefix_to, root_name, coll_name, offset):
    col = L.coll(coll_name)
    L.clear_collection(col)
    root = bpy.data.objects.new(root_name, None)
    root.empty_display_type = src_root.empty_display_type
    root.empty_display_size = src_root.empty_display_size
    root.location = offset
    col.objects.link(root)
    out = []
    for ch in src_root.children:
        ob = ch.copy()
        ob.data = ch.data.copy()
        ob.name = prefix_to + ch.name[len(prefix_from):]
        ob.data.name = ob.name
        col.objects.link(ob)
        ob.parent = root
        out.append(ob)
    return root, out


def export_fbx(root, prefix, path):
    """Export one car with clean child names (prefix stripped) and the root at the origin."""
    saved_loc = root.location.copy()
    root.location = (0, 0, 0)
    renamed = []
    for ch in root.children:
        old = ch.name
        ch.name = old[len(prefix):]
        renamed.append((ch, old))
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for ch in root.children:
        ch.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.context.view_layer.update()
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'},
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
            axis_forward='-Z', axis_up='Y', bake_space_transform=True,
            use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False,
            add_leaf_bones=False, bake_anim=False, path_mode='COPY', embed_textures=True)
    finally:
        for ch, old in renamed:
            ch.name = old
        root.location = saved_loc
    return path
