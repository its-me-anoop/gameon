"""Original modular clinic railway models. Run in a separate Blender -b process.

All meshes are authored here; no textures or external models. Export material
groups together to keep mobile draw calls bounded while retaining actor limbs.
"""
from pathlib import Path
import math
import sys
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Unity/OrbitOrchard/Assets/LittleLifeline/Resources/Models"
SOURCE = ROOT / "assets/little-lifeline-game.blend"
ART = ROOT / "Unity/OrbitOrchard/Assets/LittleLifeline/Art"
PREVIEW_ONLY = "--preview-only" in sys.argv
if PREVIEW_ONLY:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)

COLORS = {
    "Seaweed": (.19, .31, .26), "Enamel": (.31, .44, .35),
    "Oat": (.87, .81, .67), "Linen": (.94, .90, .79),
    "Brass": (.68, .46, .19), "Clay": (.65, .39, .27),
    "Wood": (.39, .24, .13), "Iron": (.14, .17, .16),
    "Glass": (.52, .69, .67), "Blue": (.31, .50, .58),
    "Leaf": (.29, .43, .18), "LeafLight": (.54, .59, .29),
    "Flower": (.86, .65, .25), "Skin": (.71, .44, .28),
    "SkinLight": (.88, .66, .45), "Hair": (.18, .12, .09),
    "Berry": (.54, .28, .26), "Paper": (.93, .88, .74),
}
MATS = {}
for name, color in COLORS.items():
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = .68
    shader.inputs["Metallic"].default_value = .35 if name == "Brass" else .05
    MATS[name] = mat


def finish(obj, name, role, smooth=False):
    obj.name = name
    obj.data.materials.append(MATS[role])
    for p in obj.data.polygons:
        p.use_smooth = smooth
    return obj


def box(name, p, size, role, bevel=.035):
    bpy.ops.mesh.primitive_cube_add(size=1, location=p)
    obj = bpy.context.object
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    finish(obj, name, role)
    if bevel:
        mod = obj.modifiers.new("Soft manufactured corners", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        obj.modifiers.new("Weighted corner normals", "WEIGHTED_NORMAL")
    return obj


def ellipsoid(name, p, size, role, segments=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=8, location=p)
    obj = bpy.context.object
    obj.scale = size
    return finish(obj, name, role, True)


def cylinder(name, p, radius, depth, role, axis="Z", vertices=20):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=p)
    obj = bpy.context.object
    if axis == "X": obj.rotation_euler.y = math.pi / 2
    if axis == "Y": obj.rotation_euler.x = math.pi / 2
    finish(obj, name, role, True)
    bevel = obj.modifiers.new("Rounded edges", "BEVEL")
    bevel.width = min(.025, radius * .18)
    bevel.segments = 2
    return obj


def torus(name, p, radius, thickness, role, axis="Z"):
    bpy.ops.mesh.primitive_torus_add(major_segments=28, minor_segments=8,
        major_radius=radius, minor_radius=thickness, location=p)
    obj = bpy.context.object
    if axis == "X": obj.rotation_euler.y = math.pi / 2
    if axis == "Y": obj.rotation_euler.x = math.pi / 2
    return finish(obj, name, role, True)


def plant(p, scale=1):
    x,y,z = p
    cylinder("Clay planter", (x,y,z+.13*scale), .14*scale, .26*scale, "Clay")
    for i in range(5):
        a = i * 2.39996
        ob=ellipsoid("Plant leaf", (x+math.cos(a)*.12*scale,y+math.sin(a)*.12*scale,z+.36*scale+i*.02*scale),
            (.065*scale,.055*scale,.20*scale), "Leaf" if i%2 else "LeafLight")
        ob.rotation_euler = (.25*math.cos(a),.55*math.sin(a),a)


def wheels(length=3.2):
    for y in (-length*.31,length*.31):
        box("Bogie", (0,y,.29), (2.0,.48,.18), "Iron")
        for x in (-1.11,1.11):
            cylinder("Railway wheel", (x,y,.31), .30,.15,"Iron","X")
            cylinder("Wheel hub", (x*1.02,y,.31), .18,.17,"Brass","X")


def carriage():
    wheels()
    box("Undercarriage", (0,0,.51), (2.45,3.4,.24), "Seaweed")
    box("Floor", (0,0,.68), (2.30,3.26,.16), "Oat")
    for i in range(10):
        box("Floor plank", (0,-1.45+i*.32,.77), (2.19,.30,.018), "Linen" if i%3==0 else "Oat",.005)
    box("Cutaway foreground wall", (-1.15,0,.85), (.13,3.3,.27), "Seaweed")
    box("Brass sill", (-1.23,0,.65), (.055,3.38,.055), "Brass",.01)
    box("Low back panel", (1.15,0,1.08), (.13,3.3,.79), "Enamel")
    for y in (-1.55,-.50,.55,1.55):
        box("Window post", (1.15,y,1.68), (.13,.10,1.17), "Oat")
    box("Window rail", (1.15,0,2.24), (.14,3.3,.12), "Seaweed")
    for y in (-1.03,.03,1.06):
        box("Upper window pane", (1.15,y,1.88), (.035,.88,.62), "Glass",.015)
        box("Lower window rail", (1.12,y,1.47), (.15,.91,.065), "Oat",.01)
        box("Folded linen curtain", (1.05,y+.35,1.86), (.075,.15,.61), "Blue",.025)
    for y in (-1.67,1.67):
        box("Door end panel", (.81,y,1.11), (.59,.14,.91), "Oat")
        box("Door frame", (.44,y,1.55), (.10,.14,1.61), "Seaweed")
        box("Door threshold", (-.15,y,.82), (1.10,.28,.10), "Wood")
        box("Coupling walkway", (0,y*1.09,.68), (.70,.24,.10), "Iron")
    plant((.78,1.12,.80),.74)


def locomotive():
    wheels(2.8)
    box("Engine chassis", (0,0,.55),(2.30,3.55,.23),"Seaweed")
    cylinder("Boiler", (0,-.43,1.26),.78,1.8,"Seaweed","Y",32)
    for y in (-1.27,-.39,.34):torus("Boiler brass band",(0,y,1.26),.79,.035,"Brass","Y")
    cylinder("Front plate",(0,-1.37,1.26),.72,.08,"Enamel","Y",32)
    cylinder("Lamp bezel",(0,-1.45,1.42),.22,.18,"Brass","Y")
    cylinder("Warm headlamp",(0,-1.56,1.42),.16,.03,"Linen","Y")
    cylinder("Chimney",(0,-.80,2.03),.18,.65,"Seaweed")
    cylinder("Chimney cap",(0,-.80,2.36),.26,.09,"Brass")
    box("Cab",(0,1.06,1.39),(2.06,1.14,1.5),"Enamel",.1)
    for x in (-1.05,1.05):
        box("Cab window",(x,1.04,1.70),(.03,.70,.56),"Glass",.06)
        box("Destination plate",(x,.98,1.10),(.05,.74,.29),"Brass")
    roof=box("Curved cab roof",(0,1.06,2.21),(2.30,1.38,.20),"Seaweed",.13)
    for x in (-.73,.73):
        cylinder("Buffer",(x,-1.79,.68),.19,.23,"Iron","Y")
    box("Brass footboard",(0,-1.62,.48),(2.50,.28,.08),"Brass")


def consultation():
    box("Desk pedestal",(.55,-.65,1.03),(.52,.72,.50),"Wood")
    box("Oat desk",(.24,-.65,1.33),(1.14,.80,.09),"Oat",.09)
    box("Paper chart",(.13,-.69,1.40),(.24,.32,.024),"Paper",.008)
    screen=box("Monitor",(.61,-.43,1.58),(.34,.09,.29),"Iron")
    box("Monitor glass",(.61,-.485,1.58),(.28,.014,.22),"Glass",.009)
    chair((-.62,-.79,.80),"Berry")
    box("Exam bed",(.32,.76,1.01),(.88,1.02,.35),"Enamel",.10)
    box("Bed cushion",(.32,.73,1.23),(.89,1.04,.13),"Linen",.08)
    box("Pillow",(.32,1.01,1.33),(.65,.30,.12),"Linen",.09)
    plant((.87,-1.24,.79),.62)


def chair(p,role):
    x,y,z=p
    box("Chair seat",(x,y,z+.30),(.45,.47,.11),role,.07)
    box("Chair back",(x,y+.19,z+.58),(.46,.10,.54),role,.07)
    for dx in (-.16,.16):
        for dy in (-.16,.16):cylinder("Chair leg",(x+dx,y+dy,z+.13),.032,.26,"Wood",vertices=8)


def diagnostics():
    box("Scanner base",(.22,.32,.98),(1.10,1.70,.34),"Enamel",.10)
    torus("Friendly scanner ring",(.23,.77,1.60),.62,.16,"Linen","Y")
    torus("Scanner brass rim",(.23,.72,1.60),.64,.026,"Brass","Y")
    box("Scanner bed",(.22,-.20,1.17),(.77,1.60,.13),"Blue",.1)
    box("Pillow",(.22,.35,1.27),(.57,.29,.10),"Linen",.07)
    box("Console",(.72,-1.04,1.16),(.39,.47,.74),"Oat",.06)
    box("Console display",(.72,-1.10,1.56),(.33,.32,.035),"Glass",.015)


def recovery():
    for y in (-.76,.76):
        box("Recovery bed frame",(.31,y,1.01),(1.26,1.12,.36),"Wood",.05)
        box("Mattress",(.27,y,1.24),(1.25,1.11,.18),"Linen",.08)
        box("Woven blanket",(-.02,y,1.35),(.66,1.04,.08),"Flower" if y<0 else "Blue",.04)
        box("Headboard",(.92,y,1.41),(.10,1.17,.65),"Seaweed",.10)
        box("Pillow",(.63,y,1.38),(.31,.73,.14),"Linen",.08)
    plant((.87,0,.81),.78)


def platform():
    box("Platform slab",(0,0,.20),(2.7,3.8,.40),"Clay",.04)
    box("Platform edge",(1.28,0,.43),(.18,3.8,.10),"Oat",.02)
    for row in range(5):
        for col in range(3):
            box("Paving tile",(-.85+col*.83,-1.54+row*.77,.423),(.80,.74,.035),"Clay" if (row+col)%4 else "Oat",.018)


def rail():
    for y in [-1.7+i*.34 for i in range(11)]:box("Rail sleeper",(0,y,.045),(2.35,.15,.09),"Wood",.015)
    for x in (-.85,.85):box("Continuous rail",(x,0,.12),(.075,3.80,.13),"Iron",.018)


def bench():
    for y in (-.16,0,.16):box("Bench seat slat",(0,y,.47),(1.32,.13,.07),"Wood",.025)
    for z in (.73,.93):box("Bench back slat",(0,.23,z),(1.32,.075,.15),"Wood",.025)
    for x in (-.50,.50):
        box("Bench foot",(x,0,.24),(.09,.48,.48),"Iron")
        box("Bench upright",(x,.25,.68),(.07,.07,.64),"Iron")


def lamp():
    cylinder("Lamp foot",(0,0,.10),.20,.20,"Iron")
    cylinder("Lamp post",(0,0,1.05),.045,1.85,"Iron")
    box("Lamp light",(0,0,2.09),(.24,.24,.36),"Linen",.015)
    for x in (-.13,.13):
        for y in (-.13,.13):box("Lamp cage",(x,y,2.1),(.026,.026,.42),"Iron",.004)
    box("Lamp hood",(0,0,2.33),(.36,.36,.08),"Iron",.05)


def station():
    box("Station walls",(0,0,1.15),(3.5,2.6,2.3),"Oat",.05)
    for x in (-1.15,1.15):
        box("Station window",(x,-1.32,1.35),(.71,.05,.89),"Seaweed",.07)
        box("Station glass",(x,-1.36,1.36),(.53,.03,.69),"Glass",.035)
    box("Station door",(0,-1.32,.97),(.74,.07,1.88),"Seaweed",.08)
    box("Station sign",(0,-1.37,2.03),(1.58,.10,.32),"Paper",.035)
    for side in (-1,1):
        roof=box("Station roof",(side*.90,0,2.65),(2.13,3.02,.14),"Seaweed",.04)
        roof.rotation_euler.y=side*.37
    cylinder("Station clock",(0,-1.44,2.04),.115,.055,"Brass","Y")
    cylinder("Clock face",(0,-1.48,2.04),.093,.018,"Linen","Y")
    box("Station step",(0,-1.62,.12),(1.15,.57,.24),"Clay")
    plant((-1.49,-1.69,0),1.2)


def tree():
    cylinder("Tree trunk",(0,0,.88),.14,1.76,"Wood",vertices=12)
    for i in range(7):
        a=i*2.39996
        ellipsoid("Soft tree crown",(math.cos(a)*.47,math.sin(a)*.47,1.77+(i%3)*.25),(.61,.54,.61),"Leaf" if i%2 else "LeafLight",12)


def garden():
    box("Garden soil",(0,0,.10),(1.55,2.25,.20),"Wood",.11)
    for y in (-1.1,1.1):box("Garden raised end",(0,y,.21),(1.72,.11,.38),"Seaweed")
    for x in (-.82,.82):box("Garden raised side",(x,0,.21),(.11,2.20,.38),"Seaweed")
    for i in range(13):
        x=(-.52+(i%3)*.51);y=-.89+(i//3)*.43;z=.32+(i%4)*.07
        cylinder("Flower stem",(x,y,z),.018,z*1.2,"Leaf",vertices=6)
        for j in range(5):
            a=j*math.tau/5
            ellipsoid("Garden petal",(x+math.cos(a)*.075,y+math.sin(a)*.075,z*1.65),(.076,.060,.027),"Linen" if i%2 else "Flower",10)
        ellipsoid("Flower heart",(x,y,z*1.65+.02),(.046,.046,.031),"Brass",10)


def human(crew=False):
    role="Blue" if crew else "Berry"
    body=ellipsoid("Body",(0,0,.66),(.19,.13,.28),role)
    ellipsoid("Head",(0,-.015,1.07),(.145,.13,.17),"SkinLight" if crew else "Skin")
    ellipsoid("Hair",(0,.025,1.19),(.15,.13,.095),"Hair")
    for x in (-.047,.047):ellipsoid("Eye",(x,-.139,1.10),(.014,.009,.018),"Hair",10)
    if crew:box("Linen apron",(0,-.115,.66),(.24,.04,.34),"Linen",.025)
    for side,letter in ((-1,"L"),(1,"R")):
        arm=ellipsoid("Arm_"+letter,(side*.225,0,.66),(.060,.065,.25),role)
        leg=ellipsoid("Leg_"+letter,(side*.085,0,.26),(.071,.082,.245),"Seaweed")
        for obj,pivot in ((arm,(side*.205,0,.87)),(leg,(side*.085,0,.47))):
            bpy.context.scene.cursor.location=pivot
            bpy.context.view_layer.objects.active=obj
            bpy.ops.object.select_all(action="DESELECT")
            obj.select_set(True)
            bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        hand=ellipsoid("Hand",(side*.225,-.014,.42),(.059,.062,.076),"SkinLight" if crew else "Skin")
        shoe=ellipsoid("Shoe",(side*.085,-.041,.08),(.085,.13,.066),"Iron")
        for limb,part in ((arm,hand),(leg,shoe)):
            bpy.ops.object.select_all(action="DESELECT")
            limb.select_set(True);part.select_set(True)
            bpy.context.view_layer.objects.active=limb
            bpy.ops.object.join()


def export_asset(name, make, preserve=False):
    collection=bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    layer=bpy.context.view_layer.layer_collection.children[collection.name]
    bpy.context.view_layer.active_layer_collection=layer
    make()
    objects=list(collection.objects)
    for ob in objects:
        bpy.ops.object.select_all(action="DESELECT")
        ob.select_set(True)
        bpy.context.view_layer.objects.active=ob
        bpy.ops.object.convert(target="MESH")
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if not preserve:
        for role in MATS:
            group=[o for o in list(collection.objects) if o.type=="MESH" and o.data.materials and o.data.materials[0].name==role]
            if not group:continue
            bpy.ops.object.select_all(action="DESELECT")
            for ob in group:ob.select_set(True)
            bpy.context.view_layer.objects.active=group[0]
            bpy.ops.object.join()
            bpy.context.object.name=role
    pivot=bpy.data.objects.new(name,None)
    collection.objects.link(pivot)
    for ob in list(collection.objects):
        if ob!=pivot:ob.parent=pivot
    bpy.ops.object.select_all(action="DESELECT")
    for ob in collection.objects:ob.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+".fbx")),use_selection=True,object_types={"EMPTY","MESH"},
        apply_unit_scale=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,
        axis_forward="-Z",axis_up="Y",path_mode="AUTO")
    triangles=sum(len(o.data.polygons) for o in collection.objects if o.type=="MESH")
    print(f"LIFELINE_ASSET {name}: {triangles} polygons, {(OUT/(name+'.fbx')).stat().st_size} bytes")
    return pivot


OUT.mkdir(parents=True,exist_ok=True)
ART.mkdir(parents=True,exist_ok=True)


def render_preview():
    def linear(value):return value/12.92 if value<=.04045 else ((value+.055)/1.055)**2.4
    for mat in bpy.data.materials:
        role=mat.name.split('.')[0]
        if role in COLORS and mat.use_nodes:
            mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=(*[linear(c) for c in COLORS[role]],1)
    source={name:bpy.data.collections[name] for name in ("Carriage","Locomotive","Consultation","Diagnostics","Recovery","Platform","Rail","Bench","Lamp","Station","Tree","Garden","Resident","Crew")}
    for collection in source.values(): collection.hide_render=True
    stage=bpy.data.collections.new("Preview stage")
    bpy.context.scene.collection.children.link(stage)
    bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[stage.name]
    def place(name,p,rotation=0,scale=1):
        src=next(o for o in source[name].objects if o.parent is None)
        root=src.copy();stage.objects.link(root)
        root.location=p;root.rotation_euler=(0,0,rotation);root.scale=(scale,scale,scale)
        for item in src.children:
            clone=item.copy();stage.objects.link(clone)
            local=item.matrix_local.copy();clone.parent=root;clone.matrix_local=local
        return root
    for slot in range(4):
        y=4.65-slot*3.8
        place("Carriage",(0,y,0))
        place(("Consultation","Diagnostics","Recovery","Consultation")[slot],(0,y,0))
        place("Crew",(-.56,y+.59,.79),-.9,.84)
        place("Resident",(-.58,y-.62,.79),-1.5,.81)
    place("Locomotive",(0,8.35,0),math.pi)
    for slot in range(-1,5):
        y=4.65-slot*3.8
        place("Platform",(-2.93,y,0));place("Rail",(0,y,0))
    for i in range(5):
        y=5.8-i*3.7
        place("Bench",(-3.8,y,.45),math.pi/2,.82)
        place("Lamp",(-4.1,y+1.3,.45),0,.82)
    for i in range(7):place("Tree",(3.4 if i%2 else -6.6,8.0-i*2.7,0),0,.80+(i%3)*.14)
    place("Station",(-6.15,-6.7,0),-math.pi/2)
    place("Garden",(-5.25,2.2,0))
    for i in range(5):place("Resident",(-2.2,5.6-i*.61,.45),math.pi,.80)
    box("Meadow plinth",(-1.5,0,-.18),(14,27,.35),"LeafLight",.5)
    box("Studio ground",(0,0,-.65),(200,200,.3),"Paper",0)
    bpy.ops.object.camera_add(location=(-13,18,23))
    camera=bpy.context.object
    target=Vector((-1.15,-.1,.7))
    camera.rotation_euler=(target-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.type="ORTHO";camera.data.ortho_scale=24.5
    scene=bpy.context.scene;scene.camera=camera
    scene.world=bpy.data.worlds.new("Lifeline studio")
    scene.world.use_nodes=True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value=(.83,.85,.79,1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value=.55
    bpy.ops.object.light_add(type="AREA",location=(-10,9,20))
    light=bpy.context.object;light.data.energy=3200;light.data.shape="DISK";light.data.size=12
    light.rotation_euler=(Vector((0,0,0))-light.location).to_track_quat("-Z","Y").to_euler()
    scene.render.engine="CYCLES";scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.resolution_x=1000;scene.render.resolution_y=1300;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG";scene.render.image_settings.color_mode="RGB"
    scene.render.film_transparent=False
    scene.view_settings.view_transform="Standard"
    scene.render.filepath=str(ART/"LifelineTrainPreview.png")
    bpy.ops.render.render(write_still=True)
    for obj in list(stage.objects):
        if obj.type not in {"CAMERA","LIGHT"}:bpy.data.objects.remove(obj,do_unlink=True)
    place("Locomotive",(0,0,0))
    # A small living leaf beside the cab is a care motif, authored as real geometry.
    leaf=ellipsoid("Care leaf",(.67,.9,2.67),(.16,.085,.45),"Leaf")
    leaf.rotation_euler=(.15,-.65,-.3)
    leaf=ellipsoid("Care leaf small",(.34,.9,2.62),(.13,.07,.31),"LeafLight")
    leaf.rotation_euler=(0,.55,.25)
    box("Icon ground",(0,0,-.24),(200,200,.3),"Oat",0)
    camera.location=(5,-7,5.2)
    camera.rotation_euler=(Vector((0,-.08,1.2))-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale=4.5
    light.location=(-3,-4,8);light.data.energy=1050;light.data.size=5
    light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat("-Z","Y").to_euler()
    scene.render.resolution_x=1024;scene.render.resolution_y=1024;scene.cycles.samples=48
    scene.render.filepath=str(ART/"AppIcon.png")
    bpy.ops.render.render(write_still=True)
    print("LIFELINE_PREVIEWS_COMPLETE")


if PREVIEW_ONLY:
    render_preview()
else:
    creators=[("Carriage",carriage), ("Locomotive",locomotive), ("Consultation",consultation),
        ("Diagnostics",diagnostics), ("Recovery",recovery), ("Platform",platform), ("Rail",rail),
        ("Bench",bench), ("Lamp",lamp), ("Station",station), ("Tree",tree), ("Garden",garden)]
    roots=[]
    for name,creator in creators:roots.append(export_asset(name,creator))
    roots.append(export_asset("Resident",lambda:human(False),True))
    roots.append(export_asset("Crew",lambda:human(True),True))
    # An editable asset gallery; the FBX files above retain their gameplay origins.
    for index,root in enumerate(roots):root.location=(index%4*4.8,index//4*5.0,0)
    bpy.context.scene.cursor.location=(0,0,0)
    SOURCE.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
    print("LIFELINE_COMPLETE",SOURCE)
