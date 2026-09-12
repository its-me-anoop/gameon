"""Original fixed-clinic furniture and skinned, articulated characters.

Run using Blender -b --python Tools/create_clinic_assets.py. This separate
process never reads or changes the user's open Blender scene.
"""
from pathlib import Path
import math
import sys
import bpy
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Unity/OrbitOrchard/Assets/IdleClinic/Art/Resources/Clinic/Models"
SOURCE = ROOT / "assets/idle-clinic-game.blend"
ICON_ONLY="--icon-only" in sys.argv
SIGN_ONLY="--sign-only" in sys.argv
if ICON_ONLY or SIGN_ONLY:bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
else:bpy.ops.wm.read_factory_settings(use_empty=True)
OUT.mkdir(parents=True, exist_ok=True)
COLORS = {
    "Ivory": (.91,.89,.79), "Linen": (.96,.94,.86), "Sage": (.40,.57,.45),
    "SageDark": (.22,.36,.28), "Apricot": (.84,.49,.31), "Gold": (.76,.56,.23),
    "Ink": (.20,.27,.26), "Blue": (.41,.62,.64), "Skin": (.78,.56,.39),
    "Wood": (.59,.40,.27), "Clay": (.74,.55,.41), "Leaf": (.31,.49,.30),
}
MATS = {}
for role, rgb in COLORS.items():
    mat=bpy.data.materials.get(role) or bpy.data.materials.new(role);mat.diffuse_color=(*rgb,1);mat.use_nodes=True
    node=mat.node_tree.nodes.get("Principled BSDF")
    node.inputs["Base Color"].default_value=(*rgb,1)
    node.inputs["Roughness"].default_value=.6
    node.inputs["Metallic"].default_value=.45 if role=="Gold" else 0
    MATS[role]=mat

# All source dimensions and sockets are specified in the Unity game frame.
# FBX's handedness conversion is applied to furniture, bones and sockets alike.
def u(p): return Vector((-p[0],-p[2],p[1]))
def material(ob,role,smooth=False):
    ob.data.materials.append(MATS[role])
    for face in ob.data.polygons:face.use_smooth=smooth
    return ob
def box(name,p,size,role,bevel=.035):
    bpy.ops.mesh.primitive_cube_add(size=1,location=u(p));ob=bpy.context.object;ob.name=name
    ob.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    material(ob,role)
    if bevel:
        mod=ob.modifiers.new("Soft edges","BEVEL");mod.width=bevel;mod.segments=2
        ob.modifiers.new("Weighted normals","WEIGHTED_NORMAL")
    return ob
def orb(name,p,size,role,segments=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=8,location=u(p))
    ob=bpy.context.object;ob.name=name;ob.scale=(size[0],size[2],size[1]);return material(ob,role,True)
def tube(name,a,b,radius,role,vertices=12):
    av,bv=u(a),u(b);direction=bv-av
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=direction.length,location=(av+bv)/2)
    ob=bpy.context.object;ob.name=name;ob.rotation_euler=direction.to_track_quat("Z","Y").to_euler()
    return material(ob,role,True)
def socket(asset,name,p,forward=(0,0,1)):
    ob=bpy.data.objects.new(asset+"__"+name,None);bpy.context.collection.objects.link(ob)
    ob.location=u(p);ob.empty_display_size=.10
    # Socket position is authoritative; facing is assigned explicitly by the renderer.
    return ob
def plant(p,scale=1):
    x,y,z=p
    tube("Planter",(x,y,z),(x,y+.30*scale,z),.20*scale,"Clay")
    for i in range(5):
        a=i*2.399
        ob=orb("Leaf",(x+math.sin(a)*.11*scale,y+.55*scale,z+math.cos(a)*.11*scale),(.10*scale,.30*scale,.07*scale),"Leaf")
        ob.rotation_euler=(.2*math.sin(a),.25*math.cos(a),a)

def desk():
    box("Sculpted counter",(0,.47,0),(1.6,.94,.68),"Sage",.11)
    box("Floating ivory top",(0,1.00,0),(1.73,.10,.83),"Ivory",.09)
    box("Apricot inset",(0,.52,-.354),(1.40,.52,.035),"Apricot",.045)
    for x in (-.60,-.40,-.20,0,.20,.40,.60):box("Counter fluting",(x,.51,-.38),(.026,.45,.027),"Gold",.008)
    tube("Monitor stand",(-.36,1.05,.10),(-.36,1.27,.10),.035,"Ink")
    box("Reception monitor",(-.36,1.34,.13),(.42,.32,.065),"Ink",.03)
    box("Monitor screen",(-.36,1.34,.17),(.35,.25,.018),"Blue",.02)
    box("Payment tray",(.51,1.07,-.15),(.42,.04,.32),"Wood",.04)
    box("Visitor register",(.02,1.07,.08),(.24,.025,.32),"Linen",.015)
    tube("Counter bell",(.10,1.06,-.22),(.10,1.15,-.22),.07,"Gold")
    socket("ReceptionDesk","patient",(0,0,-.89))
    socket("ReceptionDesk","staff",(0,0,.70))
    socket("ReceptionDesk","cash",(.51,1.17,-.15))

def seat():
    for x in (-.23,.23):
        for z in (-.22,.22):tube("Seat foot",(x,.04,z),(x,.40,z),.035,"Wood")
    box("Seat cushion",(0,.40,0),(.61,.13,.58),"Apricot",.10)
    box("Padded back",(0,.73,.26),(.63,.57,.13),"Sage",.11)
    for x in (-.35,.35):
        tube("Arm support",(x,.36,.14),(x,.62,.14),.027,"Gold")
        box("Armrest",(x,.63,0),(.07,.08,.53),"Wood",.035)
    socket("Seat","patient",(0,0,0))

def treatment():
    for x in (-.25,.25):
        for z in (-.24,.24):tube("Chair frame",(x,.04,z),(x,.40,z),.042,"Ink")
    box("Treatment cushion",(0,.40,0),(.64,.14,.61),"Blue",.10)
    box("Treatment chair back",(0,.78,.28),(.65,.65,.14),"Blue",.11)
    box("Headrest",(0,1.11,.27),(.40,.20,.15),"Linen",.08)
    for x in (-.36,.36):box("Clinical armrest",(x,.63,-.02),(.11,.08,.51),"Ivory",.04)
    box("Clinical cabinet",(-.65,.50,.77),(.60,1.0,.52),"Ivory",.06)
    box("Cabinet sage face",(-.65,.51,.493),(.52,.81,.018),"Sage",.025)
    for y in (.28,.55,.82):box("Drawer handle",(-.65,y,.472),(.18,.025,.025),"Gold",.007)
    for x in (-.8,-.58):
        tube("Bottle",(x,1.01,.79),(x,1.16,.79),.05,"Blue")
        box("Bottle cap",(x,1.18,.79),(.07,.03,.07),"Linen",.009)
    tube("Task lamp",(.44,.07,.73),(.44,1.5,.73),.025,"Gold")
    tube("Lamp arm",(.44,1.50,.73),(.12,1.62,.46),.025,"Gold")
    orb("Exam lamp",(.12,1.61,.43),(.17,.09,.12),"Ivory")
    socket("TreatmentBay","patient",(0,0,0))
    socket("TreatmentBay","staff",(.83,0,-.06))

def bench():
    for x in (-.7,.7):box("Bench leg",(x,.19,0),(.07,.38,.50),"Gold")
    box("Waiting seat",(0,.4,0),(1.8,.12,.60),"Apricot",.08)
    box("Waiting back",(0,.74,.26),(1.82,.57,.13),"Sage",.08)
    socket("WaitingBench","seat0",(-.58,0,0));socket("WaitingBench","seat1",(0,0,0));socket("WaitingBench","seat2",(.58,0,0))

def cupboard():
    box("Supply cupboard",(0,.80,0),(1.50,1.60,.50),"Ivory",.07)
    for x in (-.38,.38):
        box("Cupboard door",(x,.83,-.267),(.70,1.37,.035),"Sage",.04)
        box("Cupboard handle",(x+(.24 if x<0 else -.24),.82,-.30),(.025,.21,.04),"Gold",.01)
    box("Folded linen",(0,1.64,0),(.58,.09,.35),"Linen",.025)

def action_points(kind,t):
    bob=.012*math.sin(t*math.tau) if kind=="Idle" else 0
    p={"root":(0,0,0),"pelvis":(0,.74+bob,0),"spine":(0,.93+bob,0),"chest":(0,1.10+bob,0),"neck":(0,1.24+bob,0),"head":(0,1.38+bob,0),"head_tip":(0,1.53+bob,0)}
    sitting=kind=="Sit"
    if sitting:
        for key in ("pelvis","spine","chest","neck","head","head_tip"):
            q=p[key];p[key]=(q[0],q[1]-.19,q[2])
    for side,label in ((-1,"L"),(1,"R")):
        wave=math.sin(t*math.tau+(math.pi if side>0 else 0))
        shoulder=(side*.225,p["chest"][1],0)
        elbow=(side*.27,p["chest"][1]-.23,0)
        hand=(side*.285,p["chest"][1]-.46,.015)
        hip=(side*.115,p["pelvis"][1]-.01,0)
        knee=(side*.115,.39,.015);ankle=(side*.115,.09,.015);toe=(side*.115,.07,.18)
        if kind=="Walk":
            knee=(side*.115,.41+max(0,wave)*.055,wave*.15)
            ankle=(side*.115,.09+max(0,wave)*.12,wave*.27)
            toe=(side*.115,ankle[1]-.02,ankle[2]+.17)
            elbow=(side*.27,.89,-wave*.12);hand=(side*.28,.67,-wave*.23)
        if kind in ("CheckIn","Treat"):
            elbow=(side*.29,.96,.12)
            hand=(side*.18,1.03+math.sin(t*math.tau+side)*.025,.39+math.sin(t*math.tau)*.025)
        if kind=="Call" and side>0:
            elbow=(.36,1.20,.05);hand=(.33+math.sin(t*math.tau)*.07,1.43,.13)
        if sitting:
            knee=(side*.115,.40,.32);ankle=(side*.115,.09,.33);toe=(side*.115,.07,.49)
            elbow=(side*.28,.70,.07);hand=(side*.20,.54,.20)
        p.update({"shoulder."+label:shoulder,"elbow."+label:elbow,"hand."+label:hand,"hand_tip."+label:(hand[0],hand[1]-.05,hand[2]+.05),
            "hip."+label:hip,"knee."+label:knee,"ankle."+label:ankle,"toe."+label:toe})
    return p

def human(name,uniform):
    rest=action_points("Idle",0)
    links=[("root","root","pelvis",None),("pelvis","pelvis","spine","root"),("spine","spine","chest","pelvis"),("chest","chest","neck","spine"),("neck","neck","head","chest"),("head","head","head_tip","neck")]
    for side in ("L","R"):
        links += [("upper_arm."+side,"shoulder."+side,"elbow."+side,"chest"),("forearm."+side,"elbow."+side,"hand."+side,"upper_arm."+side),("hand."+side,"hand."+side,"hand_tip."+side,"forearm."+side),
            ("upper_leg."+side,"hip."+side,"knee."+side,"pelvis"),("lower_leg."+side,"knee."+side,"ankle."+side,"upper_leg."+side),("foot."+side,"ankle."+side,"toe."+side,"lower_leg."+side)]
    bpy.ops.object.armature_add();rig=bpy.context.object;rig.name=name+"Rig"
    bpy.ops.object.mode_set(mode="EDIT")
    for bone in list(rig.data.edit_bones):rig.data.edit_bones.remove(bone)
    for bone,a,b,parent in links:
        item=rig.data.edit_bones.new(bone);item.head=u(rest[a]);item.tail=u(rest[b])
        if parent:item.parent=rig.data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    parts=[]
    def skin(ob,bone):
        bpy.context.view_layer.objects.active=ob
        bpy.ops.object.select_all(action="DESELECT");ob.select_set(True)
        bpy.ops.object.convert(target="MESH");bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        group=ob.vertex_groups.new(name=bone);group.add(list(range(len(ob.data.vertices))),1,"REPLACE");parts.append(ob)
    skin(orb("Tailored torso",(0,1.00,0),(.225,.235,.135),uniform,16),"spine")
    skin(orb("Hip",(0,.74,0),(.20,.15,.135),"Ink"),"pelvis")
    skin(orb("Face",(0,1.38,.015),(.16,.185,.145),"Skin",16),"head")
    skin(orb("Sculpted hair",(0,1.50,-.015),(.165,.095,.15),"Ink"),"head")
    for x in (-.055,.055):skin(orb("Eye",(x,1.40,.148),(.014,.018,.009),"Ink",8),"head")
    skin(orb("Nose",(0,1.36,.157),(.030,.025,.023),"Skin",8),"head")
    skin(box("Shirt placket",(0,1.00,.132),(.045,.25,.012),"Linen",.005),"spine")
    if name!="Patient":skin(box("Name badge",(-.115,1.09,.142),(.085,.055,.018),"Gold",.008),"spine")
    for side in ("L","R"):
        for bone,a,b,r,role in (("upper_arm.","shoulder.","elbow.",.075,uniform),("forearm.","elbow.","hand.",.061,uniform),("upper_leg.","hip.","knee.",.092,"Ink"),("lower_leg.","knee.","ankle.",.071,"Ink")):
            skin(tube(bone+side,rest[a+side],rest[b+side],r,role),bone+side)
            skin(orb("Joint",rest[a+side],(r,r,r),role),bone+side)
        skin(orb("Hand",rest["hand."+side],(.06,.065,.057),"Skin"),"hand."+side)
        ankle=rest["ankle."+side]
        skin(orb("Shoe",(ankle[0],.07,.10),(.10,.065,.145),"Ink"),"foot."+side)
    bpy.ops.object.select_all(action="DESELECT")
    for ob in parts:ob.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();mesh=bpy.context.object;mesh.name=name+"Skin"
    mesh.parent=rig;mesh.matrix_parent_inverse=rig.matrix_world.inverted()
    modifier=mesh.modifiers.new("Articulated clinic skeleton","ARMATURE");modifier.object=rig
    rig.animation_data_create()
    for kind in ("Idle","Walk","CheckIn","Treat","Sit","Call"):
        action=bpy.data.actions.new(name+"_"+kind);rig.animation_data.action=action
        for frame in range(1,34,4):
            bpy.context.scene.frame_set(frame);pose=action_points(kind,(frame-1)/32)
            matrices={}
            for bone,a,b,parent in links:
                rest_bone=rig.data.bones[bone]
                target_a,target_b=u(pose[a]),u(pose[b])
                delta=(rest_bone.tail_local-rest_bone.head_local).rotation_difference(target_b-target_a)
                matrix=delta.to_matrix().to_4x4() @ rest_bone.matrix_local
                matrix.translation=target_a;matrices[bone]=matrix
            for bone,a,b,parent in links:
                pb=rig.pose.bones[bone];rest_bone=rig.data.bones[bone]
                # Compute local channels from the desired parent pose, not yesterday's
                # evaluated dependency graph. This also keeps the seated head attached.
                basis=rest_bone.matrix_local.inverted()
                if parent:basis=basis @ rig.data.bones[parent].matrix_local @ matrices[parent].inverted()
                pb.matrix_basis=basis @ matrices[bone]
                pb.rotation_mode="QUATERNION"
                pb.keyframe_insert("location",frame=frame);pb.keyframe_insert("rotation_quaternion",frame=frame);pb.keyframe_insert("scale",frame=frame)
            bpy.context.view_layer.update()
        bpy.context.scene.frame_set(17);bpy.context.view_layer.update()
        expected=action_points(kind,.5)
        for bone,a,b,parent in links:
            error=(rig.pose.bones[bone].head-u(expected[a])).length
            assert error<.002, f"{name} {kind} {bone} detached from authored joint: {error}"
        action.use_fake_user=True
        track=rig.animation_data.nla_tracks.new();track.name=kind
        strip=track.strips.new(kind,1,action);strip.action_frame_start=1;strip.action_frame_end=33
        track.mute=True
    rig.animation_data.action=None
    for track in rig.animation_data.nla_tracks:track.mute=False
    return rig

def clinic_sign():
    curve=bpy.data.curves.new("Little Lifeline raised lettering","FONT")
    curve.body="Little Lifeline";curve.align_x="CENTER";curve.align_y="CENTER"
    curve.font=bpy.data.fonts.load(str(ROOT/"Unity/OrbitOrchard/Assets/IdleClinic/Resources/Fonts/ClinicDisplay.ttf"))
    curve.size=1;curve.extrude=.012;curve.bevel_depth=.002;curve.bevel_resolution=1;curve.resolution_u=5
    ob=bpy.data.objects.new("Clinic name lettering",curve);bpy.context.collection.objects.link(ob)
    bpy.ops.object.select_all(action="DESELECT");ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.convert(target="MESH")
    width=max(v.co.x for v in ob.data.vertices)-min(v.co.x for v in ob.data.vertices)
    scale=2.90/width
    for vertex in ob.data.vertices:
        p=vertex.co.copy();vertex.co=u((p.x*scale,p.y*scale,-p.z))
    material(ob,"Linen")

def export(name,make,character=False):
    collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection)
    bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[collection.name]
    make()
    if not character:
        for ob in list(collection.objects):
            if ob.type!="MESH":continue
            bpy.ops.object.select_all(action="DESELECT");ob.select_set(True);bpy.context.view_layer.objects.active=ob
            bpy.ops.object.convert(target="MESH")
        for role in MATS:
            group=[o for o in collection.objects if o.type=="MESH" and o.data.materials[0].name==role]
            if not group:continue
            bpy.ops.object.select_all(action="DESELECT")
            for ob in group:ob.select_set(True)
            bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join();bpy.context.object.name=name+"_"+role
        root=bpy.data.objects.new(name,None);collection.objects.link(root)
        for ob in list(collection.objects):
            if ob!=root:ob.parent=root
    else:root=next(o for o in collection.objects if o.type=="ARMATURE")
    bpy.ops.object.select_all(action="DESELECT")
    for ob in collection.objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.context.scene.frame_set(1)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+".fbx")),use_selection=True,object_types={"EMPTY","MESH","ARMATURE"},
        add_leaf_bones=False,axis_forward="-Z",axis_up="Y",bake_anim=character,bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=character,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0)
    if character:
        for track in root.animation_data.nla_tracks:track.mute=True
        root.animation_data.action=root.animation_data.nla_tracks[0].strips[0].action
    print("CLINIC_ASSET",name,(OUT/(name+".fbx")).stat().st_size,flush=True)
    return root

if SIGN_ONLY:
    for ob in list(bpy.data.objects):
        if ob.name.startswith("ClinicSign"):bpy.data.objects.remove(ob,do_unlink=True)
    root=export("ClinicSign",clinic_sign);root.location=u((6.6,0,10.2))
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
elif not ICON_ONLY:
    roots=[]
    for name,make in (("ReceptionDesk",desk),("TreatmentBay",treatment),("Seat",seat),("WaitingBench",bench),("Cupboard",cupboard),("Plant",lambda:plant((0,0,0))),("ClinicSign",clinic_sign)):
        roots.append(export(name,make))
    for name,uniform in (("Patient","Apricot"),("Receptionist","Sage"),("Nurse","Blue")):
        roots.append(export(name,lambda n=name,c=uniform:human(n,c),True))
    for i,root in enumerate(roots):root.location=u(((i%3)*3.3,0,(i//3)*3.4))
    bpy.context.scene.frame_set(1)
    SOURCE.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
    print("CLINIC_COMPLETE",SOURCE,flush=True)

def render_icon():
    """A real geometry render of the clinic nurse and a sage care bag, with no text or baked border."""
    for collection in bpy.data.collections:collection.hide_render=collection.name!="Nurse"
    root=bpy.data.objects["NurseRig"];root.location=u((.54,0,.28));root.rotation_euler[2]=math.pi
    root.animation_data.action=bpy.data.actions["Nurse_Call"]
    bpy.context.scene.frame_set(9)
    collection=bpy.data.collections.new("App icon sculpture");bpy.context.scene.collection.children.link(collection)
    bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[collection.name]
    box("Care bag",(-.22,.54,-.32),(1.36,1.00,.56),"Sage",.18)
    box("Stitched front pocket",(-.22,.50,-.619),(1.06,.70,.055),"SageDark",.13)
    for x in (-.61,.17):
        tube("Brass handle riser",(x,1.03,-.31),(x,1.24,-.31),.060,"Gold")
    box("Curved handle grip",(-.22,1.26,-.31),(.88,.13,.13),"Wood",.06)
    for x in (-.74,.30):box("Bag latch",(x,.97,-.637),(.11,.18,.035),"Gold",.025)
    box("Ivory care symbol",(-.22,.52,-.66),(.46,.145,.04),"Linen",.045)
    box("Ivory care symbol",(-.22,.52,-.676),(.145,.46,.04),"Linen",.045)
    box("Studio floor",(0,-.10,0),(200,.15,200),"Ivory",0)
    scene=bpy.context.scene;scene.render.engine="CYCLES";scene.cycles.samples=48
    scene.render.resolution_x=1024;scene.render.resolution_y=1024;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG";scene.render.image_settings.color_mode="RGB";scene.render.film_transparent=False
    bpy.ops.object.camera_add(location=(3.4,6,3.8));camera=bpy.context.object
    camera.rotation_euler=(Vector((0,0,.90))-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.type="ORTHO";camera.data.ortho_scale=2.45;scene.camera=camera
    bpy.ops.object.light_add(type="AREA",location=(1,4,7));key=bpy.context.object
    key.data.energy=650;key.data.shape="DISK";key.data.size=5
    scene.world=bpy.data.worlds.new("Warm clinic studio");scene.world.use_nodes=True
    scene.world.node_tree.nodes.get("Background").inputs[0].default_value=(.82,.83,.75,1)
    scene.world.node_tree.nodes.get("Background").inputs[1].default_value=.70
    scene.view_settings.view_transform="Standard";scene.view_settings.exposure=-.55
    scene.render.filepath=str(OUT.parents[2]/"AppIcon.png")
    bpy.ops.render.render(write_still=True)
    print("CLINIC_ICON",scene.render.filepath,flush=True)

if "--icon" in sys.argv or ICON_ONLY:render_icon()
