"""Route original Abbey/Guild grey roof paintings through the existing slate finish.

Material names select Core's shared grade. No new art, UV, tint or mesh change.
"""
import argparse,json,sys
from pathlib import Path
import bpy
sys.path.insert(0,str(Path(__file__).resolve().parent))
import geometry as G,workshops as W

RENAMES={
 'abbey':{'Abbey native grey shingles v71':'Abbey native grey slate shingles v90',
          'Abbey native grey shingle cut edges v71':'Abbey native grey slate cut edges v90'},
 'guild':{'Guild armory native grey shingles v72':'Guild armory native grey slate shingles v90',
          'Guild armory native grey cut edges v72':'Guild armory native grey slate cut edges v90'}}

def build(service):
    expected=783 if service=='abbey' else 655
    assert len([o for o in bpy.data.objects if o.type=='MESH'])==expected,'Use accepted332 raw source'
    records=[]
    for before,after in RENAMES[service].items():
        material=bpy.data.materials[before];assert after not in bpy.data.materials
        members=[o.name for o in bpy.data.objects if o.type=='MESH' and material in list(o.data.materials)]
        assert len(members)==(4 if service=='abbey' else 2)
        material.name=after;records.append({'before':before,'after':after,'members':members})
    (G.OUT/(service+'-slate-finish.json')).write_text(json.dumps({'service':service,'renamed_materials':records,
     'geometry_UVs_original_paint_and_local_factors_retained':True,'existing_shared_finish':'HamletPalette.BuildingSlate',
     'old_finish':'HamletPalette.Building fallback','runtime_code_changed':False,'new_generated_art':False},indent=2),encoding='utf-8')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','out','kit'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--resolution',type=int,default=900);p.add_argument('--samples',type=int,default=8)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    if args.out.exists():raise ValueError('Preserve old studies; use a fresh private output.')
    W.run(args,refiners={service:lambda key=service:build(key) for service in RENAMES},detail='v90',
      design='Original native grey roof paint routed through the existing shared slate finish; all geometry/UVs/tints retained')
