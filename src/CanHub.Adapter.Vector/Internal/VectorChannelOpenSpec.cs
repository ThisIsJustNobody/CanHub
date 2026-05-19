namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// Vector 通道打开所需的完整配置快照。用于初次打开和自动恢复重开。<br/>
/// Complete Vector channel open configuration snapshot used for initial open and automatic recovery reopen.
/// </summary>
internal sealed record VectorChannelOpenSpec(CanOpenContext Context);
