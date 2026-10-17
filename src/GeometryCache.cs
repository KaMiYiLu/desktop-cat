using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;

namespace DesktopCat
{
    struct GeometryKey : IEquatable<GeometryKey>
    {
        public int Kind;public float Angle,Extension,Width;
        public GeometryKey(int kind,float angle=0,float extension=0,float width=0)
        {Kind=kind;Angle=angle;Extension=extension;Width=width;}
        public bool Equals(GeometryKey other)
        {return Kind==other.Kind && Angle==other.Angle && Extension==other.Extension && Width==other.Width;}
        public override bool Equals(object other) {return other is GeometryKey && Equals((GeometryKey)other);}
        public override int GetHashCode()
        {unchecked {return ((Kind*397^Angle.GetHashCode())*397^Extension.GetHashCode())*397^Width.GetHashCode();}}
    }
    sealed class GeometryCache
    {
        public const int Capacity=128;
        readonly Dictionary<GeometryKey,GraphicsPath> paths=new Dictionary<GeometryKey,GraphicsPath>();
        readonly Queue<GeometryKey> order=new Queue<GeometryKey>();
        public bool Enabled=true;
        public int Count {get {return paths.Count;}}
        public GraphicsPath Get(GeometryKey key)
        {
            GraphicsPath path;
            return Enabled && paths.TryGetValue(key,out path)?(GraphicsPath)path.Clone():null;
        }
        public void Store(GeometryKey key,GraphicsPath path)
        {
            if(!Enabled || paths.ContainsKey(key)) return;
            if(paths.Count>=Capacity)
            {
                GeometryKey oldest=order.Dequeue();paths[oldest].Dispose();paths.Remove(oldest);
            }
            paths.Add(key,(GraphicsPath)path.Clone());order.Enqueue(key);
        }
        public void Clear()
        {
            foreach(GraphicsPath path in paths.Values) path.Dispose();
            paths.Clear();order.Clear();
        }
    }
}
