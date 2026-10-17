using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

// Editable vector master; supersample each ICO size independently.
static class GenerateIcon
{
    static Color Outline=Color.FromArgb(88,99,114);
    static GraphicsPath Tile()
    {
        GraphicsPath p=new GraphicsPath();
        p.AddArc(8,8,96,96,180,90);p.AddArc(152,8,96,96,270,90);
        p.AddArc(152,152,96,96,0,90);p.AddArc(8,152,96,96,90,90);p.CloseFigure();return p;
    }
    static void Curve(Graphics g,Pen pen,float x,float y,float a,float b,float c,float d,float e,float f)
    {g.DrawBezier(pen,x,y,a,b,c,d,e,f);}
    static void Draw(Graphics g,bool tiny)
    {
        using(GraphicsPath tile=Tile())
        using(Brush green=new SolidBrush(Color.FromArgb(139,193,180))) g.FillPath(green,tile);
        using(GraphicsPath head=new GraphicsPath())
        using(Brush fur=new SolidBrush(Color.FromArgb(252,251,246)))
        using(Pen edge=new Pen(Outline,tiny?6.5f:5))
        {
            edge.LineJoin=LineJoin.Round;edge.StartCap=edge.EndCap=LineCap.Round;
            head.AddBezier(53,122,48,103,46,57,57,46);
            head.AddBezier(57,46,63,43,87,65,98,77);
            head.AddBezier(98,77,115,72,139,72,156,77);
            head.AddBezier(156,77,172,58,191,43,198,47);
            head.AddBezier(198,47,206,56,206,101,201,122);
            head.AddBezier(201,122,218,156,202,189,173,198);
            head.AddBezier(173,198,148,208,105,208,80,198);
            head.AddBezier(80,198,50,188,36,154,53,122);head.CloseFigure();
            g.FillPath(fur,head);g.DrawPath(edge,head);
            using(Brush pink=new SolidBrush(Color.FromArgb(232,160,174)))
            {
                using(GraphicsPath ear=new GraphicsPath())
                {ear.AddBezier(61,66,59,78,60,94,64,102);ear.AddLine(64,102,85,86);ear.AddBezier(85,86,77,76,68,69,61,66);g.FillPath(pink,ear);}
                using(GraphicsPath ear=new GraphicsPath())
                {ear.AddBezier(190,66,178,74,171,82,168,87);ear.AddLine(168,87,191,103);ear.AddBezier(191,103,194,89,194,76,190,66);g.FillPath(pink,ear);}
            }
            using(Brush eyes=new SolidBrush(Color.FromArgb(58,66,76)))
            {
                g.FillEllipse(eyes,91,126,10,tiny?17:18);g.FillEllipse(eyes,153,126,10,tiny?17:18);
                if(!tiny) {g.FillEllipse(Brushes.White,93,128,3,4);g.FillEllipse(Brushes.White,155,128,3,4);}
            }
            using(Brush nose=new SolidBrush(Color.FromArgb(213,136,153)))
            using(Pen mouth=new Pen(Outline,tiny?5:3.5f))
            {
                mouth.StartCap=mouth.EndCap=LineCap.Round;
                g.FillPolygon(nose,new PointF[]{new PointF(120,151),new PointF(134,151),new PointF(127,159)});
                g.DrawLine(mouth,127,158,127,166);
                Curve(g,mouth,127,166,123,174,116,174,112,169);
                Curve(g,mouth,127,166,131,174,138,174,142,169);
            }
            if(!tiny)
            {
                using(Brush blush=new SolidBrush(Color.FromArgb(244,214,211)))
                {g.FillEllipse(blush,68,151,20,9);g.FillEllipse(blush,167,151,20,9);}
                using(Pen whisker=new Pen(Outline,3))
                {
                    whisker.StartCap=whisker.EndCap=LineCap.Round;
                    g.DrawLine(whisker,83,151,39,142);g.DrawLine(whisker,82,159,36,162);
                    g.DrawLine(whisker,172,151,216,142);g.DrawLine(whisker,173,159,219,162);
                }
            }
            // One plump paw peeks over the lower edge, recalling the desktop cat.
            g.FillEllipse(fur,153,183,39,35);g.DrawArc(edge,153,183,39,35,190,340);
            if(!tiny)
            {
                using(Pen toes=new Pen(Outline,2.5f))
                {toes.StartCap=toes.EndCap=LineCap.Round;g.DrawLine(toes,166,207,166,213);g.DrawLine(toes,178,207,178,213);}
            }
        }
    }
    static Bitmap Render(int size)
    {
        int scale=size<128?8:4;
        using(Bitmap master=new Bitmap(size*scale,size*scale,PixelFormat.Format32bppArgb))
        {
            using(Graphics g=Graphics.FromImage(master))
            {g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size*scale/256f,size*scale/256f);Draw(g,size<=24);}
            Bitmap result=new Bitmap(size,size,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(result))
            {g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(master,0,0,size,size);}
            return result;
        }
    }
    public static void Main(string[] args)
    {
        string folder=args[0];Directory.CreateDirectory(folder);
        int[] sizes={16,20,24,32,40,48,64,128,256};List<byte[]> images=new List<byte[]>();
        foreach(int size in sizes)
            using(Bitmap bitmap=Render(size)) using(MemoryStream stream=new MemoryStream())
            {bitmap.Save(stream,ImageFormat.Png);images.Add(stream.ToArray());}
        using(BinaryWriter writer=new BinaryWriter(File.Create(Path.Combine(folder,"DesktopCat.ico"))))
        {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++)
            {writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
            foreach(byte[] image in images) writer.Write(image);
        }
        using(Bitmap preview=Render(512)) preview.Save(Path.Combine(folder,"DesktopCat.png"),ImageFormat.Png);
        using(Bitmap sheet=new Bitmap(760,180))
        using(Graphics g=Graphics.FromImage(sheet))
        using(Font font=new Font("Segoe UI",9))
        {
            g.Clear(Color.FromArgb(246,244,238));int x=18;
            foreach(int size in new int[]{16,24,32,48,64,128,256})
            {int shown=Math.Min(size,128);using(Bitmap icon=Render(size)) g.DrawImage(icon,x,12,shown,shown);g.DrawString(size+" px",font,Brushes.DimGray,x,148);x+=shown+22;}
            sheet.Save(Path.Combine(folder,"icon-sizes.png"),ImageFormat.Png);
        }
    }
}
