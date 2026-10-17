using System;
using System.Drawing;

namespace DesktopCat
{
    static class RenderPolicy
    {
        public static Rectangle WindowBounds(V position,MudPrint[] prints,Size desktop)
        {
            // Ground decals must not ride a moving/resizing layered window.
            // Keep a stationary desktop surface until every print has expired.
            return prints.Length>0?new Rectangle(Point.Empty,desktop):Bounds(position,prints);
        }
        public static Rectangle Bounds(V position,MudPrint[] prints)
        {
            // Covers the highest leap, carried sway, tail and ground shadow.
            Rectangle bounds=new Rectangle((int)Math.Floor(position.X)-128,(int)Math.Floor(position.Y)-192,256,256);
            foreach(MudPrint print in prints)
                bounds=Rectangle.Union(bounds,new Rectangle((int)Math.Floor(print.Position.X)-9,(int)Math.Floor(print.Position.Y)-9,18,18));
            bounds.Width=(bounds.Width+63)/64*64;bounds.Height=(bounds.Height+63)/64*64;
            return bounds;
        }
        public static int Interval(float speed,PoseKind pose,IdleKind idle,int prints,bool captured,bool exiting)
        {
            return speed>5 || (pose!=PoseKind.Normal && pose!=PoseKind.Sleep) || idle!=IdleKind.None || prints>0 || captured || exiting?16:33;
        }
    }
}
