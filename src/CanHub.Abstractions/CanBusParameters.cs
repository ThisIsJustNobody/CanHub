namespace CanHub;

/// <summary>
/// CAN 总线参数配置。包含波特率、时序和功能开关。所有时序参数以 Tq 为单位，null 时适配器根据波特率自行选择默认值。<br/>
/// CAN bus parameter configuration. Contains bitrate, timing, and feature switches.
/// All timing parameters are in Tq units; null means the adapter selects its own defaults based on the bitrate.
/// </summary>
public sealed class CanBusParameters : IEquatable<CanBusParameters>
{
    /// <summary>是否为 CAN FD 总线。<br/>Whether this is a CAN FD bus.</summary>
    public bool IsFd { get; init; }

    /// <summary>仲裁段波特率（bps）。<br/>Arbitration phase bitrate (bps).</summary>
    public int ArbitrationBitrate { get; init; }

    // CAN FD
    /// <summary>数据段波特率（bps，CAN FD 时必填）。<br/>Data phase bitrate (bps, required for CAN FD).</summary>
    public int? DataBitrate { get; init; }

    /// <summary>是否为非 ISO CAN FD 模式。<br/>Whether this is non-ISO CAN FD mode.</summary>
    public bool IsNonIsoFd { get; init; }

    // 仲裁段时序
    /// <summary>仲裁段 Tseg1（Tq）。<br/>Arbitration phase Tseg1 (Tq).</summary>
    public int? ArbitrationTseg1 { get; init; }

    /// <summary>仲裁段 Tseg2（Tq）。<br/>Arbitration phase Tseg2 (Tq).</summary>
    public int? ArbitrationTseg2 { get; init; }

    /// <summary>仲裁段 SJW（Tq）。<br/>Arbitration phase SJW (Tq).</summary>
    public int? ArbitrationSjw { get; init; }

    // 数据段时序
    /// <summary>数据段 Tseg1（Tq）。<br/>Data phase Tseg1 (Tq).</summary>
    public int? DataTseg1 { get; init; }

    /// <summary>数据段 Tseg2（Tq）。<br/>Data phase Tseg2 (Tq).</summary>
    public int? DataTseg2 { get; init; }

    /// <summary>数据段 SJW（Tq）。<br/>Data phase SJW (Tq).</summary>
    public int? DataSjw { get; init; }

    // 功能开关
    /// <summary>终端电阻是否启用。<br/>Whether termination is enabled.</summary>
    public bool? TerminationEnabled { get; init; }

    /// <summary>是否关闭 ACK。<br/>Whether ACK is turned off.</summary>
    public bool? AckOff { get; init; }

    /// <summary>是否启用自应答。<br/>Whether self-acknowledgement is enabled.</summary>
    public bool? SelfAck { get; init; }

    /// <summary>适配器不支持参数时的处理策略。<br/>Policy for handling unsupported parameters.</summary>
    public CanBusParameterPolicy UnsupportedParameterPolicy { get; init; }

    /// <summary>Classic CAN 500 kbps 标准配置。<br/>Classic CAN 500 kbps standard configuration.</summary>
    public static CanBusParameters Classic500k => new()
    {
        IsFd = false,
        ArbitrationBitrate = 500_000,
    };

    /// <summary>CAN FD 500 kbps 仲裁段 / 2 Mbps 数据段预设配置。<br/>CAN FD 500 kbps arbitration / 2 Mbps data phase preset configuration.</summary>
    public static CanBusParameters Fd500k2M => new()
    {
        IsFd = true,
        ArbitrationBitrate = 500_000,
        DataBitrate = 2_000_000,
    };

    /// <summary>
    /// 校验总线参数是否处于适配器可解释的基础范围。<br/>
    /// Validates that bus parameters are within a range the adapter can interpret.
    /// </summary>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ArbitrationBitrate);
        if (IsFd && DataBitrate is null)
            throw new ArgumentNullException(nameof(DataBitrate), "CAN FD data bitrate must be specified.");
        if (DataBitrate is <= 0)
            throw new ArgumentOutOfRangeException(nameof(DataBitrate), DataBitrate, "Data bitrate must be positive when specified.");

        ValidatePositive(ArbitrationTseg1, nameof(ArbitrationTseg1));
        ValidatePositive(ArbitrationTseg2, nameof(ArbitrationTseg2));
        ValidatePositive(ArbitrationSjw, nameof(ArbitrationSjw));
        ValidatePositive(DataTseg1, nameof(DataTseg1));
        ValidatePositive(DataTseg2, nameof(DataTseg2));
        ValidatePositive(DataSjw, nameof(DataSjw));

        if (!Enum.IsDefined(UnsupportedParameterPolicy))
            throw new ArgumentOutOfRangeException(nameof(UnsupportedParameterPolicy), UnsupportedParameterPolicy, "Unsupported parameter policy is invalid.");
    }

    /// <summary>判断两组总线参数是否相等。<br/>Determines whether two bus parameter sets are equal.</summary>
    public bool Equals(CanBusParameters? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return IsFd == other.IsFd &&
               ArbitrationBitrate == other.ArbitrationBitrate &&
               DataBitrate == other.DataBitrate &&
               IsNonIsoFd == other.IsNonIsoFd &&
               ArbitrationTseg1 == other.ArbitrationTseg1 &&
               ArbitrationTseg2 == other.ArbitrationTseg2 &&
               ArbitrationSjw == other.ArbitrationSjw &&
               DataTseg1 == other.DataTseg1 &&
               DataTseg2 == other.DataTseg2 &&
               DataSjw == other.DataSjw &&
               TerminationEnabled == other.TerminationEnabled &&
               AckOff == other.AckOff &&
               SelfAck == other.SelfAck &&
               UnsupportedParameterPolicy == other.UnsupportedParameterPolicy;
    }

    /// <inheritdoc cref="Equals(CanBusParameters?)"/>
    public override bool Equals(object? obj) => obj is CanBusParameters other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IsFd);
        hash.Add(ArbitrationBitrate);
        hash.Add(DataBitrate);
        hash.Add(IsNonIsoFd);
        hash.Add(ArbitrationTseg1);
        hash.Add(ArbitrationTseg2);
        hash.Add(ArbitrationSjw);
        hash.Add(DataTseg1);
        hash.Add(DataTseg2);
        hash.Add(DataSjw);
        hash.Add(TerminationEnabled);
        hash.Add(AckOff);
        hash.Add(SelfAck);
        hash.Add(UnsupportedParameterPolicy);
        return hash.ToHashCode();
    }

    /// <summary>判断两组总线参数是否相等。<br/>Determines whether two bus parameter sets are equal.</summary>
    public static bool operator ==(CanBusParameters? left, CanBusParameters? right) =>
        ReferenceEquals(left, right) || (left is not null && left.Equals(right));

    /// <summary>判断两组总线参数是否不等。<br/>Determines whether two bus parameter sets are not equal.</summary>
    public static bool operator !=(CanBusParameters? left, CanBusParameters? right) => !(left == right);

    private static void ValidatePositive(int? value, string paramName)
    {
        if (value is <= 0)
            throw new ArgumentOutOfRangeException(paramName, value, "CAN timing values must be positive when specified.");
    }
}
