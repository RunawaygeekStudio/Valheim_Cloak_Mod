# Generates assets/icons/<Aug>_<Level>.png (128px). Flat, legible at 64px. Replace with painted art any time.
from PIL import Image, ImageDraw, ImageFilter
import math, os
S=128; C=S/2
AUGS={ # id: (motif colour, glow colour)
 "Rainproof":((120,170,215),(60,90,130)), "ToxinWard":((230,190,70),(120,90,20)),
 "Frostbound":((170,220,255),(70,120,170)), "Emberbound":((255,140,50),(150,50,10)),
 "Lightweave":((245,240,220),(120,120,100)), "Fleetfoot":((140,210,120),(50,110,50)),
 "Seabound":((70,150,220),(20,60,120)), "Ironbound":((190,195,205),(70,75,85)),
}
LEVELS=["Simple","Weathered","Hardened","StormWorn","OdinsGift"]

def base(glow):
    im=Image.new("RGBA",(S,S),(0,0,0,0)); d=ImageDraw.Draw(im)
    d.ellipse([4,4,S-4,S-4],fill=(38,34,30,255),outline=(90,78,60,255),width=4)
    g=Image.new("RGBA",(S,S),(0,0,0,0)); gd=ImageDraw.Draw(g)
    gd.ellipse([26,26,S-26,S-26],fill=glow+(170,))
    im=Image.alpha_composite(im,g.filter(ImageFilter.GaussianBlur(14)))
    d=ImageDraw.Draw(im)
    # hooded cloak silhouette
    cloak=[(C,22),(C+14,34),(C+30,70),(C+36,108),(C-36,108),(C-30,70),(C-14,34)]
    d.polygon(cloak,fill=(92,66,44,255),outline=(140,105,70,255))
    d.polygon([(C,30),(C+9,40),(C+6,56),(C-6,56),(C-9,40)],fill=(28,24,20,255))  # hood opening
    d.line([(C,58),(C,106)],fill=(140,105,70,255),width=2)
    return im

def motif(d,aug,col):
    c2=tuple(min(255,x+40) for x in col)
    if aug=="Rainproof":
        for x,y in [(C-16,70),(C+2,62),(C+18,78)]:
            d.polygon([(x,y-10),(x+6,y),(x,y+8),(x-6,y)],fill=col); d.ellipse([x-6,y-2,x+6,y+8],fill=col)
    elif aug=="ToxinWard":
        for i,(x,y) in enumerate([(C-12,68),(C+12,68),(C,88)]):
            d.regular_polygon((x,y,11),6,rotation=30,fill=col,outline=c2)
    elif aug=="Frostbound":
        for a in range(0,180,60):
            r=math.radians(a); dx,dy=22*math.cos(r),22*math.sin(r)
            d.line([(C-dx,76-dy),(C+dx,76+dy)],fill=col,width=4)
            for s in (-1,1):
                px,py=C+s*dx*0.6,76+s*dy*0.6
                for b in (-35,35):
                    rb=math.radians(a+b); d.line([(px,py),(px+s*8*math.cos(rb),py+s*8*math.sin(rb))],fill=col,width=3)
    elif aug=="Emberbound":
        d.polygon([(C,52),(C+16,74),(C+10,96),(C-10,96),(C-16,74),(C-6,66),(C,74),(C+6,64)],fill=col)
        d.polygon([(C,70),(C+7,84),(C,94),(C-7,84)],fill=(255,230,140,255))
    elif aug=="Lightweave":
        # feather: tapered vane along a diagonal quill
        import math as m
        ax,ay,bx,by=C-16,98,C+16,54
        L=m.hypot(bx-ax,by-ay); ux,uy=(bx-ax)/L,(by-ay)/L; nx,ny=-uy,ux
        pts=[]
        for t in [i/20 for i in range(21)]:
            w=12*m.sin(m.pi*t)**0.7
            pts.append((ax+ux*L*t+nx*w, ay+uy*L*t+ny*w))
        for t in [i/20 for i in range(20,-1,-1)]:
            w=8*m.sin(m.pi*t)**0.7
            pts.append((ax+ux*L*t-nx*w, ay+uy*L*t-ny*w))
        d.polygon(pts,fill=col,outline=c2)
        d.line([(ax,ay),(bx,by)],fill=(120,110,90,255),width=2)
        for t in [i/8 for i in range(1,8)]:
            px,py=ax+ux*L*t,ay+uy*L*t
            d.line([(px,py),(px+nx*9-ux*4,py+ny*9-uy*4)],fill=(120,110,90,255),width=1)
    elif aug=="Fleetfoot":
        for i in range(3):
            y=64+i*13; d.line([(C-18,y),(C,y+9),(C+18,y)],fill=col,width=5)
    elif aug=="Seabound":
        for i in range(3):
            y=66+i*13; pts=[(C-24+x, y+6*math.sin(x/6.0)) for x in range(0,49,2)]
            d.line(pts,fill=col,width=4)
    elif aug=="Ironbound":
        d.polygon([(C,56),(C+18,62),(C+16,84),(C,98),(C-16,84),(C-18,62)],fill=col,outline=c2)
        for x,y in [(C-9,66),(C+9,66),(C,80),(C-7,84),(C+7,84)]:
            d.ellipse([x-2,y-2,x+2,y+2],fill=(50,50,60,255))

def pips(d,n,col):
    for i in range(5):
        x=C-24+i*12; f=col if i<n else (70,62,52,255)
        d.ellipse([x-4,113,x+4,121],fill=f,outline=(20,18,16,255))

os.makedirs("assets/icons",exist_ok=True)
for aug,(col,glow) in AUGS.items():
    for n,lvl in enumerate(LEVELS,1):
        im=base(glow); d=ImageDraw.Draw(im); motif(d,aug,col+(255,)); pips(d,n,col+(255,))
        im.save(f"assets/icons/{aug}_{lvl}.png")
    im=base(glow); d=ImageDraw.Draw(im); motif(d,aug,col+(255,)); im.save(f"assets/icons/{aug}.png")  # HUD status icon, no pips
# contact sheet
sheet=Image.new("RGBA",(S*5+48,S*8+72),(20,20,20,255))
for r,aug in enumerate(AUGS):
    for c,lvl in enumerate(LEVELS):
        sheet.paste(Image.open(f"assets/icons/{aug}_{lvl}.png"),(8+c*(S+8),8+r*(S+8)))
sheet.save("assets/icons_sheet.png"); print("ok")
