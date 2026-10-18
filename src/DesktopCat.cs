using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopCat
{
    struct V
    {
        public float X, Y;
        public V(float x, float y) { X = x; Y = y; }
        public float Length { get { return (float)Math.Sqrt(X * X + Y * Y); } }
        public V Unit { get { float n = Length; return n > 0.001f ? this / n : new V(); } }
        public PointF Point { get { return new PointF(X, Y); } }
        public static V operator +(V a, V b) { return new V(a.X+b.X,a.Y+b.Y); }
        public static V operator -(V a, V b) { return new V(a.X-b.X,a.Y-b.Y); }
        public static V operator *(V a, float b) { return new V(a.X*b,a.Y*b); }
        public static V operator /(V a, float b) { return new V(a.X/b,a.Y/b); }
    }

    sealed class Settings
    {
        public bool CanAttackMouse = true, AttackRandomly = false;
        public bool CanDragWindows = false;
        public bool FollowMouse = false;
        public bool CanPickUp = true;
        public bool SoundEnabled = true;
        public bool FootstepsEnabled = true;
        public float MinWander = 45, MaxWander = 60, FirstWander = 30;
        public static Settings Load(string path)
        {
            Settings s = new Settings();
            if (!File.Exists(path)) return s;
            foreach (string line in File.ReadAllLines(path))
            {
                string[] p = line.Split('='); if (p.Length != 2) continue;
                bool flag; float value;
                if (bool.TryParse(p[1], out flag))
                {
                    if (p[0] == "Task_CanAttackMouse") s.CanAttackMouse = flag;
                    if (p[0] == "AttackRandomly") s.AttackRandomly = flag;
                    if (p[0] == "Task_CanDragWindows") s.CanDragWindows = flag;
                    if (p[0] == "FollowMouse") s.FollowMouse = flag;
                    if (p[0] == "CanPickUp") s.CanPickUp = flag;
                    if (p[0] == "SoundEnabled") s.SoundEnabled = flag;
                    if (p[0] == "FootstepsEnabled") s.FootstepsEnabled = flag;
                }
                if (float.TryParse(p[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value) && value >= 1 && value <= 3600)
                {
                    if (p[0] == "MinWanderingTimeSeconds") s.MinWander = value;
                    if (p[0] == "MaxWanderingTimeSeconds") s.MaxWander = value;
                    if (p[0] == "FirstWanderTimeSeconds") s.FirstWander = value;
                }
            }
            s.MaxWander = Math.Max(s.MinWander, s.MaxWander); return s;
        }
    }

    enum CatTask { Wander, Mud, Note, Picture, Mouse, Lick, Jump, Run, Sprint, Follow, Held, Drop, Land, Leap, Sleep }

    sealed class CatWindow : Form
    {
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr handle,int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr handle,int index,int value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr handle,IntPtr insertAfter,int x,int y,int width,int height,uint flags);
        readonly Timer timer = new Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Random rng = new Random();
        readonly IdleMotion idle=new IdleMotion(Environment.TickCount);
        readonly Settings settings;
        readonly CatAudio audio;
        readonly NotifyIcon tray;
        readonly MudTrail mud = new MudTrail();
        readonly List<Form> gifts = new List<Form>();
        readonly Queue<CatTask> deck = new Queue<CatTask>();
        static readonly CatTask[] DropReactions={CatTask.Lick,CatTask.Jump,CatTask.Leap,CatTask.Run,CatTask.Sprint,CatTask.Mud,CatTask.Sleep};
        int leapsRemaining;
        V position, velocity, target;
        V leapOrigin,leapEnd;
        float lastTime, now, heading = 0, phase, deadline, pauseUntil, quitHeld;
        int stage; bool mouseWasDown, dragging, quitting;
        Form gift;
        DebugWindow debug;
        bool followMouse;
        bool diagnosticPaintFault;
        LayeredSurface surface;
        readonly Rectangle desktopBounds;
        V renderOrigin;
        bool rendering;

        V grabOffset;
        string settingMessage="";
        readonly string smokeOutput;
        int smokeTicks;
        readonly bool actionSmoke;
        readonly bool idleSmoke;
        int actionSmokeIndex,actionSmokeMask;
        bool smokeJumpSeen;
        float actionStart,nextIdleAction=8,resumeWanderDeadline;
        int idleActionIndex;
        CatTask task = CatTask.Wander;
        string root;
        protected override CreateParams CreateParams
        {
            get { CreateParams p = base.CreateParams; p.ExStyle |= 0x80000 | 0x08000000;
                if(settings!=null && !settings.CanPickUp) p.ExStyle|=0x20;return p; }
        }
        public CatWindow(string directory, string smokePath, bool checkActions=false, bool checkDebug=false,bool checkIdle=false)
        {
            actionSmoke=checkActions;
            idleSmoke=checkIdle;
            smokeOutput = smokePath;
            root = directory; settings = Settings.Load(Path.Combine(root,"config.ini"));
            audio=new CatAudio(settings.SoundEnabled,smokePath!=null,settings.FootstepsEnabled);
            AppErrors.ReleaseOwnedInput=ReleaseMouse;
            followMouse=settings.FollowMouse;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            Icon=AppBrand.Icon;
            desktopBounds=Screen.PrimaryScreen.WorkingArea;
            StartPosition = FormStartPosition.Manual; Bounds = new Rectangle(desktopBounds.Location,new Size(256,256));
            BackColor = Color.Black; DoubleBuffered = false;
            position = new V(100,150); target = new V(220,150); deadline = settings.FirstWander;
            if(smokeOutput != null) { settings.CanAttackMouse=false;followMouse=false;deadline=100;Shown+=delegate {Hide();}; }
            if(idleSmoke) {pauseUntil=100;nextIdleAction=100;target=position;}
            if(checkDebug) Shown+=delegate {CheckDebugControls(smokeOutput);Close();};
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("打开控制台",null,delegate {ShowDebug();});
            if(settings.CanDragWindows)
            {
                menu.Items.Add("叼一张便签",null,delegate { Start(CatTask.Note); });
                menu.Items.Add("叼一张图片",null,delegate { Start(CatTask.Picture); });
            }
            menu.Items.Add("追鼠标",null,delegate { if(settings.CanAttackMouse) Start(CatTask.Mouse); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("舔爪子",null,delegate { Start(CatTask.Lick); });
            menu.Items.Add("原地跳跃",null,delegate { Start(CatTask.Jump); });
            menu.Items.Add("前进跳跃",null,delegate { Start(CatTask.Leap); });
            menu.Items.Add("跑步",null,delegate { Start(CatTask.Run); });
            menu.Items.Add("冲刺",null,delegate { Start(CatTask.Sprint); });
            menu.Items.Add("睡觉",null,delegate { Start(CatTask.Sleep); });
            menu.Items.Add("叫一声",null,delegate {audio.Meow(now);});
            menu.Items.Add("退出猫咪",null,delegate { Close(); });
            tray = new NotifyIcon {Icon=AppBrand.Icon,Text="Desktop Cat · 按住 ESC 退出",ContextMenuStrip=menu,Visible=true};
            timer.Interval = 16; timer.Tick += Tick; timer.Start();
            if(smokeOutput==null)
            {
                Shown+=delegate { if(followMouse) Start(CatTask.Follow); };
            }
        }
        void CheckDebugControls(string resultPath)
        {
            using(DebugWindow panel=new DebugWindow(Start,SetFollow,SetAttack,SetPickUp,DebugStatus,false,false,settings.CanPickUp,PerformIdle,SetSound,settings.SoundEnabled,delegate {audio.Meow(now);},SetFootsteps,settings.FootstepsEnabled))
            {
                panel.Opacity=0;panel.ShowInTaskbar=false;panel.Show();
                foreach(Control control in panel.Controls)
                {
                    Button button=control as Button;
                    if(button==null || !(button.Tag is CatTask)) continue;
                    CatTask selected=(CatTask)button.Tag;button.PerformClick();
                    if(task!=selected) throw new Exception("Debug action button failed: "+selected);
                }
                foreach(Control control in panel.Controls)
                {
                    Button button=control as Button;
                    if(button==null || !(button.Tag is IdleKind)) continue;
                    button.PerformClick();
                    if(idle.Kind!=(IdleKind)button.Tag || task!=CatTask.Wander || velocity.Length!=0)
                        throw new Exception("Small action debug button failed");
                }
                panel.FollowToggle.Checked=true;
                if(task!=CatTask.Follow || !followMouse || dragging) throw new Exception("Follow toggle failed");
                velocity=new V(50,10);V walkingTarget=target;
                PerformIdle(IdleKind.Ear);
                if(task!=CatTask.Follow || !followMouse || velocity.X!=50 || velocity.Y!=10
                    || target.X!=walkingTarget.X || target.Y!=walkingTarget.Y)
                    throw new Exception("Ear demo interrupted walking");
                Start(CatTask.Jump);Start(CatTask.Wander);
                if(task!=CatTask.Follow) throw new Exception("Follow resume failed");
                Start(CatTask.Sleep);
                if(velocity.Length!=0 || CurrentPose.Kind!=PoseKind.Sleep) throw new Exception("Sleep did not stop movement");
                now=deadline+0.01f;FinishAction();
                if(task!=CatTask.Follow || !followMouse) throw new Exception("Sleep did not resume follow");
                Start(CatTask.Sleep);BeginGrab(position+new V(0,-28));
                if(task!=CatTask.Held) throw new Exception("Pickup did not wake sleeping cat");
                EndGrab();Start(CatTask.Follow);
                panel.AttackToggle.Checked=true;Start(CatTask.Mouse);
                if(task!=CatTask.Mouse) throw new Exception("Attack enable failed");
                panel.AttackToggle.Checked=false;
                if(task!=CatTask.Follow || settings.CanAttackMouse || dragging) throw new Exception("Attack disable failed");
                panel.FollowToggle.Checked=false;
                if(task!=CatTask.Wander || followMouse) throw new Exception("Follow disable failed");
                Start(CatTask.Mouse);if(task==CatTask.Mouse) throw new Exception("Disabled attack guard failed");
                panel.PickUpToggle.Checked=true;
                V mouse=position+new V(0,-28);BeginGrab(mouse);
                if(task!=CatTask.Held || velocity.Length!=0) throw new Exception("Pickup failed");
                V before=position;MoveGrab(mouse+new V(30,20));
                if(Math.Abs(position.X-before.X-30)>0.01f || Math.Abs(position.Y-before.Y-20)>0.01f) throw new Exception("Carry offset failed");
                EndGrab();if(task!=CatTask.Drop) throw new Exception("Drop failed");
                now=deadline+0.01f;FinishAction();
                if(task!=CatTask.Land) throw new Exception("Landing buffer failed");
                now=deadline+0.01f;FinishAction();
                if(Array.IndexOf(DropReactions,task)<0) throw new Exception("Landing reaction missing");
                CheckActionChains();
                BeginGrab(position+new V(0,-28));panel.PickUpToggle.Checked=false;
                if(task!=CatTask.Drop || settings.CanPickUp || Capture) throw new Exception("Disable pickup failed");
                panel.PickUpToggle.Checked=true;
                Settings saved=Settings.Load(Path.Combine(root,"config.ini"));
                if(saved.FollowMouse || saved.CanAttackMouse) throw new Exception("Toggle persistence failed");
                panel.SoundToggle.Checked=false;
                if(audio.Enabled || Settings.Load(Path.Combine(root,"config.ini")).SoundEnabled) throw new Exception("Sound mute persistence failed");
                panel.FootstepsToggle.Checked=false;
                if(audio.FootstepsEnabled || Settings.Load(Path.Combine(root,"config.ini")).FootstepsEnabled) throw new Exception("Footsteps mute persistence failed");
                panel.SoundToggle.Checked=true;
                if(!audio.Enabled || !Settings.Load(Path.Combine(root,"config.ini")).SoundEnabled) throw new Exception("Sound unmute persistence failed");
                if(audio.FootstepsEnabled || panel.FootstepsToggle.Checked) throw new Exception("Master reset footsteps preference");
                panel.FootstepsToggle.Checked=true;
                if(!audio.FootstepsEnabled || !Settings.Load(Path.Combine(root,"config.ini")).FootstepsEnabled) throw new Exception("Footsteps unmute persistence failed");
                panel.SoundToggle.Checked=false;
                if(audio.Enabled || !audio.FootstepsEnabled) throw new Exception("Footsteps overrode master mute");
                panel.SoundToggle.Checked=true;
                V originalPosition=position;
                position=new V(desktopBounds.Width-85,desktopBounds.Height-80);RenderLayered();
                if(surface.Bitmap.Width!=256 || surface.Bitmap.Height!=256) throw new Exception("Viewport did not shrink");
                Point local=new Point((int)(position.X-renderOrigin.X),(int)(position.Y-renderOrigin.Y));
                Point expectedScreen=WorldToScreen(new Point((int)position.X,(int)position.Y));
                if(PointToScreen(local)!=expectedScreen) throw new Exception("Cropped window coordinate mapping failed");
                BeginGrab(position+new V(0,-CurrentPose.Lift-28));
                V grabStart=position;MoveGrab(position+grabOffset-new V(0,42)+new V(-25,-20));
                if(Math.Abs(position.X-grabStart.X+25)>0.1f || Math.Abs(position.Y-grabStart.Y+20)>0.1f)
                    throw new Exception("Cropped window drag near desktop edge failed");
                EndGrab();position=originalPosition;
                mud.Begin(now);
                for(int i=0;i<40;i++) mud.Update(now+i/30f,new V(30+i*4,200),0,i*0.7f,true,new CatPose());
                now+=1.5f;position=new V(desktopBounds.Width-85,desktopBounds.Height-80);RenderLayered();
                if(surface.Bitmap.Width<=256) throw new Exception("Native viewport did not include distant mud");
                now+=3;mud.Update(now,position,0,0,false,new CatPose());RenderLayered();
                if(mud.Count!=0 || surface.Bitmap.Width!=256 || surface.Bitmap.Height!=256)
                    throw new Exception("Native viewport did not shrink after mud expiry");
                position=originalPosition;
                using(Bitmap image=new Bitmap(panel.Width,panel.Height))
                {panel.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.ChangeExtension(resultPath,"png"));}
                panel.Close();
            }
            File.WriteAllText(resultPath,"PASS action buttons / follow-attack toggles / pickup-offset-drop-land-reaction-disable / 60-20-20 leap counts / 1-2-3 jump chains / interruption and edge turn / sleep-stop-wake-follow-resume / native viewport and mud expiry / saved toggles");
        }
        void CheckActionChains()
        {
            V originalPosition=position;float originalHeading=heading,originalTime=now;
            int[] buckets=new int[3];int reactionMask=0;
            for(int roll=0;roll<100;roll++) buckets[LeapCount(roll)-1]++;
            if(buckets[0]!=60 || buckets[1]!=20 || buckets[2]!=20) throw new Exception("Leap probability thresholds failed");
            for(int i=0;i<100;i++)
            {
                followMouse=i%2==0;
                Start(CatTask.Drop);now=deadline;FinishAction();
                if(task!=CatTask.Land) throw new Exception("Drop skipped landing");
                now=deadline;FinishAction();int selected=Array.IndexOf(DropReactions,task);
                if(selected<0 || followMouse!=(i%2==0)) throw new Exception("Drop reaction or follow preference failed");
                reactionMask|=1<<selected;
            }
            if(reactionMask!=(1<<DropReactions.Length)-1) throw new Exception("Drop reaction coverage failed");
            for(int roll=0;roll<DropReactions.Length;roll++)
                if(DropReaction(roll,false)==CatTask.Mouse) throw new Exception("Disabled attack in drop pool");
            if(DropReaction(DropReactions.Length,true)!=CatTask.Mouse) throw new Exception("Enabled attack missing from drop pool");
            settings.CanAttackMouse=true;
            for(int i=0;i<100;i++)
            {
                followMouse=i%2==0;Start(CatTask.Drop);now=deadline;FinishAction();now=deadline;FinishAction();
                if((task!=CatTask.Mouse && Array.IndexOf(DropReactions,task)<0) || followMouse!=(i%2==0))
                    throw new Exception("Enabled attack drop reaction or follow preference failed");
            }
            // Disabling attack during the landing buffer must exclude it immediately.
            Start(CatTask.Drop);now=deadline;FinishAction();SetAttack(false);
            now=deadline;FinishAction();
            if(Array.IndexOf(DropReactions,task)<0) throw new Exception("Landing used stale attack setting");
            for(int count=1;count<=3;count++)
            for(int following=0;following<2;following++)
            {
                followMouse=following==1;position=new V(desktopBounds.Width/2,desktopBounds.Height/2);heading=0;
                Start(CatTask.Leap);
                if(leapsRemaining<1 || leapsRemaining>3) throw new Exception("Invalid sampled leap count");
                leapsRemaining=count;
                for(int jump=0;jump<count;jump++)
                {
                    V endpoint=leapEnd;now=deadline;FinishAction();
                    if((position-endpoint).Length>0.01f) throw new Exception("Leap endpoint lost between jumps");
                    if(jump<count-1 && (task!=CatTask.Leap || leapsRemaining!=count-jump-1 || CurrentPose.Progress!=0))
                        throw new Exception("Leap chain count or pose restart failed");
                }
                if(task!=(followMouse?CatTask.Follow:CatTask.Wander) || leapsRemaining!=0)
                    throw new Exception("Leap chain did not resume normal activity");
            }
            position=new V(desktopBounds.Width-65,desktopBounds.Height/2);heading=0;
            Start(CatTask.Leap);
            if((leapEnd-position).Length<60) throw new Exception("Edge leap did not turn inward");
            Start(CatTask.Sleep);
            if(leapsRemaining!=0) throw new Exception("Interrupted leap chain survived");
            followMouse=false;position=originalPosition;heading=originalHeading;now=originalTime;Start(CatTask.Wander);
        }
        void ShowDebug()
        {
            if(debug==null || debug.IsDisposed)
            {
                debug=new DebugWindow(Start,SetFollow,SetAttack,SetPickUp,DebugStatus,followMouse,settings.CanAttackMouse,settings.CanPickUp,PerformIdle,SetSound,settings.SoundEnabled,delegate {audio.Meow(now);},SetFootsteps,settings.FootstepsEnabled);
                debug.FormClosed+=delegate {debug=null;};debug.Show();
            }
            else {debug.Show();debug.Activate();}
        }
        string DebugStatus()
        {
            string[] names={"闲逛","泥脚印","便签","图片","攻击鼠标","舔爪","原地跳跃","跑步","冲刺","跟随鼠标","被拎起","下落","落地缓冲","前进跳跃","睡觉"};
            string small=idle.Kind==IdleKind.Ear?" / 抖耳朵":idle.Kind==IdleKind.Tail?" / 摆尾尖":idle.Watching?" / 看鼠标":"";
            return "当前："+names[(int)task]+small+"   速度："+((int)velocity.Length)+
                (settingMessage==""?"":"（设置未保存）");
        }
        void SaveToggle(string name,bool enabled)
        {
            try
            {
                string file=Path.Combine(root,"config.ini");List<string> lines=new List<string>(File.Exists(file)?File.ReadAllLines(file):new string[0]);
                string value=name+"="+(enabled?"True":"False");bool found=false;
                for(int i=0;i<lines.Count;i++) if(lines[i].StartsWith(name+"=")) {lines[i]=value;found=true;}
                if(!found) lines.Add(value);File.WriteAllLines(file,lines.ToArray());settingMessage="";
            }
            catch(IOException) {settingMessage="开关已生效，但配置未能保存。";}
            catch(UnauthorizedAccessException) {settingMessage="开关已生效，但配置未能保存。";}
        }
        void SetFollow(bool enabled)
        {
            followMouse=enabled;ReleaseMouse();Start(enabled?CatTask.Follow:CatTask.Wander);SaveToggle("FollowMouse",enabled);
        }
        void SetAttack(bool enabled)
        {
            settings.CanAttackMouse=enabled;deck.Clear();
            if(!enabled) {ReleaseMouse();if(task==CatTask.Mouse) Start(CatTask.Wander);}
            SaveToggle("Task_CanAttackMouse",enabled);
        }
        void SetPickUp(bool enabled)
        {
            settings.CanPickUp=enabled;
            if(!enabled) EndGrab();
            int style=GetWindowLong(Handle,-20);
            SetWindowLong(Handle,-20,enabled?style&~0x20:style|0x20);
            SaveToggle("CanPickUp",enabled);
        }
        void SetSound(bool enabled)
        {
            settings.SoundEnabled=enabled;audio.SetEnabled(enabled);SaveToggle("SoundEnabled",enabled);
        }
        void SetFootsteps(bool enabled)
        {
            settings.FootstepsEnabled=enabled;audio.SetFootstepsEnabled(enabled);SaveToggle("FootstepsEnabled",enabled);
        }
        void PerformIdle(IdleKind kind)
        {
            if(kind==IdleKind.Ear && (task==CatTask.Wander || task==CatTask.Follow))
            {
                idle.Trigger(kind,now);return;
            }
            Start(CatTask.Wander);velocity=new V();pauseUntil=now+3.2f;
            idle.Trigger(kind,now);
        }
        CatPose CurrentPose {get {return idle.Apply(CatPose.At(PoseFor(task),now-actionStart),now);}}
        bool OverCat(V mouse)
        {
            V drawn=position+new V(0,-CurrentPose.Lift);
            return mouse.X>drawn.X-55 && mouse.X<drawn.X+55 && mouse.Y>drawn.Y-88 && mouse.Y<drawn.Y+10;
        }
        void BeginGrab(V mouse)
        {
            if(!settings.CanPickUp) return;
            V drawn=position+new V(0,-CurrentPose.Lift);
            grabOffset=mouse-drawn;
            Start(CatTask.Held);position=drawn+new V(0,42);target=position;velocity=new V();
            if(smokeOutput==null) Capture=true;
        }
        void MoveGrab(V mouse)
        {
            if(task!=CatTask.Held) return;
            V desired=mouse-grabOffset+new V(0,42);
            position=new V(Math.Max(55,Math.Min(desktopBounds.Width-55,desired.X)),Math.Max(105,Math.Min(desktopBounds.Height-12,desired.Y)));
            target=position;RenderLayered();
        }
        void EndGrab() {if(task==CatTask.Held) Start(CatTask.Drop);}
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            V mouse=MousePoint();
            if(e.Button==MouseButtons.Left && OverCat(mouse)) BeginGrab(mouse);
        }
        protected override void OnMouseMove(MouseEventArgs e) {base.OnMouseMove(e);MoveGrab(MousePoint());}
        protected override void OnMouseUp(MouseEventArgs e) {base.OnMouseUp(e);if(e.Button==MouseButtons.Left) EndGrab();}
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);if(!Capture && !quitting) EndGrab();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        void MaintainTopmost()
        {
            // Never change activation, position, size, or mouse capture. Defer while held.
            if(smokeOutput!=null || quitting || !Visible || !IsHandleCreated || Capture || task==CatTask.Held) return;
            const uint SWP_NOSIZE=0x0001,SWP_NOMOVE=0x0002,SWP_NOACTIVATE=0x0010,SWP_NOOWNERZORDER=0x0200;
            SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,SWP_NOSIZE|SWP_NOMOVE|SWP_NOACTIVATE|SWP_NOOWNERZORDER);
        }
        float RandomRange(float a,float b) { return a+(float)rng.NextDouble()*(b-a); }
        V RandomPoint() { return new V(RandomRange(65,Math.Max(66,desktopBounds.Width-65)),RandomRange(85,Math.Max(86,desktopBounds.Height-35))); }
        V SprintTarget()
        {
            V forward=new V((float)Math.Cos(heading),(float)Math.Sin(heading));
            V proposed=position+forward*420;
            proposed=new V(Math.Max(65,Math.Min(desktopBounds.Width-65,proposed.X)),Math.Max(85,Math.Min(desktopBounds.Height-35,proposed.Y)));
            return (proposed-position).Length>90?proposed:new V(desktopBounds.Width/2,desktopBounds.Height/2);
        }
        V MousePoint() {Point cursor=Cursor.Position;return new V(cursor.X-desktopBounds.X,cursor.Y-desktopBounds.Y);}
        Point WorldToScreen(Point p) {return new Point(p.X+desktopBounds.X,p.Y+desktopBounds.Y);}
        V Mouth { get { return CatPainter.Mouth(position, heading); } }
        void ReleaseMouse() { if(dragging) { Cursor.Clip=Rectangle.Empty; dragging=false; } }
        void Start(CatTask next)
        {
            if(next==CatTask.Mouse && !settings.CanAttackMouse) return;
            if(next==CatTask.Wander && followMouse) next=CatTask.Follow;
            if(!settings.CanDragWindows && (next==CatTask.Note || next==CatTask.Picture)) return;
            leapsRemaining=next==CatTask.Leap?LeapCount(rng.Next(100)):0;
            if(PoseFor(next)!=PoseKind.Normal && task==CatTask.Wander) resumeWanderDeadline=deadline;
            if(PoseFor(next)==PoseKind.Normal) resumeWanderDeadline=0;
            ReleaseMouse(); gift=null; task=next; stage=0; pauseUntil=0;actionStart=now;
            idle.Update(now,0,false,false,new V());
            if(next!=CatTask.Held && Capture) Capture=false;
            if(next==CatTask.Wander) { deadline=now+RandomRange(settings.MinWander,settings.MaxWander); target=RandomPoint(); }
            else if(next==CatTask.Mouse) deadline=now+9;
            else if(next==CatTask.Follow) target=position;
            else if(next==CatTask.Held || next==CatTask.Drop || next==CatTask.Land)
            {velocity=new V();target=position;deadline=next==CatTask.Held?float.MaxValue:now+CatPose.Duration(PoseFor(next));}
            else if(next==CatTask.Lick || next==CatTask.Jump || next==CatTask.Sleep)
            { velocity=new V();target=position;deadline=now+CatPose.Duration(PoseFor(next)); }
            else if(next==CatTask.Leap)
            {
                BeginLeap();
            }
            else if(next==CatTask.Run || next==CatTask.Sprint)
            { target=next==CatTask.Sprint?SprintTarget():RandomPoint();deadline=now+CatPose.Duration(PoseFor(next)); }
            else target=new V(position.X<desktopBounds.Width/2 ? -70 : desktopBounds.Width+70,Math.Max(100,Math.Min(position.Y,desktopBounds.Height-100)));
            audio.TaskChanged(next,now);
            
        }
        static int LeapCount(int roll) {return roll<60?1:roll<80?2:3;}
        static CatTask DropReaction(int roll,bool allowAttack)
        {return allowAttack && roll==DropReactions.Length?CatTask.Mouse:DropReactions[roll];}
        void BeginLeap()
        {
            velocity=new V();leapOrigin=position;actionStart=now;
            V direction=new V((float)Math.Cos(heading),(float)Math.Sin(heading));
            leapEnd=ClampLeap(position+direction*190);
            // Turn between jumps at an edge instead of jumping against it.
            if((leapEnd-position).Length<60)
            {
                heading=(float)Math.Atan2(desktopBounds.Height/2f-position.Y,desktopBounds.Width/2f-position.X);
                direction=new V((float)Math.Cos(heading),(float)Math.Sin(heading));
                leapEnd=ClampLeap(position+direction*190);
            }
            target=leapEnd;deadline=now+CatPose.Duration(PoseKind.Leap);
            
        }
        V ClampLeap(V point)
        {return new V(Math.Max(65,Math.Min(desktopBounds.Width-65,point.X)),Math.Max(100,Math.Min(desktopBounds.Height-35,point.Y)));}
        static PoseKind PoseFor(CatTask task)
        {
            switch(task) {case CatTask.Lick:return PoseKind.Lick;case CatTask.Jump:return PoseKind.Jump;
                case CatTask.Run:return PoseKind.Run;case CatTask.Sprint:return PoseKind.Sprint;
                case CatTask.Held:return PoseKind.Held;case CatTask.Drop:return PoseKind.Drop;case CatTask.Land:return PoseKind.Land;
                case CatTask.Leap:return PoseKind.Leap;case CatTask.Sleep:return PoseKind.Sleep;default:return PoseKind.Normal;}
        }
        void FinishAction()
        {
            if(task==CatTask.Drop) {Start(CatTask.Land);return;}
            if(task==CatTask.Land)
            {
                bool allowAttack=settings.CanAttackMouse;
                Start(DropReaction(rng.Next(DropReactions.Length+(allowAttack?1:0)),allowAttack));return;
            }
            if(task==CatTask.Leap)
            {
                position=leapEnd;
                if(--leapsRemaining>0) {BeginLeap();return;}
            }
            float resume=resumeWanderDeadline;resumeWanderDeadline=0;
            Start(CatTask.Wander);if(resume>0) deadline=Math.Max(now+1,resume);
            nextIdleAction=now+RandomRange(15,25);
        }
        CatTask NextTask()
        {
            if(deck.Count==0)
            {
                List<CatTask> list=new List<CatTask>{CatTask.Mud};
                if(settings.CanDragWindows) {list.Add(CatTask.Note);list.Add(CatTask.Picture);}
                if(settings.AttackRandomly && settings.CanAttackMouse) list.Add(CatTask.Mouse);
                for(int i=list.Count-1;i>0;i--) { int j=rng.Next(i+1); CatTask t=list[i];list[i]=list[j];list[j]=t; }
                foreach(CatTask t in list) deck.Enqueue(t);
            }
            return deck.Dequeue();
        }
        void Tick(object sender,EventArgs args)
        {
            MaintainTopmost();
            now=(float)clock.Elapsed.TotalSeconds*(actionSmoke?4:1); float dt=Math.Min(0.05f,now-lastTime); lastTime=now;
            if(actionSmoke && task==CatTask.Wander && actionSmokeIndex<6)
            {
                CatTask[] checks={CatTask.Lick,CatTask.Jump,CatTask.Run,CatTask.Sprint,CatTask.Leap,CatTask.Sleep};
                Start(checks[actionSmokeIndex++]);
            }
            bool down=(GetAsyncKeyState(1)&0x8000)!=0;
            bool overDebug=debug!=null && debug.Visible && debug.Bounds.Contains(Cursor.Position);
            if(down && !mouseWasDown && !overDebug && !settings.CanPickUp && settings.CanAttackMouse && (MousePoint()-position).Length<40) Start(CatTask.Mouse);
            mouseWasDown=down;
            quitHeld=smokeOutput==null && (GetAsyncKeyState(27)&0x8000)!=0 ? quitHeld+dt : 0;
            if(quitHeld>=3) { Close();return; }
            float speed=80,accel=1300; bool paused=false;
            if(task==CatTask.Held && !down && smokeOutput==null) EndGrab();
            if(task==CatTask.Follow)
            {
                V mouse=MousePoint();float distance=(mouse-position).Length;
                if(distance<60) paused=true;
                else
                {
                    target=mouse-(mouse-position).Unit*45;
                    target=new V(Math.Max(65,Math.Min(desktopBounds.Width-65,target.X)),Math.Max(85,Math.Min(desktopBounds.Height-35,target.Y)));
                    speed=distance>260?200:100;
                }
            }
            if(task==CatTask.Wander)
            {
                if(now>=deadline) Start(NextTask());
                else if(smokeOutput==null && now>=nextIdleAction)
                {
                    CatTask[] actions={CatTask.Lick,CatTask.Jump,CatTask.Run,CatTask.Sprint,CatTask.Leap,CatTask.Sleep};
                    Start(actions[idleActionIndex++%actions.Length]);
                }
                else if(now<pauseUntil) paused=true;
                else if((target-position).Length<20) { pauseUntil=now+RandomRange(1,2);target=RandomPoint();paused=true; }
            }
            if(PoseFor(task)!=PoseKind.Normal)
            {
                if(now>=deadline)
                {
                    FinishAction();paused=PoseFor(task)!=PoseKind.Normal && task!=CatTask.Run && task!=CatTask.Sprint;
                }
                else if(task==CatTask.Lick || task==CatTask.Jump || task==CatTask.Held || task==CatTask.Drop || task==CatTask.Land || task==CatTask.Sleep) paused=true;
                else if(task==CatTask.Leap)
                {
                    position=leapOrigin+(leapEnd-leapOrigin)*CurrentPose.ForwardProgress;
                    paused=true;
                }
                else
                {
                    speed=task==CatTask.Sprint?340:200;accel=task==CatTask.Sprint?900:1300;
                    if(task==CatTask.Sprint) speed=80+260*Math.Min(1,Math.Max(0,(deadline-now)/0.45f));
                    if((target-position).Length<25) target=task==CatTask.Sprint?SprintTarget():RandomPoint();
                }
            }
            if(task==CatTask.Mud)
            {
                speed=200;
                if(stage==0 && (target-position).Length<10) { stage=1;mud.Begin(now);deadline=now+MudTrail.ActiveSeconds;target=RandomPoint(); }
                if(stage==1) { if((target-position).Length<15) target=RandomPoint(); if(now>deadline) Start(CatTask.Wander); }
            }
            if(task==CatTask.Note || task==CatTask.Picture)
            {
                if(stage==0 && (target-position).Length<10) { stage=1;deadline=now+RandomRange(2,3.5f); }
                if(stage==1) { paused=true;if(now>=deadline) { CreateGift(task==CatTask.Picture); stage=2;target=new V(desktopBounds.Width/2,Math.Max(180,desktopBounds.Height/2)); } }
                if(stage==2)
                {
                    if(gift==null || gift.IsDisposed || (target-position).Length<10) Start(CatTask.Wander);
                    else gift.Location=WorldToScreen(new Point((int)(Mouth.X-(Math.Cos(heading)>0?0:gift.Width)),(int)Mouth.Y-35));
                }
            }
            if(task==CatTask.Mouse)
            {
                speed=400;accel=2300;
                if(stage==0)
                {
                    target=MousePoint()-(Mouth-position);
                    if((MousePoint()-Mouth).Length<15) { stage=1;dragging=true;target=RandomPoint();deadline=now+4; }
                    else if(now>deadline) Start(CatTask.Wander);
                }
                if(stage==1)
                {
                    Point p=WorldToScreen(new Point((int)Mouth.X,(int)Mouth.Y)); Cursor.Clip=new Rectangle(p.X-2,p.Y-2,5,5);
                    if((target-position).Length<30 || now>deadline) Start(CatTask.Wander);
                }
            }
            bool idleAllowed=task==CatTask.Wander || task==CatTask.Follow;
            idle.Update(now,dt,idleAllowed,paused,MousePoint()-position-new V(0,-35));
            if(idleAllowed && (idle.Watching || now<pauseUntil)) paused=true;
            V delta=target-position;
            if(paused) velocity=new V();
            else
            {
                velocity=velocity+delta.Unit*(accel*dt); if(velocity.Length>speed) velocity=velocity.Unit*speed;
                if(delta.Length<velocity.Length*dt) {position=target;velocity=new V();} else position=position+velocity*dt;
            }
            if(velocity.Length>8) heading=CatPainter.Turn(heading,(float)Math.Atan2(velocity.Y,velocity.X),dt);
            phase+=velocity.Length*dt*0.12f;
            mud.Update(now,position,heading,phase,velocity.Length>5,CurrentPose);
            audio.Update(now,phase,velocity.Length>5,CurrentPose,task==CatTask.Wander || task==CatTask.Follow);
            RenderLayered();
            int interval=RenderPolicy.Interval(velocity.Length,CurrentPose.Kind,idle.Kind,mud.Count,Capture,quitHeld>0);
            if(timer.Interval!=interval) timer.Interval=interval;
            if(smokeOutput!=null)
            {
                smokeTicks++;
                if(actionSmoke)
                {
                    PoseKind kind=PoseFor(task);
                    if(kind!=PoseKind.Normal) actionSmokeMask|=1<<((int)kind-1);
                    if(CatPose.At(kind,now-actionStart).Lift>20) smokeJumpSeen=true;
                    if((actionSmokeIndex==6 && task==CatTask.Wander) || now>45)
                    {
                        bool valid=actionSmokeMask==399 && smokeJumpSeen && task==CatTask.Wander && !float.IsNaN(position.X) && !float.IsNaN(position.Y);
                        File.WriteAllText(smokeOutput,(valid?"PASS":"FAIL")+" lick/jump/run/sprint/leap/sleep task entry and completion / finite movement / return to wander");
                        if(!valid) Environment.ExitCode=1;
                        Close();
                    }
                }
                else if(now>(idleSmoke?3:0.7f))
                {
                    bool valid=smokeTicks>5 && !float.IsNaN(position.X) && !float.IsInfinity(position.X)
                        && (idleSmoke?velocity.Length==0 && timer.Interval==33:position.X>100);
                    File.WriteAllText(smokeOutput,(valid?"PASS":"FAIL")+" UI initialization / timer / finite movement / graceful shutdown; ticks="+smokeTicks+
                        " interval="+timer.Interval+" surface="+surface.Bitmap.Width+"x"+surface.Bitmap.Height);
                    if(!valid) Environment.ExitCode=1;
                    Close();
                }
            }
        }
        void CreateGift(bool picture)
        {
            Form f=new Form {Text=picture?"猫咪带来的图片":"Cat Not-epad",Size=new Size(260,190),TopMost=true,StartPosition=FormStartPosition.Manual,ShowInTaskbar=false};
            f.Location=WorldToScreen(new Point((int)position.X,(int)position.Y));
            Image loaded=null;
            if(picture)
            {
                string folder=Path.Combine(root,"Assets","Images","Memes");
                string[] files=Directory.Exists(folder)?Directory.GetFiles(folder):new string[0];
                if(files.Length>0) for(int i=0,start=rng.Next(files.Length);i<files.Length;i++)
                {
                    try { using(Image original=Image.FromFile(files[(start+i)%files.Length])) loaded=new Bitmap(original);break; } catch(ArgumentException) {} catch(IOException) {}
                }
                if(loaded==null)
                {
                    loaded=new Bitmap(250,150); using(Graphics g=Graphics.FromImage(loaded)) {g.Clear(Color.FromArgb(246,243,234));CatPainter.Draw(g,new V(110,105),0,0,false,1);}
                }
                f.Controls.Add(new PictureBox {Dock=DockStyle.Fill,Image=loaded,SizeMode=PictureBoxSizeMode.Zoom});
            }
            else
            {
                string text="喵。\r\n\r\n工作辛苦了，摸摸猫吧。";
                string folder=Path.Combine(root,"Assets","Text","NotepadMessages");
                try {string[] files=Directory.GetFiles(folder,"*.txt");if(files.Length>0) text=File.ReadAllText(files[rng.Next(files.Length)]);} catch(IOException) {}
                f.Controls.Add(new TextBox {Text=text,Multiline=true,Dock=DockStyle.Fill,Font=new Font("Microsoft YaHei",11)});
            }
            Image owned=loaded;
            f.FormClosed+=delegate {gifts.Remove(f);if(owned!=null) owned.Dispose();if(!quitting && settings.CanAttackMouse) Start(CatTask.Mouse);};
            gifts.Add(f);gift=f;f.Show();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            RenderLayered();
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // All pixels, including transparent edges, are supplied by the DIB.
        }
        void RenderLayered()
        {
            if(quitting || rendering || !IsHandleCreated) return;
            rendering=true;
            try
            {
                Rectangle frame=RenderPolicy.WindowBounds(position,mud.Snapshot(),desktopBounds.Size);
                renderOrigin=new V(frame.X,frame.Y);
                if(surface==null || surface.Bitmap.Width!=frame.Width || surface.Bitmap.Height!=frame.Height)
                {
                    if(surface!=null) surface.Dispose();
                    surface=new LayeredSurface(frame.Width,frame.Height);
                }
                surface.Graphics.ResetTransform();
                surface.Graphics.Clear(Color.Transparent);
                surface.Graphics.TranslateTransform(-frame.X,-frame.Y);
                PaintCat(new PaintEventArgs(surface.Graphics,frame));
                surface.Present(Handle,desktopBounds.X+frame.X,desktopBounds.Y+frame.Y);
            }
            catch(Exception error) {RenderFailed(error,"render foreground");}
            finally {rendering=false;}
        }
        void RenderFailed(Exception error,string source)
        {
            if(quitting) return;
            quitting=true;timer.Stop();ReleaseMouse();Capture=false;Hide();
            AppErrors.Fail(error,source);
            BeginInvoke(new MethodInvoker(Close));
        }
        internal void TestPaintCrash()
        {
            diagnosticPaintFault=true;
            using(Bitmap b=new Bitmap(200,200)) using(Graphics g=Graphics.FromImage(b))
                OnPaint(new PaintEventArgs(g,new Rectangle(0,0,200,200)));
        }
        void PaintCat(PaintEventArgs e)
        {
            if(diagnosticPaintFault) throw new InvalidOperationException("DesktopCat intentional paint error test");
            base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            mud.Draw(g,now);
            CatPainter.Draw(g,position,heading,phase,velocity.Length>5,now,CurrentPose);
            if(quitHeld>0.1f)
            {
                GraphicsState indicator=g.Save();g.TranslateTransform(renderOrigin.X,renderOrigin.Y);
                g.FillRectangle(Brushes.LightBlue,8,8,220,30);g.FillRectangle(Brushes.LightPink,8,8,220*Math.Min(1,quitHeld/3),30);
                g.DrawString("继续按住 ESC 让猫咪休息",SystemFonts.MessageBoxFont,Brushes.Black,14,16);
                g.Restore(indicator);
            }
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            quitting=true;timer.Stop();timer.Dispose();ReleaseMouse();
            audio.Dispose();
            if(surface!=null) {surface.Dispose();surface=null;}
            CatPainter.ClearCaches();
            
            if(debug!=null && !debug.IsDisposed) debug.Close();
            foreach(Form f in gifts.ToArray()) f.Close();
            tray.Visible=false;tray.ContextMenuStrip.Dispose();tray.Dispose();base.OnFormClosed(e);
        }
    }
    static class Program
    {
        [STAThread] static void Main(string[] args)
        {
            try
            {
                AppErrors.Initialize(args);
                if(args.Length>0 && args[0]=="--diagnostic-crash-test")
                {throw new InvalidOperationException("DesktopCat intentional error test");}
                Run(args);
            }
            catch(Exception error) {AppErrors.Fail(error,"main entry");Environment.ExitCode=1;}
        }
        static void Run(string[] args)
        {
            if(args.Length==2 && args[0]=="--audio-check") {AudioCheck.Run(args[1]);return;}
            if(args.Length==2 && args[0]=="--mud-check")
            {
                for(int i=0;i<16;i++)
                {
                    float angle=(float)(i*Math.PI/8);MudTrail test=new MudTrail();test.Begin(0);
                    V origin=new V(100,100);float before=(float)Math.PI-0.1f,after=(float)Math.PI+0.1f;
                    test.Update(0,origin,angle,before,true,new CatPose());
                    test.Update(0.1f,origin,angle,after,true,new CatPose());
                    MudPrint[] marks=test.Snapshot();
                    V expected=CatGait.Footprint(origin,angle,(float)Math.Sin(after),0,new CatPose());
                    if(marks.Length!=2 || (marks[0].Position-expected).Length>0.001f) throw new Exception("Rotated foot-contact alignment failed");
                }
                foreach(PoseKind kind in new PoseKind[]{PoseKind.Jump,PoseKind.Leap,PoseKind.Held,PoseKind.Drop,PoseKind.Land,PoseKind.Lick,PoseKind.Sleep})
                {
                    MudTrail test=new MudTrail();test.Begin(0);
                    for(int i=0;i<30;i++) test.Update(i/30f,new V(i*10,100),0,i,true,CatPose.At(kind,i/30f));
                    if(test.Count!=0) throw new Exception("Airborne or stationary pose left mud");
                }
                MudTrail trail=new MudTrail();trail.Begin(0);
                Directory.CreateDirectory(args[1]);
                int mudFrames=(int)Math.Ceiling((MudTrail.ActiveSeconds+MudTrail.LifeSeconds+0.3f)*24);
                for(int frame=0;frame<mudFrames;frame++)
                {
                    float time=frame/24f,distance=Math.Min(time,MudTrail.ActiveSeconds)*180;
                    V position=new V(65+distance,140);float phase=distance*0.12f;
                    bool moving=time<MudTrail.ActiveSeconds;
                    trail.Update(time,position,0,phase,moving,new CatPose());
                    if(trail.Count>MudTrail.MaxPrints) throw new Exception("Mud capacity exceeded");
                    using(Bitmap b=new Bitmap(560,210)) using(Graphics g=Graphics.FromImage(b))
                    {
                        g.Clear(Color.FromArgb(246,243,234));
                        System.Drawing.Drawing2D.GraphicsState view=g.Save();
                        g.TranslateTransform(-Math.Max(0,position.X-420),0);trail.Draw(g,time);
                        CatPainter.Draw(g,position,0,phase,moving,time);
                        g.Restore(view);
                        g.DrawString("短泥脚印 · 落脚生成 · 迅速消失",SystemFonts.MessageBoxFont,Brushes.DimGray,25,180);
                        b.Save(Path.Combine(args[1],frame.ToString("D3")+".png"),System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                if(trail.Count!=0) throw new Exception("Mud did not expire");
                File.WriteAllText(Path.Combine(args[1],"result.txt"),"PASS 16 headings foot-contact alignment / airborne suppression / capacity / expiry");return;
            }
            if(args.Length==2 && args[0]=="--verify-idle")
            {
                int earVariants=0;
                for(int seed=0;seed<64;seed++)
                {
                    IdleMotion walking=new IdleMotion(seed);walking.Trigger(IdleKind.Ear,0);
                    walking.Update(0.25f,0.025f,true,false,new V());
                    CatPose ears=walking.Apply(new CatPose(),0.25f);
                    if(ears.EarTwitch==0) throw new Exception("Walking suppressed ears");
                    earVariants|=ears.OtherEarTwitch!=0?4:ears.EarSide<0?1:2;
                }
                if(earVariants!=7) throw new Exception("Missing single/dual ear variation");
                IdleMotion moving=new IdleMotion(3);moving.Update(5.1f,0.025f,true,false,new V());
                if(moving.Kind!=IdleKind.Ear) throw new Exception("Automatic ear twitch requires stopping");
                int frames=0;IdleMotion motion=new IdleMotion(1);
                using(Bitmap frame=new Bitmap(220,220)) using(Graphics g=Graphics.FromImage(frame))
                foreach(IdleKind kind in new IdleKind[]{IdleKind.Ear,IdleKind.Tail,IdleKind.Watch})
                {
                    motion.Trigger(kind,0);
                    for(int sample=0;sample<=120;sample++)
                    {
                        float time=sample/30f;
                        motion.Update(time,1f/30,true,true,new V(300,-150));
                        CatPose pose=motion.Apply(new CatPose(),time);
                        if(Math.Abs(pose.GazeX)>2.21f || Math.Abs(pose.GazeY)>1.01f) throw new Exception("Gaze exceeded bounds");
                        CatPose special=motion.Apply(CatPose.At(PoseKind.Held,1),time);
                        if(special.EarTwitch!=0 || special.OtherEarTwitch!=0 || special.TailFlick!=0 || special.GazeX!=0) throw new Exception("Idle leaked into special pose");
                        for(int heading=0;heading<24;heading++)
                        {
                            g.Clear(Color.White);CatPainter.Draw(g,new V(110,160),(float)(heading*Math.PI/12),0,false,time,pose);frames++;
                        }
                    }
                    if(motion.Kind!=IdleKind.None) throw new Exception("Small action did not finish");
                    motion.Trigger(kind,5);motion.Update(5.1f,0.1f,false,true,new V());
                    if(motion.Kind!=IdleKind.None) throw new Exception("Special action did not cancel small action");
                }
                motion=new IdleMotion(2);int mask=0,events=0;IdleKind previous=IdleKind.None;
                for(int i=0;i<3600;i++)
                {
                    motion.Update(i/30f,1f/30,true,true,new V());
                    if(motion.Kind!=IdleKind.None) mask|=1<<(int)motion.Kind;
                    if(motion.Kind!=previous && motion.Kind!=IdleKind.None) events++;
                    previous=motion.Kind;
                }
                if(mask!=14 || events>35) throw new Exception("Automatic idle scheduling failed");
                File.WriteAllText(args[1],"PASS "+frames+" frames / 24 directions / walking automatic ears / left-right-dual ear variants / bounded gaze / completion / special-action cancellation / low-frequency automatic scheduling; events="+events+" in 120s");return;
            }
            if(args.Length==2 && args[0]=="--ear-walk-frames")
            {
                Directory.CreateDirectory(args[1]);IdleMotion[] motions=new IdleMotion[3];
                for(int variant=0;variant<3;variant++)
                for(int seed=0;seed<64;seed++)
                {
                    IdleMotion candidate=new IdleMotion(seed);candidate.Trigger(IdleKind.Ear,0);
                    CatPose sample=candidate.Apply(new CatPose(),0.25f);
                    int kind=sample.OtherEarTwitch!=0?2:sample.EarSide<0?0:1;
                    if(kind==variant) {motions[variant]=candidate;break;}
                }
                string[] labels={"行走 · 左耳","行走 · 右耳","行走 · 双耳"};
                for(int frame=0;frame<24;frame++)
                using(Bitmap b=new Bitmap(660,240)) using(Graphics g=Graphics.FromImage(b))
                using(Font font=new Font("Microsoft YaHei",12))
                {
                    float time=frame/24f;g.Clear(Color.FromArgb(246,243,234));
                    for(int i=0;i<3;i++)
                    {
                        motions[i].Update(time,1f/24,true,false,new V());
                        CatPainter.Draw(g,new V(95+i*220+time*30,145),(float)(Math.PI/2),time*9,true,time,motions[i].Apply(new CatPose(),time));
                        g.DrawString(labels[i],font,Brushes.DimGray,60+i*220,190);
                    }
                    b.Save(Path.Combine(args[1],frame.ToString("D3")+".png"),System.Drawing.Imaging.ImageFormat.Png);
                }
                return;
            }
            if(args.Length==2 && args[0]=="--idle-frames")
            {
                Directory.CreateDirectory(args[1]);
                IdleMotion[] motions={new IdleMotion(1),new IdleMotion(2),new IdleMotion(3)};
                string[] names={"耳朵轻抖","尾尖轻摆","停下看鼠标"};
                for(int frame=0;frame<96;frame++)
                using(Bitmap b=new Bitmap(660,240)) using(Graphics g=Graphics.FromImage(b))
                using(Font font=new Font("Microsoft YaHei",12))
                {
                    float time=frame/24f;g.Clear(Color.FromArgb(246,243,234));
                    for(int i=0;i<3;i++)
                    {
                        if(frame==0 || (frame==48 && i<2)) motions[i].Trigger((IdleKind)(i+1),time);
                        motions[i].Update(time,1f/24,true,true,new V((float)Math.Sin(time*2)*240,-60));
                        CatPainter.Draw(g,new V(110+i*220,145),i==1?0:(float)(Math.PI/2),0,false,time,motions[i].Apply(new CatPose(),time));
                        g.DrawString(names[i],font,Brushes.DimGray,65+i*220,190);
                    }
                    b.Save(Path.Combine(args[1],frame.ToString("D3")+".png"),System.Drawing.Imaging.ImageFormat.Png);
                }
                return;
            }
            if(args.Length==2 && args[0]=="--verify-alpha")
            {
                int softPixels=0;
                using(LayeredSurface layer=new LayeredSurface(280,220))
                using(Bitmap review=new Bitmap(840,440))
                using(Graphics composite=Graphics.FromImage(review))
                {
                    Color[] backgrounds={Color.White,Color.FromArgb(220,227,238),Color.FromArgb(38,41,47)};
                    for(int row=0;row<2;row++)
                    {
                        layer.Graphics.Clear(Color.Transparent);
                        CatPainter.Draw(layer.Graphics,new V(140,165),0,0,false,1,
                            row==0?new CatPose():CatPose.At(PoseKind.Held,1));
                        layer.Graphics.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
                        if(layer.Bitmap.GetPixel(0,0).A!=0) throw new Exception("Layer background is not transparent");
                        System.Drawing.Imaging.BitmapData data=layer.Bitmap.LockBits(new Rectangle(0,0,280,220),
                            System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                        try
                        {
                            byte[] pixels=new byte[data.Stride*220];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
                            for(int i=0;i<pixels.Length;i+=4)
                            {
                                int alpha=pixels[i+3];
                                if(pixels[i]>alpha || pixels[i+1]>alpha || pixels[i+2]>alpha)
                                    throw new Exception("Invalid premultiplied alpha");
                                if(alpha>0 && alpha<255) softPixels++;
                            }
                        }
                        finally {layer.Bitmap.UnlockBits(data);}
                        for(int column=0;column<3;column++)
                        {
                            Rectangle tile=new Rectangle(column*280,row*220,280,220);
                            using(SolidBrush background=new SolidBrush(backgrounds[column])) composite.FillRectangle(background,tile);
                            composite.DrawImageUnscaled(layer.Bitmap,tile.Location);
                        }
                    }
                    if(softPixels<100) throw new Exception("Missing soft alpha edges or shadow");
                    review.Save(Path.ChangeExtension(args[1],"png"),System.Drawing.Imaging.ImageFormat.Png);
                }
                File.WriteAllText(args[1],"PASS reusable native DIB / premultiplied alpha / transparent background / "+softPixels+" soft pixels / white-light-dark composites");return;
            }
            if(args.Length==2 && args[0]=="--verify-lick")
            {
                int samples=0;
                using(Bitmap frame=new Bitmap(220,220)) using(Graphics g=Graphics.FromImage(frame))
                {
                    Action<float> draw=delegate(float progress)
                    {
                        
                        g.Clear(Color.White);
                        CatPainter.Draw(g,new V(110.5875f,160.6786f),-0.5098196f,0,false,1,
                            new CatPose {Kind=PoseKind.Lick,Progress=progress});samples++;
                    };
                    draw(0.06244183f);draw(0.6250229f);
                    for(int crossing=0;crossing<=16;crossing++)
                    foreach(float delta in new float[]{0,0.0000001f,-0.0000001f,0.000001f,-0.000001f,
                        0.00001f,-0.00001f,0.0001f,-0.0001f,0.001f,-0.001f})
                    {
                        float progress=crossing/16f+delta;
                        if(progress>=0 && progress<=1) draw(progress);
                    }
                    for(int i=0;i<=4096;i++) draw(i/4096f);
                }
                File.WriteAllText(args[1],"PASS "+samples+" lick frames / reported crash phases / tongue zero-length boundaries / complete action cycle");return;
            }
            if(args.Length==2 && args[0]=="--verify")
            {
                float a=(float)(179*Math.PI/180), b=(float)(-179*Math.PI/180);
                float turned=CatPainter.Turn(a,b,0.016f);
                if(turned<=a || turned-a>0.113f) throw new Exception("Shortest-angle turn failed");
                V down=CatPainter.Mouth(new V(),(float)(Math.PI/2));
                V up=CatPainter.Mouth(new V(),(float)(-Math.PI/2));
                if(down.Y<=up.Y) throw new Exception("Directional interaction anchor failed");
                using(Bitmap frame=new Bitmap(180,180)) using(Graphics g=Graphics.FromImage(frame))
                {
                    for(int i=0;i<72;i++)
                    {
                        float angle=(float)(i*Math.PI/36);V mouth=CatPainter.Mouth(new V(90,100),angle);
                        if(float.IsNaN(mouth.X) || float.IsNaN(mouth.Y)) throw new Exception("Non-finite rig anchor");
                        g.Clear(Color.White);CatPainter.Draw(g,new V(90,100),angle,i*0.2f,true,i*0.1f);
                        foreach(PoseKind kind in new PoseKind[]{PoseKind.Lick,PoseKind.Jump,PoseKind.Run,PoseKind.Sprint,PoseKind.Held,PoseKind.Drop,PoseKind.Land,PoseKind.Leap,PoseKind.Sleep})
                            CatPainter.Draw(g,new V(90,100),angle,i*0.2f,true,i*0.1f,CatPose.At(kind,CatPose.Duration(kind)*0.5f));
                    }
                }
                float jumpDuration=CatPose.Duration(PoseKind.Jump);
                CatPose preload=CatPose.At(PoseKind.Jump,jumpDuration*0.2f);
                if(CatPose.At(PoseKind.Jump,0).Lift!=0 || preload.Lift!=0 || preload.Crouch<0.9f ||
                    CatPose.At(PoseKind.Jump,jumpDuration*0.59f).Lift<41 || Math.Abs(CatPose.At(PoseKind.Jump,jumpDuration).Lift)>0.001f)
                    throw new Exception("Jump ground preload/apex/landing failed");
                if(Math.Abs(CatPose.At(PoseKind.Sprint,1).Gait-CatPose.At(PoseKind.Sprint,0).Gait-2.8f*(float)Math.PI*2)>0.001f)
                    throw new Exception("Sprint gait rate failed");
                float leapDuration=CatPose.Duration(PoseKind.Leap);
                if(CatPose.At(PoseKind.Leap,leapDuration*0.45f).Lift<67 ||
                    CatPose.At(PoseKind.Leap,0).ForwardProgress!=0 || CatPose.At(PoseKind.Leap,leapDuration).ForwardProgress!=1)
                    throw new Exception("Forward leap height/progress failed");
                CatPose push=CatPose.At(PoseKind.Leap,leapDuration*0.16f);
                CatPose flightPose=CatPose.At(PoseKind.Leap,leapDuration*0.45f);
                CatPose frontLand=CatPose.At(PoseKind.Leap,leapDuration*0.75f);
                CatPose hindLand=CatPose.At(PoseKind.Leap,leapDuration*0.87f);
                if(push.HindHeight!=0 || push.FrontHeight<=0 || flightPose.FrontExtension<=0 || flightPose.HindExtension>=0 ||
                    frontLand.FrontHeight!=0 || frontLand.HindHeight<=0 || hindLand.FrontHeight!=0 || hindLand.HindHeight<=0)
                    throw new Exception("Leap push/extension/front-first landing sequence failed");
                for(int sample=0;sample<16;sample++)
                {
                    float angle=(float)(sample*Math.PI/8);
                    V paw=CatGait.LeapPaw(angle,2,frontLand);
                    V ground=CatGait.ProjectGround(17+frontLand.FrontExtension,-10,angle);
                    if(Math.Abs(paw.Y-frontLand.Lift-ground.Y)>0.001f) throw new Exception("Front contact left ground plane");
                }
                if(CatPose.At(PoseKind.Leap,leapDuration*0.1f).Crouch>0.31f) throw new Exception("Leap crouch too deep");
                if(Math.Abs(CatPose.At(PoseKind.Drop,0).Lift-42)>0.001f || CatPose.At(PoseKind.Drop,0.42f).Lift!=0)
                    throw new Exception("Drop endpoint continuity failed");
                File.WriteAllText(args[1],"PASS 72 headings x 10 poses / sleep / leap rear-push / aerial extension / front-first landing / 16 contact projections / jump and drop endpoints");return;
            }
            if(args.Length==2 && args[0]=="--leap-sequence")
            {
                float[] progress={0.16f,0.45f,0.75f,0.87f,1};
                string[] labels={"后脚蹬地","前伸后展","前脚先落","后脚着地","站稳恢复"};
                using(Bitmap b=new Bitmap(1000,280)) using(Graphics g=Graphics.FromImage(b))
                using(Font font=new Font("Microsoft YaHei",12))
                {
                    g.Clear(Color.FromArgb(246,243,234));
                    for(int i=0;i<5;i++)
                    {
                        CatPose pose=CatPose.At(PoseKind.Leap,CatPose.Duration(PoseKind.Leap)*progress[i]);
                        CatPainter.Draw(g,new V(100+i*200,200),0,0,false,i,pose);
                        g.DrawString(labels[i],font,Brushes.DimGray,60+i*200,240);
                    }
                    b.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);
                }
                return;
            }
            if(args.Length==2 && args[0]=="--jump-frames")
            {
                Directory.CreateDirectory(args[1]);
                for(int frame=0;frame<96;frame++)
                {
                    float time=frame/24f,elapsed=time%2;
                    CatPose jump=CatPose.At(PoseKind.Jump,elapsed),leap=CatPose.At(PoseKind.Leap,elapsed);
                    using(Bitmap b=new Bitmap(720,260)) using(Graphics g=Graphics.FromImage(b))
                    using(Font font=new Font("Microsoft YaHei",12))
                    {
                        g.Clear(Color.FromArgb(246,243,234));
                        CatPainter.Draw(g,new V(140,175),0,0,false,time,jump);
                        CatPainter.Draw(g,new V(380+leap.ForwardProgress*190,175),0,0,false,time,leap);
                        g.DrawString("原地跳跃",font,Brushes.DimGray,100,215);
                        g.DrawString("前进跳跃 · 更高、更远",font,Brushes.DimGray,405,215);
                        b.Save(Path.Combine(args[1],frame.ToString("D3")+".png"),System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                return;
            }
            if(args.Length==2 && args[0]=="--grab-frames")
            {
                Directory.CreateDirectory(args[1]);
                for(int frame=0;frame<64;frame++)
                {
                    float time=frame/24f;PoseKind kind=PoseKind.Normal;float elapsed=0;
                    string label="轻轻按住猫咪";
                    if(time>=0.25f && time<1.25f) {kind=PoseKind.Held;elapsed=time-0.25f;label="四爪扑腾 · 哭哭摇摆";}
                    else if(time>=1.25f && time<1.67f) {kind=PoseKind.Drop;elapsed=time-1.25f;label="松手 · 轻轻下落";}
                    else if(time>=1.67f && time<2.02f) {kind=PoseKind.Land;elapsed=time-1.67f;label="落地 · 软软缓冲";}
                    else if(time>=2.02f) label="站稳，继续活动";
                    using(Bitmap b=new Bitmap(560,240)) using(Graphics g=Graphics.FromImage(b))
                    using(Font font=new Font("Microsoft YaHei",13))
                    {
                        g.Clear(Color.FromArgb(246,243,234));
                        CatPainter.Draw(g,new V(150,170),0.35f,0,false,time,CatPose.At(kind,elapsed));
                        g.DrawString(label,font,Brushes.DimGray,260,110);
                        b.Save(Path.Combine(args[1],frame.ToString("D3")+".png"),System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                return;
            }
            if(args.Length==2 && args[0]=="--action-frames")
            {
                Directory.CreateDirectory(args[1]);
                PoseKind[] kinds={PoseKind.Lick,PoseKind.Jump,PoseKind.Run,PoseKind.Sprint};
                string[] names={"舔爪子","跳跃","跑步","冲刺"};
                for(int frame=0;frame<96;frame++)
                {
                    float time=frame/24f;
                    using(Bitmap b=new Bitmap(800,240)) using(Graphics g=Graphics.FromImage(b))
                    using(Font font=new Font("Microsoft YaHei",12))
                    {
                        g.Clear(Color.FromArgb(246,243,234));
                        for(int i=0;i<4;i++)
                        {
                            float elapsed=i==1?time%1.7f:time;
                            CatPose pose=CatPose.At(kinds[i],elapsed);
                            float phase=time*(i==3?38:24);
                            CatPainter.Draw(g,new V(100+i*200,135),0,phase,i>=2,time,pose);
                            g.DrawString(names[i],font,Brushes.DimGray,76+i*200,175);
                        }
                        b.Save(Path.Combine(args[1],frame.ToString("D3")+".png"),System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                return;
            }
            if(args.Length==2 && (args[0]=="--sleep-preview" || args[0]=="--sleep-frames"))
            {
                bool animation=args[0]=="--sleep-frames";
                if(animation) Directory.CreateDirectory(args[1]);
                for(int frame=0;frame<(animation?96:1);frame++)
                using(Bitmap b=new Bitmap(animation?300:800,280)) using(Graphics g=Graphics.FromImage(b))
                using(Font font=new Font("Microsoft YaHei",11))
                {
                    g.Clear(Color.FromArgb(246,243,234));
                    string[] labels={"蜷下","抱爪入睡","轻轻呼吸 · zzz","醒来"};
                    float[] times={0.15f,2,6,17.8f};
                    for(int i=0;i<(animation?1:4);i++)
                    {
                        GraphicsState saved=g.Save();g.TranslateTransform(animation?150:100+i*200,190);g.ScaleTransform(1.8f,1.8f);
                        CatPainter.Draw(g,new V(),(float)Math.PI,0,false,frame/24f,
                            CatPose.At(PoseKind.Sleep,animation?frame/24f+0.5f:times[i]));g.Restore(saved);
                        if(!animation) g.DrawString(labels[i],font,Brushes.DimGray,65+i*200,235);
                    }
                    b.Save(animation?Path.Combine(args[1],frame.ToString("D3")+".png"):args[1],System.Drawing.Imaging.ImageFormat.Png);
                }
                return;
            }
            if(args.Length==2 && args[0]=="--neck-preview")
            {
                using(Bitmap b=new Bitmap(1120,600)) using(Graphics g=Graphics.FromImage(b))
                using(Font font=new Font("Microsoft YaHei",12))
                {
                    g.Clear(Color.FromArgb(246,243,234));
                    string[] labels={"向右","右下","向下","左下","向左","左上","向上","右上"};
                    for(int i=0;i<8;i++)
                    {
                        GraphicsState saved=g.Save();g.TranslateTransform(140+i%4*280,210+i/4*290);g.ScaleTransform(2.4f,2.4f);
                        CatPainter.Draw(g,new V(),(float)(i*Math.PI/4),0,false,1);g.Restore(saved);
                        g.DrawString(labels[i],font,Brushes.DimGray,115+i%4*280,245+i/4*290);
                    }
                    b.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);
                }
                return;
            }
            if(args.Length==2 && (args[0]=="--preview" || args[0]=="--held-preview" || args[0]=="--angle-review"))
            {
                bool angleReview=args[0]=="--angle-review";
                float[] angles={0,-15,-20,-25,-30,-45,-90,-135,-150,-155,-160,180};
                using(Bitmap b=new Bitmap(800,angleReview?575:400)) using(Graphics g=Graphics.FromImage(b))
                {
                    g.Clear(Color.FromArgb(246,243,234));
                    string[] labels={"向右","右下","向下","左下","向左","左上","向上","右上"};
                    using(Font font=new Font("Microsoft YaHei",11))
                    {
                        for(int i=0;i<(angleReview?angles.Length:8);i++)
                        {
                            V p=new V(100+(i%4)*200,130+(i/4)*175);
                            float angle=angleReview?(float)(angles[i]*Math.PI/180):(float)(i*Math.PI/4);
                            if(args[0]=="--held-preview")
                                CatPainter.Draw(g,p,angle,0,false,1,CatPose.At(PoseKind.Held,1));
                            else CatPainter.Draw(g,p,angle,0,false,1);
                            g.DrawString(angleReview?angles[i]+"°":labels[i],font,Brushes.DimGray,p.X-24,p.Y+25);
                        }
                        g.DrawString("Desktop Cat · 连续转向 / 地面投影骨架 · 四足步态",font,Brushes.DimGray,30,angleReview?540:365);
                    }
                    b.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);
                }
                return;
            }
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            bool debugCheck=args.Length==2 && args[0]=="--debug-check";
            bool paintCheck=args.Length==2 && args[0]=="--diagnostic-paint-test";
            string smoke=args.Length==2 && (args[0]=="--smoke" || args[0]=="--smoke-idle" || args[0]=="--smoke-actions" || debugCheck || paintCheck) ? args[1] : null;
            string root=AppDomain.CurrentDomain.BaseDirectory;
            if(debugCheck)
            {
                root=Path.Combine(root,"debug-check-work");Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root,"config.ini"),"Task_CanAttackMouse=False\nFollowMouse=False\nTask_CanDragWindows=False\n");
            }
            CatWindow window=new CatWindow(root,smoke,args.Length==2 && args[0]=="--smoke-actions",debugCheck,args.Length==2 && args[0]=="--smoke-idle");
            if(paintCheck) window.Shown+=delegate {window.TestPaintCrash();};
            Application.Run(window);
        }
    }
}




