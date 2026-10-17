using System;
using System.Threading;
using System.Windows.Forms;

namespace DesktopCat
{
    // Keep failure cleanup and a short user notification; never write log files.
    static class AppErrors
    {
        static int fatal;
        static bool diagnostic;
        public static Action ReleaseOwnedInput;
        public static void Initialize(string[] args)
        {
            diagnostic=args.Length>0;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException+=delegate(object sender,ThreadExceptionEventArgs e)
            {Fail(e.Exception,"UI thread");Environment.Exit(1);};
            AppDomain.CurrentDomain.UnhandledException+=delegate(object sender,UnhandledExceptionEventArgs e)
            {Fail(e.ExceptionObject as Exception,"unhandled process exception");};
        }
        public static void Fail(Exception error,string source)
        {
            if(Interlocked.Exchange(ref fatal,1)!=0) return;
            try {if(ReleaseOwnedInput!=null) ReleaseOwnedInput();} catch(Exception) {}
            Environment.ExitCode=1;
            if(!diagnostic) MessageBox.Show("猫咪遇到异常，需要退出。\n\n"+(error==null?source:error.Message),
                "猫咪遇到问题",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
}
