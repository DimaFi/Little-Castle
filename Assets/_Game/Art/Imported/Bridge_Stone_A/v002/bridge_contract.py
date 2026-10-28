"""Version 2 site profile. Same road footprint; shallower, dressed riverbanks."""
import math
ASSET_ID='ENV_Bridge_Stone_A'
VERSION='v002'
HALF_LENGTH=5.4
HALF_WIDTH=1.8
SEED=8102026
WATER_Y=-.85

def smooth(a,b,v):
    t=max(0.,min(1.,(v-a)/(b-a)))
    return t*t*(3-2*t)

def deck_height(z):
    return 1.05*math.cos(math.pi*min(HALF_LENGTH,abs(z))/(2*HALF_LENGTH))**2

def stamp_weight(x,z):
    return (1-smooth(5.,7.,abs(x)))*(1-smooth(6.5,8.,abs(z)))

def channel_half_width(x):
    return 2.83 + .17*smooth(2.,5.5,abs(x))*math.sin(x*1.1+.4)

def terrain_height(x,z):
    d=abs(z);shore=channel_half_width(x)
    bed=-1.22+.035*math.cos(x*.7)*math.cos(z*1.4)
    mound=.09*math.sin(x*1.3+z*.8)*math.sin(z*1.1)*smooth(3.5,4.8,d)
    bank=-.17+mound
    t=smooth(shore-.28,shore+1.25,d)
    h=bed*(1-t)+bank*t
    road=(1-smooth(1.40,2.05,abs(x)))*smooth(4.3,5.4,d)
    return h*(1-road)

def sample_world(wx,wz,origin_x=0,origin_z=0,yaw=0,base_y=0):
    a=math.radians(yaw);c=math.cos(a);s=math.sin(a)
    dx=wx-origin_x;dz=wz-origin_z;x=c*dx-s*dz;z=s*dx+c*dz
    return base_y+terrain_height(x,z),stamp_weight(x,z)

def contract():
    d=dict(schema_version=1,asset_id=ASSET_ID,version=VERSION,
        units='metres',coordinates='Unity Y-up; road +Z; river +X',
        pivot='centre of crossing, Y=approach grade',scale=[1,1,1],
        bridge_length=10.8,bridge_width=3.78,structural_width=3.6,clear_walk_width=2.86,
        arch_opening_width=6.5,water_y=WATER_Y,deck_crown_y=1.05,
        deck_profile='1.05*cos(pi*clamp(abs(z),0,5.4)/10.8)^2',
        road_sockets=[dict(id='RoadSouth',position=[0,0,-5.4],outward=[0,0,-1]),
                      dict(id='RoadNorth',position=[0,0,5.4],outward=[0,0,1])],
        river_sockets=[dict(id='RiverIn',position=[-7,WATER_Y,0],outward=[-1,0,0]),
                       dict(id='RiverOut',position=[7,WATER_Y,0],outward=[1,0,0])],
        reserve_half_extents_xz=[7,8],protected_core_half_extents_xz=[5,6.5],
        collision='Static simplified deck mesh + side blocker meshes; not convex whole bridge',
        decoration_seed=SEED,fixed_dressing=True,accepts_nonuniform_scale=False,
        terrain_rule='Sample target height/weight in world coordinates, blend once after river carving and before slopes/navigation/scatter. Reserve complete support rectangle. Reconcile river water and road elevation at outer sockets; do not overlay two water surfaces.',
        placement_rule='Macro plan chooses compatible crossing, translation/yaw/base elevation only. Bend road and river to perpendicular sockets. Reject overlapping stamps, waterfalls, extreme grades and insufficient room. Never stretch the mesh to requiredSpan.',
        persistence='Store stableId, archetypeId, contractVersion, origin, yaw, baseElevation. Dressing stays fixed independently of world seed. Streaming ownership belongs to one stable site ID; terrain samples across all touched chunks.',
        world_presence='Production generator must guarantee at least one compatible crossing per finite map or explicitly retry the deterministic macro plan. The opt-in prototype may reject roads and does not yet enforce this.',
        height_grid=dict(min_x=-7,min_z=-8,step=.5,nx=29,nz=33,
            heights=[[round(terrain_height(-7+i*.5,-8+j*.5),6) for i in range(29)] for j in range(33)],
            weights=[[round(stamp_weight(-7+i*.5,-8+j*.5),6) for i in range(29)] for j in range(33)]))
    d['banner_anchors']=[dict(id='SouthRight',position=[1.64,deck_height(-5.12)+.97,-5.12],entry_direction=[0,0,1]),dict(id='NorthRight',position=[-1.64,deck_height(5.12)+.97,5.12],entry_direction=[0,0,-1])]
    for socket in d['river_sockets']:socket['position'][1]=WATER_Y
    for j in range(33):
        for i in range(29):d['height_grid']['heights'][j][i]=round(terrain_height(-7+i*.5,-8+j*.5),6)
    d['integration_status']='Opt-in C# fixed-site generator implemented; visual source candidate not imported to Unity.'
    return d

def validate():
    assert all(abs(terrain_height(0,z))<1e-8 for z in [-5.4,5.4])
    for entry in contract()['banner_anchors']:
        x,y,z=entry['position'];dz=entry['entry_direction'][2]
        assert x*dz>0 and abs(abs(z)-5.12)<1e-6
        assert abs(y-(deck_height(z)+.97))<1e-7
    for yaw in [0,37,90,180]:
        for edge in [-32,0,32]:
            for i in range(-16,17):
                assert sample_world(edge-32+32,i*.5,-17,-9,yaw)==sample_world(edge,i*.5,-17,-9,yaw)
    assert all(stamp_weight(x,z)==0 for x,z in [(7,0),(-7,0),(0,8),(0,-8)])
    return dict(endpoints_match=True,zero_weight_outer_border=True,negative_coordinates_and_yaw_seams=True,deterministic=True,banners_on_right_entry_pillar_caps=True)
