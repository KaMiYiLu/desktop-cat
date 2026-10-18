using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;

namespace DesktopCat
{
    // Rotate the rig on the ground plane, then project its height separately.
    // The head stays upright instead of rotating a flat side-view sprite.
    static class CatPainter
    {
        struct PawPlacement
        {
            public V Position;
            public float Depth,Angle;
        }
        static readonly Color Fur = Color.FromArgb(250,250,247);
        static readonly Color Edge = Color.FromArgb(173,174,184);
        static readonly GeometryCache geometry=new GeometryCache();
        static Font sleepFont;
        public static bool CacheEnabled {get {return geometry.Enabled;}set {geometry.Enabled=value;}}
        public static int CachedPaths {get {return geometry.Count;}}
        public static void ClearCaches() {geometry.Clear();if(sleepFont!=null) {sleepFont.Dispose();sleepFont=null;}}
        public static bool FaceVisible(float angle) {return Math.Sin(angle)>=-0.45;}
        public static V Forward(float angle) { return new V((float)Math.Cos(angle), (float)Math.Sin(angle)*0.65f); }
        static float FaceOffsetX(float angle) {return (float)Math.Cos(angle)*3;}
        public static V Mouth(V position, float angle)
        {
            // Share the eyes' turned centerline and keep the mouth up on the face.
            return position+HeadCenter(angle)+new V(FaceOffsetX(angle),9);
        }
        static V HeadCenter(float angle) {return Forward(angle)*22+new V(0,-35);}
        public static float Turn(float angle, float target, float dt)
        {
            float delta=(float)Math.Atan2(Math.Sin(target-angle),Math.Cos(target-angle));
            float limit=7*dt;
            return angle+Math.Max(-limit,Math.Min(limit,delta));
        }
        static void Ellipse(Graphics g,Brush fill,Pen edge,V p,float rx,float ry)
        {
            g.FillEllipse(fill,p.X-rx,p.Y-ry,rx*2,ry*2);
            if(edge!=null) g.DrawEllipse(edge,p.X-rx,p.Y-ry,rx*2,ry*2);
        }
        static float Cross(PointF a,PointF b,PointF c)
        {
            return (b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
        }
        static void PawRing(List<PointF> points,V center,float length,float breadth,float volume,float angle)
        {
            float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
            float xx=length*length*c*c+breadth*breadth*s*s;
            float xy=0.65f*(length*length-breadth*breadth)*c*s;
            float yy=0.65f*0.65f*(length*length*s*s+breadth*breadth*c*c)+volume*volume;
            float width=(float)Math.Sqrt(xx),tilt=xy/width,height=(float)Math.Sqrt(yy-tilt*tilt);
            for(int i=0;i<24;i++)
            {
                float t=(float)(i*Math.PI/12);
                points.Add(new PointF(center.X+width*(float)Math.Cos(t),center.Y+tilt*(float)Math.Cos(t)+height*(float)Math.Sin(t)));
            }
        }
        static GraphicsPath PawShape(float angle)
        {
            GeometryKey key=new GeometryKey(0,angle);
            GraphicsPath cached=geometry.Get(key);if(cached!=null) return cached;
            // Project a plump toe volume and a short, narrower ankle together.
            // Their shared silhouette keeps height upright while the toes turn.
            List<PointF> points=new List<PointF>();
            PawRing(points,CatGait.ProjectGround(1,0,angle)+new V(0,-1.4f),6.2f,5.0f,3.5f,angle);
            PawRing(points,CatGait.ProjectGround(-2,0,angle)+new V(0,-8),2.7f,2.6f,1.1f,angle);
            points.Sort(delegate(PointF a,PointF b){int order=a.X.CompareTo(b.X);return order!=0?order:a.Y.CompareTo(b.Y);});
            List<PointF> hull=new List<PointF>();
            foreach(PointF point in points)
            {
                while(hull.Count>=2 && Cross(hull[hull.Count-2],hull[hull.Count-1],point)<=0) hull.RemoveAt(hull.Count-1);
                hull.Add(point);
            }
            int lower=hull.Count;
            for(int i=points.Count-2;i>=0;i--)
            {
                PointF point=points[i];
                while(hull.Count>lower && Cross(hull[hull.Count-2],hull[hull.Count-1],point)<=0) hull.RemoveAt(hull.Count-1);
                hull.Add(point);
            }
            hull.RemoveAt(hull.Count-1);
            GraphicsPath path=new GraphicsPath();path.AddClosedCurve(hull.ToArray(),0.2f);geometry.Store(key,path);return path;
        }
        static void Paw(Graphics g,Brush fur,Pen edge,Pen toes,V position,float angle,float scale=1)
        {
            GraphicsState pawState=g.Save();g.TranslateTransform(position.X,position.Y);g.ScaleTransform(scale,scale);
            position=new V();
            using(GraphicsPath path=PawShape(angle))
            {
                Color baseColor=((SolidBrush)fur).Color;
                Color underside=Color.FromArgb(Math.Max(0,baseColor.R-20),Math.Max(0,baseColor.G-20),Math.Max(0,baseColor.B-17));
                using(LinearGradientBrush volume=new LinearGradientBrush(
                    new PointF(0,-9.5f),new PointF(0,4.3f),baseColor,underside))
                    g.FillPath(volume,path);
                g.DrawPath(edge,path);
                GraphicsState saved=g.Save();g.SetClip(path,CombineMode.Intersect);
                for(int i=0;i<2;i++)
                {
                    float lateral=i==0?-1.9f:1.9f;
                    V start=CatGait.ProjectGround(2.8f,lateral,angle)+new V(0,-3.8f);
                    V mid=CatGait.ProjectGround(4.0f,lateral,angle)+new V(0,-3.25f);
                    V bend=CatGait.ProjectGround(5.05f,lateral,angle)+new V(0,-2.25f);
                    V finish=CatGait.ProjectGround(5.7f,lateral,angle)+new V(0,-1.2f);
                    g.DrawBezier(toes,start.Point,mid.Point,bend.Point,finish.Point);
                }
                g.Restore(saved);
            }
            g.Restore(pawState);
        }
        // Center a fixed-length opening on the overlapping torso contour.
        static void JoinedOutline(Graphics g,Pen edge,GraphicsPath outline,GraphicsPath behind,GeometryKey key,bool cacheable)
        {
            if(behind==null) {g.DrawPath(edge,outline);return;}
            using(GraphicsPath cached=cacheable?geometry.Get(key):null)
            {
                if(cached!=null)
                {using(SolidBrush stroke=new SolidBrush(edge.Color)) g.FillPath(stroke,cached);return;}
            }
            using(GraphicsPath flat=(GraphicsPath)outline.Clone())
            {
                flat.Flatten(null,0.15f);
                PointF[] points=flat.PathPoints;
                int count=points.Length;
                // Fill a variable-width ribbon instead of cutting a thick
                // stroke with a clip: both ends taper smoothly to zero.
                List<PointF> samples=new List<PointF>();
                for(int i=0;i<count;i++)
                {
                    PointF a=points[i],b=points[(i+1)%count];
                    float length=(float)Math.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Y-a.Y)*(b.Y-a.Y));
                    int steps=Math.Max(1,(int)Math.Ceiling(length/0.65f));
                    for(int j=0;j<steps;j++)
                    {
                        float t=(float)j/steps;
                        PointF sample=new PointF(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);
                        if(samples.Count==0 || Math.Abs(samples[samples.Count-1].X-sample.X)
                            +Math.Abs(samples[samples.Count-1].Y-sample.Y)>0.001f) samples.Add(sample);
                    }
                }
                if(samples.Count>1)
                {
                    PointF first=samples[0],last=samples[samples.Count-1];
                    if(Math.Abs(first.X-last.X)+Math.Abs(first.Y-last.Y)<0.001f) samples.RemoveAt(samples.Count-1);
                }
                int n=samples.Count;
                float[] arc=new float[n+1];
                bool[] overlaps=new bool[n];
                for(int i=0;i<n;i++)
                {
                    PointF a=samples[i],b=samples[(i+1)%n];
                    arc[i+1]=arc[i]+new V(b.X-a.X,b.Y-a.Y).Length;
                    overlaps[i]=behind.IsVisible(a) && behind.IsVisible(b);
                }
                float bestLength=0,runLength=0;
                int bestStart=0,runStart=0;
                for(int i=0;i<n*2;i++)
                {
                    if(overlaps[i%n] && i-runStart<n)
                    {
                        runLength+=arc[i%n+1]-arc[i%n];
                        if(runLength>bestLength) {bestLength=runLength;bestStart=runStart;}
                    }
                    else {runStart=i+1;runLength=0;}
                }
                if(bestLength<=0) {g.DrawPath(edge,outline);return;}
                float total=arc[n],center=(arc[bestStart%n]+bestLength*0.5f)%total;
                const float halfOpening=10,taperLength=5;
                PointF[] ribbon=new PointF[n*2];
                for(int i=0;i<n;i++)
                {
                    PointF p=samples[i],before=samples[(i+n-1)%n],after=samples[(i+1)%n];
                    V tangent=new V(after.X-before.X,after.Y-before.Y).Unit;
                    float distance=Math.Abs(arc[i]-center);
                    distance=Math.Min(distance,total-distance);
                    float taper=Math.Max(0,Math.Min(1,(distance-halfOpening)/taperLength));
                    float halfWidth=edge.Width*0.5f*taper*taper*(3-2*taper);
                    ribbon[i]=new PointF(p.X-tangent.Y*halfWidth,p.Y+tangent.X*halfWidth);
                    ribbon[ribbon.Length-1-i]=new PointF(p.X+tangent.Y*halfWidth,p.Y-tangent.X*halfWidth);
                }
                PointF[] outside=new PointF[n],inside=new PointF[n];
                Array.Copy(ribbon,0,outside,0,n);Array.Copy(ribbon,n,inside,0,n);
                using(GraphicsPath strokeShape=new GraphicsPath(FillMode.Alternate))
                using(SolidBrush stroke=new SolidBrush(edge.Color))
                {
                    strokeShape.AddPolygon(outside);strokeShape.AddPolygon(inside);
                    g.FillPath(stroke,strokeShape);
                    if(cacheable) geometry.Store(key,strokeShape);
                }
            }
        }
        static void Body(Graphics g,Brush fill,Pen edge,float angle,float extension=0,GraphicsPath headBehind=null,bool cacheJoin=true)
        {
            using(GraphicsPath path=BodyShape(angle,extension))
            {g.FillPath(fill,path);JoinedOutline(g,edge,path,headBehind,new GeometryKey(3,angle,extension,edge.Width),cacheJoin);}
        }
        static GraphicsPath BodyShape(float angle,float extension)
        {
            GeometryKey key=new GeometryKey(2,angle,extension);
            GraphicsPath cached=geometry.Get(key);if(cached!=null) return cached;
            float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
            float length=27+3*extension;
            float xx=length*length*c*c+20*20*s*s;
            float xy=0.65f*(length*length-20*20)*c*s;
            float yy=0.65f*0.65f*(length*length*s*s+20*20*c*c)+12*12;
            float width=(float)Math.Sqrt(xx);
            float tilt=xy/width;
            float height=(float)Math.Sqrt(yy-tilt*tilt);
            // Rounded rump, domed back, tucked chest and a soft, flatter belly.
            GraphicsPath path=new GraphicsPath();
            using(Matrix projection=new Matrix(width,tilt,0,height,0,-22))
            {
                path.AddBezier(-1,0.12f,-1.12f,-0.55f,-0.66f,-1.08f,-0.12f,-1.02f);
                path.AddBezier(-0.12f,-1.02f,0.34f,-1.08f,0.77f,-0.82f,0.89f,-0.45f);
                path.AddBezier(0.89f,-0.45f,1.10f,-0.03f,0.99f,0.58f,0.66f,0.83f);
                path.AddBezier(0.66f,0.83f,0.27f,1.02f,-0.35f,1.02f,-0.69f,0.82f);
                path.AddBezier(-0.69f,0.82f,-0.89f,0.69f,-1.01f,0.42f,-1,0.12f);
                path.CloseFigure();path.Transform(projection);
            }
            geometry.Store(key,path);return path;
        }
        static void Tail(Graphics g,Brush fur,Pen edge,float angle,float time,float flick=0)
        {
            V root=CatGait.ProjectGround(-21,0,angle)+new V(0,-22);
            V end=CatGait.ProjectGround(-38,(float)Math.Sin(time*2.8f)*8,angle)+new V(0,-52);
            V control=CatGait.ProjectGround(-49,0,angle)+new V(0,-46);
            V curl=end+CatGait.ProjectGround(8,0,angle)+new V(0,-4);
            end=end+new V(flick*1.5f,0);
            curl=curl+new V(flick*5,-Math.Abs(flick)*1.8f);
            const int count=28;
            const int capSteps=8;
            const float tipRadius=4.6f;
            PointF[] contour=new PointF[count*2+capSteps-1];
            for(int i=0;i<count;i++)
            {
                float t=(float)i/(count-1),u=1-t;
                V p=root*(u*u*u)+control*(3*u*u*t)+end*(3*u*t*t)+curl*(t*t*t);
                V tangent=((control-root)*(3*u*u)+(end-control)*(6*u*t)+(curl-end)*(3*t*t)).Unit;
                V normal=new V(-tangent.Y,tangent.X);
                float radius=tipRadius-1.8f*(float)Math.Pow(1-t,4);
                contour[i]=(p+normal*radius).Point;
                contour[contour.Length-1-i]=(p-normal*radius).Point;
            }
            V tipTangent=(curl-end).Unit;
            V tipNormal=new V(-tipTangent.Y,tipTangent.X);
            for(int j=1;j<capSteps;j++)
            {
                float arc=(float)(Math.PI*j/capSteps);
                contour[count+j-1]=(curl+tipNormal*(tipRadius*(float)Math.Cos(arc))
                    +tipTangent*(tipRadius*(float)Math.Sin(arc))).Point;
            }
            using(GraphicsPath path=new GraphicsPath())
            {
                path.AddClosedCurve(contour,0.25f);g.FillPath(fur,path);
                // Omit only the root cap. Rear views need the tail's sides
                // visible over the rump, so do not clip the whole body area.
                const int rootSkip=3;
                PointF[] openOutline=new PointF[contour.Length-rootSkip*2];
                Array.Copy(contour,rootSkip,openOutline,0,openOutline.Length);
                g.DrawCurve(edge,openOutline,0.25f);
            }
        }
        static void TwitchEar(GraphicsPath path,float twitch,float side)
        {
            if(twitch==0) return;
            PointF[] points=path.PathPoints;byte[] types=path.PathTypes;
            for(int i=0;i<points.Length;i++)
            {
                float sideWeight=Math.Max(0,Math.Min(1,(points[i].X*side-4)/8));
                float height=Math.Max(0,Math.Min(1,(-points[i].Y-12)/16));
                points[i].X+=twitch*side*3.6f*height*sideWeight;
                points[i].Y+=Math.Abs(twitch)*1.5f*height*sideWeight;
            }
            using(GraphicsPath ear=new GraphicsPath(points,types)) {path.Reset();path.AddPath(ear,false);}
        }
        static void TwitchEars(GraphicsPath path,float twitch,float side,float other)
        {
            TwitchEar(path,twitch,side);TwitchEar(path,other,-side);
        }
        static GraphicsPath HeadShape(float angle,float twitch=0,float earSide=1,float other=0)
        {
            bool cacheable=twitch==0 && other==0;
            GeometryKey key=new GeometryKey(1);
            GraphicsPath cached=cacheable?geometry.Get(key):null;if(cached!=null) return cached;
            GraphicsPath silhouette=new GraphicsPath();
            silhouette.AddBezier(-21,-7,-23,-13,-21,-23,-18,-28);
            silhouette.AddBezier(-18,-28,-16,-31,-8,-21,-5,-15);
            silhouette.AddBezier(-5,-15,-2,-17,3,-17,6,-15);
            silhouette.AddBezier(6,-15,10,-21,16,-29,18,-27);
            silhouette.AddBezier(18,-27,21,-23,22,-14,21,-7);
            silhouette.AddBezier(21,-7,26,-2,25,6,19,10);
            silhouette.AddBezier(19,10,15,14,7,17,0,17);
            silhouette.AddBezier(0,17,-8,17,-16,14,-20,9);
            silhouette.AddBezier(-20,9,-26,5,-26,-3,-21,-7);
            silhouette.CloseFigure();TwitchEars(silhouette,twitch,earSide,other);
            if(cacheable) geometry.Store(key,silhouette);return silhouette;
        }
        static void Head(Graphics g,Brush fur,Brush pink,Pen edge,Pen ink,float angle,float time,bool worried=false,CatPose idle=default(CatPose))
        {
            V center=HeadCenter(angle);
            float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
            GraphicsState headState=g.Save();g.TranslateTransform(center.X,center.Y);
            using(GraphicsPath silhouette=HeadShape(angle,idle.EarTwitch,idle.EarSide,idle.OtherEarTwitch))
            {
                g.FillPath(fur,silhouette);g.DrawPath(edge,silhouette);
                if(!FaceVisible(angle))
                {
                    GraphicsState whiskerClip=g.Save();g.SetClip(silhouette,CombineMode.Exclude);
                    Whiskers(g,ink,new V(),angle);g.Restore(whiskerClip);
                }
            }
            if(FaceVisible(angle))
            {
                using(GraphicsPath ear=new GraphicsPath())
                {
                    ear.AddBezier(-17,-23,-16,-25,-10,-18,-8,-14);
                    ear.AddBezier(-8,-14,-12,-15,-16,-12,-17,-13);
                    ear.AddBezier(-17,-13,-18,-16,-18,-20,-17,-23);
                    ear.CloseFigure();TwitchEars(ear,idle.EarTwitch,idle.EarSide,idle.OtherEarTwitch);g.FillPath(pink,ear);
                }
                using(GraphicsPath ear=new GraphicsPath())
                {
                    ear.AddBezier(17,-23,18,-20,18,-16,17,-13);
                    ear.AddBezier(17,-13,14,-14,11,-15,8,-14);
                    ear.AddBezier(8,-14,11,-19,16,-25,17,-23);
                    ear.CloseFigure();TwitchEars(ear,idle.EarTwitch,idle.EarSide,idle.OtherEarTwitch);g.FillPath(pink,ear);
                }
            }
            g.Restore(headState);
            if(FaceVisible(angle)) Whiskers(g,ink,center,angle);
            // Looking away exposes the back of the head, not a mirrored face.
            if(!FaceVisible(angle)) return;
            float faceX=center.X+FaceOffsetX(angle);
            float eyeSpread=5.5f+1.5f*Math.Abs(s);
            bool blink=!worried && time%5.5f>5.35f;
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                float x=faceX+side*eyeSpread+idle.GazeX,y=center.Y-2+idle.GazeY;
                if(idle.Kind==PoseKind.Sleep) g.DrawBezier(ink,x-3,y-1,x-1.5f,y+1.3f,x+1.5f,y+1.3f,x+3,y-1);
                else if(blink) g.DrawLine(ink,x-2,y,x+2,y);
                else if(worried)
                {
                    WateryEye(g,x,y,side,time);
                }
                else g.FillEllipse(Brushes.Black,x-1.5f,y-3,3,5);
            }
            V mouthAnchor=Mouth(new V(),angle);
            float noseX=mouthAnchor.X,noseY=mouthAnchor.Y-4;
            GraphicsState muzzle=g.Save();g.TranslateTransform(noseX,noseY);
            noseX=noseY=0;
            g.FillPolygon(pink,new PointF[]{new PointF(noseX-3,noseY-2),new PointF(noseX+3,noseY-2),new PointF(noseX,noseY+1)});
            if(worried) g.DrawBezier(ink,noseX-3,noseY+5,noseX-1,noseY+1.5f,noseX+1,noseY+1.5f,noseX+3,noseY+5);
            else
            {
                g.DrawLine(ink,noseX,noseY+1,noseX,noseY+4);
                g.DrawArc(ink,noseX-5,noseY+1,5,5,0,100);g.DrawArc(ink,noseX,noseY+1,5,5,80,100);
            }
            g.Restore(muzzle);
        }
        static void WateryEye(Graphics g,float x,float y,float side,float time)
        {
            using(GraphicsPath eye=new GraphicsPath())
            using(LinearGradientBrush iris=new LinearGradientBrush(new PointF(x,y-5),new PointF(x,y+4),
                Color.FromArgb(43,42,55),Color.FromArgb(83,115,143)))
            using(Pen rim=new Pen(Color.FromArgb(65,62,77),0.85f))
            using(SolidBrush water=new SolidBrush(Color.FromArgb(144,209,239)))
            {
                eye.AddEllipse(x-3.5f,y-5,7,9);g.FillPath(iris,eye);
                GraphicsState saved=g.Save();
                try
                {
                    g.SetClip(eye,CombineMode.Intersect);
                    float shimmer=(float)Math.Sin(time*3)*0.2f;
                    g.FillEllipse(water,x-3.4f,y+1.3f+shimmer,6.8f,3.6f);
                    g.FillEllipse(Brushes.White,x-2.2f,y-3.8f,2.8f,2.8f);
                    g.FillEllipse(Brushes.White,x+0.9f,y-0.3f,1.4f,1.4f);
                    using(Pen glint=new Pen(Color.FromArgb(224,245,255),0.7f))
                        g.DrawArc(glint,x-2.7f,y+1.4f,5.4f,2.0f,10,160);
                }
                finally {g.Restore(saved);}
                g.DrawPath(rim,eye);
                float tearX=x+side*2.3f;
                g.FillEllipse(water,tearX-1.2f,y+2.4f,2.4f,3.8f);
                g.FillEllipse(Brushes.White,tearX-0.35f,y+3.4f,0.7f,1.1f);
            }
        }
        static void Whiskers(Graphics g,Pen ink,V center,float angle)
        {
            float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
            for(int side=-1;side<=1;side+=2)
            for(int i=0;i<3;i++)
            {
                float whiskerCenter=center.X+(FaceVisible(angle)?FaceOffsetX(angle):0);
                float anchor=whiskerCenter+side*10;
                float tip=whiskerCenter+side*(27+6*Math.Abs(s));
                g.DrawBezier(ink,anchor,center.Y+3+i*2,
                    anchor+side*6,center.Y+2+i*3,
                    tip-side*4,center.Y-1+i*5,tip,center.Y-2+i*5);
            }
        }
        public static void Draw(Graphics g,V position,float angle,float phase,bool moving,float time)
        {
            Draw(g,position,angle,phase,moving,time,new CatPose());
        }
        static void GroundShadow(Graphics g,CatPose pose)
        {
            // A single stippled oval stays flat on the desktop as the cat turns.
            // Height only changes its size and opacity, never its ground position.
            float lift=Math.Max(0,pose.Lift);
            float scale=1-Math.Min(lift*0.003f,0.22f);
            int alpha=(int)(200/(1+lift*0.025f));
            float radiusX=(pose.Kind==PoseKind.Sleep?34:30)*scale;
            float radiusY=15*scale;
            using(HatchBrush shade=new HatchBrush(HatchStyle.Percent25,
                Color.FromArgb(alpha,110,110,110),Color.Transparent))
                g.FillEllipse(shade,-radiusX,4-radiusY,radiusX*2,radiusY*2);
        }
        static void Lick(Graphics g,Brush fur,Brush pink,Pen edge,Pen ink,Pen toes,float time,CatPose pose)
        {
            float cycle=(float)Math.Sin(pose.Progress*8*Math.PI*2);
            // Rear paws sit behind the haunches; the supporting front paw
            // stays planted slightly closer to the viewer, below the chest.
            Paw(g,fur,edge,toes,new V(-15,0),1.57f,0.94f);
            Paw(g,fur,edge,toes,new V(15,0),1.57f,0.94f);
            using(GraphicsPath seated=new GraphicsPath())
            {
                seated.AddBezier(-18,-8,-24,-17,-18,-43,-8,-48);
                seated.AddBezier(-8,-48,3,-53,17,-43,18,-27);
                seated.AddBezier(18,-27,24,-10,13,-3,0,-4);
                seated.AddBezier(0,-4,-7,-3,-15,-3,-18,-8);
                seated.CloseFigure();g.FillPath(fur,seated);g.DrawPath(edge,seated);
            Paw(g,fur,edge,toes,new V(-5,5),1.57f);
            float lickAngle=(float)(Math.PI/2);
            V faceOffset=new V(-2,-17+cycle*0.5f);
            GraphicsState face=g.Save();g.TranslateTransform(faceOffset.X,faceOffset.Y);
            Head(g,fur,pink,edge,ink,lickAngle,5.4f);
            g.Restore(face);
            V lifted=new V(8,-24+cycle*0.8f);
            Paw(g,fur,edge,toes,lifted,(float)(-Math.PI/2));
            // Near zero, a round-capped GDI+ Bezier can fail with OutOfMemory.
            // Subpixel extensions are invisible; draw the remaining curve
            // with flat caps and explicit round tips instead.
            if(cycle>0.015f)
            {
                using(Pen tongue=new Pen(Color.FromArgb(231,143,161),2.8f))
                using(SolidBrush tongueFill=new SolidBrush(tongue.Color))
                {
                    tongue.StartCap=tongue.EndCap=LineCap.Flat;
                    V root=Mouth(faceOffset,lickAngle)+new V(0,2);
                    V contact=lifted+new V(-3,-1.3f);
                    V tip=root+(contact-root)*cycle+new V(0,-1.2f*cycle);
                    V control1=root+(tip-root)*0.35f+new V(0,1.8f*cycle);
                    V control2=tip-new V(1.4f*cycle,-2.4f*cycle);
                    g.DrawBezier(tongue,root.Point,control1.Point,control2.Point,tip.Point);
                    g.FillEllipse(tongueFill,root.X-1.4f,root.Y-1.4f,2.8f,2.8f);
                    g.FillEllipse(tongueFill,tip.X-1.4f,tip.Y-1.4f,2.8f,2.8f);
                }
            }
            }
        }
        static void SleepForepaw(Graphics g,Pen edge,V position,float scale,float tilt,bool far)
        {
            GraphicsState saved=g.Save();
            g.TranslateTransform(position.X,position.Y);g.RotateTransform(tilt);g.ScaleTransform(scale,scale);
            // A folded, rounded paw, rather than the upright walking ankle.
            using(GraphicsPath paw=new GraphicsPath())
            using(LinearGradientBrush volume=new LinearGradientBrush(new PointF(-2,-8),new PointF(3,5),
                far?Color.FromArgb(235,235,233):Fur,far?Color.FromArgb(215,216,219):Color.FromArgb(229,229,228)))
            using(Pen crease=new Pen(far?Color.FromArgb(175,176,183):Color.FromArgb(154,154,166),0.8f))
            {
                paw.AddBezier(-3,4,-6,2,-6,-2,-4,-5);
                paw.AddBezier(-4,-5,-2,-8,2,-9,4,-6);
                paw.AddBezier(4,-6,7,-3,5,2,2,4);
                paw.AddBezier(2,4,0,5,-2,5,-3,4);paw.CloseFigure();
                g.FillPath(volume,paw);g.DrawPath(edge,paw);
                crease.StartCap=crease.EndCap=LineCap.Round;
                g.DrawBezier(crease,-1.5f,-6,-2,-5.5f,-2.1f,-4.7f,-2,-4);
                g.DrawBezier(crease,1.2f,-6.8f,0.7f,-6.1f,0.5f,-5.4f,0.5f,-4.8f);
            }
            g.Restore(saved);
        }
        static void Sleep(Graphics g,Brush fur,Brush pink,Pen edge,Pen ink,Pen toes,CatPose pose,float angle)
        {
            float elapsed=pose.Progress*CatPose.Duration(PoseKind.Sleep);
            float settle=Math.Min(1,Math.Min(elapsed/0.7f,(CatPose.Duration(PoseKind.Sleep)-elapsed)/0.7f));
            settle=settle*settle*(3-2*settle);
            float breath=(float)Math.Sin(elapsed*2.1f);
            GraphicsState curled=g.Save();
            if(Math.Cos(angle)>0) g.ScaleTransform(-1,1);
            g.TranslateTransform(0,-5*(1-settle));
            g.ScaleTransform(1+breath*0.008f,1-settle*0.04f+breath*0.016f);
            // A tucked rear paw, round haunches and the head nestled low.
            Paw(g,fur,edge,toes,new V(20,-1),2.8f,0.9f);
            using(GraphicsPath body=new GraphicsPath())
            {
                body.AddBezier(-26,-17,-29,-34,-15,-47,3,-46);
                body.AddBezier(3,-46,24,-47,35,-34,33,-18);
                body.AddBezier(33,-18,35,-3,17,4,0,2);
                body.AddBezier(0,2,-17,3,-27,-3,-26,-17);
                body.CloseFigure();g.FillPath(fur,body);g.DrawPath(edge,body);
            }
            using(GraphicsPath wrap=new GraphicsPath())
            using(Pen tailEdge=new Pen(Edge,11.7f))
            using(Pen tailFur=new Pen(Fur,9.2f))
            {
                // A broad, round-ended tail follows the rump and curls under
                // the two front paws rather than sticking upright.
                wrap.AddBezier(27,-31,43,-16,36,2,17,5);
                wrap.AddBezier(17,5,7,7,-5,5,-11,1);
                tailEdge.StartCap=tailEdge.EndCap=tailFur.StartCap=tailFur.EndCap=LineCap.Round;
                tailEdge.LineJoin=tailFur.LineJoin=LineJoin.Round;
                g.DrawPath(tailEdge,wrap);g.DrawPath(tailFur,wrap);
            }
            // The smaller, higher far paw sits behind the chin; the near paw
            // overlaps it below. Mirror the entire rig together with the head.
            SleepForepaw(g,edge,new V(-7,-3),0.78f,-28,true);
            GraphicsState head=g.Save();
            g.TranslateTransform(-14,-28+10*settle);g.RotateTransform(-10*settle);g.ScaleTransform(0.84f,0.84f);
            V center=HeadCenter((float)(Math.PI/2));g.TranslateTransform(-center.X,-center.Y);
            Head(g,fur,pink,edge,ink,(float)(Math.PI/2),elapsed,false,pose);g.Restore(head);
            SleepForepaw(g,edge,new V(-15,0),0.98f,32,false);
            g.Restore(curled);
            if(sleepFont==null) sleepFont=new Font(FontFamily.GenericSansSerif,10,FontStyle.Bold,GraphicsUnit.Pixel);
            for(int i=0;i<3;i++)
            {
                float drift=(elapsed*0.32f+i/3f)%1;
                float opacity=(float)Math.Sin(Math.PI*drift)*settle;
                if(opacity<0.01f) continue;
                using(SolidBrush z=new SolidBrush(Color.FromArgb((int)(180*opacity),128,137,158)))
                    g.DrawString(i==2?"Z":"z",sleepFont,z,18+drift*13,-48-drift*25);
            }
        }
        public static void Draw(Graphics g,V position,float angle,float phase,bool moving,float time,CatPose pose)
        {
            GraphicsState saved=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(position.X,position.Y);
            using(SolidBrush fur=new SolidBrush(Fur))
            using(SolidBrush pink=new SolidBrush(Color.FromArgb(238,168,179)))
            using(Pen edge=new Pen(Edge,2.5f))
            using(Pen ink=new Pen(Color.FromArgb(95,95,109),1.25f))
            using(Pen toes=new Pen(Color.FromArgb(154,154,166),0.9f))
            using(Pen farToes=new Pen(Color.FromArgb(177,177,185),0.8f))
            using(SolidBrush farFur=new SolidBrush(Color.FromArgb(230,230,230)))
            {
                edge.LineJoin=LineJoin.Round;edge.StartCap=edge.EndCap=LineCap.Round;
                toes.StartCap=toes.EndCap=LineCap.Round;
                farToes.StartCap=farToes.EndCap=LineCap.Round;
                float s=(float)Math.Sin(angle);
                // Side views keep the face above the body. A small negative
                // sine (including roundoff at PI) is not a rear-facing view.
                bool rearFacing=!FaceVisible(angle);
                if(pose.Kind==PoseKind.Sleep)
                {
                    GroundShadow(g,pose);Sleep(g,fur,pink,edge,ink,toes,pose,angle);
                    g.Restore(saved);return;
                }
                GroundShadow(g,pose);
                g.TranslateTransform(0,-pose.Lift);
                if(pose.Kind==PoseKind.Held)
                {
                    g.TranslateTransform((float)Math.Sin(time*3.4f)*2,(float)Math.Sin(time*11)*1.3f);
                    g.TranslateTransform(0,-40);
                    g.RotateTransform((float)Math.Sin(time*5.5f)*9+(float)Math.Sin(time*9.2f)*1.5f);
                    g.TranslateTransform(0,40);
                }
                if(pose.Kind==PoseKind.Drop) g.ScaleTransform(0.97f,1.04f);
                if(pose.Kind==PoseKind.Land)
                {
                    float bounce=(float)Math.Sin(pose.Progress*Math.PI);
                    g.ScaleTransform(1+bounce*0.12f,1-bounce*0.18f);
                }
                if(pose.Kind==PoseKind.Lick)
                {
                    Tail(g,fur,edge,(float)(Math.PI/2),time*0.45f);
                    Lick(g,fur,pink,edge,ink,toes,time,pose);
                    g.Restore(saved);return;
                }
                if(pose.Kind==PoseKind.Jump)
                {
                    g.ScaleTransform(1+pose.Crouch*0.10f,1-pose.Crouch*0.24f);
                }
                if(pose.Kind==PoseKind.Run || pose.Kind==PoseKind.Sprint)
                {
                    phase=pose.Gait;
                    if(moving) g.TranslateTransform(0,pose.Kind==PoseKind.Sprint?
                        -(float)Math.Pow(Math.Sin(phase),2)*1.2f:(float)Math.Sin(phase*2)*0.6f);
                }
                float tailTime=pose.Kind==PoseKind.Held?time*1.7f:time;
                float extension=pose.Kind==PoseKind.Sprint && moving?(float)Math.Cos(phase):
                    pose.Kind==PoseKind.Leap?pose.Extension:0;
                if(s>=0) Tail(g,fur,edge,angle,tailTime,pose.TailFlick);
                PawPlacement[] feet=new PawPlacement[4];
                for(int i=0;i<4;i++)
                {
                    float swing=CatGait.Swing(phase,i,pose,moving);
                    V ground=CatGait.Footprint(new V(),angle,swing,i,pose);
                    feet[i]=new PawPlacement {Position=ground+new V(0,-Math.Max(0,swing)*4),Depth=ground.Y,Angle=angle};
                    if(pose.Kind==PoseKind.Jump)
                        feet[i].Position=feet[i].Position+new V(0,-pose.Lift*0.1f);
                    if(pose.Kind==PoseKind.Leap)
                    {
                        V point=CatGait.LeapPaw(angle,i,pose);
                        feet[i]=new PawPlacement {Position=point,Depth=ground.Y,Angle=angle};
                    }
                    if(pose.Kind==PoseKind.Held)
                    {
                        float flutter=(float)Math.Sin(time*12+i*1.9f);
                        float flank=i%2==0?-1:1;
                        ground=CatGait.ProjectGround((i<2?-16:16)+flank*3+flutter*2,flank*12,angle);
                        feet[i]=new PawPlacement {Position=ground+new V(0,7+flutter*3),
                            Depth=ground.Y,Angle=angle+flank*flutter*0.16f};
                    }
                    if(pose.Kind==PoseKind.Drop) feet[i].Position=feet[i].Position+new V(0,2);
                }
                // Depth comes from the ground rig, never from lifted paw height.
                Array.Sort(feet,delegate(PawPlacement a,PawPlacement b){return a.Depth.CompareTo(b.Depth);});
                bool layeredPaws=pose.Kind==PoseKind.Held || pose.Kind==PoseKind.Leap;
                int beforeBody=layeredPaws?2:4;
                for(int i=0;i<beforeBody;i++)
                    Paw(g,i<2?farFur:fur,edge,i<2?farToes:toes,feet[i].Position,feet[i].Angle,i<2?0.9f:1);
                bool worried=pose.Kind==PoseKind.Held;
                GraphicsState torso=g.Save();
                if(pose.Kind==PoseKind.Leap)
                {
                    g.TranslateTransform(0,-18);
                    g.RotateTransform(-pose.Pitch*(float)Math.Cos(angle)*180/(float)Math.PI);
                    g.ScaleTransform(1+pose.Crouch*0.1f,1-pose.Crouch*0.24f);
                    g.TranslateTransform(0,18);
                }
                using(GraphicsPath head=HeadShape(angle,pose.EarTwitch,pose.EarSide,pose.OtherEarTwitch))
                using(Matrix placement=new Matrix())
                {
                    V center=HeadCenter(angle);placement.Translate(center.X,center.Y);head.Transform(placement);
                    if(rearFacing) Head(g,fur,pink,edge,ink,angle,time,worried,pose);
                    Body(g,fur,edge,angle,extension,rearFacing?head:null,pose.EarTwitch==0 && pose.OtherEarTwitch==0);
                    if(!rearFacing) Head(g,fur,pink,edge,ink,angle,time,worried,pose);
                }
                g.Restore(torso);
                if(layeredPaws)
                    for(int i=2;i<feet.Length;i++)
                        Paw(g,fur,edge,toes,feet[i].Position,feet[i].Angle);
                if(s<0) Tail(g,fur,edge,angle,tailTime,pose.TailFlick);
            }
            g.Restore(saved);
        }
    }
}

