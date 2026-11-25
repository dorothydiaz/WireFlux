using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace WireFlux
{
    internal static class Program
    {
        private const string MutexName = @"Local\WireFlux_SingleInstance_Mutex";
        private const string ShowEventName = @"Local\WireFlux_ShowWindow_Event";

        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null && args.Length >= 2 &&
                string.Equals(args[0], "--set-airplane", StringComparison.OrdinalIgnoreCase))
            {
                bool enableAirplane = string.Equals(args[1], "on", StringComparison.OrdinalIgnoreCase);
                int hr = AirplaneModeService.SetStateDirectCom(enableAirplane);
                Environment.ExitCode = (hr == 0) ? 0 : 1;
                return;
            }

            bool startInBackground = args != null && args.Any(a =>
                string.Equals(a, StartupManager.StartupArgument, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));

            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
            {
                if (!createdNew)
                {
                    if (!startInBackground)
                    {
                        showEvent.Set();
                    }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (var context = new TrayApplicationContext(startInBackground, showEvent))
                {
                    Application.Run(context);
                }
            }
        }
    }
}
