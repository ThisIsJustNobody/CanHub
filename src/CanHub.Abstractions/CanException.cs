namespace CanHub;

/// <summary>
/// CAN Hub 操作中发生的异常，携带适配器上下文和错误分类信息。<br/>
/// Exception occurring during CanHub operations, carrying adapter context and error classification information.
/// </summary>
public sealed class CanException : Exception
{
    /// <summary>发生错误的适配器标识符。<br/>The adapter identifier where the error occurred.</summary>
    public string AdapterId { get; }

    /// <summary>错误分类。<br/>Error category.</summary>
    public CanErrorCategory Category { get; }

    /// <summary>
    /// 关联的端点（如果错误与特定端点有关）。<br/>
    /// The associated endpoint (if the error is related to a specific endpoint).
    /// </summary>
    public CanEndpoint? Endpoint { get; }

    /// <summary>触发错误的底层驱动函数名（如果有）。<br/>The underlying driver function name that triggered the error (if any).</summary>
    public string? NativeFunction { get; }

    /// <summary>硬件/驱动供应商自定义错误码（如果有）。<br/>Hardware/driver vendor custom error code (if any).</summary>
    public int? VendorCode { get; }

    /// <summary>错误的可恢复级别。<br/>Error recoverability level.</summary>
    public CanRecoverability Recoverability { get; }

    /// <summary>
    /// 创建一个不包装内部异常的 CanException，使用基于 category 的默认消息。<br/>
    /// Creates a CanException without an inner exception, using a default message derived from the category.
    /// </summary>
    public CanException(
        string adapterId,
        CanErrorCategory category,
        CanEndpoint? endpoint = null,
        string? nativeFunction = null,
        int? vendorCode = null,
        CanRecoverability recoverability = CanRecoverability.Fatal)
        : this(adapterId, category, $"[{category}] Adapter '{adapterId}': {category}.",
              null, endpoint, nativeFunction, vendorCode, recoverability)
    {
    }

    /// <summary>
    /// 创建一个带自定义消息的 CanException。<br/>
    /// Creates a CanException with a custom message.
    /// </summary>
    public CanException(
        string adapterId,
        CanErrorCategory category,
        string message)
        : this(adapterId, category, message, null, null, null, null, CanRecoverability.Fatal)
    {
    }

    /// <summary>
    /// 创建一个包装内部异常的 CanException。<br/>
    /// Creates a CanException that wraps an inner exception.
    /// </summary>
    public CanException(
        string adapterId,
        CanErrorCategory category,
        string message,
        Exception innerException)
        : this(adapterId, category, message, innerException, null, null, null, CanRecoverability.Fatal)
    {
    }

    private CanException(
        string adapterId,
        CanErrorCategory category,
        string message,
        Exception? innerException,
        CanEndpoint? endpoint,
        string? nativeFunction,
        int? vendorCode,
        CanRecoverability recoverability)
        : base(message, innerException)
    {
        AdapterId = adapterId;
        Category = category;
        Endpoint = endpoint;
        NativeFunction = nativeFunction;
        VendorCode = vendorCode;
        Recoverability = recoverability;
    }
}
