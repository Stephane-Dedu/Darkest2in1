"""Shared DD1 stage framing for privately rendered DD2 rooms and corridors.

The crawl normalises hero bodies to four world units / 400 virtual pixels. Author
the ground at y680 and compensate for the corridor's runtime 8% overscan here,
rather than enlarging completed plates differently for each biome.
"""
import math

ENVIRONMENT_SCALE = 2.0  # DD2 scenery is authored for roughly two-unit characters.


def configure_materials(L):
    # This is a solid tile foundation, not foliage. Leaving its side faces in the
    # wall row makes floating black platforms across the walking lane.
    if 'forest_ground_dark' not in L.DROP_MATS:
        L.DROP_MATS.append('forest_ground_dark')


def settings(corridor=False):
    angle, distance = math.radians(6), 24.0
    overscan = 1.08 if corridor else 1.0
    tangent = 360 * 4 * math.cos(angle) / ((distance-4*math.sin(angle)) * (400/overscan))
    return {'position_blender': [0, -distance*math.cos(angle), distance*math.sin(angle)],
            'target_blender': [0, 0, 0], 'vertical_fov': math.degrees(2*math.atan(tangent)),
            'lens_shift_y': 320/720, 'pitch_degrees': 6, 'distance': distance,
            'runtime_overscan': overscan, 'hero_body_units': 4, 'hero_body_pixels': 400,
            'hero_foot_y': 680}


def project(view, point):
    """Reference projection in the virtual 720px-high source, independent of Blender."""
    x, depth, height = point
    angle = math.radians(view['pitch_degrees'])
    forward = view['distance'] + depth*math.cos(angle) - height*math.sin(angle)
    up = depth*math.sin(angle) + height*math.cos(angle)
    focal = 360/math.tan(math.radians(view['vertical_fov'])/2)
    return 960 + focal*x/forward, 360 + 720*view['lens_shift_y'] - focal*up/forward


def create(L, corridor=False, res=(2880,1080)):
    view = settings(corridor)
    cam = L.camera(view['position_blender'], view['target_blender'], fov=view['vertical_fov'], res=res)
    cam.data.shift_y = view['lens_shift_y']
    return cam


def floor(L, folder, key, data, shade, band='ground', depth_end=210):
    """Continuous native material, oriented across the screen like the rotated road.

    Use world UVs at the same doubled scale in rooms and corridors. Separate
    per-biome image zooms and stretched foreground cards cannot move the floor.
    """
    import bpy
    length, center = depth_end+90, (depth_end-90)/2
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0,center,-.04))
    obj = bpy.context.object
    obj.name = 'crawl_native_continuous_ground'
    obj.scale = (500,length,1)
    obj['band'] = band
    # Native road travels along Z; the crawl travels along X. Rotate the mapping,
    # with one texture repeat per eight staged units, including environment scale.
    uv = obj.data.uv_layers.active
    for face in obj.data.polygons:
        for index in face.loop_indices:
            v = obj.data.vertices[obj.data.loops[index].vertex_index].co
            uv.data[index].uv = ((v.y*length+center)/8, -v.x*500/8)
    obj.data.materials.append(L.make_mat(folder, key+'-crawl-stage', dict(data,scale=[1,1],offset=[0,0]), shade=shade))
    return obj
