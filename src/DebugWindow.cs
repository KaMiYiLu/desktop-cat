using System;
using System.Drawing;
using System.Windows.Forms;

namespace DesktopCat
{
    sealed class DebugWindow : Form
    {
        public readonly CheckBox FollowToggle,AttackToggle,PickUpToggle,SoundToggle,FootstepsToggle,StartupToggle;
        readonly Label status;
        readonly Timer refresh=new Timer();
        public DebugWindow(Action<CatTask> perform,Action<bool> follow,Action<bool> attack,Action<bool> pickup,
            Func<string> state,bool following,bool attacking,bool canPickUp,Action<IdleKind> idle=null,
            Action<bool> sound=null,bool soundEnabled=true,Action meow=null,
            Action<bool> footsteps=null,bool footstepsEnabled=true,
            Func<bool,bool> startup=null,bool startupEnabled=false)
        {
            Text="猫咪控制台";ClientSize=new Size(320,270);TopMost=true;
            Icon=AppBrand.Icon;
            FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;
            StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;
            Font=new Font("Microsoft YaHei",9);BackColor=Color.FromArgb(246,243,234);
            Label title=new Label {Text="动作",Location=new Point(10,7),AutoSize=true,Font=new Font(Font,FontStyle.Bold)};
            Controls.Add(title);
            string[] names={"闲逛 / 停止","舔爪子","原地跳跃","前进跳跃","跑步","冲刺","泥脚印","睡觉"};
            CatTask[] tasks={CatTask.Wander,CatTask.Lick,CatTask.Jump,CatTask.Leap,CatTask.Run,CatTask.Sprint,CatTask.Mud,CatTask.Sleep};
            for(int i=0;i<tasks.Length;i++)
            {
                CatTask selected=tasks[i];
                Button button=new Button {Text=names[i],Location=new Point(10+(i%3)*102,30+(i/3)*28),Size=new Size(96,26),Tag=selected};
                button.Click+=delegate {perform(selected);};Controls.Add(button);
            }
            string[] smallNames={"抖耳朵","摆尾尖","看鼠标"};
            IdleKind[] smallKinds={IdleKind.Ear,IdleKind.Tail,IdleKind.Watch};
            for(int i=0;i<3;i++)
            {
                IdleKind selected=smallKinds[i];
                Button button=new Button {Text=smallNames[i],Location=new Point(10+i*102,114),Size=new Size(96,26),Enabled=idle!=null,Tag=selected};
                button.Click+=delegate {if(idle!=null) idle(selected);};Controls.Add(button);
            }
            Controls.Add(new Label {Text="互动与声音",Location=new Point(10,148),AutoSize=true,Font=title.Font});
            FollowToggle=new CheckBox {Text="跟随鼠标",Checked=following,Location=new Point(12,172),AutoSize=true};
            AttackToggle=new CheckBox {Text="允许攻击鼠标",Checked=attacking,Location=new Point(12,196),AutoSize=true};
            FollowToggle.CheckedChanged+=delegate {follow(FollowToggle.Checked);};
            AttackToggle.CheckedChanged+=delegate {attack(AttackToggle.Checked);};
            Controls.Add(FollowToggle);Controls.Add(AttackToggle);
            PickUpToggle=new CheckBox {Text="允许抓猫",Checked=canPickUp,Location=new Point(168,172),AutoSize=true};
            PickUpToggle.CheckedChanged+=delegate {pickup(PickUpToggle.Checked);};Controls.Add(PickUpToggle);
            Button attackNow=new Button {Text="演示攻击",Location=new Point(214,192),Size=new Size(96,24),Enabled=attacking};
            attackNow.Click+=delegate {perform(CatTask.Mouse);};Controls.Add(attackNow);
            AttackToggle.CheckedChanged+=delegate {attackNow.Enabled=AttackToggle.Checked;};
            CheckBox soundToggle=new CheckBox {Text="声音总开关",Checked=soundEnabled,Location=new Point(12,220),AutoSize=true,Enabled=sound!=null};
            SoundToggle=soundToggle;
            soundToggle.CheckedChanged+=delegate {if(sound!=null) sound(soundToggle.Checked);};Controls.Add(soundToggle);
            Button meowNow=new Button {Text="叫一声",Location=new Point(214,86),Size=new Size(96,26),Enabled=meow!=null && soundEnabled};
            meowNow.Click+=delegate {if(meow!=null) meow();};Controls.Add(meowNow);
            soundToggle.CheckedChanged+=delegate {meowNow.Enabled=meow!=null && soundToggle.Checked;};
            FootstepsToggle=new CheckBox {Text="开启脚步声",Checked=footstepsEnabled,Location=new Point(168,220),AutoSize=true,Enabled=footsteps!=null};
            FootstepsToggle.CheckedChanged+=delegate {if(footsteps!=null) footsteps(FootstepsToggle.Checked);};Controls.Add(FootstepsToggle);
            StartupToggle=new CheckBox {Text="开机自启动",Checked=startupEnabled,Location=new Point(12,244),AutoSize=true,Enabled=startup!=null};
            bool updatingStartup=false;
            StartupToggle.CheckedChanged+=delegate {
                if(updatingStartup || startup==null) return;
                bool actual=startup(StartupToggle.Checked);
                updatingStartup=true;try {StartupToggle.Checked=actual;}finally {updatingStartup=false;}
            };
            Controls.Add(StartupToggle);
            Controls.Add(new Label {Text="登录 Windows 后启动",Location=new Point(168,246),AutoSize=true,ForeColor=Color.DimGray});
            status=new Label {Location=new Point(46,6),Size=new Size(264,20),Text=state(),TextAlign=ContentAlignment.MiddleRight,AutoEllipsis=true};Controls.Add(status);
            refresh.Interval=200;refresh.Tick+=delegate {status.Text=state();};refresh.Start();
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            refresh.Stop();refresh.Dispose();base.OnFormClosed(e);
        }
    }
}
