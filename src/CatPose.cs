using System;

namespace DesktopCat
{
    enum PoseKind { Normal, Lick, Jump, Run, Sprint, Held, Drop, Land, Leap, Sleep }
    enum LeapStage { None, Load, Push, Flight, FrontLanding, HindLanding, Settle }
    struct CatPose
    {
        public PoseKind Kind;
        public float Progress;
        public float Lift;
        public float EarTwitch,OtherEarTwitch,EarSide,TailFlick,GazeX,GazeY;
        public float Crouch,Gait,ForwardProgress;
        public LeapStage Stage;
        public float FrontExtension,HindExtension,FrontHeight,HindHeight,Pitch,Extension;
        public static float Duration(PoseKind kind)
        {
            switch(kind) { case PoseKind.Lick:return 4;case PoseKind.Jump:return 1.2f;case PoseKind.Leap:return 1.05f;
                case PoseKind.Run:return 5;case PoseKind.Sprint:return 2.5f;
                case PoseKind.Sleep:return 18;
                case PoseKind.Held:return 3600;case PoseKind.Drop:return 0.42f;case PoseKind.Land:return 0.35f;default:return 0; }
        }
        public static CatPose At(PoseKind kind,float elapsed)
        {
            float duration=Duration(kind);
            float p=duration>0?Math.Max(0,Math.Min(1,elapsed/duration)):0;
            if(kind==PoseKind.Leap) return LeapAt(p);
            float lift=kind==PoseKind.Held?42:kind==PoseKind.Drop?42*(1-p*p):0;
            float crouch=0,forward=0;
            if(kind==PoseKind.Jump)
            {
                float preload=0.22f,takeoff=0.32f;
                if(p<preload) crouch=(float)Math.Sin(p/preload*Math.PI/2);
                else if(p<takeoff) crouch=1-(p-preload)/(takeoff-preload);
                else if(p<0.86f) {float flight=(p-takeoff)/(0.86f-takeoff);lift=42*4*flight*(1-flight);forward=flight;}
                else crouch=0.7f*(float)Math.Sin((p-0.86f)/0.14f*Math.PI);
                if(p>=0.86f) forward=1;
            }
            return new CatPose {Kind=kind,Progress=p,Lift=lift,Crouch=crouch,ForwardProgress=forward,
                Gait=elapsed*(float)(Math.PI*2)*(kind==PoseKind.Sprint?2.8f:2.5f)};
        }
        static CatPose LeapAt(float p)
        {
            CatPose pose=new CatPose {Kind=PoseKind.Leap,Progress=p};
            float u;
            if(p<0.1f)
            {
                u=p/0.1f;pose.Stage=LeapStage.Load;pose.Crouch=0.3f*(float)Math.Sin(u*Math.PI/2);
            }
            else if(p<0.22f)
            {
                u=(p-0.1f)/0.12f;pose.Stage=LeapStage.Push;
                pose.Lift=12*u;pose.Crouch=0.3f*(1-u);pose.Pitch=0.13f*u;
                pose.FrontExtension=6*u;pose.HindExtension=-3*u;
                pose.FrontHeight=20*u;pose.HindHeight=0;pose.ForwardProgress=0.04f*u;
            }
            else if(p<0.68f)
            {
                u=(p-0.22f)/0.46f;pose.Stage=LeapStage.Flight;
                float stretch=(float)Math.Sin(u*Math.PI);
                pose.Lift=12+56*4*u*(1-u);pose.Pitch=0.13f*(1-2*u);pose.Extension=stretch;
                pose.FrontExtension=6+7*stretch;pose.HindExtension=-3-10*stretch;
                float reach=Math.Max(0,(u-0.75f)/0.25f);
                pose.FrontHeight=pose.Lift+8*(1-u)-12*reach;
                pose.HindHeight=pose.Lift*Math.Min(1,u/0.15f)+10*stretch;
                pose.ForwardProgress=0.04f+0.86f*u;
            }
            else if(p<0.82f)
            {
                u=(p-0.68f)/0.14f;pose.Stage=LeapStage.FrontLanding;
                pose.Lift=12-6*u;pose.Pitch=-0.13f*(1-u*0.5f);
                pose.ForwardProgress=0.9f+0.07f*u;pose.FrontHeight=0;pose.HindHeight=12-6*u;
                pose.FrontExtension=6-190*(pose.ForwardProgress-0.9f);pose.HindExtension=-3*(1-u);
            }
            else if(p<0.92f)
            {
                u=(p-0.82f)/0.1f;pose.Stage=LeapStage.HindLanding;
                pose.Lift=6*(1-u);pose.Pitch=-0.065f*(1-u);
                pose.ForwardProgress=0.97f+0.03f*u;pose.FrontHeight=0;pose.HindHeight=6*(1-u);
                pose.FrontExtension=6-190*(pose.ForwardProgress-0.9f);
            }
            else
            {
                u=(p-0.92f)/0.08f;pose.Stage=LeapStage.Settle;pose.ForwardProgress=1;
                pose.Crouch=0.15f*(float)Math.Sin(u*Math.PI);
                pose.FrontExtension=-13*(1-u);pose.FrontHeight=4*(float)Math.Sin(u*Math.PI);
            }
            return pose;
        }
    }
}
