using vxlapi_NET;

namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// Vector 驱动级生命周期管理。使用引用计数，多通道共享同一驱动实例。<br/>
/// Vector driver-level lifecycle management. Uses reference counting so multiple channels
/// share the same driver instance.
/// </summary>
internal sealed class VectorDriver : IAsyncDisposable
{
    private static readonly XLDriver s_driver = new();
    private static readonly object s_gate = new();
    private static Func<XLDefine.XL_Status> s_openDriver = s_driver.XL_OpenDriver;
    private static Func<XLDefine.XL_Status> s_closeDriver = s_driver.XL_CloseDriver;
    private static Func<XLDefine.XL_Status, string> s_getErrorString = s_driver.XL_GetErrorString;
    private static int s_referenceCount;
    private static bool s_isOpen;

    /// <summary>
    /// 共享的 Vector XL Driver 实例。<br/>
    /// The shared Vector XL Driver instance.
    /// </summary>
    public static XLDriver Driver => s_driver;

    internal static IDisposable UseLifecycleHooksForTesting(
        Func<XLDefine.XL_Status> openDriver,
        Func<XLDefine.XL_Status>? closeDriver = null,
        Func<XLDefine.XL_Status, string>? getErrorString = null)
    {
        ArgumentNullException.ThrowIfNull(openDriver);

        lock (s_gate)
        {
            if (s_referenceCount != 0 || s_isOpen)
                throw new InvalidOperationException("Vector driver test hooks require an idle driver.");

            var previousOpenDriver = s_openDriver;
            var previousCloseDriver = s_closeDriver;
            var previousGetErrorString = s_getErrorString;

            s_openDriver = openDriver;
            s_closeDriver = closeDriver ?? s_driver.XL_CloseDriver;
            s_getErrorString = getErrorString ?? s_driver.XL_GetErrorString;

            return new LifecycleHookScope(previousOpenDriver, previousCloseDriver, previousGetErrorString);
        }
    }

    /// <summary>
    /// 驱动是否已打开。<br/>
    /// Whether the driver is currently open.
    /// </summary>
    public bool IsOpen
    {
        get
        {
            lock (s_gate)
            {
                return s_isOpen;
            }
        }
    }

    /// <summary>
    /// 获取驱动引用。首个调用者打开驱动，后续调用仅增加引用计数。<br/>
    /// Acquires a driver reference. The first caller opens the driver; subsequent
    /// callers only increment the reference count.
    /// </summary>
    public ValueTask AcquireAsync()
    {
        lock (s_gate)
        {
            s_referenceCount++;
            if (s_referenceCount == 1)
            {
                XLDefine.XL_Status status;
                try
                {
                    status = s_openDriver();
                }
                catch
                {
                    s_referenceCount--;
                    s_isOpen = false;
                    throw;
                }

                if (status != XLDefine.XL_Status.XL_SUCCESS)
                {
                    s_referenceCount--;
                    var errorString = s_getErrorString(status);
                    throw new CanException("vector", CanErrorCategory.AdapterError,
                        nativeFunction: $"XL_OpenDriver(error={errorString})",
                        vendorCode: (int)status);
                }

                s_isOpen = true;
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 释放驱动引用。最后一个调用者关闭驱动。<br/>
    /// Releases a driver reference. The last caller closes the driver.
    /// </summary>
    public void Release()
    {
        lock (s_gate)
        {
            if (s_referenceCount == 0)
                return;

            s_referenceCount--;
            if (s_referenceCount == 0 && s_isOpen)
            {
                s_closeDriver();
                s_isOpen = false;
            }
        }
    }

    /// <summary>
    /// 异步释放驱动引用。<br/>
    /// Asynchronously releases a driver reference.
    /// </summary>
    public ValueTask ReleaseAsync()
    {
        Release();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ReleaseAsync();

    private sealed class LifecycleHookScope(
        Func<XLDefine.XL_Status> previousOpenDriver,
        Func<XLDefine.XL_Status> previousCloseDriver,
        Func<XLDefine.XL_Status, string> previousGetErrorString) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            lock (s_gate)
            {
                if (_disposed)
                    return;

                s_referenceCount = 0;
                s_isOpen = false;
                s_openDriver = previousOpenDriver;
                s_closeDriver = previousCloseDriver;
                s_getErrorString = previousGetErrorString;
                _disposed = true;
            }
        }
    }
}
