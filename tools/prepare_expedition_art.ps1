param(
    [Parameter(Mandatory=$true)][string]$NativeMap,
    [Parameter(Mandatory=$true)][string]$LegacyMap,
    [string]$SluiceMap,
    [Parameter(Mandatory=$true)][string]$Output
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    $childArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath,
        '-NativeMap', $NativeMap, '-LegacyMap', $LegacyMap, '-Output', $Output)
    if ($SluiceMap) { $childArgs += @('-SluiceMap', $SluiceMap) }
    & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" @childArgs
    if ($LASTEXITCODE -ne 0) { throw 'Expedition art conversion failed.' }
    return
}
Add-Type -AssemblyName System.Drawing
# Format conversion and registered alpha mattes only. Paintings are made with ImageGen.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
public static class ExpeditionArtBuilder {
    static readonly Dictionary<string, PointF[]> Masks = new Dictionary<string, PointF[]> {
        {"crypts", P(390,90, 1000,90, 1100,260, 1090,375, 940,402, 740,402, 540,343, 390,343)},
        {"warrens", P(350,335, 530,330, 745,395, 990,400, 1080,480, 1020,572, 750,635, 450,625, 340,495)},
        {"weald", P(470,610, 710,570, 1040,580, 1190,680, 1220,915, 1050,955, 740,955, 490,850)},
        {"cove", P(1080,290, 1550,275, 1800,400, 1810,860, 1500,930, 1230,850, 1150,680, 1090,560)}
    };
    static PointF[] P(params float[] xy) {
        var result = new PointF[xy.Length / 2];
        for(int i=0;i<result.Length;i++) result[i]=new PointF(xy[i*2],xy[i*2+1]);
        return result;
    }
    static Bitmap Scale(string path) {
        using(var source = Image.FromFile(path)) {
            if(Math.Abs((double)source.Width/source.Height - 16.0/9) > 0.03) throw new Exception("Expected widescreen map: " + path);
            var result = new Bitmap(1920,1080,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(result)) {
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                g.DrawImage(source,new Rectangle(0,0,1920,1080));
            }
            return result;
        }
    }
    static bool Inside(float x,float y,PointF[] p) {
        bool inside=false;
        for(int i=0,j=p.Length-1;i<p.Length;j=i++)
            if((p[i].Y>y)!=(p[j].Y>y) && x<(p[j].X-p[i].X)*(y-p[i].Y)/(p[j].Y-p[i].Y)+p[i].X) inside=!inside;
        return inside;
    }
    static float Distance(float x,float y,PointF[] p) {
        float best=float.MaxValue;
        for(int i=0,j=p.Length-1;i<p.Length;j=i++) {
            float dx=p[i].X-p[j].X,dy=p[i].Y-p[j].Y;
            float t=Math.Max(0,Math.Min(1,((x-p[j].X)*dx+(y-p[j].Y)*dy)/(dx*dx+dy*dy)));
            float ex=x-p[j].X-t*dx,ey=y-p[j].Y-t*dy;
            best=Math.Min(best,ex*ex+ey*ey);
        }
        return (float)Math.Sqrt(best);
    }
    static void Overlay(Bitmap source,PointF[] polygon,string path) {
        using(var output=source.Clone(new Rectangle(0,0,1920,1080),PixelFormat.Format32bppArgb)) {
            var data=output.LockBits(new Rectangle(0,0,1920,1080),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            var bytes=new byte[data.Stride*1080];
            Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
            for(int y=0;y<1080;y++) for(int x=0;x<1920;x++) {
                int at=y*data.Stride+x*4;
                float alpha=0;
                if(Inside(x+0.5f,y+0.5f,polygon)) {
                    float t=Math.Min(1,Distance(x+0.5f,y+0.5f,polygon)/32f);
                    alpha=t*t*(3-2*t);
                }
                bytes[at+3]=(byte)Math.Round(255*alpha);
                if(bytes[at+3]==0) bytes[at]=bytes[at+1]=bytes[at+2]=0;
            }
            Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
            output.UnlockBits(data);
            output.Save(path,ImageFormat.Png);
        }
    }
    static void Preview(Bitmap source,Rectangle crop,string path) {
        using(var output=new Bitmap(512,256,PixelFormat.Format32bppArgb)) {
            using(var g=Graphics.FromImage(output)) {
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                g.DrawImage(source,new Rectangle(0,0,512,256),crop,GraphicsUnit.Pixel);
            }
            output.Save(path,ImageFormat.Png);
        }
    }
    public static void Prepare(string nativePath,string legacyPath,string sluicePath,string folder) {
        Directory.CreateDirectory(folder);
        using(var native=Scale(nativePath)) using(var legacy=Scale(legacyPath)) {
            native.Save(Path.Combine(folder,"map.png"),ImageFormat.Png);
            var crops=new Dictionary<string,Rectangle> {
                {"crypts",new Rectangle(430,100,600,300)}, {"warrens",new Rectangle(430,325,600,300)},
                {"weald",new Rectangle(630,530,600,300)}, {"cove",new Rectangle(1050,300,600,300)}
            };
            var nativeIds=new Dictionary<string,string> {
                {"crypts","dd2_city"}, {"warrens","dd2_farm"}, {"weald","dd2_forest"}, {"cove","dd2_coast"}
            };
            foreach(var region in crops) {
                Preview(native,region.Value,Path.Combine(folder,"preview-"+nativeIds[region.Key]+".png"));
                Preview(legacy,region.Value,Path.Combine(folder,"preview-"+region.Key+".png"));
                Overlay(legacy,Masks[region.Key],Path.Combine(folder,"overlay-"+region.Key+".png"));
            }
            Preview(native,new Rectangle(950,0,600,300),Path.Combine(folder,"preview-darkestdungeon.png"));
            if(!String.IsNullOrEmpty(sluicePath)) using(var sluice=Scale(sluicePath)) {
                Preview(sluice,crops["warrens"],Path.Combine(folder,"preview-dd2_cave.png"));
                Overlay(sluice,Masks["warrens"],Path.Combine(folder,"overlay-dd2_cave.png"));
            }
        }
    }
}
'@
[ExpeditionArtBuilder]::Prepare((Resolve-Path -LiteralPath $NativeMap).Path,
    (Resolve-Path -LiteralPath $LegacyMap).Path,
    $(if ($SluiceMap) { (Resolve-Path -LiteralPath $SluiceMap).Path } else { '' }),
    [IO.Path]::GetFullPath($Output))
Get-ChildItem -LiteralPath $Output -Filter '*.png' | Select-Object Name,Length
