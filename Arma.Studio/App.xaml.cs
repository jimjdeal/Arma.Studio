﻿using Arma.Studio.Data;
using Arma.Studio.Data.UI;
using Sentry;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Arma.Studio
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IApp
    {
        public const string CONST_DOCKING_MANAGER_LAYOUT_NAME = "layout.xml";
        public const string CONST_DOCKING_MANAGER_LAYOUT_JSON_NAME = "layout.json";
        public const string CONST_CONFIG_NAME = "config.xml";
        public const string CONST_VENDOR = "X39";
        public const string CONST_APPLICATIONNAME = "Arma.Studio";
        public const string CONST_PLUGINS = "Plugins";

        public static readonly string ExecutableFile = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.Substring("file:///".Length);
        public static readonly string ExecutablePath = System.IO.Path.GetDirectoryName(ExecutableFile);
        public static readonly string ApplicationDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), CONST_VENDOR, CONST_APPLICATIONNAME);
        public static readonly string TempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), CONST_VENDOR, CONST_APPLICATIONNAME);
        public static readonly string CommonApplicationDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), CONST_VENDOR, CONST_APPLICATIONNAME);
        public static readonly string ConfigPath = System.IO.Path.Combine(ApplicationDataPath, CONST_CONFIG_NAME);
        public static readonly string LayoutFilePath = System.IO.Path.Combine(ApplicationDataPath, CONST_DOCKING_MANAGER_LAYOUT_NAME);
        public static readonly string LayoutConfigFilePath = System.IO.Path.Combine(ApplicationDataPath, CONST_DOCKING_MANAGER_LAYOUT_JSON_NAME);
        public static readonly Version CurrentVersion = typeof(App).Assembly.GetName().Version;

        public static readonly string PluginDir_Executable = System.IO.Path.Combine(ExecutablePath, CONST_PLUGINS);

        internal static void ShutdownSafe(int v)
        {
            (App.Current as App).Sentry?.Dispose();
            (App.Current as App).Sentry = null;
            App.Current.Shutdown(v);
        }

        public static readonly string PluginDir_RoamingUser = System.IO.Path.Combine(CommonApplicationDataPath, CONST_PLUGINS);
        public static readonly string PluginDir_Data = System.IO.Path.Combine(ApplicationDataPath, CONST_PLUGINS);

        static App()
        {
            using (var stream = typeof(App).Assembly.GetManifestResourceStream(typeof(App).Assembly.GetName().Name + ".git-version.txt"))
            using (var reader = new System.IO.StreamReader(stream))
            {
                var text = reader.ReadToEnd().Trim();
                var split = text.Split('\n');
                GitCommitId = split[0];
                GitCommitNumber = split[1];
            }
        }
        public static string GitCommitId { get; }
        public static string GitCommitNumber { get; }
        public static UI.Windows.MainWindowDataContext MWContext { get; set; }

        IMainWindow IApp.MainWindow => MWContext;

        /// <summary>
        /// Displays generic error messagebox for given exception.
        /// </summary>
        /// <param name="ex">Exception to display.</param>
        public static void DisplayOperationFailed(Exception ex)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }
            else
            {
                SentrySdk.WithScope((scope) =>
                {
                    // var windowsIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
                    // var userName = windowsIdentity.Name.LastIndexOf('\\') != -1 ?
                    //                windowsIdentity.Name.Substring(windowsIdentity.Name.LastIndexOf('\\') + 1) :
                    //                windowsIdentity.Name;
                    // scope.User = new Sentry.Protocol.User
                    // {
                    //     Username = userName,
                    //     Other = new Dictionary<string, string>
                    //     {
                    //         { "OS-Language", System.Globalization.CultureInfo.InstalledUICulture.EnglishName },
                    //         { "App-Language", System.Globalization.CultureInfo.CurrentUICulture.EnglishName },
                    //         { "Full-username", windowsIdentity.Name },
                    //     }
                    // };
                    scope.User = new Sentry.Protocol.User
                    {
                        Username = Configuration.Instance.UserGuid.ToString(),
                        Other = new Dictionary<string, string>
                        {
                            { "OS-Language", System.Globalization.CultureInfo.InstalledUICulture.EnglishName },
                            { "App-Language", System.Globalization.CultureInfo.CurrentUICulture.EnglishName },
                            { "UserIdentifier", Configuration.Instance.UserIdentifier }
                        }
                    };

                    SentrySdk.CaptureException(ex);
                });
            }
            DisplayOperationFailed(ex, ex.Message);
        }

        public static void Update(UpdateHelper.DownloadInfo downloadInfo)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var dlgdc = new UI.Windows.UpdateDialogDataContext(downloadInfo);
                var dlg = new UI.Windows.UpdateDialog(dlgdc);
                dlg.ShowDialog();
                (App.Current as App).Sentry?.Dispose();
                (App.Current as App).Sentry = null;
                App.Current.Shutdown();
            });
        }

        /// <summary>
        /// Displays generic error messagebox for given exception.
        /// Will display the <paramref name="body"/> in front of the exception.
        /// </summary>
        /// <param name="ex">Exception to display.</param>
        /// <param name="body">The Text to display in front of the exception.</param>
        public static void DisplayOperationFailed(Exception ex, string body)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                UI.Windows.ErrorDialog.DataContextInstance.Add(ex);
            });
        }
        void IApp.DisplayOperationFailed(Exception ex)
        {
            DisplayOperationFailed(ex);
        }

        void IApp.DisplayOperationFailed(Exception ex, string body)
        {
            DisplayOperationFailed(ex, body);
        }

        private DateTime LastException = DateTime.MinValue;
        private int ExceptionCount = 0;
        private static readonly TimeSpan ExceptionMaxTimeSpan = new TimeSpan(0, 0, 0, 0, 100);
        private static readonly int ExceptionMaxCount = 1;
        /// <summary>
        /// https://msdn.microsoft.com/en-us/library/system.windows.application.dispatcherunhandledexception(v=vs.110).aspx
        /// </summary>
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (this.LastException + ExceptionMaxTimeSpan > DateTime.Now)
            {
                if (++this.ExceptionCount > ExceptionMaxCount)
                {
                    e.Handled = false;
                    Configuration.Save(ConfigPath);
                    this.Sentry?.Dispose();
                    this.Sentry = null;
                    App.Current.Shutdown(-1);
                }
                else
                {
                    e.Handled = true;
                    DisplayOperationFailed(e.Exception);
                }
                this.LastException = DateTime.Now;
            }
            else
            {
                this.ExceptionCount = 1;
                e.Handled = true;
                this.LastException = DateTime.Now;
                DisplayOperationFailed(e.Exception);
            }
        }
        private IDisposable Sentry = null;
        /// <summary>
        /// https://msdn.microsoft.com/en-us/library/system.windows.application.exit(v=vs.110).aspx
        /// </summary>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Configuration.Save(ConfigPath);
            this.Sentry?.Dispose();
            this.Sentry = null;
        }
        public static DateTime GetLinkerTime(System.Reflection.Assembly assembly, TimeZoneInfo target = null)
        {
            var filePath = assembly.Location;
            const int c_PeHeaderOffset = 60;
            const int c_LinkerTimestampOffset = 8;

            var buffer = new byte[2048];

            using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                stream.Read(buffer, 0, 2048);

            var offset = BitConverter.ToInt32(buffer, c_PeHeaderOffset);
            var secondsSince1970 = BitConverter.ToInt32(buffer, offset + c_LinkerTimestampOffset);
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var linkTimeUtc = epoch.AddSeconds(secondsSince1970);

            var tz = target ?? TimeZoneInfo.Local;
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(linkTimeUtc, tz);

            return localTime;
        }

        public static string GetReleaseInfos()
        {
            var builder = new StringBuilder();
            builder.Append("Arma.Studio ");
            builder.Append(GitCommitId);
            builder.Append(" (");
            builder.Append(GitCommitNumber);
            builder.Append(")");
            return builder.ToString();
        }

        public static void Restart()
        {
            Configuration.Save(ConfigPath);
            (App.Current as App).Sentry?.Dispose();
            (App.Current as App).Sentry = null;
            var info = new System.Diagnostics.ProcessStartInfo();
            info.Arguments = "/C choice /C Y /N /D Y /T 1 & START \"\" \"" + System.Reflection.Assembly.GetEntryAssembly().Location + "\"";
            info.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            info.CreateNoWindow = true;
            info.FileName = "cmd.exe";
            System.Diagnostics.Process.Start(info);
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Configuration.Load(ConfigPath);
            if (!Configuration.Instance.UserIdentificationDialogWasDisplayed)
            {
                var dlgdc = new UI.Windows.UserIdentificationDialogDataContext();
                var dlg = new UI.Windows.UserIdentificationDialog(dlgdc);
                dlg.ShowDialog();
            }


            if (!Configuration.Instance.OptOutOfReportingAndUpdates)
            {
                this.Sentry = SentrySdk.Init((sentryOptions) =>
                {
                    sentryOptions.Dsn = new Dsn("https://76551f4da2934dc5b5553184ca3ebc03@sentry.io/2670888");
                    sentryOptions.Release = GetReleaseInfos();
#if DEBUG
                    sentryOptions.Environment = $"Debug-{(System.Environment.Is64BitProcess ? "x64" : "x86")}";
#else
                    sentryOptions.Environment = $"Release-{(System.Environment.Is64BitProcess ? "x64" : "x86")}";
#endif
                    sentryOptions.SendDefaultPii = true;
                });
            }
            foreach (string dll in System.IO.Directory.GetFiles(App.ExecutablePath, "*.dll"))
            {
                PluginManager.Instance.LoadAssemblySafe(dll);
            }
            var splashDataContext = new UI.Windows.SplashScreenDataContext();
            var splash = new UI.Windows.SplashScreen(splashDataContext);
            splash.Show();
        }
    }
}
