"""Build a private Shieldbreaker model from the owner's DD2 Hellion rig.

Run with uv run --with UnityPy python tools/shieldbreaker/build_model.py --help.
The output contains modified game geometry/textures: never redistribute it.
"""
import argparse
import json
import subprocess
from pathlib import Path


def extract(game, output):
    import UnityPy
    from UnityPy.helpers.MeshHelper import MeshHandler

    bundle = game / 'Darkest Dungeon II_Data/StreamingAssets/aa/hero_hellion_assets_basegame.bundle'
    env = UnityPy.load(str(bundle))
    mesh = next(o.read() for o in env.objects if o.type.name == 'Mesh' and o.read().m_Name == 'msh_hellion')
    renderer = next(o.read() for o in env.objects if o.type.name == 'SkinnedMeshRenderer'
                    and o.read().m_Mesh.m_PathID == mesh.object_reader.path_id)
    h = MeshHandler(mesh); h.process()
    bones = [p.read() for p in renderer.m_Bones]
    names = [b.m_GameObject.read().m_Name for b in bones]
    ids = [p.m_PathID for p in renderer.m_Bones]
    parents = []
    for bone in bones:
        parent = bone.m_Father
        while parent.m_PathID and parent.m_PathID not in ids:
            parent = parent.read().m_Father
        parents.append(ids.index(parent.m_PathID) if parent.m_PathID in ids else -1)
    data = dict(vertices=h.m_Vertices, normals=h.m_Normals, uv=h.m_UV0,
                triangles=[i for sub in h.get_triangles() for tri in sub for i in tri],
                bones=names, parents=parents,
                bindposes=[[getattr(m, f'e{r}{c}') for r in range(4) for c in range(4)] for m in mesh.m_BindPose],
                indices=h.m_BoneIndices, weights=h.m_BoneWeights)
    (output / 'donor.json').write_text(json.dumps(data), encoding='utf-8')
    for o in env.objects:
        if o.type.name == 'Texture2D':
            t = o.read()
            if t.m_Name in ('tex_hellion_col', 'tex_hellion_ink'):
                t.image.save(output / (t.m_Name + '.png'))


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--game', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True, help='Private output directory, outside the repository')
    p.add_argument('--blender', type=Path, required=True)
    a = p.parse_args()
    repo = Path(__file__).resolve().parents[2]
    output = a.output.resolve()
    if output == repo or repo in output.parents:
        p.error('Derived game assets must be written outside the repository')
    output.mkdir(parents=True, exist_ok=True)
    extract(a.game, output)
    subprocess.run([str(a.blender), '--background', '--factory-startup', '--python-exit-code', '1', '--python',
                    str(Path(__file__).with_name('model_blender.py')), '--', str(output)], check=True)
