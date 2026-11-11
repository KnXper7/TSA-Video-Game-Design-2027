"""
Brings an AI-generated GLB model (for example from Higgsfield / Meshy) into the Unity project.

  Blender --background --python Tools/blender/import_generated.py -- <file.glb> <Name> [longest_side_in_metres] [--square]

Writes Assets/_Project/Models/People/<Name>.fbx, its textures, and <Name>.json (which texture goes on which
part), in the same layout as build_people.py, so the scene builder treats it like any other character.
Rigged models keep their skeleton. The model is stood on the ground, facing -Y, and scaled to the height given.
"""
import bpy, json, os, sys
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "_Project", "Models", "People")
TEXTURES = os.path.join(OUT, "Textures")

args = sys.argv[sys.argv.index("--") + 1:]
source, name = args[0], args[1]
height = float(args[2]) if len(args) > 2 and not args[2].startswith('--') else None

for ob in list(bpy.data.objects):
    bpy.data.objects.remove(ob, do_unlink=True)
bpy.ops.import_scene.gltf(filepath=source)
objects = list(bpy.context.scene.objects)
meshes = [ob for ob in objects if ob.type == 'MESH']
bpy.context.view_layer.update()

def bounds():
    pts = [ob.matrix_world @ Vector(c) for ob in meshes for c in ob.bound_box]
    return Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])

roots = [ob for ob in objects if ob.parent is None]
if "--square" in args:
    # Props generated from a three-quarter photo often come in turned a little. Turn the model about the vertical
    # so its widest horizontal direction lies along X (a flat panel or target then faces straight down Y).
    import math
    pts = [ob.matrix_world @ v.co for ob in meshes for v in ob.data.vertices]
    mx = sum(p.x for p in pts) / len(pts); my = sum(p.y for p in pts) / len(pts)
    sxx = sum((p.x - mx) ** 2 for p in pts); syy = sum((p.y - my) ** 2 for p in pts); sxy = sum((p.x - mx) * (p.y - my) for p in pts)
    angle = 0.5 * math.atan2(2 * sxy, sxx - syy)
    from mathutils import Matrix
    turn = Matrix.Rotation(-angle, 4, 'Z')
    for ob in roots:
        ob.matrix_world = turn @ ob.matrix_world
    bpy.context.view_layer.update()
    print(f"SQUARED by {math.degrees(-angle):.1f} degrees")
lo, hi = bounds()
if height:
    scale = height / max(hi - lo)      # the longest side becomes this long
    for ob in roots:
        ob.scale *= scale
        ob.location *= scale
    bpy.context.view_layer.update()
    lo, hi = bounds()
for ob in roots:
    ob.location.z -= lo.z
    ob.location.x -= (lo.x + hi.x) / 2
    ob.location.y -= (lo.y + hi.y) / 2
bpy.context.view_layer.update()

os.makedirs(TEXTURES, exist_ok=True)

def save(image, suffix):
    if image is None:
        return ""
    filename = f"{name}_{suffix}.png"
    copy = image.copy()
    width, h = copy.size
    if max(width, h) > 2048:
        k = 2048 / max(width, h)
        copy.scale(int(width * k), int(h * k))
    copy.filepath_raw = os.path.join(TEXTURES, filename)
    copy.file_format = 'PNG'
    copy.save()
    return filename

def linked_image(node_input):
    if not node_input.is_linked:
        return None
    node = node_input.links[0].from_node
    seen = 0
    while node is not None and node.type != 'TEX_IMAGE' and seen < 6:
        linked = [i for i in node.inputs if i.is_linked]
        node = linked[0].links[0].from_node if linked else None
        seen += 1
    return node.image if node is not None and node.type == 'TEX_IMAGE' else None

parts = []
for index, ob in enumerate(meshes):
    ob.name = f"Part{index + 1}"
    info = {"name": ob.name, "kind": "skin", "texture": "", "normal": "", "color": [0.8, 0.8, 0.8]}
    mat = ob.active_material
    if mat and mat.node_tree:
        shader = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if shader:
            info["texture"] = save(linked_image(shader.inputs["Base Color"]), f"{index + 1}_color")
            info["normal"] = save(linked_image(shader.inputs["Normal"]), f"{index + 1}_normal")
    parts.append(info)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'},
                         apply_scale_options='FBX_SCALE_UNITS', add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE')
with open(os.path.join(OUT, name + ".json"), "w") as f:
    json.dump({"parts": parts}, f, indent=1)

bones = [b.name for ob in objects if ob.type == 'ARMATURE' for b in ob.data.bones]
tris = sum(len(p.vertices) - 2 for ob in meshes for p in ob.data.polygons)
lo, hi = bounds()
print(f"IMPORTED {name}: {len(meshes)} meshes, {tris} triangles, size {tuple(round(v, 2) for v in (hi - lo))}, bones: {bones}")
