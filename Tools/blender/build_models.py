"""
Builds every 3D model and texture the game uses and saves them into the Unity project.

  /Applications/Blender.app/Contents/MacOS/Blender --background --python Tools/blender/build_models.py

Everything is made from code, so a model can be changed by editing its function below and re-running.
Models are built Z-up, facing -Y (Blender's front), which arrives in Unity as Y-up, facing +Z.
Material names matter: the Unity scene builder swaps each one for the project material with the same name.
"""
import bpy, bmesh, math, os, random, sys
import numpy as np
from mathutils import Vector, Matrix, noise

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MODELS = os.path.join(ROOT, "Assets", "_Project", "Models")
TEXTURES = os.path.join(ROOT, "Assets", "_Project", "Textures")
FONT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Fonts", "michroma_Michroma-Regular.ttf")
TITLE = "THE GATEWAY"

TAU = math.pi * 2


# ----------------------------------------------------------------------------------------------
# Mesh building helpers
# ----------------------------------------------------------------------------------------------

# Preview colours only: in the game each name is swapped for the Unity material of the same name.
COLORS = {
    "Rocket_White": (0.9, 0.9, 0.92), "Rocket_Dark": (0.06, 0.06, 0.07), "Rocket_Accent": (0.9, 0.35, 0.08),
    "Rocket_Metal": (0.55, 0.57, 0.6), "Rocket_Glass": (0.3, 0.7, 1.0), "Rocket_Nozzle": (0.2, 0.2, 0.22),
    "Rocket_EngineGlow": (1.0, 0.6, 0.2), "Rocket_Solar": (0.05, 0.1, 0.35),
    "Portal_Metal": (0.12, 0.14, 0.2), "Portal_Glow": (0.3, 0.9, 1.0), "Portal_Trim": (0.3, 0.33, 0.42),
    "GripBall_Metal": (0.75, 0.72, 0.6), "Asteroid_Rock": (0.4, 0.36, 0.32),
    "Person_Skin": (0.8, 0.6, 0.48), "Person_Cloth": (0.9, 0.5, 0.15), "Person_Pants": (0.9, 0.5, 0.15),
    "Person_Eye": (0.03, 0.03, 0.04), "Person_Shoes": (0.1, 0.1, 0.1), "Person_Hair": (0.15, 0.1, 0.07), "Person_Visor": (0.5, 0.8, 1.0), "Person_SuitTrim": (0.85, 0.85, 0.88),
    "Tower_Steel": (0.5, 0.2, 0.15), "Tower_Light": (1.0, 0.95, 0.7), "Tower_Concrete": (0.5, 0.5, 0.48), "Tower_Scorch": (0.1, 0.09, 0.08),
    "Tree_Dead": (0.3, 0.24, 0.18), "Crate_Body": (0.8, 0.8, 0.78), "Crate_Trim": (0.2, 0.2, 0.22), "Crate_Light": (0.3, 0.9, 1.0),
    "Tree_Bark": (0.32, 0.25, 0.2), "Tree_Leaves": (0.2, 0.45, 0.15), "Desk_Top": (0.12, 0.13, 0.15), "Desk_Frame": (0.3, 0.32, 0.36),
    "Desk_Keys": (0.05, 0.05, 0.06), "Desk_Light": (0.3, 0.8, 1.0), "Monitor_Bezel": (0.04, 0.04, 0.05), "Monitor_Screen": (0.1, 0.4, 0.7),
    "Glove_Suit": (0.88, 0.88, 0.86), "Glove_Grip": (0.12, 0.12, 0.14), "Chair_Fabric": (0.1, 0.12, 0.16), "Chair_Frame": (0.25, 0.26, 0.28),
    "Title_Face": (0.95, 0.95, 1.0), "Title_Side": (0.3, 0.6, 0.9),
}
PREVIEW = "--preview" in sys.argv
PREVIEWS = os.path.join(ROOT, "Tools", "blender", "previews")


def material(name, color=None):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.diffuse_color = (*(color or COLORS.get(name, (0.8, 0.8, 0.8))), 1.0)
    return m


def render_preview(filename, objects, view=(1.0, -1.6, 0.7)):
    """Renders the objects to Tools/blender/previews/<filename>.png so a model can be checked without opening Unity."""
    scene = bpy.context.scene
    pts = [ob.matrix_world @ Vector(c) for ob in objects if ob.type == 'MESH' for c in ob.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    centre, radius = (lo + hi) / 2, (hi - lo).length / 2
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    direction = Vector(view).normalized()
    cam.data.lens = 60
    cam.data.clip_end = 5000
    cam.location = centre + direction * radius * 3.6
    cam.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    scene.render.engine = 'BLENDER_WORKBENCH'
    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.color_type = 'MATERIAL'
    shading.show_cavity = True
    shading.show_shadows = True
    scene.display.render_aa = '8'
    tall = (hi - lo).z > max((hi - lo).x, (hi - lo).y) * 1.5
    scene.render.resolution_x, scene.render.resolution_y = (700, 1100) if tall else (1100, 800)
    scene.world = scene.world or bpy.data.worlds.new("w")
    scene.world.color = (0.02, 0.025, 0.04)
    scene.render.film_transparent = False
    os.makedirs(PREVIEWS, exist_ok=True)
    scene.render.filepath = os.path.join(PREVIEWS, filename + ".png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)


class Mesh:
    """Collects geometry for one object. Every shape takes a material name."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()
        self.mats = []

    def mi(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def _finish(self, start, mat, closed):
        self.bm.faces.ensure_lookup_table()
        faces = self.bm.faces[start:]
        if mat is not None:
            index = self.mi(mat)
            for f in faces:
                f.material_index = index
        if closed:
            bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        return faces

    def lathe(self, profile, mat, segs=48, M=None, cap_start=True, cap_end=True, matfn=None, closed=True):
        """Spins a profile of (radius, height) points around Z. profile mats: mat is a name, or a list with one
        name per profile segment. matfn(i, j) can override the material per face (i = segment, j = slice)."""
        M = M or Matrix.Identity(4)
        start = len(self.bm.faces)
        rings = []
        for r, z in profile:
            rings.append([self.bm.verts.new(M @ Vector((r * math.cos(TAU * j / segs), r * math.sin(TAU * j / segs), z)))
                          for j in range(segs)])
        for i in range(len(rings) - 1):
            name = mat[i] if isinstance(mat, (list, tuple)) else mat
            for j in range(segs):
                k = (j + 1) % segs
                f = self.bm.faces.new((rings[i][j], rings[i][k], rings[i + 1][k], rings[i + 1][j]))
                u0, u1, v0, v1 = j / segs * 6, (j + 1) / segs * 6, profile[i][1] * 0.3, profile[i + 1][1] * 0.3
                for loop, uv in zip(f.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
                    loop[self.uv].uv = uv
                override = matfn(i, j) if matfn else None
                f.material_index = self.mi(override or name)
        if cap_start:
            self.bm.faces.new(rings[0][::-1]).material_index = self.mi(mat[0] if isinstance(mat, (list, tuple)) else mat)
        if cap_end:
            self.bm.faces.new(rings[-1]).material_index = self.mi(mat[-1] if isinstance(mat, (list, tuple)) else mat)
        return self._finish(start, None, closed and cap_start and cap_end)

    def box(self, size, M, mat):
        start = len(self.bm.faces)
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        v = {(sx, sy, sz): self.bm.verts.new(M @ Vector((sx * hx, sy * hy, sz * hz)))
             for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)}
        for quad in (((1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1)), ((-1, -1, -1), (-1, -1, 1), (-1, 1, 1), (-1, 1, -1)),
                     ((-1, 1, -1), (-1, 1, 1), (1, 1, 1), (1, 1, -1)), ((-1, -1, -1), (1, -1, -1), (1, -1, 1), (-1, -1, 1)),
                     ((-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)), ((-1, -1, -1), (-1, 1, -1), (1, 1, -1), (1, -1, -1))):
            self.bm.faces.new([v[q] for q in quad])
        return self._finish(start, mat, True)

    def cyl(self, p1, p2, r1, r2, mat, segs=12):
        p1, p2 = Vector(p1), Vector(p2)
        axis = p2 - p1
        M = Matrix.Translation(p1) @ axis.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        return self.lathe([(r1, 0), (r2, axis.length)], mat, segs, M)

    def strut(self, p1, p2, t, mat):
        return self.cyl(p1, p2, t, t, mat, 4)

    def sphere(self, center, radius, mat, subdiv=2, scale=(1, 1, 1)):
        start = len(self.bm.faces)
        M = Matrix.Translation(Vector(center)) @ Matrix.Diagonal((*scale, 1))
        bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=radius, matrix=M)
        return self._finish(start, mat, False)

    def prism(self, points, thickness, M, mat):
        """Extrudes a flat polygon given as (x, z) points; thickness runs along Y."""
        start = len(self.bm.faces)
        front = [self.bm.verts.new(M @ Vector((x, thickness / 2, z))) for x, z in points]
        back = [self.bm.verts.new(M @ Vector((x, -thickness / 2, z))) for x, z in points]
        self.bm.faces.new(front)
        self.bm.faces.new(back[::-1])
        n = len(points)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((front[i], back[i], back[j], front[j]))
        return self._finish(start, mat, True)

    def sweep(self, section, radius, mat, segs=128, matfn=None, skip=None):
        """Sweeps a closed cross-section of (radial offset, depth) points around a circle in the XY plane."""
        start = len(self.bm.faces)
        loops = []
        for k in range(segs):
            a = TAU * k / segs
            loops.append([self.bm.verts.new(Vector(((radius + dr) * math.cos(a), (radius + dr) * math.sin(a), dz)))
                          for dr, dz in section])
        n = len(section)
        for k in range(segs):
            if skip and skip(k):
                continue
            nk = (k + 1) % segs
            for i in range(n):
                j = (i + 1) % n
                f = self.bm.faces.new((loops[k][i], loops[k][j], loops[nk][j], loops[nk][i]))
                f.material_index = self.mi((matfn(k, i) if matfn else None) or mat)
        faces = self._finish(start, None, False)
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        return faces

    def box_uvs(self, scale=1.0):
        """Simple box mapping, so tiling textures work without unwrapping."""
        uv = self.uv
        for f in self.bm.faces:
            n = f.normal
            axis = max(range(3), key=lambda a: abs(n[a]))
            u, v = [a for a in range(3) if a != axis]
            for loop in f.loops:
                loop[uv].uv = (loop.vert.co[u] * scale, loop.vert.co[v] * scale)

    def build(self, transform=None, origin=None):
        self.bm.normal_update()
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for name in self.mats:
            me.materials.append(material(name))
        if transform is not None:
            me.transform(transform)
        ob = bpy.data.objects.new(self.name, me)
        if origin is not None:
            me.transform(Matrix.Translation(-Vector(origin)))
            ob.location = origin
        for p in me.polygons:
            p.use_smooth = True
        bpy.context.scene.collection.objects.link(ob)
        return ob


def anchor(name, location):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_size = 0.3
    ob.location = location
    bpy.context.scene.collection.objects.link(ob)
    return ob


def Rz(deg): return Matrix.Rotation(math.radians(deg), 4, 'Z')
def Ry(deg): return Matrix.Rotation(math.radians(deg), 4, 'Y')
def Rx(deg): return Matrix.Rotation(math.radians(deg), 4, 'X')
def T(x, y, z): return Matrix.Translation((x, y, z))


def export(filename, objects, view=(1.0, -1.6, 0.7)):
    if PREVIEW:
        bpy.context.view_layer.update()
        render_preview(filename, objects, view)
        for ob in objects:
            bpy.data.objects.remove(ob, do_unlink=True)
        return
    os.makedirs(MODELS, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    path = os.path.join(MODELS, filename + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_UNITS',
                             bake_space_transform=True, object_types={'MESH', 'EMPTY'}, mesh_smooth_type='FACE',
                             add_leaf_bones=False, bake_anim=False, use_mesh_modifiers=True)
    tris = sum(len(p.vertices) - 2 for ob in objects if ob.type == 'MESH' for p in ob.data.polygons)
    print(f"  {filename}.fbx  {len(objects)} objects, {tris} triangles")
    for ob in objects:
        bpy.data.objects.remove(ob, do_unlink=True)


# ----------------------------------------------------------------------------------------------
# Rocket: a core stage with two side boosters, an upper stage, a service module and a crew capsule.
# Layout based on real heavy launchers (core + strap-on boosters, capsule with an escape tower).
# ----------------------------------------------------------------------------------------------

def build_rocket():
    W, D, A, MT, G, N, EG, S = ("Rocket_White", "Rocket_Dark", "Rocket_Accent", "Rocket_Metal", "Rocket_Glass",
                                "Rocket_Nozzle", "Rocket_EngineGlow", "Rocket_Solar")
    hull = Mesh("Hull")
    R, RU = 1.8, 1.4
    profile, mats = [], []

    def to(r, z, mat):
        """Adds a profile point; mat is the material of the surface from this point to the next."""
        profile.append((r, z))
        mats.append(mat)

    def band(r, z, h, mat=D, lift=0.05):
        to(r, z, mat); to(r + lift, z, mat); to(r + lift, z + h, mat); to(r, z + h, None)

    to(1.45, -0.3, D); to(1.84, 0.5, D); to(1.84, 1.0, D)
    to(R, 1.0, "PATTERN"); band(R, 5.0, 0.25)
    mats[-1] = W; band(R, 10.0, 0.15)
    mats[-1] = W; to(R, 14.4, A); band(R, 15.3, 0.2)
    mats[-1] = D; to(RU, 17.5, D); band(RU, 17.5, 0.2)
    mats[-1] = W; band(RU, 20.2, 0.08)
    mats[-1] = W; band(RU, 22.5, 0.15)
    mats[-1] = MT; to(RU, 24.0, D); to(1.55, 24.4, W); to(1.55, 24.7, W)
    to(0.55, 27.2, D); to(0.35, 27.5, D); to(0.16, 27.9, W); to(0.16, 30.2, W)
    to(0.3, 30.5, W); to(0.3, 31.6, A); to(0.02, 32.6, A)

    segs = 64
    def roll_pattern(i, j):
        if mats[i] != "PATTERN":
            return None
        return D if (j * 4 // segs) % 2 == 0 else W
    hull.lathe(profile, [m if m != "PATTERN" else W for m in mats[:-1]], segs, matfn=roll_pattern)

    # Engines: one in the middle, four around it
    bell = [(0.24, 0.0), (0.3, -0.45), (0.52, -1.45), (0.62, -1.95), (0.57, -1.95), (0.46, -1.45), (0.24, -0.55), (0.02, -0.35)]
    bell_mats = [N, N, N, N, N, EG, EG]
    hull.lathe([(1.25, -0.55), (1.25, -0.25)], MT, 32)
    for x, y in ((0, 0), (0.82, 0.82), (-0.82, 0.82), (0.82, -0.82), (-0.82, -0.82)):
        hull.lathe(bell, bell_mats, 24, T(x, y, -0.3), cap_start=False, cap_end=False, closed=False)
        hull.cyl((x, y, -0.3), (x, y, -0.55), 0.3, 0.26, MT, 12)

    # Fins
    for angle in (45, 135, 225, 315):
        hull.prism([(1.75, 0.2), (3.3, -0.5), (3.3, 0.5), (1.75, 3.8)], 0.12, Rz(angle), D)
        hull.box((0.5, 0.2, 1.0), Rz(angle) @ T(3.3, 0, 0), MT)

    # Cable raceways and feed lines along the core
    for angle in (90, 270):
        hull.box((0.14, 0.34, 13.5), Rz(angle) @ T(R + 0.06, 0, 8.2), D)
    for angle in (60, 240):
        hull.cyl(Rz(angle) @ Vector((R + 0.09, 0, 1.2)), Rz(angle) @ Vector((R + 0.09, 0, 14.2)), 0.07, 0.07, MT, 8)
    for z in (2.5, 6.0, 9.0, 12.5):
        for angle in (60, 240):
            hull.box((0.2, 0.22, 0.12), Rz(angle) @ T(R + 0.08, 0, z), D)

    # Upper stage details: thruster pods, vents, small panels
    for angle in (45, 135, 225, 315):
        M = Rz(angle) @ T(RU + 0.1, 0, 23.3)
        hull.box((0.22, 0.34, 0.5), M, D)
        for dy, dz in ((0.2, 0), (-0.2, 0), (0, 0.3), (0, -0.3)):
            hull.cyl(M @ Vector((0.05, dy * 0.7, dz * 0.7)), M @ Vector((0.14, dy * 1.5, dz * 1.5)), 0.03, 0.07, N, 8)
    for i in range(10):
        hull.box((0.04, 0.45, 0.7), Rz(18 + i * 36) @ T(RU + 0.01, 0, 19.0), MT)
    for i in range(16):
        hull.box((0.05, 0.12, 1.1), Rz(i * 22.5 + 11) @ T(1.62, 0, 16.4) @ Ry(11.3), D)

    # Capsule windows (on the cone, tilted to sit flush)
    tilt = -math.degrees(math.atan2(1.0, 2.5))
    for angle in (-28, 28, 152, 208):
        hull.box((0.05, 0.36, 0.3), Rz(angle) @ T(1.2, 0, 25.6) @ Ry(tilt), G)
        hull.box((0.03, 0.46, 0.4), Rz(angle) @ T(1.19, 0, 25.6) @ Ry(tilt), D)

    # Crew hatch on the +X side
    hatch_z = 21.3
    hull.box((0.08, 1.0, 1.5), T(RU, 0, hatch_z), MT)
    hull.box((0.06, 0.84, 1.34), T(RU + 0.04, 0, hatch_z), W)
    hull.box((0.05, 0.3, 0.3), T(RU + 0.07, 0, hatch_z + 0.3), G)
    hull.cyl((RU + 0.12, -0.25, hatch_z - 0.3), (RU + 0.12, 0.25, hatch_z - 0.3), 0.025, 0.025, MT, 8)
    for dy in (-0.25, 0.25):
        hull.cyl((RU + 0.02, dy, hatch_z - 0.3), (RU + 0.12, dy, hatch_z - 0.3), 0.02, 0.02, MT, 6)
    for dz in (-0.9, 0.9):  # hand rails beside the hatch
        hull.cyl((RU + 0.1, -0.6, hatch_z + dz), (RU + 0.1, 0.6, hatch_z + dz), 0.025, 0.025, A, 8)

    # Dish antenna on the -X side
    hull.cyl((-RU, 0, 23.6), (-2.3, 0, 23.9), 0.05, 0.05, MT, 8)
    dish = T(-2.3, 0, 23.9) @ Ry(-70)
    hull.lathe([(0.03, 0.0), (0.35, 0.06), (0.62, 0.22), (0.6, 0.24), (0.33, 0.09), (0.03, 0.04)], W, 24, dish, False, False, closed=False)
    hull.cyl(dish @ Vector((0, 0, 0.04)), dish @ Vector((0, 0, 0.5)), 0.015, 0.015, MT, 6)

    # Solar wings on the service module (unfolded in space; the scene builder hides them on the launch pad)
    solar = Mesh("SolarPanels")
    for side in (1, -1):
        solar.cyl((0, side * RU, 23.3), (0, side * 7.6, 23.3), 0.04, 0.04, MT, 8)
        for k in range(3):
            y = side * (2.6 + k * 2.0)
            solar.box((1.3, 1.9, 0.04), T(0, y, 23.3), S)
            solar.box((1.36, 0.05, 0.06), T(0, y - side * 0.97, 23.3), MT)
            for gx in (-0.33, 0.33):
                solar.box((0.015, 1.9, 0.05), T(gx, y, 23.3), MT)

    # Side boosters
    boosters = Mesh("Boosters")
    for side in (1, -1):
        x = side * 2.78
        prof, m = [], []
        def tb(r, z, mat): prof.append((r, z)); m.append(mat)
        tb(0.78, -0.9, N); tb(0.6, 0.0, D); tb(0.86, 0.3, D); tb(0.86, 0.9, W)
        for z in (3.2, 6.6, 10.0):
            tb(0.86, z, D); tb(0.9, z, D); tb(0.9, z + 0.14, D); tb(0.86, z + 0.14, W)
        tb(0.86, 12.6, A); tb(0.86, 13.0, W); tb(0.3, 14.9, W); tb(0.02, 15.4, W)
        boosters.lathe(prof, m[:-1], 40, T(x, 0, 0))
        boosters.lathe([(0.55, -0.9), (0.72, -0.9), (0.5, -0.1), (0.05, -0.05)], [N, EG, EG], 24, T(x, 0, 0), False, False, closed=False)
        for z in (1.6, 11.6):
            boosters.box((0.5, 0.3, 0.3), T(side * 1.9, 0, z), MT)
            boosters.strut((side * 1.8, 0.5, z + 0.8), (side * 2.3, 0.3, z), 0.04, MT)
            boosters.strut((side * 1.8, -0.5, z + 0.8), (side * 2.3, -0.3, z), 0.04, MT)

    objs = [hull.build(), solar.build(), boosters.build(),
            anchor("Anchor_Hatch", (RU + 0.2, 0, hatch_z)), anchor("Anchor_Engines", (0, 0, -2.2)), anchor("Anchor_Nose", (0, 0, 32.6))]
    export("Rocket", objs)


# ----------------------------------------------------------------------------------------------
# Portal: a machined ring with glowing channels, plus two thin rings that spin separately.
# Built in the XY plane and then stood upright, so in Unity it faces along Z.
# ----------------------------------------------------------------------------------------------

def build_portal():
    ME, GL, TR = "Portal_Metal", "Portal_Glow", "Portal_Trim"
    upright = Rx(90)
    ring = Mesh("RingMain")
    section = [(-1.0, -0.45), (-1.0, 0.45), (-0.55, 1.0), (0.6, 1.0), (1.15, 0.4), (1.15, -0.4), (0.6, -1.0), (-0.55, -1.0)]

    def ring_mats(k, i):
        if i == 0:
            return GL                                   # the inner rim glows all the way round
        if i in (2, 6) and k % 10 in (0, 1, 2):
            return GL                                   # dashes of light on the front and back faces
        if i in (1, 7, 3, 5):
            return TR
        return ME
    ring.sweep(section, 11.0, ME, 160, ring_mats)

    for k in range(12):
        M = Rz(k * 30 + 15)
        ring.box((3.0, 1.9, 2.7), M @ T(11.2, 0, 0), ME)
        ring.box((2.2, 2.0, 0.35), M @ T(11.3, 0, 1.25), TR)
        ring.box((2.2, 2.0, 0.35), M @ T(11.3, 0, -1.25), TR)
        ring.box((1.6, 0.3, 2.8), M @ T(11.2, 0, 0), GL)
        ring.prism([(12.4, -0.5), (16.2, -1.6), (15.4, -0.4), (12.4, 0.6)], 0.5, M, ME)      # swept-back spike
        ring.prism([(12.6, -0.2), (15.2, -0.95), (12.6, 0.15)], 0.56, M, GL)
    for k in range(6):
        M = Rz(k * 60 + 45)
        ring.prism([(10.2, 1.0), (8.4, 2.6), (9.0, 1.0), (10.2, 0.4)], 0.7, M, TR)             # claws reaching inward
        ring.prism([(10.0, 0.9), (8.8, 2.0), (9.3, 0.9)], 0.76, M, GL)

    outer = Mesh("RingOuter")
    outer.sweep([(-0.22, 0), (0, 0.22), (0.22, 0), (0, -0.22)], 14.2, ME, 128)
    for k in range(24):
        outer.box((0.7, 0.5, 0.7), Rz(k * 15) @ T(14.2, 0, 0), GL if k % 3 == 0 else TR)

    inner = Mesh("RingInner")
    inner.sweep([(-0.1, -0.35), (-0.1, 0.35), (0.1, 0.35), (0.1, -0.35)], 9.4, GL, 144, skip=lambda k: k % 12 in (9, 10, 11) or k % 12 in (4,))
    export("Portal", [ring.build(upright), outer.build(upright), inner.build(upright)], view=(0.7, -1.6, 0.35))


# ----------------------------------------------------------------------------------------------
# The grip ball: a lumpy metal sphere, like the reference image.
# ----------------------------------------------------------------------------------------------

def lumpy_sphere(name, mat, subdiv, radius, amount, frequency, seed, squash=(1, 1, 1), craters=0, detail=0.0):
    m = Mesh(name)
    m.sphere((0, 0, 0), 1.0, mat, subdiv)
    rng = random.Random(seed)
    offset = Vector((rng.uniform(-50, 50), rng.uniform(-50, 50), rng.uniform(-50, 50)))
    pits = [(Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))).normalized(), rng.uniform(0.18, 0.5), rng.uniform(0.06, 0.16))
            for _ in range(craters)]
    for v in m.bm.verts:
        d = v.co.normalized()
        h = amount * noise.noise(d * frequency + offset)
        h += amount * 0.45 * noise.noise(d * frequency * 2.3 + offset)
        if detail:
            h += detail * noise.fractal(d * frequency * 5 + offset, 1.0, 2.0, 4)
        for centre, size, depth in pits:
            a = d.angle(centre)
            if a < size * 1.25:
                k = a / size
                h += -depth * (1 - k * k) if k < 1 else depth * 0.35 * (1 - (k - 1) / 0.25)
        v.co = Vector((d.x * squash[0], d.y * squash[1], d.z * squash[2])) * (1 + h) * radius
    m.bm.normal_update()
    return m


def build_grip_ball():
    ball = lumpy_sphere("GripBall", "GripBall_Metal", 5, 0.07, 0.17, 2.4, seed=11)
    export("GripBall", [ball.build()])


def build_asteroids():
    shapes = [(1.0, 0.8, 0.7), (1.0, 0.6, 0.85), (0.8, 1.0, 0.6), (1.0, 0.95, 0.9), (0.7, 0.75, 1.0)]
    for i, squash in enumerate(shapes):
        rock = lumpy_sphere(f"Asteroid_{i + 1}", "Asteroid_Rock", 5 if i < 2 else 4, 1.0, 0.32, 1.5, seed=40 + i, squash=squash, craters=7, detail=0.06)
        rock.box_uvs(0.6)
        export(f"Asteroid_{i + 1}", [rock.build()])


# ----------------------------------------------------------------------------------------------
# People: one simple stylised figure, standing or seated. Astronauts get a helmet and a backpack.
# ----------------------------------------------------------------------------------------------

def build_person(name, seated):
    SK, CL, PA, SH, HA = "Person_Skin", "Person_Cloth", "Person_Pants", "Person_Shoes", "Person_Hair"
    p = Mesh("Body")
    hip = 0.52 if seated else 0.95
    chest = hip + 0.5
    head = chest + 0.15
    # torso and head
    flat = Matrix.Diagonal((1, 0.62, 1, 1))
    p.lathe([(0.13, 0.0), (0.17, 0.05), (0.16, 0.22), (0.2, 0.42), (0.19, 0.5), (0.07, 0.56)], CL, 14, T(0, 0, hip - 0.05) @ flat)
    p.cyl((0, 0, chest - 0.02), (0, 0, head - 0.03), 0.055, 0.05, SK, 10)
    p.sphere((0, 0, head + 0.03), 0.11, SK, 2, (0.92, 1.0, 1.12))
    p.sphere((0, 0.025, head + 0.055), 0.112, HA, 2, (0.95, 1.0, 1.02))
    for s in (1, -1):                                              # eyes and brows, so the face reads as a face
        p.sphere((s * 0.04, -0.098, head + 0.045), 0.016, "Person_Eye", 2, (1, 0.5, 1))
        p.box((0.04, 0.012, 0.008), T(s * 0.04, -0.104, head + 0.075), HA)
    p.box((0.022, 0.03, 0.04), T(0, -0.108, head + 0.015), SK)     # nose
    p.box((0.05, 0.01, 0.008), T(0, -0.1, head - 0.03), HA)        # mouth
    for s in (1, -1):
        shoulder = Vector((s * 0.21, 0, chest - 0.05))
        if seated:
            elbow, wrist = Vector((s * 0.25, -0.1, chest - 0.3)), Vector((s * 0.17, -0.38, chest - 0.28))
            knee, ankle = Vector((s * 0.1, -0.42, hip + 0.02)), Vector((s * 0.1, -0.44, 0.08))
        else:
            elbow, wrist = Vector((s * 0.25, 0.01, chest - 0.33)), Vector((s * 0.26, -0.05, chest - 0.58))
            knee, ankle = Vector((s * 0.1, -0.01, 0.5)), Vector((s * 0.1, 0.02, 0.08))
        hipj = Vector((s * 0.09, 0, hip))
        p.sphere(shoulder, 0.065, CL, 2)
        p.cyl(shoulder, elbow, 0.055, 0.045, CL, 10)
        p.sphere(elbow, 0.046, CL, 2)
        p.cyl(elbow, wrist, 0.045, 0.035, CL, 10)
        p.sphere(wrist + (wrist - elbow).normalized() * 0.04, 0.045, SK, 1, (0.8, 1, 1))
        p.sphere(hipj, 0.085, PA, 2)
        p.cyl(hipj, knee, 0.085, 0.062, PA, 10)
        p.sphere(knee, 0.063, PA, 2)
        p.cyl(knee, ankle, 0.062, 0.048, PA, 10)
        p.box((0.1, 0.26, 0.09), T(ankle.x, ankle.y - 0.06, 0.045), SH)

    helmet = Mesh("Helmet")
    helmet.sphere((0, -0.01, head + 0.04), 0.19, "Person_Visor", 3)
    helmet.lathe([(0.13, 0.0), (0.16, 0.02), (0.16, 0.06), (0.12, 0.08)], "Person_SuitTrim", 20, T(0, 0, chest + 0.0))
    pack = Mesh("Backpack")
    pack.box((0.36, 0.16, 0.5), T(0, 0.16, chest - 0.2), "Person_SuitTrim")
    pack.box((0.2, 0.05, 0.14), T(0, -0.13, chest - 0.18), "Person_SuitTrim")
    export(name, [p.build(), helmet.build(), pack.build()])


# ----------------------------------------------------------------------------------------------
# Launch site pieces
# ----------------------------------------------------------------------------------------------

def build_launch_tower():
    ST, LI, CO = "Tower_Steel", "Tower_Light", "Tower_Concrete"
    t = Mesh("Tower")
    cx, half, height = 8.0, 2.0, 36.0
    corners = [(cx - half, -half), (cx + half, -half), (cx + half, half), (cx - half, half)]
    for x, y in corners:
        t.box((0.35, 0.35, height), T(x, y, height / 2), ST)
    levels = [i * 3.0 for i in range(13)]
    for z in levels:
        for i in range(4):
            a, b = corners[i], corners[(i + 1) % 4]
            t.strut((*a, z), (*b, z), 0.09, ST)
            if z < height - 1:
                first, second = (a, b) if int(z / 3) % 2 == 0 else (b, a)
                t.strut((*first, z), (*second, z + 3.0), 0.06, ST)
    for z in (9.0, 18.0, 27.0, 36.0):
        t.box((4.4, 4.4, 0.15), T(cx, 0, z), ST)
    t.box((1.6, 1.6, height), T(cx, 0, height / 2), CO)                      # lift shaft
    # arms reaching to the rocket (rocket stands at x = 0)
    for z, reach in ((8.0, 2.9), (15.0, 2.0), (21.3, 1.7)):
        t.box((cx - half - reach, 0.9, 0.25), T((cx - half + reach) / 2, 0, z - 0.6), ST)
        for y in (-0.45, 0.45):
            t.strut((reach, y, z + 0.5), (cx - half, y, z + 0.5), 0.04, ST)
            for k in range(6):
                x = reach + (cx - half - reach) * k / 5
                t.strut((x, y, z - 0.5), (x, y, z + 0.5), 0.03, ST)
        t.strut((cx - half, 0, z + 3.0), ((cx - half + reach) / 2, 0, z - 0.4), 0.06, ST)
    # crane at the top with a lightning mast
    t.strut((cx, 0, height), (cx, 0, height + 9), 0.12, ST)
    t.strut((cx, 0, height + 3), (cx - 7, 0, height + 1.2), 0.1, ST)
    t.strut((cx, 0, height + 6), (cx - 7, 0, height + 1.2), 0.05, ST)
    for z in (12.0, 24.0, 36.2, 45.0):
        t.sphere((cx - half, -half, z) if z < 40 else (cx, 0, z), 0.22, LI, 1)

    pad = Mesh("Pad")
    pad.lathe([(13.0, -1.2), (13.0, -0.25), (12.2, 0.0), (0.02, 0.0)], CO, 8, Rz(22.5), cap_end=False, closed=False)
    pad.lathe([(2.3, 0.0), (2.5, 0.0), (2.5, 0.5), (2.3, 0.5)], ST, 32, T(0, 0, 1.6), False, False, closed=False)
    for angle in (45, 135, 225, 315):
        pad.box((0.5, 0.5, 2.1), Rz(angle) @ T(2.4, 0, 1.05), ST)
        pad.strut(Rz(angle) @ Vector((2.4, 0, 1.9)), Rz(angle) @ Vector((4.6, 0, 0)), 0.1, ST)
    pad.box((3.4, 9.0, 0.5), T(0, -8.0, -0.2) @ Rx(0), "Tower_Scorch")       # flame trench
    for x in (-6.0, 6.0):
        for y in (-9.0, 9.0):
            pad.strut((x, y, 0), (x, y, 14), 0.12, ST)                       # floodlight masts
            pad.box((1.4, 0.3, 0.9), T(x, y, 14.2), LI)
    export("LaunchTower", [t.build(), pad.build()])


def build_dead_tree(name, seed):
    rng = random.Random(seed)
    t = Mesh(name)

    def branch(base, direction, length, radius, depth):
        tip = base + direction * length
        t.cyl(base, tip, radius, radius * 0.6, "Tree_Dead", 7)
        if depth == 0:
            return
        for _ in range(rng.randint(2, 3)):
            bend = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0.2, 0.9))).normalized()
            new_dir = (direction * 0.55 + bend * 0.75).normalized()
            start = base + direction * length * rng.uniform(0.55, 1.0)
            branch(start, new_dir, length * rng.uniform(0.5, 0.75), radius * 0.55, depth - 1)

    lean = Vector((rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15), 1)).normalized()
    branch(Vector((0, 0, -0.2)), lean, rng.uniform(2.6, 3.6), 0.2, 3)
    export(name, [t.build()])


def build_crate():
    c = Mesh("Crate")
    c.box((0.9, 0.6, 0.5), T(0, 0, 0.25), "Crate_Body")
    for x in (-0.36, 0.36):
        c.box((0.08, 0.64, 0.54), T(x, 0, 0.25), "Crate_Trim")
    c.box((0.3, 0.05, 0.12), T(0, -0.31, 0.3), "Crate_Light")
    c.cyl((-0.2, 0, 0.56), (0.2, 0, 0.56), 0.02, 0.02, "Crate_Trim", 8)
    for x in (-0.2, 0.2):
        c.cyl((x, 0, 0.5), (x, 0, 0.56), 0.02, 0.02, "Crate_Trim", 6)
    export("Crate", [c.build()])


# ----------------------------------------------------------------------------------------------
# Living trees: a branching trunk with bark, and leaves as flat cards that carry a leaf-cluster picture.
# ----------------------------------------------------------------------------------------------

def build_tree(name, seed, height=9.0, spread=1.0):
    rng = random.Random(seed)
    tree = Mesh(name)
    tips = []

    def branch(base, direction, length, radius, depth):
        tip = base + direction * length
        tree.cyl(base, tip, radius, radius * 0.62, "Tree_Bark", 9 if depth >= 2 else 6)
        if depth == 0:
            tips.append((tip, length))
            return
        for _ in range(rng.randint(3, 4) if depth >= 2 else rng.randint(2, 3)):
            outward = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0.15, 0.7))).normalized()
            new_dir = (direction * 0.5 + outward * 0.85 * spread).normalized()
            start = base + direction * length * rng.uniform(0.6, 1.0)
            branch(start, new_dir, length * rng.uniform(0.55, 0.72), radius * 0.55, depth - 1)
            if depth == 1:
                tips.append((start + new_dir * length * 0.3, length))

    lean = Vector((rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08), 1)).normalized()
    branch(Vector((0, 0, -0.3)), lean, height * 0.42, 0.3 * height / 9.0, 3)

    # Leaf cards: a few crossed squares around every branch tip.
    uv = tree.uv
    leaf = tree.mi("Tree_Leaves")
    leaf_start = len(tree.bm.faces)
    for tip, length in tips:
        for _ in range(3):
            size = rng.uniform(1.5, 2.4) * height / 9.0
            centre = tip + Vector((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), rng.uniform(-0.2, 0.5)))
            right = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.4, 0.4))).normalized()
            up = right.cross(Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1)))).normalized()
            corners = [centre + (right * sx + up * sy) * size / 2 for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
            face = tree.bm.faces.new([tree.bm.verts.new(c) for c in corners])
            face.material_index = leaf
            for loop, coords in zip(face.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
                loop[uv].uv = coords
    tree.bm.faces.ensure_lookup_table()
    leaf_faces = {f.index for f in tree.bm.faces[leaf_start:]}
    crown = sum((t for t, _ in tips), Vector()) / len(tips)

    ob = tree.build()
    # Leaves are lit as if the crown were one soft ball, which hides the flat cards.
    me = ob.data
    normals = [Vector(n.vector) for n in me.corner_normals]
    for poly in me.polygons:
        if poly.index in leaf_faces:
            for li in poly.loop_indices:
                v = me.vertices[me.loops[li].vertex_index].co
                normals[li] = (v - crown + Vector((0, 0, 1.5))).normalized()
    me.normals_split_custom_set([tuple(n) for n in normals])
    export(name, [ob])


def build_glove():
    """The player's own suited hand, seen in first person, shaped to hold the grip ball (ball centre 6 cm above the palm)."""
    g = Mesh("Glove")
    W, D = "Glove_Suit", "Glove_Grip"
    g.cyl((0.01, 0.5, -0.12), (0, 0.085, -0.02), 0.06, 0.047, W, 14)          # forearm, running back out of view
    g.lathe([(0.052, 0.0), (0.06, 0.01), (0.06, 0.035), (0.052, 0.045)], D, 16, T(0, 0.11, -0.028) @ Rx(-102), closed=False, cap_start=False, cap_end=False)
    g.sphere((0, 0.01, -0.012), 0.05, W, 2, (1.0, 1.25, 0.5))                  # palm
    for k, x in enumerate((-0.04, -0.013, 0.014, 0.04)):
        reach = 1.0 - abs(k - 1.5) * 0.08
        pts = [Vector((x, -0.035, -0.005)), Vector((x * 1.1, -0.078 * reach, 0.03)), Vector((x * 1.1, -0.088 * reach, 0.085)), Vector((x, -0.06 * reach, 0.128))]
        for a, b in zip(pts, pts[1:]):
            g.cyl(a, b, 0.0125, 0.0115, W, 8)
            g.sphere(b, 0.0125, D, 1)
    thumb = [Vector((0.05, 0.02, -0.005)), Vector((0.082, -0.005, 0.04)), Vector((0.075, -0.025, 0.095))]
    for a, b in zip(thumb, thumb[1:]):
        g.cyl(a, b, 0.015, 0.013, W, 8)
        g.sphere(b, 0.014, D, 1)
    export("Glove", [g.build()], view=(0.8, -1.2, 0.8))


# ----------------------------------------------------------------------------------------------
# Mission control furniture
# ----------------------------------------------------------------------------------------------

def build_furniture():
    desk = Mesh("Desk")
    desk.box((7.0, 1.25, 0.05), T(0, 0, 0.75), "Desk_Top")
    desk.box((7.0, 0.04, 0.5), T(0, -0.6, 0.48), "Desk_Frame")                 # front panel (faces the room's screen)
    desk.box((6.9, 0.3, 0.04), T(0, -0.42, 0.98), "Desk_Top")                  # raised shelf the monitors stand on
    desk.box((6.9, 0.03, 0.2), T(0, -0.56, 0.87), "Desk_Frame")
    for x in (-3.46, -1.15, 1.15, 3.46):
        desk.box((0.06, 1.2, 0.74), T(x, 0, 0.37), "Desk_Frame")
    for x in (-2.2, 0.0, 2.2):                                                # keyboard, mouse and a mug per seat
        desk.box((0.46, 0.16, 0.02), T(x, 0.42, 0.785), "Desk_Keys")
        desk.box((0.07, 0.11, 0.025), T(x + 0.38, 0.44, 0.788), "Desk_Keys")
        desk.cyl((x - 0.45, 0.3, 0.775), (x - 0.45, 0.3, 0.87), 0.04, 0.04, "Desk_Frame", 10)
    desk.box((6.8, 0.012, 0.012), T(0, -0.585, 0.71), "Desk_Light")           # thin light strip under the lip
    export("Desk", [desk.build()])

    monitor = Mesh("Monitor")
    monitor.box((1.44, 0.05, 0.84), T(0, 0, 0.5), "Monitor_Bezel")
    monitor.box((0.12, 0.06, 0.2), T(0, -0.04, 0.1), "Monitor_Bezel")
    monitor.box((0.5, 0.26, 0.02), T(0, -0.03, 0.01), "Monitor_Bezel")
    screen = Mesh("Screen")
    v = [screen.bm.verts.new(c) for c in ((-0.68, 0.027, 0.12), (0.68, 0.027, 0.12), (0.68, 0.027, 0.88), (-0.68, 0.027, 0.88))]
    face = screen.bm.faces.new(v[::-1])
    face.material_index = screen.mi("Monitor_Screen")
    for loop in face.loops:
        co = loop.vert.co
        loop[screen.uv].uv = (1.0 - (co.x + 0.68) / 1.36, (co.z - 0.12) / 0.76)
    export("Monitor", [monitor.build(), screen.build()])

    chair = Mesh("Chair")
    chair.box((0.5, 0.5, 0.09), T(0, 0, 0.47), "Chair_Fabric")
    chair.box((0.46, 0.07, 0.62), T(0, 0.26, 0.86) @ Rx(-8), "Chair_Fabric")
    chair.cyl((0, 0, 0.1), (0, 0, 0.44), 0.035, 0.035, "Chair_Frame", 10)
    for k in range(5):
        a = TAU * k / 5
        end = Vector((math.cos(a) * 0.32, math.sin(a) * 0.32, 0.05))
        chair.strut((0, 0, 0.1), end, 0.02, "Chair_Frame")
        chair.sphere(end - Vector((0, 0, 0.02)), 0.03, "Chair_Frame", 1)
    for side in (-1, 1):
        chair.box((0.05, 0.3, 0.03), T(side * 0.29, 0.02, 0.68), "Chair_Frame")
        chair.box((0.03, 0.03, 0.2), T(side * 0.29, 0.1, 0.58), "Chair_Frame")
    export("Chair", [chair.build()])


# ----------------------------------------------------------------------------------------------
# Title letters: each letter is its own object so the game can fly them in one by one.
# ----------------------------------------------------------------------------------------------

def build_title():
    font = bpy.data.fonts.load(FONT)
    objects, x = [], 0.0
    for index, ch in enumerate(TITLE):
        if ch == " ":
            x += 0.55
            continue
        curve = bpy.data.curves.new("letter", 'FONT')
        curve.body = ch
        curve.font = font
        curve.size = 1.0
        curve.extrude = 0.09
        curve.bevel_depth = 0.012
        curve.bevel_resolution = 2
        temp = bpy.data.objects.new("temp", curve)
        bpy.context.scene.collection.objects.link(temp)
        bpy.context.view_layer.update()
        me = bpy.data.meshes.new_from_object(temp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        bpy.data.objects.remove(temp, do_unlink=True)
        me.materials.clear()
        me.materials.append(material("Title_Face"))
        me.materials.append(material("Title_Side"))
        for poly in me.polygons:
            poly.material_index = 0 if abs(poly.normal.z) > 0.9 else 1
        xs = [v.co.x for v in me.vertices]
        ys = [v.co.y for v in me.vertices]
        width = max(xs) - min(xs)
        # centre the letter on its own origin, then stand it upright
        me.transform(T(-(max(xs) + min(xs)) / 2, -0.36, 0))
        me.transform(Rx(90))
        ob = bpy.data.objects.new(f"L{index:02d}_{ch}", me)
        ob.location = (x + width / 2, 0, 0)
        bpy.context.scene.collection.objects.link(ob)
        objects.append(ob)
        x += width + 0.16
    total = x - 0.16
    for ob in objects:
        ob.location.x -= total / 2
    export("TitleLetters", objects, view=(0.35, -1.6, 0.25))


# ----------------------------------------------------------------------------------------------
# Textures
# ----------------------------------------------------------------------------------------------

def fractal(n, beta, seed):
    """Tileable fractal noise in 0..1 (random noise shaped in frequency space)."""
    rng = np.random.default_rng(seed)
    f = np.fft.fft2(rng.standard_normal((n, n)))
    fx, fy = np.fft.fftfreq(n)[:, None], np.fft.fftfreq(n)[None, :]
    k = np.sqrt(fx ** 2 + fy ** 2)
    k[0, 0] = 1
    f /= k ** beta
    f[0, 0] = 0
    h = np.real(np.fft.ifft2(f))
    return (h - h.min()) / (h.max() - h.min())


def normal_map(height, strength):
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * strength
    dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * strength
    n = np.stack([-dx, -dy, np.ones_like(height)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def save_png(name, rgb, alpha=None):
    os.makedirs(TEXTURES, exist_ok=True)
    h, w = rgb.shape[:2]
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[..., :3] = np.clip(rgb, 0, 1)
    if alpha is not None:
        rgba[..., 3] = np.clip(alpha, 0, 1)
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = os.path.join(TEXTURES, name + ".png")
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)
    print(f"  {name}.png  {w}x{h}")


def build_textures():
    # Asteroid rock
    n = 512
    h = fractal(n, 1.6, 1) * 0.7 + fractal(n, 1.0, 2) * 0.3
    tone = 0.3 + 0.5 * h
    rgb = np.stack([tone * 0.62, tone * 0.56, tone * 0.5], -1)
    save_png("Rock", rgb)
    save_png("Rock_n", normal_map(h, 9.0))

    # Cracked, dried-out ground
    n = 1024
    rng = np.random.default_rng(5)
    points = rng.random((70, 2))
    ys, xs = np.mgrid[0:n, 0:n] / n
    f1 = np.full((n, n), 9.0)
    f2 = np.full((n, n), 9.0)
    for px, py in points:
        for ox in (-1, 0, 1):
            for oy in (-1, 0, 1):
                d = np.sqrt((xs - px - ox) ** 2 + (ys - py - oy) ** 2)
                closer = d < f1
                f2 = np.where(closer, f1, np.minimum(f2, d))
                f1 = np.where(closer, d, f1)
    edge = f2 - f1
    crack = np.clip(edge / 0.012, 0, 1)                    # 0 in the cracks, 1 on the plates
    grain = fractal(n, 1.3, 7)
    tone = (0.55 + 0.35 * grain) * (0.25 + 0.75 * crack)
    rgb = np.stack([tone * 0.78, tone * 0.6, tone * 0.42], -1)
    save_png("CrackedGround", rgb)
    save_png("CrackedGround_n", normal_map(crack * 0.6 + grain * 0.4, 6.0))

    # Soft cloud for nebulae (white with a fading alpha, tinted in Unity)
    n = 512
    cloud = fractal(n, 2.2, 11)
    ys, xs = np.mgrid[0:n, 0:n] / (n - 1) * 2 - 1
    falloff = np.clip(1 - np.sqrt(xs ** 2 + ys ** 2), 0, 1) ** 1.5
    alpha = np.clip((cloud - 0.35) * 2.2, 0, 1) * falloff
    save_png("Nebula", np.ones((n, n, 3)), alpha)

    # Planets (wrap around a sphere)
    w, h = 2048, 1024
    def stretch(a):
        return np.repeat(a, 2, axis=1)
    rough = stretch(fractal(h, 1.8, 21))
    fine = stretch(fractal(h, 1.1, 22))
    lat = np.abs(np.linspace(-1, 1, h))[:, None] * np.ones((1, w))
    bands = 0.5 + 0.5 * np.sin(lat * 9 + rough * 5)
    tone = np.clip(0.35 + 0.4 * rough + 0.2 * bands + 0.15 * fine, 0, 1)[..., None]
    rust, sand = np.array([0.45, 0.2, 0.12]), np.array([0.85, 0.66, 0.45])
    save_png("PlanetX", rust + (sand - rust) * tone)

    land = np.clip((stretch(fractal(h, 2.0, 31)) * 0.75 + fine * 0.25 - 0.5) * 9, 0, 1)[..., None]
    ocean = np.array([0.03, 0.14, 0.38]) + np.array([0.02, 0.1, 0.15]) * rough[..., None]
    ground = np.array([0.14, 0.34, 0.12]) + (np.array([0.45, 0.38, 0.22]) - np.array([0.14, 0.34, 0.12])) * fine[..., None]
    surface = ocean + (ground - ocean) * land
    ice = np.clip((lat - 0.8) * 8, 0, 1)[..., None]
    surface = surface + (1 - surface) * ice
    clouds = np.clip((stretch(fractal(h, 1.7, 33)) - 0.52) * 4, 0, 1)[..., None]
    save_png("NotEarth", surface + (1 - surface) * clouds * 0.9)

    # Hull plating normal map: panel seams and rivets
    n = 512
    height = np.ones((n, n))
    for i in range(0, n, 128):
        height[i:i + 3, :] = 0
    for row in range(4):
        shift = 64 if row % 2 else 0
        for col in range(0, n, 128):
            c = (col + shift) % n
            height[row * 128:(row + 1) * 128, c:c + 3] = 0
    height = height * 0.85 + fractal(n, 1.2, 3) * 0.15
    save_png("HullPanels_n", normal_map(height, 3.0))


def main():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    only = [a for a in sys.argv[sys.argv.index("--") + 1:] if not a.startswith("--")] if "--" in sys.argv else []
    steps = {
        "rocket": build_rocket, "portal": build_portal, "ball": build_grip_ball, "asteroids": build_asteroids,
        "people": lambda: (build_person("Person_Standing", False), build_person("Person_Seated", True)),
        "tower": build_launch_tower, "trees": lambda: (build_dead_tree("DeadTree_1", 3), build_dead_tree("DeadTree_2", 8)),
        "crate": build_crate, "furniture": build_furniture, "glove": build_glove,
        "living_trees": lambda: (build_tree("Tree_1", 5, 9.5, 1.0), build_tree("Tree_2", 12, 12.0, 0.8), build_tree("Tree_3", 31, 7.0, 1.2)), "title": build_title, "textures": (lambda: None) if PREVIEW else build_textures,
    }
    for name, step in steps.items():
        if not only or name in only:
            print(f"[{name}]")
            step()
    print("Done.")


main()
