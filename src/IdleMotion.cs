using System;

namespace DesktopCat
{
    enum IdleKind { None, Ear, Tail, Watch }
    sealed class IdleMotion
    {
        readonly Random random;
        float start,end,nextEar=5,nextTail=10,nextWatch=15,earSide=1;
        V gaze;
        bool bothEars;
        public IdleKind Kind {get;private set;}
        public bool Watching {get {return Kind==IdleKind.Watch;}}
        public IdleMotion(int seed) {random=new Random(seed);}
        float Between(float min,float max) {return min+(float)random.NextDouble()*(max-min);}
        public void Trigger(IdleKind kind,float now)
        {
            Kind=kind;start=now;gaze=new V();
            end=now+(kind==IdleKind.Ear?0.85f:kind==IdleKind.Tail?1.5f:3);
            earSide=random.Next(2)==0?-1:1;
            bothEars=kind==IdleKind.Ear && random.Next(3)==0;
            if(bothEars) end=now+0.95f;
            if(kind==IdleKind.Ear) nextEar=now+Between(7,14);
            if(kind==IdleKind.Tail) nextTail=now+Between(11,19);
            if(kind==IdleKind.Watch) nextWatch=now+Between(18,30);
        }
        public void Update(float now,float dt,bool allowed,bool stationary,V mouseDelta)
        {
            if(!allowed)
            {
                Kind=IdleKind.None;gaze=new V();
                nextEar=Math.Max(nextEar,now+5);nextTail=Math.Max(nextTail,now+9);nextWatch=Math.Max(nextWatch,now+12);
                return;
            }
            if(Kind!=IdleKind.None && now>=end) Kind=IdleKind.None;
            if(Kind==IdleKind.None)
            {
                if(now>=nextEar) Trigger(IdleKind.Ear,now);
                else if(now>=nextTail) Trigger(IdleKind.Tail,now);
                else if(stationary && now>=nextWatch) Trigger(IdleKind.Watch,now);
            }
            if(Watching)
            {
                V target=new V(Math.Max(-2.2f,Math.Min(2.2f,mouseDelta.X/65)),Math.Max(-1,Math.Min(1,mouseDelta.Y/100)));
                gaze=gaze+(target-gaze)*Math.Min(1,dt*7);
            }
        }
        public CatPose Apply(CatPose pose,float now)
        {
            if(pose.Kind!=PoseKind.Normal || Kind==IdleKind.None || now<start || now>=end) return pose;
            float p=(now-start)/(end-start),envelope=(float)Math.Sin(Math.PI*p);
            if(Kind==IdleKind.Ear)
            {
                pose.EarTwitch=EarPulse(now-start);pose.EarSide=earSide;
                if(bothEars) pose.OtherEarTwitch=EarPulse(now-start-0.1f)*0.85f;
            }
            if(Kind==IdleKind.Tail) pose.TailFlick=(float)Math.Sin(p*Math.PI*3)*envelope;
            if(Kind==IdleKind.Watch)
            {
                float blend=Math.Min(1,Math.Min((now-start)/0.25f,(end-now)/0.45f));
                pose.GazeX=gaze.X*blend;pose.GazeY=gaze.Y*blend;
            }
            return pose;
        }
        static float EarPulse(float elapsed)
        {
            if(elapsed<=0 || elapsed>=0.85f) return 0;
            float p=elapsed/0.85f;
            return (float)(Math.Sin(p*Math.PI*4)*Math.Sin(p*Math.PI));
        }
    }
}
