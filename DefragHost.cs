namespace DiskDefrag;

/// <summary>Entry points for hosting the defrag tool inside PC Medic.</summary>
public static class DefragHost
{
    /// <summary>Creates the tool as a child control with no window chrome of its own.</summary>
    public static Form CreateEmbedded()
    {
        var form = new MainForm();
        form.Embed();
        return form;
    }

    /// <summary>True while the hosted tool is running an operation.</summary>
    public static bool IsWorking(Form host) => host is MainForm form && form.IsWorking;
}
