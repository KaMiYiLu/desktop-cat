using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace DesktopCat
{
    static class AudioCheck
    {
        static void Require(bool ok,string message) {if(!ok) throw new Exception("Audio check: "+message);}
        static double[] Levels(string name)
        {
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopCat."+name+".wav"))
            using(BinaryReader reader=new BinaryReader(stream))
            {
                Require(reader.ReadUInt32()==0x46464952,"RIFF "+name);reader.ReadUInt32();
                Require(reader.ReadUInt32()==0x45564157,"WAVE "+name);
                bool pcm=false;
                while(stream.Position+8<=stream.Length)
                {
                    uint chunk=reader.ReadUInt32(),length=reader.ReadUInt32();long end=stream.Position+length;
                    Require(end<=stream.Length,"truncated "+name);
                    if(chunk==0x20746d66)
                    {
                        Require(length>=16,"format "+name);
                        ushort tag=reader.ReadUInt16(),channels=reader.ReadUInt16();uint rate=reader.ReadUInt32();
                        reader.ReadUInt32();reader.ReadUInt16();ushort bits=reader.ReadUInt16();
                        pcm=tag==1 && channels==1 && rate==44100 && bits==16;
                    }
                    if(chunk==0x61746164)
                    {
                        Require(pcm && length>0 && length%2==0,"mono PCM16 44.1kHz "+name);
                        double peak=0,sum=0;int count=(int)(length/2);
                        for(int i=0;i<count;i++) {double sample=reader.ReadInt16()/32768.0;peak=Math.Max(peak,Math.Abs(sample));sum+=sample*sample;}
                        return new double[]{peak,Math.Sqrt(sum/count),count/44100.0};
                    }
                    stream.Position=end+(length&1);
                }
            }
            throw new Exception("Missing PCM data: "+name);
        }
        public static void VerifyAssets(string output)
        {
            double[] meow=Levels("meow"),bite=Levels("bite");
            Require(bite[0]>0 && bite[2]<1,"non-silent short bite");
            Require(bite[0]*CatAudio.BiteGain<=meow[0]*CatAudio.MeowGain,"bite peak louder than meow");
            Require(bite[1]*CatAudio.BiteGain<=meow[1]*CatAudio.MeowGain,"bite RMS louder than meow");
            File.WriteAllText(output,"PASS embedded bite PCM16 / bite peak and RMS no louder than meow / short non-silent one-shot");
        }
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
            using(WaveVoice bite=new WaveVoice("bite",0))
            {
                purr.Play(true);meow.Play(false);step.Play(false);bite.Play(false);Thread.Sleep(80);
                Require(purr.Playing,"loop not playing");
                purr.Stop();Require(!purr.Playing,"loop did not stop");
                purr.Play(true);step.Stop();step.Play(false);Thread.Sleep(400);
                Require(purr.Playing && !step.Playing && !bite.Playing,"loop / one-shot lifetime");
                bite.Play(false);bite.Stop();Require(!bite.Playing,"bite mute/reset");
                meow.Dispose();meow.Dispose();
            }
            using(CatAudio audio=new CatAudio(true,true))
            {
                audio.Meow(1);audio.Bite();audio.SetEnabled(false);audio.Bite();audio.TaskChanged(CatTask.Sleep,2);
                audio.Update(3,0,false,CatPose.At(PoseKind.Sleep,1),false);
                Require(!audio.Enabled,"mute");audio.SetEnabled(true);Require(audio.Enabled,"unmute");
            }
            File.WriteAllText(output,"PASS embedded meow/purr/step/bite PCM decode / walking contacts / no airborne-sleep steps / jump-leap-drop landing / silent native concurrent voices / loop reset-reuse / bite one-shot completion and reset / mute-unmute / double dispose");
        }
    }
}

