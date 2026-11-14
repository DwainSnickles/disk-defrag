using System.Runtime.InteropServices;

namespace DiskDefrag;

internal sealed class KeepAwakeLease : IDisposable
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;
    private bool _disposed;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint executionState);

    private KeepAwakeLease() { }

    public static KeepAwakeLease? TryAcquire()
    {
        uint state = SetThreadExecutionState(EsContinuous | EsSystemRequired | EsDisplayRequired);
        return state == 0 ? null : new KeepAwakeLease();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SetThreadExecutionState(EsContinuous);
    }
}
