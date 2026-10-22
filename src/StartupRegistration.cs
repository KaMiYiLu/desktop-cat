using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DesktopCat
{
    sealed class StartupRegistration
    {
        const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName="DesktopCat";
        const string PreferenceKey=@"Software\DesktopCat";
        const string PreferenceName="StartupEnabled";
        readonly Func<string> read;
        readonly Action<string> write;
        readonly Func<bool?> readPreference;
        readonly Action<bool> writePreference;
        readonly string command;
        public StartupRegistration():this(Application.ExecutablePath,ReadRun,WriteRun,ReadPreference,WritePreference) {}
        internal StartupRegistration(string executable,Func<string> readValue,Action<string> writeValue,
            Func<bool?> readPreference=null,Action<bool> writePreference=null)
        {
            command="\""+executable+"\" --autostart";read=readValue;write=writeValue;
            this.readPreference=readPreference??delegate {return null;};
            this.writePreference=writePreference??delegate(bool value) {};
        }
        public bool Enabled
        {
            get {return string.Equals(read(),command,StringComparison.OrdinalIgnoreCase);}
        }
        public void SetEnabled(bool enabled)
        {
            // Remember an explicit opt-out even after the Run entry is removed.
            writePreference(enabled);write(enabled?command:null);
        }
        public void ApplyDefault()
        {
            bool? preference=readPreference();
            if(preference==false) return;
            if(!Enabled) SetEnabled(true);
            else if(!preference.HasValue) writePreference(true);
        }
        static bool? ReadPreference()
        {
            using(RegistryKey key=Registry.CurrentUser.OpenSubKey(PreferenceKey))
            {
                object value=key==null?null:key.GetValue(PreferenceName);
                return value is int && ((int)value==0 || (int)value==1)?(bool?)((int)value==1):null;
            }
        }
        static void WritePreference(bool enabled)
        {
            using(RegistryKey key=Registry.CurrentUser.CreateSubKey(PreferenceKey))
            {
                if(key==null) throw new InvalidOperationException("无法保存自启动偏好。");
                key.SetValue(PreferenceName,enabled?1:0,RegistryValueKind.DWord);
            }
        }
        static string ReadRun()
        {
            using(RegistryKey key=Registry.CurrentUser.OpenSubKey(RunKey))
                return key==null?null:key.GetValue(ValueName) as string;
        }
        static void WriteRun(string value)
        {
            if(value==null)
            {
                using(RegistryKey key=Registry.CurrentUser.OpenSubKey(RunKey,true))
                    if(key!=null) key.DeleteValue(ValueName,false);
            }
            else using(RegistryKey key=Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if(key==null) throw new InvalidOperationException("无法打开当前用户的启动设置。");
                key.SetValue(ValueName,value,RegistryValueKind.String);
            }
        }
    }
}
