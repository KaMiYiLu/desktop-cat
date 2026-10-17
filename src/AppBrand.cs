using System.Drawing;
using System.IO;
using System.Reflection;

[assembly: AssemblyTitle("Desktop Cat")]
[assembly: AssemblyDescription("桌面猫咪 · 连续朝向与互动动作")]
[assembly: AssemblyProduct("Desktop Cat")]

namespace DesktopCat
{
    static class AppBrand
    {
        public static readonly Icon Icon=Load();
        static Icon Load()
        {
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopCat.ico"))
            using(Icon original=new Icon(stream,32,32)) return (Icon)original.Clone();
        }
    }
}


