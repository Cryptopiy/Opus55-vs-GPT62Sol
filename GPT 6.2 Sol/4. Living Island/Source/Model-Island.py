import bpy
import math
import os
import random
import re
import bmesh
from mathutils import Vector

EXPECTED = r"<PROJECT_ROOT>"
assert os.path.normcase(bpy.data.filepath) == os.path.normcase(EXPECTED)
config_path = os.path.join(os.path.dirname(EXPECTED), "Assets", "Island", "IslandConfig.cs")
with open(config_path, encoding="utf-8") as config_file:
    config_text = config_file.read()

def setting(name):
    return int(re.search(rf"\b{name}\s*=\s*(\d+)", config_text).group(1))

SEED = setting("Seed")
TAU = math.tau
OUT = os.path.join(os.path.dirname(EXPECTED), "Assets", "Models")
os.makedirs(OUT, exist_ok=True)
old = bpy.data.collections.get("Living Island assets")
if old:
    for obj in list(old.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(old)
collection = bpy.data.collections.new("Living Island assets")
bpy.context.scene.collection.children.link(collection)
bpy.context.scene.unit_settings.system = 'METRIC'

def material(name, rgb, rough=.8, metallic=0, emission=0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    shader = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = (*rgb, 1)
    shader.inputs["Roughness"].default_value = rough
    shader.inputs["Metallic"].default_value = metallic
    if emission:
        shader.inputs["Emission Color"].default_value = (*rgb, 1)
        shader.inputs["Emission Strength"].default_value = emission
    return mat

cream = material("Saltwashed ivory", (.77,.68,.48))
red = material("Burnt coral paint", (.46,.13,.084))
teal = material("Oxidized copper", (.023,.151,.14),.5,.26)
black = material("Lantern iron", (.027,.038,.036),.43,.24)
wood = material("Honey oak", (.36,.177,.066))
dark_wood = material("End grain", (.175,.081,.046))
stone = material("Coastal basalt", (.22,.236,.212))
stone_light = material("Stone inclusions", (.31,.318,.26))
window = material("NightWindow amber", (.83,.39,.098),.28,0,2.0)
lamp = material("Beacon lamp", (1,.71,.27),.24,0,3.6)
bark = material("Palm bark", (.26,.169,.085))
bark_ring = material("Palm collar", (.33,.22,.113))
leaf = material("PalmFrond", (.092,.30,.065))
leaf_gold = material("PalmFrond gold", (.20,.37,.066))

class Model:
    def __init__(self):
        self.v, self.f, self.m, self.uv, self.smooth = [],[],[],[],[]

    def add(self, v, faces, mat, uv=None, smooth=False):
        start = len(self.v)
        self.v.extend(v)
        for f in faces:
            self.f.append(tuple(start+i for i in f))
            self.m.append(mat)
            self.uv.append([uv[i] if uv else (v[i][0]*.33+v[i][1]*.21, v[i][2]*.33+v[i][1]*.19) for i in f])
            self.smooth.append(smooth)

    def box(self, mat, cx,cy,cz, sx,sy,sz, angle=0):
        verts=[]
        for z in (-1,1):
            for x,y in ((-1,-1),(1,-1),(1,1),(-1,1)):
                x,y = x*sx/2,y*sy/2
                verts.append((cx+x*math.cos(angle)-y*math.sin(angle),cy+x*math.sin(angle)+y*math.cos(angle),cz+z*sz/2))
        self.add(verts,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat)

    def tube(self, mat, rings, sides=20, cap=True):
        verts = [(cx+r*math.cos(TAU*i/sides),cy+r*math.sin(TAU*i/sides),z)
                 for cx,cy,z,r in rings for i in range(sides)]
        faces = [(j*sides+i,j*sides+(i+1)%sides,(j+1)*sides+(i+1)%sides,(j+1)*sides+i)
                 for j in range(len(rings)-1) for i in range(sides)]
        if cap:
            faces += [tuple(range(sides-1,-1,-1)),tuple((len(rings)-1)*sides+i for i in range(sides))]
        self.add(verts,faces,mat,smooth=sides>=10)

    def bar(self, mat, a,b, radius, sides=8, rng=None):
        axis=(Vector(b)-Vector(a)).normalized()
        u=axis.cross(Vector((0,0,1)))
        if u.length < .01: u=axis.cross(Vector((0,1,0)))
        u.normalize()
        v=axis.cross(u)
        verts=[]
        for t in ((0,.53,1) if rng else (0,1)):
            p=Vector(a).lerp(Vector(b),t)
            for j in range(sides):
                theta=TAU*j/sides
                rough=rng.uniform(.78,1.16) if rng else 1
                verts.append(p+radius*(1-.25*t)*rough*(u*math.cos(theta)+v*math.sin(theta)))
        rows=len(verts)//sides
        indices=[(r*sides+j,r*sides+(j+1)%sides,(r+1)*sides+(j+1)%sides,(r+1)*sides+j)
                 for r in range(rows-1) for j in range(sides)]
        self.add(verts,indices,mat)
        self.add([verts[i] for i in range(sides)], [tuple(range(sides-1,-1,-1))],dark_wood if rng else mat)
        self.add([verts[(rows-1)*sides+i] for i in range(sides)], [tuple(range(sides))],dark_wood if rng else mat)

    def create(self, name):
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata(self.v,[],self.f)
        mesh.update()
        materials=list(dict.fromkeys(self.m))
        for mat in materials: mesh.materials.append(mat)
        layer=mesh.uv_layers.new(name="UVMap")
        for polygon, mat, uv, smooth in zip(mesh.polygons,self.m,self.uv,self.smooth):
            polygon.material_index=materials.index(mat)
            polygon.use_smooth=smooth
            for idx,value in zip(polygon.loop_indices,uv):
                layer.data[idx].uv=value
        # Weld coincident material borders while leaving loop UVs and separate fittings intact.
        bm=bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        mesh.calc_loop_triangles()
        obj=bpy.data.objects.new(name,mesh)
        collection.objects.link(obj)
        return obj,len(mesh.loop_triangles)

def export(name, models, location, limits):
    objects=[]
    for (obj_name,model),limit in zip(models,limits):
        obj,count=model.create(obj_name)
        assert count <= limit, (obj_name,count,limit)
        print(f"{obj_name}: {count} tris")
        objects.append(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,name+".glb"),export_format='GLB',
                             use_selection=True,export_yup=True,export_texcoords=True,
                             export_normals=True,export_animations=False)
    for obj in objects: obj.location=location
    for obj in objects[1:]: obj.hide_set(True)

tower=Model()
def collar(z,r1,r2,h,mat):
    tower.tube(mat,[(0,0,z,r1),(0,0,z+h,r2)],24)
tower.tube(stone,[(0,0,0,2.28),(0,0,.26,2.28),(0,0,.93,1.92)],24)
collar(.93,1.92,1.92,.22,cream)
stripes=[(1.15,1.82),(3.65,1.69),(6.13,1.57),(8.61,1.44),(11.09,1.32),(13.57,1.22)]
for i,((lo,r1),(hi,r2)) in enumerate(zip(stripes,stripes[1:])):
    tower.tube(cream if i%2==0 else red,[(0,0,lo,r1),(0,0,hi,r2)],24,False)
tower.tube(teal,[(0,0,13.57,1.22),(0,0,13.85,1.53),(0,0,14.03,2.02),(0,0,14.21,2.02)],24)
collar(14.21,1.18,1.18,.29,red)
for i in range(16):
    a,b=TAU*i/16,TAU*(i+1)/16
    x,y=1.89*math.cos(a),1.89*math.sin(a)
    tower.bar(black,(x,y,14.20),(x,y,15.10),.029,5)
    for z in (14.55,15.10):
        tower.bar(black,(x,y,z),(1.89*math.cos(b),1.89*math.sin(b),z),.031,5)
for i in range(8):
    a=TAU*i/8
    x,y=1.13*math.cos(a),1.13*math.sin(a)
    tower.bar(black,(x,y,14.45),(x,y,16.42),.069,6)
tower.tube(lamp,[(0,0,14.86,.22),(0,0,15.05,.36),(0,0,15.79,.36),(0,0,15.95,.22)],14)
tower.tube(teal,[(0,0,16.30,1.15),(0,0,16.39,1.36),(0,0,16.57,1.36),(0,0,17.50,.21),(0,0,17.63,.14)],16)
tower.tube(black,[(0,0,17.61,.075),(0,0,setting("LighthouseHeight"),.021)],8)
for i,z in enumerate((3.14,5.62,8.11,10.58,13.03)):
    a=(i*.73)-2.3
    r=1.83-(z-1.15)*.048
    tower.box(black,r*math.cos(a),r*math.sin(a),z,.14,.43,.66,a)
    tower.box(window,(r+.075)*math.cos(a),(r+.075)*math.sin(a),z,.025,.32,.52,a)
tower.box(teal,0,-1.83,1.20,.84,.28,1.85)
for j in range(3):
    tower.box(stone,0,-2.04-j*.21,.31-j*.10,1.18,.40,.22)
export("SM_Lighthouse",[("SM_Lighthouse",tower)],(0,5,0),[setting("LighthouseTriangleLimit")])

boat=Model()
sections=[(-2.40,.045,.40),(-1.92,.48,.23),(-1.28,.78,.13),(-.51,.91,.073),(.34,.92,.065),(1.10,.78,.11),(1.72,.48,.20),(2.14,.07,.34)]
def hull_ring(k):
    ring=[]
    for x,w,z in sections:
        for j in range(13):
            a=math.pi*j/12
            ring.append((x,k*w*math.cos(a),z+.55-.49*k*math.sin(a)+(.025 if k<1 else 0)))
    return ring
for scale,mat in ((1,teal),(.945,wood)):
    verts=hull_ring(scale)
    faces=[(i*13+j,(i+1)*13+j,(i+1)*13+j+1,i*13+j+1) for i in range(len(sections)-1) for j in range(12)]
    boat.add(verts,[tuple(reversed(f)) for f in faces] if scale<1 else faces,mat)
for k,mat in ((0,cream),(12,cream),(2,red),(10,red)):
    edge=hull_ring(1)
    for n in range(len(sections)-1):
        a,b=list(edge[n*13+k]),list(edge[(n+1)*13+k])
        a[1]*=1.018
        b[1]*=1.018
        boat.bar(mat,a,b,.043,6)
for j in range(-4,5):
    boat.box(wood,j*.35,0,.239,.328,1.10-abs(j)*.12,.055)
for x,w in ((-.95,1.18),(.47,1.56),(1.24,1.11)):
    boat.box(cream,x,0,.536,.275,w,.080)
for s in (-1,1):
    boat.bar(dark_wood,(-1.43,s*.22,.64),(1.65,s*.56,.64),.031,8)
    boat.box(wood,1.46,s*.535,.638,.55,.158,.045,s*.11)
export("SM_Rowboat",[("SM_Rowboat",boat)],(6,0,0),[setting("RowboatTriangleLimit")])

for variant in range(1,setting("RockVariants")+1):
    rng=random.Random(SEED+variant)
    bm=bmesh.new()
    bmesh.ops.create_icosphere(bm,subdivisions=2,radius=1)
    bm.verts.index_update()
    scale=(rng.uniform(.66,1.12),rng.uniform(.54,.88),rng.uniform(.57,.94))
    verts=[]
    for vert in bm.verts:
        x,y,z=vert.co
        d=rng.uniform(.80,1.20)
        verts.append((x*d*scale[0]+z*.18,y*d*scale[1],max(-.11,z*d*scale[2]+scale[2]*.88)))
    rock=Model()
    face_groups={stone:[],stone_light:[]}
    for face in bm.faces:
        face_groups[stone_light if rng.random()<.23 else stone].append(tuple(v.index for v in face.verts))
    for mat,faces in face_groups.items(): rock.add(verts,faces,mat)
    bm.free()
    name=f"SM_Rock_{variant:02}"
    export(name,[(name,rock)],(variant*2,-3,0),[setting("RockTriangleLimit")])

def palm(variant, low):
    rng=random.Random(SEED+variant*17)
    m=Model()
    height=6.35
    rings=setting("PalmSegments") if low else 22
    sides=6 if low else 9
    lean=rng.uniform(.66,1.23)
    for j in range(rings):
        a,b=j/rings,(j+1)/rings
        def trunk(t):
            return (lean*t*t, -.29*math.sin(t*math.pi*.65), height*t,.185-.114*t+.072*math.exp(-t*16))
        m.tube(bark_ring if j%4==2 else bark,[trunk(a),trunk(b)],sides,False)
    head=trunk(1)[:3]
    fronds=min(setting("PalmFrondMax"),setting("PalmFrondMin")+variant)
    steps=5 if low else 11
    for j in range(fronds):
        angle=TAU*(j+rng.uniform(-.16,.16))/fronds
        length=rng.uniform(2.22,3.27)
        spread=rng.uniform(.39,.53)
        lift=.52+.46*(j%3)
        droop=rng.uniform(.44,1.28)
        direction=Vector((math.cos(angle),math.sin(angle),0))
        lateral=Vector((-direction.y,direction.x,0))
        verts,uv=[],[]
        for k in range(steps+1):
            t=k/steps
            mid=Vector(head)+ direction*(length*t)+Vector((0,0,.11+math.sin(t*math.pi)*lift-t*t*droop))
            width=max(.008,spread*math.pow(math.sin(math.pi*t),.72))
            for s in (-1,0,1):
                verts.append(mid+lateral*(width*s)-Vector((0,0,width*abs(s)*.31)))
                uv.append((t,(s+1)/2))
        faces=[(k*3+s,(k+1)*3+s,(k+1)*3+s+1,k*3+s+1) for k in range(steps) for s in range(2)]
        m.add(verts,faces,leaf if j%3 else leaf_gold,uv,True)
    return m

for v in range(1,setting("PalmVariants")+1):
    name=f"SM_Palm_{v:02}"
    limits=[setting("PalmLod0TriangleLimit"),setting("PalmLod1TriangleLimit")]
    export(name,[(name,palm(v,False)),(name+"_LOD1",palm(v,True))],(v*4,-8,0),limits)

rng=random.Random(SEED)
drift=Model()
drift.bar(bark,(-1.40,0,.22),(1.40,.12,.25),.245,11,rng)
drift.bar(bark,(-.59,.02,.24),(-.89,.20,.54),.118,8,rng)
drift.bar(bark,(.30,.07,.24),(.69,-.24,.47),.109,8,rng)
fire=Model()
for i in range(5):
    a=i*TAU/5
    fire.bar(dark_wood,(math.cos(a)*.56,math.sin(a)*.56,.11),(math.cos(a)*-.22,math.sin(a)*-.22,.27),.109,8,rng)
export("SM_Driftwood",[("SM_Driftwood",drift),("SM_CampfireLogs",fire)],(10,0,0),[1500,1500])

# Keep the supplied template objects for reference and show only the modeled collection in the workspace.
for obj in bpy.context.scene.objects:
    if obj.name in ("Cube","Camera","Light") and obj not in collection.objects[:]:
        obj.hide_set(True)
for obj in collection.objects:
    if obj.name == "SM_CampfireLogs":
        obj.location=(12,0,0)
        obj.hide_set(False)
bpy.ops.object.select_all(action='DESELECT')
preferences=bpy.context.preferences.filepaths
previous=preferences.save_version
try:
    preferences.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=EXPECTED)
finally:
    preferences.save_version=previous

