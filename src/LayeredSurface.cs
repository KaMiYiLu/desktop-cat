using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DesktopCat
{
    // Reuse one premultiplied-alpha DIB; never blend antialiasing with a color key.
    sealed class LayeredSurface : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] struct Point {public int X,Y;public Point(int x,int y){X=x;Y=y;}}
        [StructLayout(LayoutKind.Sequential)] struct Size {public int Width,Height;public Size(int w,int h){Width=w;Height=h;}}
        [StructLayout(LayoutKind.Sequential,Pack=1)] struct Blend {public byte Op,Flags,Alpha,Format;}
        [StructLayout(LayoutKind.Sequential)] struct Header
        {
            public uint Size;public int Width,Height;public ushort Planes,BitCount;
            public uint Compression,ImageSize;public int XPels,YPels;public uint Used,Important;
        }
        [DllImport("gdi32.dll",SetLastError=true)] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll",SetLastError=true)] static extern IntPtr CreateDIBSection(IntPtr dc,ref Header header,uint usage,out IntPtr bits,IntPtr section,uint offset);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr window,IntPtr dest,ref Point position,ref Size size,IntPtr source,ref Point origin,uint key,ref Blend blend,uint flags);
        IntPtr dc,dib,previous;
        public Bitmap Bitmap {get;private set;}
        public Graphics Graphics {get;private set;}
        public LayeredSurface(int width,int height)
        {
            try
            {
                dc=CreateCompatibleDC(IntPtr.Zero);
                if(dc==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                Header header=new Header {Size=40,Width=width,Height=-height,Planes=1,BitCount=32};
                IntPtr bits;dib=CreateDIBSection(dc,ref header,0,out bits,IntPtr.Zero,0);
                if(dib==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                previous=SelectObject(dc,dib);
                Bitmap=new Bitmap(width,height,width*4,PixelFormat.Format32bppPArgb,bits);
                Graphics=Graphics.FromImage(Bitmap);
            }
            catch {Dispose();throw;}
        }
        public void Present(IntPtr window,int x,int y)
        {
            Graphics.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
            Point position=new Point(x,y),origin=new Point(0,0);
            Size size=new Size(Bitmap.Width,Bitmap.Height);
            Blend blend=new Blend {Alpha=255,Format=1};
            if(!UpdateLayeredWindow(window,IntPtr.Zero,ref position,ref size,dc,ref origin,0,ref blend,2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        public void Dispose()
        {
            if(Graphics!=null) {Graphics.Dispose();Graphics=null;}
            if(Bitmap!=null) {Bitmap.Dispose();Bitmap=null;}
            if(previous!=IntPtr.Zero) {SelectObject(dc,previous);previous=IntPtr.Zero;}
            if(dib!=IntPtr.Zero) {DeleteObject(dib);dib=IntPtr.Zero;}
            if(dc!=IntPtr.Zero) {DeleteDC(dc);dc=IntPtr.Zero;}
        }
    }
}
