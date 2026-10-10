"""Inspect connected native surfaces and select Hellion anatomy for the remodel."""
import json
from pathlib import Path


def surfaces(d, weld_seams=False):
    vertices=d['vertices']; parents=list(range(len(vertices))); canonical={}
    def find(i):
        while parents[i]!=i:
            parents[i]=parents[parents[i]]; i=parents[i]
        return i
    weld=[canonical.setdefault(tuple(round(x,3) for x in v),i) for i,v in enumerate(vertices)] if weld_seams else list(range(len(vertices)))
    for start in range(0,len(d['triangles']),3):
        a,b,c=(find(weld[i]) for i in d['triangles'][start:start+3])
        parents[b]=a; parents[c]=a
    groups={}
    for i in range(len(vertices)): groups.setdefault(find(weld[i]),[]).append(i)
    return sorted(groups.values(),key=len,reverse=True)


def describe(d,ids):
    points=[d['vertices'][i] for i in ids]; weights={}
    for i in ids:
        for b,w in zip(d['indices'][i],d['weights'][i]): weights[b]=weights.get(b,0)+w
    ranked=sorted(weights,key=weights.get,reverse=True)
    return dict(count=len(ids),low=[min(p[j] for p in points) for j in range(3)],
                high=[max(p[j] for p in points) for j in range(3)],
                bone=d['bones'][ranked[0]],
                uv=[sum(d['uv'][i][j] for i in ids)/len(ids) for j in range(2)])


def select(d):
    """Keep anatomy/boots, removing Hellion's hair, feathers, skirt and bone jewellery."""
    categories={}
    for ids in surfaces(d):
        info=describe(d,ids); low,high,bone,uv=(info[k] for k in ('low','high','bone','uv'))
        kind=None
        if bone.startswith(('Head_','l_eyebrow','r_eyebrow','l_Eye','r_Eye')) and uv[0]<.16 and uv[1]>.92 and low[2]>-5:
            kind='skin'
        elif bone=='Neck_01_01SHJnt' and len(ids)>70:
            kind='skin'
        elif bone=='Spine_02SHJnt' and len(ids)>150 and low[1]>109:
            kind='skin'
        elif ('Arm_Lower_Curve' in bone and len(ids)>95) or ('Shoulder_Twist' in bone and high[1]>132):
            kind='skin'
        elif bone.startswith(('r_Finger','r_Thumb','r_Arm_Wrist')):
            kind='hand'
        elif bone.startswith(('l_Leg','r_Leg')):
            kind='pants' if high[1]>34 else 'boots'
        if (bone=='ROOTSHJnt' and len(ids)>180 and high[1]>110
            or bone.startswith('skirt_f_') and high[1]>100):
            # The upper skirt contains the donor's pelvis surface. Keep that band
            # to join the waist and leg meshes without retaining the long skirt.
            for i in ids: categories[i]='pelvis'
        if kind:
            for i in ids: categories[i]=kind
    return categories


def clip_surface(d,ids,minimum_y):
    """Clip native triangles at a costume hem, interpolating UVs and skin weights."""
    rows=('vertices','normals','uv','tangents','colors')
    output={key:[] for key in rows}; output['weights']=[]; output['faces']=[]
    def source(i):
        value={key:list(d[key][i]) for key in rows}
        value['weights']={b:w for b,w in zip(d['indices'][i],d['weights'][i]) if w>0}
        return value
    def lerp(a,b):
        t=(minimum_y-a['vertices'][1])/(b['vertices'][1]-a['vertices'][1])
        value={key:[x+(y-x)*t for x,y in zip(a[key],b[key])] for key in rows}
        value['weights']={i:a['weights'].get(i,0)*(1-t)+b['weights'].get(i,0)*t for i in a['weights'].keys()|b['weights'].keys()}
        return value
    for start in range(0,len(d['triangles']),3):
        tri=d['triangles'][start:start+3]
        if not all(i in ids for i in tri): continue
        polygon=[source(i) for i in tri]; clipped=[]
        for a,b in zip(polygon[-1:]+polygon[:-1],polygon):
            inside_a=a['vertices'][1]>=minimum_y; inside_b=b['vertices'][1]>=minimum_y
            if inside_a!=inside_b: clipped.append(lerp(a,b))
            if inside_b: clipped.append(b)
        base=len(output['vertices'])
        for vertex in clipped:
            for key in rows: output[key].append(vertex[key])
            weights=sorted(vertex['weights'].items(),key=lambda item:-item[1])[:4]; total=sum(w for _,w in weights)
            output['weights'].append([(b,w/total) for b,w in weights])
        for i in range(1,len(clipped)-1): output['faces'].append((base,base+i,base+i+1))
    return output


def paint_atlas(folder):
    """Recolour native fabric while preserving its original painted value variation."""
    import numpy as np
    from PIL import Image,ImageDraw
    d=json.loads((folder/'donor.json').read_text()); categories=select(d)
    image=Image.open(folder/'tex_hellion_col.png').convert('RGBA')
    pixels=np.array(image).astype(float)/255; size=image.width
    for kind in ('skin','hand','pants','pelvis'):
        mask=Image.new('L',image.size); draw=ImageDraw.Draw(mask)
        for start in range(0,len(d['triangles']),3):
            tri=d['triangles'][start:start+3]
            if all(categories.get(i)==kind for i in tri):
                draw.polygon([(d['uv'][i][0]*(size-1),(1-d['uv'][i][1])*(size-1)) for i in tri],fill=255)
        selected=np.array(mask)>0; rgb=pixels[:,:,:3]
        if kind in ('pants','pelvis'):
            value=rgb.max(2)
            paint=np.array([.48,.32,.095])*(.68+value[:,:,None]*1.4)
            rgb[selected]=np.clip(paint[selected],0,1)
        else:
            # Remove the donor's turquoise warpaint and warm the original skin paint.
            blue=(rgb[:,:,2]>rgb[:,:,0]*1.1)&(rgb[:,:,1]>rgb[:,:,0]*1.1)
            value=rgb.max(2)
            warm=value[:,:,None]*np.array([1,.64,.39])
            rgb[selected&blue]=warm[selected&blue]
            rgb[selected]*=np.array([.88,.79,.72])
    # Replace the donor's leather corset paint on the exposed midriff.
    belly=Image.new('L',image.size); draw=ImageDraw.Draw(belly)
    for start in range(0,len(d['triangles']),3):
        tri=d['triangles'][start:start+3]; points=[d['vertices'][i] for i in tri]
        if all(categories.get(i)=='skin' for i in tri) and 109<sum(p[1] for p in points)/3<126 and all(abs(p[0])<19 for p in points):
            draw.polygon([(d['uv'][i][0]*(size-1),(1-d['uv'][i][1])*(size-1)) for i in tri],fill=255)
    selected=np.array(belly)>0; value=pixels[:,:,:3].max(2)
    paint=np.array([.36,.215,.125])*(.7+value[:,:,None]*1.2)
    pixels[:,:,:3][selected]=paint[selected]
    Image.fromarray(np.uint8(np.clip(pixels,0,1)*255)).save(folder/'tex_hellion_remodel_col.png')


if __name__=='__main__':
    import sys
    d=json.loads(Path(sys.argv[1]).read_text())
    for n,ids in enumerate(surfaces(d)):
        info=describe(d,ids)
        if len(ids)<60 and 'Head' not in info['bone']: continue
        print(n,info['count'],info['bone'],[round(x,1) for x in info['low']],
              [round(x,1) for x in info['high']],[round(x,3) for x in info['uv']])
