using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace FamilyPlanner
{

























    public static class Program
    {
        const string ShowEventName = "Local\\Onharu.ShowOnLaunch";

        [STAThread]
        public static void Main()
        {
            var executable = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var localTest = Path.GetFileNameWithoutExtension(executable).IndexOf("local-test", StringComparison.OrdinalIgnoreCase) >= 0 ||
                File.Exists(Path.Combine(Path.GetDirectoryName(executable), "ONHARU-TEST-MODE"));
            var instanceName = localTest ? "Local\\Onharu.LocalTest.SingleInstance" : "Local\\Onharu.SingleInstance";
            var showEventName = localTest ? "Local\\Onharu.LocalTest.ShowOnLaunch" : ShowEventName;
            bool first;
            using (var singleInstance = new Mutex(true, instanceName, out first))
            {
                if (!first)
                {
                    try { using (var show = EventWaitHandle.OpenExisting(showEventName)) show.Set(); }
                    catch { }
                    return;
                }
                try
                {
                    LegacyMigration.CopyV1UserStateOnce();
                    V21Migration.BackupPreUpgradeOnce();
                    AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                    {
                        var error = e.ExceptionObject as Exception;
                        ErrorLog.Write("Unhandled application error", error, error == null ? null : error.StackTrace);
                    };
                    TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs e)
                    { ErrorLog.Write("Unobserved task error", e.Exception); };
                    using (var show = new EventWaitHandle(false, EventResetMode.AutoReset, showEventName))
                    {
                        var app = new Application();
                        var window = new MainWindow();
                        var showWait = ThreadPool.RegisterWaitForSingleObject(show, delegate
                        {
                            window.Dispatcher.BeginInvoke(new Action(window.ShowFromExternalLaunch));
                        }, null, Timeout.Infinite, false);
                        try { app.Run(window); }
                        finally { showWait.Unregister(null); }
                    }
                }
                finally { LayerHostController.Stop(); singleInstance.ReleaseMutex(); }
            }
        }
    }
}
