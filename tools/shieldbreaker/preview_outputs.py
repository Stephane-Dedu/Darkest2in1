"""Make private review sheets and a GIF from the exported model's Blender renders."""
import argparse
import struct
from pathlib import Path
from PIL import Image, ImageDraw

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('folder',type=Path)
a=p.parse_args()
folder=a.folder.resolve()
repo=Path(__file__).resolve().parents[2]
if folder==repo or repo in folder.parents:
    p.error('Review images contain owned game art and must stay outside the repository')

sheet=Image.new('RGB',(1440,590),(30,29,26)); draw=ImageDraw.Draw(sheet)
for i,label in enumerate(('idle','attack','defend')):
    image=Image.open(folder/('preview_'+label+'.png')).convert('RGB').resize((480,560))
    sheet.paste(image,(i*480,30)); draw.text((i*480+20,10),label,fill=(225,215,188))
sheet.save(folder/'preview_sheet.png')

reference=folder/'reference/reference.png'
raw=reference.with_suffix('.rgba')
if raw.exists():
    data=raw.read_bytes()
    width,height=struct.unpack('<II',data[:8])
    Image.frombytes('RGBA',(width,height),data[8:]).save(reference)
if reference.exists():
    compare=Image.new('RGB',(900,660),(53,51,46)); draw=ImageDraw.Draw(compare)
    source=Image.open(reference).convert('RGBA'); source.thumbnail((410,590))
    compare.paste(source,((450-source.width)//2,50),source)
    current=Image.open(folder/'preview_idle.png').convert('RGB').crop((150,240,565,785))
    current.thumbnail((430,590)); compare.paste(current,(450+(450-current.width)//2,50))
    draw.text((20,16),'DD1 combat design',fill=(225,215,188))
    draw.text((470,16),'Reworked 3D mesh',fill=(225,215,188))
    draw.text((20,635),'Costume comparison. Offline render; DD2 lighting remains unverified.',fill=(190,183,167))
    compare.save(folder/'comparison.png')

frames=[Image.open(folder/'walk'/f'{i:03}.png').convert('RGB') for i in range(24)]
frames[0].save(folder/'walk.gif',save_all=True,append_images=frames[1:],duration=53,loop=0)
walk=Image.new('RGB',(1440,560))
for i,n in enumerate((0,8,16)): walk.paste(frames[n],(i*480,0))
walk.save(folder/'walk_sheet.png')
print('Wrote private pose sheet, costume comparison, walk sheet and 24-frame GIF')
