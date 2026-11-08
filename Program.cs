namespace DiskDefrag;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            Application.SetColorMode(SystemColorMode.Dark);
        }
        catch
        {
            // No dark title bar on this window manager; the app itself is dark regardless.
        }

        Application.ThreadException += (_, e) => MessageBox.Show(e.Exception.Message, "Disk Defrag");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) MessageBox.Show(ex.Message, "Disk Defrag");
        };

        Application.Run(new DefragForm());
    }
}
