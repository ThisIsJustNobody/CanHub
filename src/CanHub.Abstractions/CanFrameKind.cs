namespace CanHub;

/// <summary>
/// CAN 帧类型（互斥）。<br/>
/// CAN frame type (mutual exclusive).
/// </summary>
public enum CanFrameKind : byte
{
    /// <summary>未指定帧类型；默认/未初始化帧。<br/>No frame kind specified; default/uninitialized frame.</summary>
    None = 0,

    /// <summary>数据帧。<br/>Data frame.</summary>
    Data = 1,

    /// <summary>远程帧（请求数据）。<br/>Remote frame (request data).</summary>
    Remote = 2,

    /// <summary>错误帧。<br/>Error frame.</summary>
    Error = 3,

    /// <summary>过载帧。<br/>Overload frame.</summary>
    Overload = 4
}
