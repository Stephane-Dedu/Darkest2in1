"""Convert Core model-review paint or bind its unlit geometry in Blender.

No art is generated. Core owns the grading, tint, contact bands and model reader.
All inputs/outputs are private. Native shader appearance still needs comparison.
"""
import argparse
import hashlib
import json
from pathlib import Path


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def images(folder, finish=False):
    from PIL import Image
    folder=folder.resolve();repo=Path(__file__).resolve().parents[2]
    if folder==repo or repo in folder.parents:raise ValueError('Private review data belongs outside Git.')
    data=json.loads((folder/'review.json').read_text());sizes={};source_sizes={};hashes={};resampled={}
    for key,entry in data['Images'].items():
        if len(key)!=64 or any(c not in '0123456789abcdef' for c in key):raise ValueError('Invalid private image key')
        path=Path(entry['Source'])
        if sha(path)!=entry['SourceHash']:raise ValueError('Changed source paint '+str(path))
        image=Image.open(path).convert('RGBA');size=image.size
        source_sizes[key]=size
        if max(size)>2048:
            target_size=tuple(max(1,round(v*2048/max(size))) for v in size)
            image=image.resize(target_size,Image.Resampling.BILINEAR);size=target_size
            # Core validates the native target extent. PIL's filter approximates
            # Unity's GPU readback for this rare oversized source, so record it.
            resampled[key]={'source':source_sizes[key],'target':size,'filter':'PIL bilinear; native GPU sampling remains approximate'}
        sizes[key]=size
        if finish:
            raw=(folder/(key+'-graded.rgba')).read_bytes()
            if len(raw)!=size[0]*size[1]*4:raise ValueError('Incomplete Core graded pixels')
            target=folder/(key+'.png');Image.frombytes('RGBA',size,raw).save(target)
            assert Image.open(target).tobytes()==raw
            hashes[key]=sha(target)
        else:(folder/(key+'-input.rgba')).write_bytes(image.tobytes())
    if finish:(folder/'graded-images.json').write_text(json.dumps(hashes,indent=2))
    else:
        (folder/'image-sizes.json').write_text(json.dumps(sizes))
        (folder/'source-sizes.json').write_text(json.dumps(source_sizes))
        (folder/'resampled-sources.json').write_text(json.dumps(resampled,indent=2))
    print('Converted Core graded pixels exactly' if finish else 'Prepared original RGBA grade inputs',len(sizes))


def load(folder, sources, verified, surface):
    import bpy
    folder=folder.resolve();data=json.loads((folder/'review.json').read_text());hashes=json.loads((folder/'graded-images.json').read_text())
    if data['Version']!=1 or not sources or sources!=data['CoreSources']:raise ValueError('Core model review/layout source versions differ.')
    reader=Path(__file__).resolve().parents[2]/'src/DarkestDungeon3.Core/Presentation/GltfModel.cs'
    if hashlib.sha256(reader.read_text(encoding='utf-8').encode()).hexdigest()!=data['ReaderHash']:raise ValueError('Native model reader changed; export matching review data.')
    package=Path(next(iter(data['Images'].values()))['Source']).parent
    for name,digest in data['SourceFiles'].items():
        if Path(name).name!=name or sha(package/name)!=digest:raise ValueError('Source package changed: '+name)
    textures={}
    for key in data['Images']:
        path=folder/(key+'.png')
        if sha(path)!=hashes[key]:raise ValueError('Changed Core graded paint '+key)
        textures[key]=bpy.data.images.load(str(path),check_existing=True)
        textures[key].colorspace_settings.name='sRGB'
    buildings={}
    for service,model in data['Models'].items():
        if model['SourceGlbHash']!=verified[service]['sha256']:raise ValueError('Model review and verified raw geometry differ: '+service)
        mats=[]
        for info in model['Materials']:
            bands=[]
            for band,tint in enumerate(info['Tints']):
                material=bpy.data.materials.new(service+' '+info['Name']+' native band '+str(band));material.use_nodes=True
                nt=material.node_tree;nt.nodes.clear()
                emission=nt.nodes.new('ShaderNodeEmission');emission.inputs['Color'].default_value=tint
                alpha=None
                if info['Image']:
                    tex=nt.nodes.new('ShaderNodeTexImage');tex.image=textures[info['Image']]
                    mul=nt.nodes.new('ShaderNodeMixRGB');mul.blend_type='MULTIPLY';mul.inputs[0].default_value=1;mul.inputs[2].default_value=tint
                    nt.links.new(tex.outputs['Color'],mul.inputs[1]);nt.links.new(mul.outputs[0],emission.inputs['Color']);alpha=tex.outputs['Alpha']
                output=nt.nodes.new('ShaderNodeOutputMaterial')
                if info['Mask'] and alpha:
                    cut=nt.nodes.new('ShaderNodeMath');cut.operation='GREATER_THAN';cut.inputs[1].default_value=info['Cutoff'];nt.links.new(alpha,cut.inputs[0])
                    transparent=nt.nodes.new('ShaderNodeBsdfTransparent');mix=nt.nodes.new('ShaderNodeMixShader')
                    nt.links.new(cut.outputs[0],mix.inputs[0]);nt.links.new(transparent.outputs[0],mix.inputs[1]);nt.links.new(emission.outputs[0],mix.inputs[2]);nt.links.new(mix.outputs[0],output.inputs[0])
                else:nt.links.new(emission.outputs[0],output.inputs[0])
                material.use_backface_culling=not info['DoubleSided'];bands.append(material)
            mats.append(bands)
        objects=[]
        for part in model['Primitives']:
            groups=[[] for _ in range(3)]
            for i,band in enumerate(part['Bands']):groups[band].extend(part['Indices'][i*3:i*3+3])
            obj=surface(service+' / '+part['Name'],part,mats[part['Material']],groups=groups)
            obj['Service']=service;objects.append(obj)
        buildings[service]=objects
    return buildings


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('folder',type=Path);parser.add_argument('--finish',action='store_true')
    args=parser.parse_args();images(args.folder,args.finish)
