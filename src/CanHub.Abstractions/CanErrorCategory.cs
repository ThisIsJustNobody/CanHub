namespace CanHub;

/// <summary>
/// CAN 适配器错误分类，用于区分错误的性质和处理策略。<br/>
/// CAN adapter error classification, used to distinguish error nature and handling strategy.
/// </summary>
public enum CanErrorCategory
{
    /// <summary>无特定错误分类。<br/>No specific error classification.</summary>
    None = 0,

    /// <summary>找不到匹配的适配器（scheme 无注册、硬件未连接等）。<br/>No matching adapter found (scheme not registered, hardware not connected, etc.).</summary>
    AdapterNotFound = 1,

    /// <summary>端点 URI 无效或无法解析。<br/>Endpoint URI is invalid or cannot be parsed.</summary>
    InvalidEndpoint = 2,

    /// <summary>同一 scheme 被多个适配器重复注册。<br/>The same scheme is registered by multiple adapters.</summary>
    DuplicateAdapterScheme = 3,

    /// <summary>适配器自身运行时错误（驱动超时、硬件故障等）。<br/>Adapter runtime error (driver timeout, hardware fault, etc.).</summary>
    AdapterError = 4,

    /// <summary>配置冲突（同设备+通道，不同配置指纹）。<br/>Configuration conflict (same device + channel, different configuration fingerprint).</summary>
    ConfigurationConflict = 5
}
