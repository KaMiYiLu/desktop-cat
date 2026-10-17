using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DesktopCat
{
    // UI-thread scheduler: contacts use the same swing crossings as MudTrail.
    sealed class AudioContacts
    {
        readonly float[] previous=new float[4];
        bool primed;
        CatPose last;
        float nextStep;
        public bool Update(float now,float phase,bool moving,CatPose pose)
        {
            bool walking=moving && CatGait.Grounded(pose),contact=false;
            for(int foot=0;foot<4;foot++)
            {
                float swing=CatGait.Swing(phase,foot,pose,moving);
                if(walking && primed && last.Kind==pose.Kind && previous[foot]>0 && swing<=0) contact=true;
                previous[foot]=swing;
            }
            if(last.Kind==pose.Kind)
            {
                if(pose.Kind==PoseKind.Leap)
                    contact=last.Stage<LeapStage.FrontLanding && pose.Stage>=LeapStage.FrontLanding;
                if(pose.Kind==PoseKind.Jump) contact=last.Lift>0 && pose.Lift<=0 && pose.Progress>0.8f;
            }
            if(pose.Kind==PoseKind.Land && last.Kind==PoseKind.Drop) contact=true;
            primed=walking;last=pose;
            if(!contact || now<nextStep) return false;
            nextStep=now+0.09f;return true;
        }
    }

    sealed class CatAudio : IDisposable
    {
        readonly Random random=new Random();
        readonly bool diagnostic;
        AudioContacts contacts=new AudioContacts();
        WaveVoice meow,purr,leftStep,rightStep;
        bool enabled,footstepsEnabled,failed,disposed,alternate;
        float nextMeow=35,lastMeow=-100;
        public bool Enabled {get {return enabled;}}
        public bool FootstepsEnabled {get {return footstepsEnabled;}}
        public CatAudio(bool enabled,bool diagnostic,bool footstepsEnabled=true)
        {this.enabled=enabled;this.diagnostic=diagnostic;this.footstepsEnabled=footstepsEnabled;}
        public void SetFootstepsEnabled(bool value)
        {
            footstepsEnabled=value;contacts=new AudioContacts();
            if(value) return;
            try {if(leftStep!=null) leftStep.Stop();if(rightStep!=null) rightStep.Stop();}
            catch(Exception) {Disable();}
        }
        public void SetEnabled(bool value)
        {
            enabled=value;contacts=new AudioContacts();
            if(!value) StopAll();
        }
        bool Ready()
        {
            if(!enabled || diagnostic || failed || disposed) return false;
            if(meow!=null) return true;
            try
            {
                meow=new WaveVoice("meow",0.3f);purr=new WaveVoice("purr",0.12f);
                leftStep=new WaveVoice("step",0.035f);rightStep=new WaveVoice("step",0.035f);
                return true;
            }
            catch(Exception) {Disable();return false;}
        }
        void Disable()
        {
            failed=true;DisposeVoices();
        }
        public void Meow(float now)
        {
            if(now-lastMeow<4 || !Ready()) return;
            try {meow.Play(false);lastMeow=now;nextMeow=now+35+(float)random.NextDouble()*30;}
            catch(Exception) {Disable();}
        }
        public void TaskChanged(CatTask next,float now)
        {
            try {if(next!=CatTask.Sleep && purr!=null) purr.Stop();}
            catch(Exception) {Disable();}
            if(next==CatTask.Held) Meow(now);
        }
        public void Update(float now,float phase,bool moving,CatPose pose,bool mayMeow)
        {
            bool step=contacts.Update(now,phase,moving,pose) && footstepsEnabled;
            if(!enabled || diagnostic || failed || disposed) return;
            bool sleeping=pose.Kind==PoseKind.Sleep && pose.Progress>0.035f;
            try {if(!sleeping && purr!=null) purr.Stop();}
            catch(Exception) {Disable();return;}
            if(!sleeping && !step && !(mayMeow && now>=nextMeow)) return;
            if(!Ready()) return;
            try
            {
                if(sleeping && !purr.Playing) purr.Play(true);
                if(step) {(alternate?leftStep:rightStep).Play(false);alternate=!alternate;}
                if(mayMeow && now>=nextMeow) Meow(now);
            }
            catch(Exception) {Disable();}
        }
        void StopAll()
        {
            try
            {
                if(meow!=null) meow.Stop();if(purr!=null) purr.Stop();
                if(leftStep!=null) leftStep.Stop();if(rightStep!=null) rightStep.Stop();
            }
            catch(Exception) {Disable();}
        }
        void DisposeVoices()
        {
            if(meow!=null) meow.Dispose();if(purr!=null) purr.Dispose();
            if(leftStep!=null) leftStep.Dispose();if(rightStep!=null) rightStep.Dispose();
            meow=purr=leftStep=rightStep=null;
        }
        public void Dispose() {if(disposed) return;disposed=true;DisposeVoices();}
    }

    // A prepared PCM buffer per voice; no per-step files, threads or allocations.
    // Gain is applied to our samples, never to the system/device volume.
    sealed class WaveVoice : IDisposable
    {
        [StructLayout(LayoutKind.Sequential,Pack=2)]
        struct Format {public ushort Tag,Channels;public uint Rate,BytesPerSecond;public ushort Align,Bits,Extra;}
        [StructLayout(LayoutKind.Sequential)]
        struct Header {public IntPtr Data;public uint Length,Recorded;public UIntPtr User;public uint Flags,Loops;public IntPtr Next;public UIntPtr Reserved;}
        [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr device,uint id,ref Format format,IntPtr callback,IntPtr instance,uint flags);
        [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr device,IntPtr header,uint size);
        [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr device,IntPtr header,uint size);
        [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr device);
        [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr device,IntPtr header,uint size);
        [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr device);
        IntPtr device,data,header;
        bool prepared,queued;
        static readonly uint HeaderSize=(uint)Marshal.SizeOf(typeof(Header));
        public bool Playing {get {return queued && (((Header)Marshal.PtrToStructure(header,typeof(Header))).Flags&1)==0;}}
        static void Check(uint result,string operation) {if(result!=0) throw new InvalidOperationException(operation+" winmm="+result);}
        public WaveVoice(string name,float gain)
        {
            try
            {
                byte[] pcm=null;Format format=new Format();
                using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopCat."+name+".wav"))
                {
                    if(stream==null) throw new InvalidDataException("Missing audio: "+name);
                    using(BinaryReader reader=new BinaryReader(stream))
                    {
                        if(reader.ReadUInt32()!=0x46464952) throw new InvalidDataException("Invalid WAV");
                        reader.ReadUInt32();if(reader.ReadUInt32()!=0x45564157) throw new InvalidDataException("Invalid WAVE");
                        while(stream.Position+8<=stream.Length)
                        {
                            uint chunk=reader.ReadUInt32(),length=reader.ReadUInt32();long end=stream.Position+length;
                            if(end>stream.Length) throw new InvalidDataException("Truncated WAV");
                            if(chunk==0x20746d66 && length>=16)
                            {
                                format.Tag=reader.ReadUInt16();format.Channels=reader.ReadUInt16();format.Rate=reader.ReadUInt32();
                                format.BytesPerSecond=reader.ReadUInt32();format.Align=reader.ReadUInt16();format.Bits=reader.ReadUInt16();
                            }
                            if(chunk==0x61746164) pcm=reader.ReadBytes(checked((int)length));
                            stream.Position=end+(length&1);
                        }
                    }
                }
                if(format.Tag!=1 || format.Bits!=16 || pcm==null || pcm.Length%2!=0) throw new InvalidDataException("Expected PCM16");
                for(int i=0;i<pcm.Length;i+=2)
                {
                    short sample=(short)((short)(pcm[i]|pcm[i+1]<<8)*gain);
                    pcm[i]=(byte)sample;pcm[i+1]=(byte)(sample>>8);
                }
                Check(waveOutOpen(out device,uint.MaxValue,ref format,IntPtr.Zero,IntPtr.Zero,0),"open");
                data=Marshal.AllocHGlobal(pcm.Length);Marshal.Copy(pcm,0,data,pcm.Length);
                header=Marshal.AllocHGlobal((int)HeaderSize);
                Marshal.StructureToPtr(new Header {Data=data,Length=(uint)pcm.Length},header,false);
                Check(waveOutPrepareHeader(device,header,HeaderSize),"prepare");prepared=true;
            }
            catch {Dispose();throw;}
        }
        public void Play(bool loop)
        {
            Stop();Header state=(Header)Marshal.PtrToStructure(header,typeof(Header));
            state.Flags=(state.Flags&2u)|(loop?12u:0u);state.Loops=loop?uint.MaxValue:0;
            Marshal.StructureToPtr(state,header,false);
            Check(waveOutWrite(device,header,HeaderSize),"write");queued=true;
        }
        public void Stop()
        {
            if(!queued) return;
            Check(waveOutReset(device),"reset");queued=false;
        }
        public void Dispose()
        {
            if(device!=IntPtr.Zero)
            {
                // Never release memory still owned by the driver on a device error.
                uint result=waveOutReset(device);
                if(result==0 && prepared) result=waveOutUnprepareHeader(device,header,HeaderSize);
                if(result!=0) {return;}
                prepared=false;result=waveOutClose(device);
                if(result!=0) {return;}
                device=IntPtr.Zero;
            }
            queued=false;
            if(header!=IntPtr.Zero) {Marshal.FreeHGlobal(header);header=IntPtr.Zero;}
            if(data!=IntPtr.Zero) {Marshal.FreeHGlobal(data);data=IntPtr.Zero;}
        }
    }
}





