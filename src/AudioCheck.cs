using System;
using System.IO;
using System.Threading;

namespace DesktopCat
{
    static class AudioCheck
    {
        static void Require(bool ok,string message) {if(!ok) throw new Exception("Audio check: "+message);}
        public static void Run(string output)
        {
            AudioContacts contacts=new AudioContacts();int steps=0;
            for(int i=0;i<120;i++)
                if(contacts.Update(i/60f,i*0.25f,true,new CatPose())) steps++;
            Require(steps>=8 && steps<=10,"walking contacts "+steps);
            for(int i=0;i<60;i++)
                Require(!contacts.Update(2+i/60f,i,true,CatPose.At(PoseKind.Sleep,i/60f)),"sleeping footsteps");
            contacts=new AudioContacts();steps=0;
            for(int i=0;i<=100;i++)
            {
                CatPose pose=CatPose.At(PoseKind.Leap,i*0.0105f);
                bool step=contacts.Update(i*0.0105f,0,false,pose);
                Require(!step || pose.Stage>=LeapStage.FrontLanding,"airborne leap step");
                if(step) steps++;
            }
            Require(steps==1,"leap landing "+steps);
            contacts=new AudioContacts();steps=0;
            for(int i=0;i<=120;i++)
                if(contacts.Update(i*0.01f,0,false,CatPose.At(PoseKind.Jump,i*0.01f))) steps++;
            Require(steps==1,"jump landing "+steps);
            contacts=new AudioContacts();
            contacts.Update(0,0,false,CatPose.At(PoseKind.Drop,0.4f));
            Require(contacts.Update(0.1f,0,false,CatPose.At(PoseKind.Land,0)),"drop landing");
            // Exercise real driver buffers silently: looping + concurrent one-shots,
            // reset/reuse, automatic completion, double disposal.
            using(WaveVoice purr=new WaveVoice("purr",0))
            using(WaveVoice meow=new WaveVoice("meow",0))
            using(WaveVoice step=new WaveVoice("step",0))
            {
                purr.Play(true);meow.Play(false);step.Play(false);Thread.Sleep(80);
                Require(purr.Playing,"loop not playing");
                purr.Stop();Require(!purr.Playing,"loop did not stop");
                purr.Play(true);step.Stop();step.Play(false);Thread.Sleep(400);
                Require(purr.Playing && !step.Playing,"loop / one-shot lifetime");
                meow.Dispose();meow.Dispose();
            }
            using(CatAudio audio=new CatAudio(true,true))
            {
                audio.Meow(1);audio.SetEnabled(false);audio.TaskChanged(CatTask.Sleep,2);
                audio.Update(3,0,false,CatPose.At(PoseKind.Sleep,1),false);
                Require(!audio.Enabled,"mute");audio.SetEnabled(true);Require(audio.Enabled,"unmute");
            }
            File.WriteAllText(output,"PASS embedded M08/P01/F11 PCM decode / walking contacts / no airborne-sleep steps / jump-leap-drop landing / silent native concurrent voices / loop reset-reuse / one-shot completion / mute-unmute / double dispose");
        }
    }
}

