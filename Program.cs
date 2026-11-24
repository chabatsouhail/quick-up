using System.IO.Pipes;
using System.Text;

namespace QuickUp;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        bool startHidden = IsStartupHiddenLaunch(args);

        using var singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: TrayAppContext.SingleInstanceMutexName,
            createdNew: out bool isFirstInstance);

        if (!isFirstInstance)
        {
            if (!startHidden)
            {
                NotifyPrimaryInstanceToShow();
            }
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
#pragma warning disable WFO5001
        Application.SetColorMode(SystemColorMode.System);
#pragma warning restore WFO5001

        using var appContext = new TrayAppContext(startHidden);
        Application.Run(appContext);
    }

    private static bool IsStartupHiddenLaunch(string[] args)
    {
        foreach (string arg in args)
        {
            if (arg.Equals("--hidden", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--background", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--startup", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/hidden", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/background", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/startup", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static void NotifyPrimaryInstanceToShow()
    {
        try
        {
            using var client = new NamedPipeClientStream(
                serverName: ".",
                pipeName: TrayAppContext.IpcPipeName,
                direction: PipeDirection.Out);

            client.Connect(timeout: 1000);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine("SHOW");
        }
        catch
        {
            // Ignore if primary instance is shutting down or unreachable.
        }
    }
}
