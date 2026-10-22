using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DesktopCat
{
    static class StartupCheck
    {
        public static void Run(string output)
        {
            // Use an isolated in-memory backend; never enable logon startup
            // or change the user's real Run entry during automated checks.
            string stored=null;int writes=0;
            string executable=@"E:\猫咪 测试\DesktopCat.exe";
            StartupRegistration registration=new StartupRegistration(executable,delegate {return stored;},delegate(string value) {stored=value;writes++;});
            if(registration.Enabled) throw new Exception("Startup enabled without registration");
            using(DebugWindow panel=Panel(delegate(bool enabled) {registration.SetEnabled(enabled);return registration.Enabled;},registration.Enabled))
            {
                if(writes!=0) throw new Exception("Opening console changed startup");
                panel.StartupToggle.Checked=true;
                if(!registration.Enabled || stored!="\""+executable+"\" --autostart" || writes!=1)
                    throw new Exception("Enable or quoted Unicode path failed");
                using(DebugWindow reopened=Panel(null,registration.Enabled))
                    if(!reopened.StartupToggle.Checked) throw new Exception("Startup state lost on reopen");
                panel.StartupToggle.Checked=false;
                if(stored!=null || registration.Enabled || writes!=2) throw new Exception("Disable failed");
                panel.Opacity=0;panel.ShowInTaskbar=false;panel.Show();
                using(Bitmap image=new Bitmap(panel.Width,panel.Height))
                {panel.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.ChangeExtension(output,"png"));}
                panel.Close();
            }
            stored=@"""E:\old\DesktopCat.exe"" --autostart";
            if(registration.Enabled) throw new Exception("Old executable path mistaken for current startup");
            bool rejected=false;
            StartupRegistration denied=new StartupRegistration(executable,delegate {return null;},delegate(string value) {throw new UnauthorizedAccessException();});
            try {denied.SetEnabled(true);}catch(UnauthorizedAccessException) {rejected=true;}
            if(!rejected || denied.Enabled) throw new Exception("Write failure hidden");
            for(int i=0;i<2;i++)
            {
                bool actual=i==1;int callbacks=0;
                using(DebugWindow panel=Panel(delegate(bool requested) {callbacks++;return actual;},actual))
                {
                    panel.StartupToggle.Checked=!actual;
                    if(panel.StartupToggle.Checked!=actual || callbacks!=1) throw new Exception("Failed change did not restore checkbox");
                }
            }
            CheckDefault();
            File.WriteAllText(output,"PASS first-run default on / idempotent startup / remembered opt-out / upgrade path refresh / opening console does not register / quoted Unicode path / enable-disable / reopen / stale path / permission failure / checkbox rollback / no real registry writes");
        }
        static void CheckDefault()
        {
            string stored=null;bool? preference=null;int writes=0;
            Func<string> read=delegate {return stored;};
            Action<string> write=delegate(string value) {stored=value;writes++;};
            Func<bool?> readPreference=delegate {return preference;};
            Action<bool> savePreference=delegate(bool value) {preference=value;};
            StartupRegistration first=new StartupRegistration(@"E:\猫咪\DesktopCat.exe",read,write,readPreference,savePreference);
            first.ApplyDefault();
            if(!first.Enabled || preference!=true || writes!=1) throw new Exception("Default startup was not enabled");
            first.ApplyDefault();
            if(writes!=1) throw new Exception("Unchanged startup was rewritten");
            first.SetEnabled(false);
            StartupRegistration next=new StartupRegistration(@"E:\新版本\DesktopCat.exe",read,write,readPreference,savePreference);
            next.ApplyDefault();
            if(stored!=null || preference!=false || writes!=2) throw new Exception("Explicit opt-out was overridden");
            first.SetEnabled(true);next.ApplyDefault();
            if(!next.Enabled || first.Enabled || preference!=true || writes!=4) throw new Exception("Upgrade startup path was not refreshed");
        }
        static DebugWindow Panel(Func<bool,bool> startup,bool enabled)
        {
            return new DebugWindow(delegate(CatTask task) {},delegate(bool value) {},delegate(bool value) {},delegate(bool value) {},
                delegate {return "自启动检查";},false,false,true,null,null,true,null,null,true,startup,enabled);
        }
    }
}
