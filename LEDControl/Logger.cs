using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.IO.Packaging;

namespace LEDControl
{
    public static class Logger
    {
        public static string Filename;
        private static string Folder;
        public static FileStream File;
        private static DateTime OpenTime;
        private static Queue<string> LogQueue;
        private static System.Windows.Threading.DispatcherTimer DQTimer;

        public static void Init()
        {
            LogQueue = new Queue<string>();
            OpenTime = DateTime.Now;
            DQTimer = new System.Windows.Threading.DispatcherTimer();

            DQTimer.Interval = new TimeSpan(0, 0, 0, 1);
            DQTimer.Tick += DQTimer_Tick;

            var verObj = Assembly.GetExecutingAssembly().GetName().Version;
            string Version = String.Format("{0}.{1}.{2}.{3}", verObj.Major,
                verObj.Minor, verObj.Build, verObj.Revision);
            //await WriteLine("Started logging session at {0:MM/dd/yy H:mm:ss zzz}. Application version {1}", OpenTime, Version);
#if DEBUG
            QueueLine("Started logging session at {0:MM/dd/yy H:mm:ss zzz}. Application version {1}, Debug build", OpenTime, Version);
#else
            QueueLine("Started logging session at {0:MM/dd/yy H:mm:ss zzz}. Application version {1}, Release build", OpenTime, Version);
#endif
            if (System.Diagnostics.Debugger.IsAttached) QueueLine("Debugger attached");

            InitAsync();
        }

        private static async Task InitAsync()
        {
            string prog = Assembly.GetExecutingAssembly().GetName().Name;
            Filename = String.Format(prog+"_{0}.log", DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd_HHmm"));
            Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "xTdub", prog);
            if (!Directory.Exists(Folder))
                Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, Filename);
            File = new FileStream(path, FileMode.CreateNew);
        }

        static async void DQTimer_Tick(object sender, object e)
        {
            if (File == null) return;
            await FlushQueueAsync();
            DQTimer.Stop();
        }

        public async static Task WriteLine(string format, params object[] args)
        {
            if (File != null)
            {
                var time = (DateTime.Now - OpenTime);
                string str = String.Format("{0:hh\\:mm\\:ss\\.fff}:\t{1}\n", time, String.Format(format, args).Replace("\r\n", "\r\n\t\t\t\t"));
                await File.WriteAsync(ASCIIEncoding.UTF8.GetBytes(str), 0, str.Length);
            }
        }

        public async static Task FlushQueueAsync()
        {
            while (LogQueue.Count > 0)
            {
                var str = LogQueue.Dequeue();
                if (str == null)
                {
                    continue;
                }
                await File.WriteAsync(ASCIIEncoding.UTF8.GetBytes(str), 0, str.Length);
                await File.FlushAsync();
            }
            
        }

        public static void QueueLine(string format, params object[] args)
        {
            var time = (DateTime.Now - OpenTime);
            string str = String.Format("{0:hh\\:mm\\:ss\\.fff}:\t{1}\r\n", time, String.Format(format, args).Replace("\r\n", "\r\n\t\t\t\t"));
            LogQueue.Enqueue(str);
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debug.WriteLine(str.TrimEnd('\r', '\n'));
            }
            DQTimer.Start();
        }

        public static void QueueException(string name, Exception exception)
        {
            QueueLine("{0}: {1}\r\nSource: {2}\r\nStack Trace:\r\n{3}", name, exception.Message, exception.Source, exception.StackTrace);
        }

        public static void Cleanup(int number)
        {
            string[] fs = Directory.GetFiles(Folder);
            var files = new List<string>(fs);
            files.Sort((a, b) => { return System.IO.File.GetCreationTime(b).CompareTo(System.IO.File.GetCreationTime(a)); }); //sort newest to oldest
            for (int i = 0; i < files.Count; i++)
            {
                if (i >= number) System.IO.File.Delete(files[i]);
            }
        }

        public static void Cleanup(DateTime time)
        {
            string[] files = Directory.GetFiles(Folder);
            for (int i = 0; i < files.Length; i++)
            {
                if (System.IO.File.GetCreationTime(files[i]) < time) System.IO.File.Delete(files[i]);
            }
        }
    }
}
