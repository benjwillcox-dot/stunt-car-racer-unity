# Preview stage: camera, lights, ground. Lives in its own collection and is never exported.
import bpy
import math
from mathutils import Vector


def look_at(ob, target):
    d = Vector(target) - ob.location
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


def setup(coll_name="Preview"):
    sc = bpy.context.scene
    c = bpy.data.collections.get(coll_name)
    if c is None:
        c = bpy.data.collections.new(coll_name)
        sc.collection.children.link(c)
    for ob in list(c.objects):
        bpy.data.objects.remove(ob, do_unlink=True)

    # keep the startup cube / light out of the renders without deleting them
    for name in ("Cube", "Light"):
        ob = bpy.data.objects.get(name)
        if ob:
            ob.hide_render = True
            ob.hide_set(True)

    cam_data = bpy.data.cameras.get("PreviewCam") or bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    c.objects.link(cam)
    sc.camera = cam

    sun_data = bpy.data.lights.get("PreviewSun") or bpy.data.lights.new("PreviewSun", 'SUN')
    sun_data.energy = 3.5
    sun_data.angle = math.radians(3)
    sun = bpy.data.objects.new("PreviewSun", sun_data)
    sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
    c.objects.link(sun)

    fill_data = bpy.data.lights.get("PreviewFill") or bpy.data.lights.new("PreviewFill", 'AREA')
    fill_data.energy = 900
    fill_data.size = 6
    fill = bpy.data.objects.new("PreviewFill", fill_data)
    fill.location = (6, 4, 4)
    look_at(fill, (0, 0, 0.5))
    c.objects.link(fill)

    rim_data = bpy.data.lights.get("PreviewRim") or bpy.data.lights.new("PreviewRim", 'AREA')
    rim_data.energy = 700
    rim_data.size = 5
    rim = bpy.data.objects.new("PreviewRim", rim_data)
    rim.location = (-5, 5, 3)
    look_at(rim, (0, 0, 0.5))
    c.objects.link(rim)

    me = bpy.data.meshes.get("PreviewGround")
    if me is None:
        me = bpy.data.meshes.new("PreviewGround")
        s = 40
        me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    gm = bpy.data.materials.get("PreviewGround") or bpy.data.materials.new("PreviewGround")
    try:
        gm.use_nodes = True
    except Exception:
        pass
    b = gm.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (0.32, 0.33, 0.35, 1)
    b.inputs["Roughness"].default_value = 0.6
    if not me.materials:
        me.materials.append(gm)
    g = bpy.data.objects.new("PreviewGround", me)
    c.objects.link(g)

    world = sc.world or bpy.data.worlds.new("World")
    sc.world = world
    try:
        world.use_nodes = True
    except Exception:
        pass
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.55, 0.62, 0.72, 1)
    bg.inputs["Strength"].default_value = 0.9

    r = sc.render
    r.engine = 'BLENDER_EEVEE'
    r.resolution_x, r.resolution_y = 1400, 800
    r.resolution_percentage = 100
    r.film_transparent = False
    try:
        sc.eevee.taa_render_samples = 48
        sc.eevee.use_shadows = True
    except Exception:
        pass
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Medium High Contrast'
    return cam


def studio_world(strength=0.85):
    """Gradient sky with a bright horizon band so chrome and paint have something to reflect."""
    world = bpy.context.scene.world
    nt = world.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputWorld")
    bg = nt.nodes.new("ShaderNodeBackground")
    bg.inputs["Strength"].default_value = strength
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = -1.0
    mr.inputs["From Max"].default_value = 1.0
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0.0, (0.06, 0.06, 0.07, 1)
    els[1].position, els[1].color = 1.0, (0.22, 0.38, 0.75, 1)
    for pos, col in ((0.47, (0.30, 0.30, 0.32, 1)), (0.505, (1.6, 1.6, 1.6, 1)), (0.58, (0.70, 0.78, 0.92, 1))):
        e = els.new(pos)
        e.color = col
    nt.links.new(tc.outputs["Generated"], sep.inputs["Vector"])
    nt.links.new(sep.outputs["Z"], mr.inputs["Value"])
    nt.links.new(mr.outputs["Result"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    nt.links.new(bg.outputs["Background"], out.inputs["Surface"])


def shoot(path, loc, target=(0, 0, 0.55), lens=50):
    sc = bpy.context.scene
    cam = sc.camera
    cam.data.lens = lens
    cam.location = Vector(loc)
    look_at(cam, target)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path
