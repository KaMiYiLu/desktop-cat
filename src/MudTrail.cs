using System;
using System.Collections.Generic;
using System.Drawing;

namespace DesktopCat
{
    sealed class MudPrint {public V Position;public float Born,Angle;}
    sealed class MudTrail
    {
        public const float ActiveSeconds=4.4f,LifeSeconds=2.5f;
        public const int MaxPrints=24;
        readonly List<MudPrint> prints=new List<MudPrint>();
        static readonly MudPrint[] Empty=new MudPrint[0];
        readonly float[] previous=new float[4];
        readonly V[] lastPosition=new V[4];
        readonly bool[] hasPosition=new bool[4];
        bool primed;
        float until;
        public int Count {get {return prints.Count;}}
        public MudPrint[] Snapshot() {return prints.Count==0?Empty:prints.ToArray();}
        public void Begin(float now) {until=now+ActiveSeconds;primed=false;Array.Clear(hasPosition,0,4);}
        public void Update(float now,V origin,float angle,float phase,bool moving,CatPose pose)
        {
            prints.RemoveAll(delegate(MudPrint p){return now-p.Born>=LifeSeconds;});
            bool leave=now<until && moving && CatGait.Grounded(pose);
            for(int foot=0;foot<4;foot++)
            {
                float swing=CatGait.Swing(phase,foot,pose,moving);
                if(leave && primed && previous[foot]>0 && swing<=0)
                {
                    V position=CatGait.Footprint(origin,angle,swing,foot,pose);
                    if(!hasPosition[foot] || (position-lastPosition[foot]).Length>5)
                    {
                        prints.Add(new MudPrint {Position=position,Angle=angle,Born=now});
                        lastPosition[foot]=position;hasPosition[foot]=true;
                    }
                }
                previous[foot]=swing;
            }
            primed=leave;
            while(prints.Count>MaxPrints) prints.RemoveAt(0);
        }
        public void Draw(Graphics g,float now)
        {
            foreach(MudPrint p in prints)
            {
                float remaining=Math.Max(0,1-(now-p.Born)/LifeSeconds);
                if(remaining<=0) continue;
                DrawPrint(g,p,remaining);
            }
        }
        static void DrawPrint(Graphics g,MudPrint p,float remaining)
        {
            // Dissolve the small prints by shrinking them toward the ground.
            System.Drawing.Drawing2D.GraphicsState saved=g.Save();
            g.TranslateTransform(p.Position.X,p.Position.Y);
            float scale=1.15f*(float)Math.Sqrt(remaining);g.ScaleTransform(scale,scale);
            using(SolidBrush brush=new SolidBrush(Color.FromArgb(161,132,105)))
                {
                    V pad=CatGait.ProjectGround(-0.8f,0,p.Angle);
                    g.FillEllipse(brush,pad.X-1.8f,pad.Y-1.4f,3.6f,2.8f);
                    for(int toe=-1;toe<=1;toe++)
                    {
                        V tip=CatGait.ProjectGround(2,toe*1.65f,p.Angle);
                        g.FillEllipse(brush,tip.X-0.7f,tip.Y-0.7f,1.4f,1.4f);
                    }
                }
            g.Restore(saved);
        }
    }
}
