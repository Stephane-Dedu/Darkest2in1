"""Give the Guild armory the hall's accepted clay paint at matching course scale.

Blender authoring only. Reuses existing reviewed paint; retains all geometry,
other surfaces, glass, timber and original native assemblies.
"""
import argparse
import json
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import geometry as G
import workshops as W
from abbey_pinnacles import islands
from guild_roof_panels import CAP_RECT


def build():
    targets = {'Guild armory painted roof', 'Guild armory fitted ridge saddles'}
    meshes = {o.name: o for o in bpy.data.objects if o.type == 'MESH'}
    assert targets <= meshes.keys()
    before = {name: (np.array([v.co[:] for v in ob.data.vertices]),
                     np.array([v.uv[:] for v in ob.data.uv_layers.active.data]),
                     [m.name for m in ob.data.materials]) for name, ob in meshes.items()}
    hall = meshes['Guild red hall painted roof']
    face = next(m for m in hall.data.materials if 'clay faces' in m.name)
    edge = next(m for m in hall.data.materials if 'clay cut edges' in m.name)
    assert face != edge
    def arc(width, rise):
        return np.hypot(np.diff(np.array([0,.10,.23,.36,.50])*width),
                        np.diff(np.array([1,.71,.35,.13,0])*rise)).sum()
    hall_arc, wing_arc = arc(6.5,3.4), arc(2.7,1.8)
    u_range = (.08, .08+5.2/6.3)
    v_range = (.20, .20+wing_arc/hall_arc)
    assert max(*u_range,*v_range) < 1
    records = []
    for name in sorted(targets):
        ob = meshes[name]
        assert len(ob.data.materials) == 2 and all('Guild armory native' in m.name for m in ob.data.materials)
        old = list(ob.data.materials)
        for i, material in enumerate(old):
            ob.data.materials[i] = edge if 'cut edges' in material.name else face
        uv = ob.data.uv_layers.active
        if name.endswith('painted roof'):
            ds = np.array([0,.10,.23,.36,.50])*2.7
            hs = np.array([1,.71,.35,.13,0])*1.8
            distances = np.r_[0,np.cumsum(np.hypot(np.diff(ds),np.diff(hs)))]
            for poly in ob.data.polygons:
                for li in poly.loop_indices:
                    p = ob.matrix_world @ ob.data.vertices[ob.data.loops[li].vertex_index].co
                    u = np.clip(.5+(p.y-.7)/5.2,0,1)
                    v = np.clip(1-np.interp(abs(p.x-3),ds,distances)/distances[-1],0,1)
                    uv.data[li].uv = (u_range[0]+u*(u_range[1]-u_range[0]),
                                          v_range[0]+v*(v_range[1]-v_range[0]))
        else:
            points = np.array([v.co[:] for v in ob.data.vertices])
            groups = islands(points,np.array([list(p.vertices) for p in ob.data.polygons]))
            x0,y0,x1,y1 = CAP_RECT
            for group in groups:
                loops = [li for fi in group for li in ob.data.polygons[fi].loop_indices]
                coords = np.array([uv.data[li].uv[:] for li in loops])
                lo = coords.min(0); span = np.maximum(coords.max(0)-lo,.00001)
                for li, coord in zip(loops,coords):
                    u,v = np.clip((coord-lo)/span,0,1)
                    uv.data[li].uv = (x0+u*(x1-x0),y0+v*(y1-y0))
        records.append({'member':name,'previous_materials':[m.name for m in old],
                        'new_materials':[m.name for m in ob.data.materials]})
    for name, ob in meshes.items():
        positions, coords, materials = before[name]
        assert np.array_equal(positions,np.array([v.co[:] for v in ob.data.vertices]))
        if name not in targets:
            assert np.array_equal(coords,np.array([v.uv[:] for v in ob.data.uv_layers.active.data]))
            assert materials == [m.name for m in ob.data.materials]
    (G.OUT/'guild-roof-harmony.json').write_text(json.dumps({
        'new_generated_art':False,'raster_edits':False,'geometry_changes':False,
        'reused_paint':face.name,'wing_source_fraction':{'u':u_range,'v':v_range},
        'hall_slope_arc':float(hall_arc),'wing_slope_arc':float(wing_arc),
        'changed_members':records,'all_other_members_unchanged':True},indent=2))


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=1000)
    p.add_argument('--samples',type=int,default=8)
    args = p.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'guild':build},detail='v92',
          design='Guild hall, dormers and armory share accepted clay paint and physical course scale; geometry and other finishes retained')
