"""Remove the nurse and robe-shaped foreground figure; retain the memorial and services."""
import argparse
import sys
from pathlib import Path
import bpy

sys.path.insert(0,str(Path(__file__).resolve().parent))
import workshops as W

NURSE='common_field_hospital_prefab / field_hospital_nurse'

def remove_nurse():
    assert NURSE in bpy.data.objects, 'Expected the original native nurse mesh'
    bpy.data.objects.remove(bpy.data.objects[NURSE],do_unlink=True)
    assert not any('nurse' in o.name.lower() for o in bpy.data.objects)


def remove_foreground_figure():
    prefixes=('Ancestor ','Carved ','Cheekbone ','Chin ','Folded ancestor robes','Statue neck',
              'Long coat lapels','Left upper sleeve','Left folded forearm','Right upper sleeve','Right folded forearm')
    figures=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith(prefixes)]
    assert len(figures)==30, 'Expected the complete v02 foreground figure, including its sculpted details'
    for ob in figures:bpy.data.objects.remove(ob,do_unlink=True)
    assert any(o.name.startswith('Tapered statue plinth') for o in bpy.data.objects)
    finish_memorial()


def finish_memorial():
    """Use complete reviewed paintings at a world scale, not the old tiny stone crop."""
    stone=W.G.material('Memorial weathered limestone v23',(.68,.72,.71,1),'abbey_wall',.4)
    paving=W.G.material('Memorial native small paving v23',(.68,.72,.70,1),'cobbles_native',.4)
    for ob in bpy.data.objects:
        if ob.type!='MESH' or ' / ' in ob.name:continue
        if ob.name.startswith(('Memorial stone step','Plinth cornice')):
            W.brick_paint(ob,paving)
        elif ob.name.startswith('Tapered statue plinth'):
            W.brick_paint(ob,stone)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','out','kit']:parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--resolution',type=int,default=900);parser.add_argument('--samples',type=int,default=12)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    W.run(args,refiners={'sanitarium':remove_nurse,'statue':remove_foreground_figure},
          detail='v23',replaced_native=(NURSE,),
          design='v23: no nurse or foreground humanoid; Sanitarium architecture and memorial plinth retained')
