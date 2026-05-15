namespace CanHub;

/// <summary>
/// 设备级租约接口。适配器实现此接口管理设备句柄的生命周期。<br/>
/// Device-level lease interface. Adapters implement this interface to manage
/// the lifecycle of device handles.
/// </summary>
public interface IDeviceLease : IAsyncDisposable
{
    /// <summary>设备 ID。<br/>Device ID.</summary>
    string DeviceId { get; }

    /// <summary>设备是否已打开。<br/>Whether the device is open.</summary>
    bool IsOpen { get; }

    /// <summary>打开设备。<br/>Opens the device.</summary>
    ValueTask OpenAsync(CanOpenContext context, CancellationToken ct = default);
}
