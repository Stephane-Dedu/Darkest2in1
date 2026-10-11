"""Make private model/reference contact sheets and an offline four-angle HTML gallery."""
import argparse
import html
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from references import BUILDINGS


def gallery(out,kit,previous=None):
    out,kit=out.resolve(),kit.resolve()
    repo=Path(__file__).resolve().parents[2]
    if out==repo or repo in out.parents:
        raise ValueError('Private model artwork must stay outside Git.')
    manifest=json.loads((out/'models.json').read_text())
    version=manifest.get('detail_pass','v01')
    if version not in ['v01','v02']:
        raise ValueError('Unknown model pass: '+version)
    models=manifest['models']
    checks=json.loads((out/'verified.json').read_text())
    sources=json.loads((out/'dd1-sources.json').read_text())
    if set(models)!=set(BUILDINGS) or set(checks)!=set(BUILDINGS):
        raise ValueError('Build and verify all ten models before publishing the gallery.')
    for key,record in models.items():
        if checks[key]['sha256']!=hashlib.sha256((out/record['glb']).read_bytes()).hexdigest():
            raise ValueError('Re-verify the changed GLB before publishing: '+key)
        for image_path in [*record['views'].values(),checks[key]['render']]:
            with Image.open(out/image_path) as im:
                if min(im.size)<1 or im.width!=im.height:
                    raise ValueError('Unexpected render dimensions: '+image_path)
                im.verify()
    font=ImageFont.truetype(str(kit/'fonts/NDDunkelD-Bold.otf'),29)
    small=ImageFont.truetype(str(kit/'fonts/AlegreyaSans-Regular.ttf'),21)
    sheet=Image.new('RGB',(2200,1020),'#192027')
    comparison=Image.new('RGB',(2000,2700),'#192027')
    d=ImageDraw.Draw(sheet)
    dc=ImageDraw.Draw(comparison)
    before_after=Image.new('RGB',(2000,2700),'#192027') if previous else None
    if previous:
        previous=previous.resolve()
        prior_checks=json.loads((previous/'verified.json').read_text())
        if set(prior_checks)!=set(BUILDINGS):
            raise ValueError('Previous gallery must contain all ten verified models.')
        db=ImageDraw.Draw(before_after)
    for i,(key,label) in enumerate(BUILDINGS.items()):
        x,y=i%5*440,i//5*510
        model=Image.open(out/checks[key]['render']).convert('RGB')
        model=model.crop((30,30,model.width-30,model.height-30))
        model.thumbnail((440,450))
        sheet.paste(model,(x+(440-model.width)//2,y))
        d.text((x+20,y+460),label,font=font,fill='#e1d3b7')
        cx,cy=i%2*1000,i//2*540
        dc.text((cx+25,cy+15),label,font=font,fill='#e1d3b7')
        reference=Image.open(sources[key]['level03']['source']).convert('RGBA')
        reference=reference.crop(reference.getbbox())
        reference.thumbnail((310,420))
        comparison.paste(reference,(cx+20+(310-reference.width)//2,cy+480-reference.height),reference)
        model=Image.open(out/checks[key]['render']).convert('RGB')
        model.thumbnail((620,440))
        comparison.paste(model,(cx+360+(620-model.width)//2,cy+50))
        dc.text((cx+30,cy+500),'DD1 installed exterior',font=small,fill='#a89e89')
        dc.text((cx+400,cy+500),'3D model, rendered from exported GLB',font=small,fill='#a89e89')
        if previous:
            prior=Image.open(previous/prior_checks[key]['render']).convert('RGB')
            prior.save(out/key/'previous.png')
            db.text((cx+25,cy+15),label,font=font,fill='#e1d3b7')
            current=Image.open(out/checks[key]['render']).convert('RGB')
            for column,im in enumerate([prior,current]):
                im.thumbnail((480,440))
                before_after.paste(im,(cx+column*500+(500-im.width)//2,cy+50))
            db.text((cx+30,cy+500),'First pass',font=small,fill='#a89e89')
            db.text((cx+530,cy+500),'Native detail pass',font=small,fill='#a89e89')
    sheet_name=f'hamlet-building-models-{version}.png'
    comparison_name=f'dd1-to-dd2-comparison-{version}.png'
    sheet.save(out/sheet_name)
    comparison.save(out/comparison_name)
    if previous:
        before_after.save(out/f'before-after-{version}.png')
    cards=[]
    for key,label in BUILDINGS.items():
        info=models[key]
        views=[('exported','3D export'),('front','Front'),('hero','Three-quarter'),('rear','Rear'),('side','Side')]
        if previous:
            views.append(('previous','First pass'))
        buttons=''.join(f'<button class="{"active" if view=="exported" else ""}" data-view="{view}" onclick="view(this,\'{key}/{view}.png\')">{title}</button>'
            for view,title in views)
        cards.append(f'''<article id="{key}"><h2>{html.escape(label)}</h2><img src="{key}/exported.png" alt="{html.escape(label)} three-dimensional model">
        <nav>{buttons}</nav><p>{html.escape(info['design'])}</p><p class="files">{info['triangles']:,} triangles ·
        <a href="{info['glb']}">3D GLB</a> · <a href="{info['blend']}">Editable Blender model</a></p></article>''')
    document='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Hamlet building models</title><style>
    *{box-sizing:border-box}body{margin:0;background:#12171c;color:#d8ccb5;font:18px Georgia,serif}header{padding:32px 5vw;max-width:1200px}
    h1{font-size:38px;font-weight:normal;margin:0 0 12px}h2{font-size:26px;font-weight:normal;margin:16px 20px}p{line-height:1.5;margin:16px 20px}
    header p{margin:0;max-width:1000px;color:#b3ab9c}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(360px,1fr));gap:24px;padding:0 5vw 40px}
    article{background:#192027;border:1px solid #35342f}img{width:100%;display:block}nav{display:flex;flex-wrap:wrap;gap:6px;padding:15px 20px 0}
    button{background:#25272a;color:#d8ccb5;border:1px solid #5e523a;padding:8px 12px;cursor:pointer}button:hover,button.active{background:#51402c}
    a{color:#d5b878}.files{font-size:16px;color:#a89e89}@media(max-width:500px){main{grid-template-columns:1fr;padding:0 12px 30px}}
    </style><header><h1>Hamlet building models</h1><p>Ten distinct 3D studies based on DD1's repaired exteriors, built with native DD2 parts and painted surfaces.
    The first view is rendered after re-importing the exported GLB. Other buttons show the authoring scene from four camera angles.
    These private assets are ready for further modeling; they have not replaced the in-game town.</p>
    <p>GALLERY_LINKS</p></header><main>'''
    links=f'<a href="{comparison_name}">DD1 comparison sheet</a> · <a href="{sheet_name}">All ten models</a>'
    if previous:
        links+=f' · <a href="before-after-{version}.png">Before and after</a>'
        document=document.replace('<h1>Hamlet building models</h1>','<h1>Hamlet buildings — native detail pass</h1>')
    document=document.replace('GALLERY_LINKS',links)
    document+='\n'.join(cards)
    document+='''</main><script>function view(button,path){const card=button.closest('article');card.querySelector('img').src=path;
    card.querySelectorAll('button').forEach(b=>b.classList.remove('active'));button.classList.add('active');}</script></html>'''
    (out/'gallery.html').write_text(document,encoding='utf-8')
    print('Wrote model sheet, DD1 comparison and offline gallery.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out',required=True,type=Path)
    parser.add_argument('--kit',required=True,type=Path)
    parser.add_argument('--previous',type=Path,help='Previous verified model gallery for before/after images.')
    args=parser.parse_args()
    gallery(args.out,args.kit,args.previous)
