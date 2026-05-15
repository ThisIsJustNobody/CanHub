namespace CanHub;

/// <summary>
/// 将发送请求提交到本地队列/驱动的结果。仅表示队列/驱动的接受状态，不代表总线级别成功。<br/>
/// Result of submitting a transmit request to the local queue/driver.
/// Represents queue/driver acceptance only, not bus-level success.
/// </summary>
public readonly struct CanTransmitSubmissionResult : IEquatable<CanTransmitSubmissionResult>
{
    /// <summary>与请求匹配的关联标识。<br/>Correlation identifier matching the request.</summary>
    public ulong CorrelationId { get; }

    /// <summary>提交状态。<br/>Submission status.</summary>
    public CanTransmitSubmissionStatus Status { get; }

    /// <summary>驱动返回的原始状态码。<br/>Native status code from the driver.</summary>
    public uint NativeStatusCode { get; }

    /// <summary>驱动返回的原始错误码。<br/>Native error code from the driver.</summary>
    public uint NativeErrorCode { get; }

    /// <summary>请求是否被接受。<br/>Whether the request was accepted.</summary>
    public bool Accepted => Status == CanTransmitSubmissionStatus.Accepted;

    private CanTransmitSubmissionResult(
        ulong correlationId, CanTransmitSubmissionStatus status,
        uint nativeStatusCode, uint nativeErrorCode)
    {
        CorrelationId = correlationId;
        Status = status;
        NativeStatusCode = nativeStatusCode;
        NativeErrorCode = nativeErrorCode;
    }

    /// <summary>创建接受成功的结果。<br/>Create an accepted result.</summary>
    public static CanTransmitSubmissionResult AcceptedResult(
        ulong correlationId,
        uint nativeStatusCode = 0,
        uint nativeErrorCode = 0) =>
        new(correlationId, CanTransmitSubmissionStatus.Accepted, nativeStatusCode, nativeErrorCode);

    /// <summary>
    /// 创建失败结果。status 不能是 None 或 Accepted，否则抛出 ArgumentOutOfRangeException。<br/>
    /// Create a failed result. status must not be None or Accepted, otherwise throws ArgumentOutOfRangeException.
    /// </summary>
    public static CanTransmitSubmissionResult Failed(
        ulong correlationId,
        CanTransmitSubmissionStatus status,
        uint nativeStatusCode = 0,
        uint nativeErrorCode = 0)
    {
        if (status is CanTransmitSubmissionStatus.None or CanTransmitSubmissionStatus.Accepted)
            throw new ArgumentOutOfRangeException(nameof(status), status,
                "Use AcceptedResult for accepted submissions, and do not create failed results with None status.");
        return new(correlationId, status, nativeStatusCode, nativeErrorCode);
    }

    /// <summary>判断两个提交结果是否相等。<br/>Determines whether two submission results are equal.</summary>
    public bool Equals(CanTransmitSubmissionResult other) =>
        CorrelationId == other.CorrelationId &&
        Status == other.Status &&
        NativeStatusCode == other.NativeStatusCode &&
        NativeErrorCode == other.NativeErrorCode;

    /// <inheritdoc cref="Equals(CanTransmitSubmissionResult)"/>
    public override bool Equals(object? obj) => obj is CanTransmitSubmissionResult other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CorrelationId);
        hash.Add(Status);
        hash.Add(NativeStatusCode);
        hash.Add(NativeErrorCode);
        return hash.ToHashCode();
    }

    /// <summary>判断两个提交结果是否相等。<br/>Determines whether two submission results are equal.</summary>
    public static bool operator ==(CanTransmitSubmissionResult left, CanTransmitSubmissionResult right) => left.Equals(right);

    /// <summary>判断两个提交结果是否不等。<br/>Determines whether two submission results are not equal.</summary>
    public static bool operator !=(CanTransmitSubmissionResult left, CanTransmitSubmissionResult right) => !left.Equals(right);
}
