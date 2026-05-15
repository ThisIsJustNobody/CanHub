namespace CanHub;

/// <summary>
/// 打开 CAN 通道的配置选项。包含总线参数和原生驱动特有选项。<br/>
/// Configuration options for opening a CAN channel. Contains bus parameters and native driver-specific options.
/// </summary>
public sealed class CanOpenOptions
{
    private CanBusParameters _busParameters = CanBusParameters.Classic500k;

    /// <summary>总线参数配置（默认 Classic500k）。<br/>Bus parameter configuration (defaults to Classic500k).</summary>
    public CanBusParameters BusParameters
    {
        get => _busParameters;
        set => _busParameters = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>原生驱动特有选项（传递给底层驱动）。<br/>Native driver-specific options (passed through to the underlying driver).</summary>
    public object? NativeOptions { get; set; }
}
