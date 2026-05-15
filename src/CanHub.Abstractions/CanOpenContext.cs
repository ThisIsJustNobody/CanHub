namespace CanHub;

/// <summary>
/// 打开 CAN 通道的上下文，包含解析后的端点和完整配置选项。构造时自动验证总线参数。<br/>
/// Context for opening a CAN channel, containing the parsed endpoint and full configuration options. Bus parameters are validated on construction.
/// </summary>
public sealed class CanOpenContext
{
    /// <summary>解析后的端点。<br/>The parsed endpoint.</summary>
    public CanEndpoint Endpoint { get; }

    /// <summary>完整的打开配置选项。<br/>The complete open configuration options.</summary>
    public CanOpenOptions Options { get; }

    /// <summary>创建打开上下文。构造时自动调用 BusParameters.Validate()。<br/>Creates an open context. Automatically calls BusParameters.Validate() on construction.</summary>
    /// <param name="endpoint">端点信息。<br/>The endpoint information.</param>
    /// <param name="options">打开配置选项。<br/>The open configuration options.</param>
    public CanOpenContext(CanEndpoint endpoint, CanOpenOptions options)
    {
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Options.BusParameters.Validate();
    }
}
