namespace CanHub;

/// <summary>
/// 适配器不支持的总线参数处理策略。<br/>
/// Bus parameter handling strategy when unsupported by the adapter.
/// </summary>
public enum CanBusParameterPolicy
{
    /// <summary>默认：不支持时通过状态事件提示。<br/>Default: notify via status event when unsupported.</summary>
    Request = 0,

    /// <summary>不支持时抛出 CanException。<br/>Throw CanException when unsupported.</summary>
    Require = 1,
}
