"""Blender projection check against the actual render camera, without a game launch.

blender -b --python-exit-code 1 --python tools/verify_scenery_camera.py -- KIT
"""
import sys
from pathlib import Path

sys.path.insert(0,str(Path(__file__).resolve().parent))
from scenery_camera import create, project, settings


def main():
    kit = Path(sys.argv[sys.argv.index('--')+1]).resolve()
    sys.path.insert(0,str(kit/'tools'))
    import bpy
    import bl_lib as L
    from mathutils import Vector
    from bpy_extras.object_utils import world_to_camera_view
    checks = 0
    for corridor in (False,True):
        L.reset()
        camera = create(L,corridor=corridor,res=(4096,768) if corridor else (2880,1080))
        bpy.context.view_layer.update()
        view = settings(corridor)
        for x in (-8,-4,0,4,8):
            projected=[]
            for depth,height in ((0,0),(0,4),(-1,0),(5,0),(10,0)):
                p = world_to_camera_view(bpy.context.scene,camera,Vector((x,depth,height)))
                y = (1-p.y)*720
                expected = project(view,(x,depth,height))[1]
                assert abs(y-expected)<.002,(corridor,x,depth,height,y,expected)
                projected.append(y)
                checks+=1
            assert abs(projected[0]-680)<.002
            assert abs((projected[0]-projected[1])*view['runtime_overscan']-400)<.002
        print(('CORRIDOR' if corridor else 'ROOM')+' PASS: ground y680, body400px after actual crop, 6-degree floor',flush=True)
    print('PASS: '+str(checks)+' actual Blender camera projections',flush=True)


if __name__=='__main__':
    main()
