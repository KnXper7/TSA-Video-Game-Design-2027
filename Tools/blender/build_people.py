"""
Builds the game's people with MPFB (MakeHuman for Blender): realistic, rigged, clothed characters.

  /Applications/Blender.app/Contents/MacOS/Blender --background --python Tools/blender/build_people.py
  ... -- Engineer Botanist      # only these characters
  ... -- --preview              # also render Tools/blender/previews/People.png

Needs the MPFB add-on (Blender > Preferences > Get Extensions > "MPFB") and the "MakeHuman system assets"
pack (skins, hair, clothes), both free and CC0. Each character below is a recipe: change the numbers, hair
or clothes and run again. Output: Assets/_Project/Models/People/<Name>.fbx, its textures, and <Name>.json
listing which texture goes on which part (the Unity scene builder reads that to make the materials).
"""
import bpy, glob, json, os, shutil, sys
from mathutils import Vector

from bl_ext.blender_org.mpfb.services.humanservice import HumanService
from bl_ext.blender_org.mpfb.services.targetservice import TargetService
from bl_ext.blender_org.mpfb.services.locationservice import LocationService

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "_Project", "Models", "People")
TEXTURES = os.path.join(OUT, "Textures")
PREVIEWS = os.path.join(ROOT, "Tools", "blender", "previews")
DATA = LocationService.get_user_data()

# gender: 0 female .. 1 male.  age: 0.5 is about 25, 0.75 about 55, 1.0 about 90.  The rest are 0..1 with 0.5 average.
PEOPLE = {
    "Commander":      dict(gender=1.0, age=0.56, muscle=0.62, weight=0.5, height=0.6, race=(0.2, 0.6, 0.2),
                           skin="young_caucasian_male_special_suit", hair="short02", eyes="brown", brows="eyebrow003",
                           clothes=["shoes04"]),      # the "special suit" skin is a close-fitting flight suit painted on the body
    "Pilot":          dict(gender=0.0, age=0.55, muscle=0.6, weight=0.45, height=0.55, race=(0.15, 0.7, 0.15),
                           skin="young_caucasian_female", hair="braid01", eyes="green", brows="eyebrow006",
                           clothes=["male_worksuit01", "shoes04"]),
    "FlightDirector": dict(gender=1.0, age=0.74, muscle=0.45, weight=0.58, height=0.55, race=(0.1, 0.8, 0.1),
                           skin="middleage_caucasian_male", hair="short04", eyes="grey", brows="eyebrow001",
                           clothes=["male_elegantsuit01", "shoes02"]),
    "Controller":     dict(gender=0.0, age=0.6, muscle=0.5, weight=0.5, height=0.5, race=(0.7, 0.2, 0.1),
                           skin="young_asian_female", hair="bob01", eyes="brown", brows="eyebrow008",
                           clothes=["female_elegantsuit01", "shoes01"]),
    "Technician":     dict(gender=0.0, age=0.58, muscle=0.5, weight=0.55, height=0.45, race=(0.1, 0.2, 0.7),
                           skin="young_african_female", hair="afro01", eyes="brown", brows="eyebrow005",
                           clothes=["female_casualsuit01", "shoes03"]),
    "OperatorA":      dict(gender=1.0, age=0.58, muscle=0.5, weight=0.5, height=0.5, race=(0.7, 0.2, 0.1),
                           skin="young_asian_male", hair="short03", eyes="brown", brows="eyebrow002",
                           clothes=["male_casualsuit02", "shoes02"]),
    "OperatorB":      dict(gender=1.0, age=0.64, muscle=0.5, weight=0.62, height=0.55, race=(0.1, 0.2, 0.7),
                           skin="middleage_african_male", hair="short01", eyes="brown", brows="eyebrow004",
                           clothes=["male_casualsuit04", "shoes02"]),
    "OperatorC":      dict(gender=0.0, age=0.57, muscle=0.45, weight=0.45, height=0.5, race=(0.1, 0.8, 0.1),
                           skin="young_caucasian_female", hair="ponytail01", eyes="green", brows="eyebrow007",
                           clothes=["female_casualsuit01", "shoes01"]),
    "Engineer":       dict(gender=1.0, age=0.6, muscle=0.55, weight=0.55, height=0.5, race=(0.3, 0.35, 0.35),
                           skin="young_caucasian_male2", hair="short04", eyes="brownlight", brows="eyebrow002",
                           clothes=["male_worksuit01", "shoes04"]),
    "Botanist":       dict(gender=0.0, age=0.62, muscle=0.45, weight=0.5, height=0.48, race=(0.6, 0.3, 0.1),
                           skin="middleage_asian_female", hair="long01", eyes="brown", brows="eyebrow009",
                           clothes=["female_casualsuit01", "shoes03"]),
    "Quartermaster":  dict(gender=1.0, age=0.85, muscle=0.4, weight=0.6, height=0.45, race=(0.1, 0.8, 0.1),
                           skin="old_caucasian_male", hair="short01", eyes="blue", brows="eyebrow001",
                           clothes=["male_casualsuit05", "shoes05"]),
}


def find(subdir, name, ext):
    matches = glob.glob(os.path.join(DATA, subdir, name, "*." + ext))
    if not matches:
        raise IOError(f"No {ext} for {subdir}/{name}. Is the MakeHuman system assets pack installed in {DATA}?")
    return matches[0]


def read_mhmat(path):
    """The bits of a MakeHuman material file the game needs: its textures and base colour."""
    info = {"texture": None, "normal": None, "color": [0.8, 0.8, 0.8]}
    for line in open(path, encoding="utf-8", errors="ignore"):
        parts = line.split()
        if len(parts) < 2:
            continue
        if parts[0] == "diffuseTexture":
            info["texture"] = os.path.normpath(os.path.join(os.path.dirname(path), parts[1]))
        elif parts[0] == "normalmapTexture":
            info["normal"] = os.path.normpath(os.path.join(os.path.dirname(path), parts[1]))
        elif parts[0] == "diffuseColor" and len(parts) >= 4:
            info["color"] = [float(v) for v in parts[1:4]]
    return info


def copy_texture(path):
    """Copies a texture into the Unity project, shrunk to 2048 pixels at most to keep the project small."""
    if not path or not os.path.isfile(path):
        return None
    os.makedirs(TEXTURES, exist_ok=True)
    target = os.path.join(TEXTURES, os.path.basename(path))
    if not os.path.isfile(target):
        image = bpy.data.images.load(path)
        width, height = image.size
        if max(width, height) > 2048:
            scale = 2048 / max(width, height)
            image.scale(int(width * scale), int(height * scale))
            image.filepath_raw = target
            image.file_format = 'PNG'
            image.save()
        else:
            shutil.copyfile(path, target)
        bpy.data.images.remove(image)
    return os.path.basename(path)


def clear_scene():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)


def build(name, spec):
    before = set(bpy.data.objects)
    macro = TargetService.get_default_macro_info_dict()
    macro.update(gender=spec["gender"], age=spec["age"], muscle=spec["muscle"], weight=spec["weight"], height=spec["height"])
    macro["race"] = dict(zip(("asian", "caucasian", "african"), spec["race"]))
    # Feet are put on the ground at the end: doing it here would move the body away from the skeleton added next.
    body = HumanService.create_human(macro_detail_dict=macro, scale=0.1, detailed_helpers=False, extra_vertex_groups=True, feet_on_ground=False)
    body.name = "Body"
    rig = HumanService.add_builtin_rig(body, "mixamo_unity", import_weights=True)
    rig.name = "Rig"

    manifest = []

    def part(label, kind, subdir, asset, material=None):
        """Adds hair, clothes and so on, renames the new object and records its textures."""
        known = set(bpy.data.objects)
        HumanService.add_mhclo_asset(find(subdir, asset, "mhclo"), body, asset_type=kind, subdiv_levels=0, material_type="GAMEENGINE")
        new = [ob for ob in bpy.data.objects if ob not in known and ob.type == 'MESH']
        if not new:
            print(f"  ! {asset} did not load")
            return
        new[0].name = label
        info = read_mhmat(material or find(subdir, asset, "mhmat"))
        manifest.append({"name": label, "kind": kind.lower(), "texture": copy_texture(info["texture"]) or "", "normal": copy_texture(info["normal"]) or "", "color": info["color"]})

    part("Eyes", "Eyes", "eyes", "low-poly", os.path.join(DATA, "eyes", "materials", spec["eyes"] + ".mhmat"))
    part("Eyebrows", "Eyebrows", "eyebrows", spec["brows"])
    part("Eyelashes", "Eyelashes", "eyelashes", "eyelashes02")
    part("Teeth", "Teeth", "teeth", "teeth_base")
    part("Tongue", "Tongue", "tongue", "tongue01")
    part("Hair", "Hair", "hair", spec["hair"])
    for i, item in enumerate(spec["clothes"]):
        part("Shoes" if item.startswith("shoes") else f"Clothes{i + 1}", "Clothes", "clothes", item)

    skin = find("skins", spec["skin"], "mhmat")
    HumanService.set_character_skin(skin, body, skin_type="GAMEENGINE", material_instances=False)
    info = read_mhmat(skin)
    manifest.append({"name": "Body", "kind": "skin", "texture": copy_texture(info["texture"]) or "", "normal": copy_texture(info["normal"]) or "", "color": info["color"]})

    # Freeze the body shape into the mesh so modifiers (hidden skin under clothes) can be applied on export.
    if body.data.shape_keys:
        TargetService.bake_targets(body)

    objects = [ob for ob in bpy.data.objects if ob not in before]
    for ob in objects:
        if ob.type == 'MESH':
            for poly in ob.data.polygons:
                poly.use_smooth = True

    # Stand the whole character (skeleton and meshes together) on the ground.
    bpy.context.view_layer.update()
    lowest = min((ob.matrix_world @ Vector(c)).z for ob in objects if ob.type == 'MESH' for c in ob.bound_box)
    for ob in objects:
        if ob.parent is None:
            ob.location.z -= lowest
    bpy.context.view_layer.update()
    hips = rig.matrix_world @ rig.data.bones["mixamorig:Hips"].head_local
    print(f"  hips at {hips.z:.2f} m, lowest point was {lowest:.2f}, parents: {sorted({ob.parent.name if ob.parent else 'none' for ob in objects})}")
    return objects, manifest


def export(name, objects, manifest):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:
        ob.hide_set(False)
        ob.select_set(True)
    bpy.context.view_layer.objects.active = next(ob for ob in objects if ob.type == 'ARMATURE')
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={'ARMATURE', 'MESH'},
                             apply_scale_options='FBX_SCALE_UNITS', add_leaf_bones=False, bake_anim=False,
                             use_mesh_modifiers=True, mesh_smooth_type='FACE', use_armature_deform_only=False)
    with open(os.path.join(OUT, name + ".json"), "w") as f:
        json.dump({"parts": manifest}, f, indent=1)
    tris = sum(len(p.vertices) - 2 for ob in objects if ob.type == 'MESH' for p in ob.data.polygons)
    height = max((ob.matrix_world @ Vector(c)).z for ob in objects if ob.type == 'MESH' for c in ob.bound_box)
    print(f"  {name}.fbx  {tris} triangles, {height:.2f} m tall, parts: {', '.join(sorted(p['name'] for p in manifest))}")


def preview():
    """One picture of everyone standing in a row."""
    scene = bpy.context.scene
    meshes = [ob for ob in bpy.data.objects if ob.type == 'MESH']
    xs = [ob.matrix_world.translation.x for ob in bpy.data.objects if ob.type == 'ARMATURE']
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    width = max(xs) - min(xs) + 1.6
    cam.data.lens = 70
    cam.location = ((max(xs) + min(xs)) / 2, -width * 1.75, 1.15)
    cam.rotation_euler = (1.5708, 0, 0)
    scene.camera = cam
    scene.render.engine = 'BLENDER_WORKBENCH'
    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.color_type = 'TEXTURE'
    shading.show_shadows = True
    shading.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 1800, 700
    scene.world = scene.world or bpy.data.worlds.new("w")
    scene.world.color = (0.05, 0.06, 0.08)
    os.makedirs(PREVIEWS, exist_ok=True)
    scene.render.filepath = os.path.join(PREVIEWS, "People.png")
    bpy.ops.render.render(write_still=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    want_preview = "--preview" in args
    names = [a for a in args if not a.startswith("--")] or list(PEOPLE)
    clear_scene()
    for index, name in enumerate(names):
        print(f"[{name}]")
        objects, manifest = build(name, PEOPLE[name])
        export(name, objects, manifest)
        if want_preview:
            rig = next(ob for ob in objects if ob.type == 'ARMATURE')
            rig.location.x = index * 0.95
        else:
            for ob in objects:
                bpy.data.objects.remove(ob, do_unlink=True)
    if want_preview:
        bpy.context.view_layer.update()
        preview()
    print("Done.")


main()
