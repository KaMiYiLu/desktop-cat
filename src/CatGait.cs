using System;

namespace DesktopCat
{
    static class CatGait
    {
        public static V ProjectGround(float x,float y,float angle)
        {
            float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
            return new V(x*c-y*s,(x*s+y*c)*0.65f);
        }
        public static float Swing(float phase,int foot,CatPose pose,bool moving)
        {
            if(!moving || pose.Kind==PoseKind.Leap) return 0;
            bool bound=pose.Kind==PoseKind.Sprint;
            if(pose.Kind==PoseKind.Run || bound) phase=pose.Gait;
            float offset=bound?(foot<2?0:(float)Math.PI*0.75f):
                ((foot==0 || foot==3)?0:(float)Math.PI);
            return (float)Math.Sin(phase+offset);
        }
        public static V Footprint(V origin,float angle,float swing,int foot,CatPose pose)
        {
            bool bound=pose.Kind==PoseKind.Sprint;
            return origin+ProjectGround((foot<2?-17:17)+swing*(bound?10:5),foot%2==0?-10:10,angle);
        }
        public static V LeapPaw(float angle,int foot,CatPose pose)
        {
            bool front=foot>=2;
            V ground=ProjectGround((front?17:-17)+(front?pose.FrontExtension:pose.HindExtension),foot%2==0?-10:10,angle);
            // The painter lifts the whole rig first. Undo exactly that lift for
            // a paw that has already contacted the ground.
            return ground+new V(0,pose.Lift-(front?pose.FrontHeight:pose.HindHeight));
        }
        public static bool Grounded(CatPose pose)
        {
            return pose.Kind==PoseKind.Normal || pose.Kind==PoseKind.Run || pose.Kind==PoseKind.Sprint;
        }
    }
}
