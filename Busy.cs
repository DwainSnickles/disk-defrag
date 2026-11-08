namespace DiskDefrag;

/// <summary>Shows the wait cursor while the volume bitmap is read from the disk.</summary>
internal sealed class BusyScope : IDisposable
{
    private readonly Form? _form;
    private readonly Cursor _previous;

    public BusyScope(Control? anchor)
    {
        _form = anchor?.FindForm();
        _previous = Cursor.Current ?? Cursors.Default;

        if (_form is not null && !_form.IsDisposed) _form.UseWaitCursor = true;
        Cursor.Current = Cursors.WaitCursor;
    }

    public void Dispose()
    {
        if (_form is not null && !_form.IsDisposed) _form.UseWaitCursor = false;
        Cursor.Current = _previous;
    }
}

internal static class Busy
{
    /// <summary>Hold the returned scope for as long as the slow work runs.</summary>
    public static IDisposable Enter(Control? anchor) => new BusyScope(anchor);
}
