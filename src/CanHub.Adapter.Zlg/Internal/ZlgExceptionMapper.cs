namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 异常映射工具。将原生异常分类并转换为 CanException。<br/>
/// ZLG exception mapping utility. Classifies native exceptions and converts them to CanException.
/// </summary>
internal static class ZlgExceptionMapper
{
    /// <summary>
    /// 判断异常是否属于原生边界异常（应映射为 CanException）。<br/>
    /// Determines whether an exception is a native boundary exception that should be mapped to CanException.
    /// </summary>
    public static bool IsNativeBoundaryException(Exception exception) =>
        exception is ZlgApiException
            or PlatformNotSupportedException
            or DllNotFoundException
            or BadImageFormatException
            or EntryPointNotFoundException
            or System.ComponentModel.Win32Exception;

    /// <summary>
    /// 将原生异常映射为 CanException。根据异常类型提供对应的中文错误信息。<br/>
    /// Maps a native exception to a CanException with appropriate error information based on exception type.
    /// </summary>
    public static CanException ToCanException(Exception exception)
    {
        if (exception is CanException canException)
            return canException;

        return exception switch
        {
            ZlgApiException apiException => new CanException(
                "zlg",
                CanErrorCategory.AdapterError,
                $"ZLG native call '{apiException.NativeFunction}' failed with status {apiException.Status} ({(uint)apiException.Status}).",
                apiException),
            PlatformNotSupportedException => new CanException(
                "zlg",
                CanErrorCategory.AdapterError,
                $"ZLG adapter native support is unavailable on this platform: {exception.Message}",
                exception),
            DllNotFoundException => new CanException(
                "zlg",
                CanErrorCategory.AdapterError,
                $"ZLG native driver library could not be loaded: {exception.Message}",
                exception),
            BadImageFormatException => new CanException(
                "zlg",
                CanErrorCategory.AdapterError,
                $"ZLG native driver library architecture is incompatible with the current process: {exception.Message}",
                exception),
            EntryPointNotFoundException => new CanException(
                "zlg",
                CanErrorCategory.AdapterError,
                $"ZLG native driver library is missing a required entry point: {exception.Message}",
                exception),
            _ => new CanException(
                "zlg",
                CanErrorCategory.AdapterError,
                $"ZLG adapter native operation failed: {exception.Message}",
                exception),
        };
    }
}
