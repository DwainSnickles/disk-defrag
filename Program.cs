namespace DiskDefrag;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (!IsAdministrator())
        {
            MessageBox.Show(
                "Disk Defrag requires administrator access. Approve the Windows security prompt to continue.",
                "Administrator access required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
