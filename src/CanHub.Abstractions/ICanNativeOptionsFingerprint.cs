namespace CanHub;

/// <summary>
/// 为适配器原生选项提供稳定的租约指纹文本。<br/>
/// Provides stable lease fingerprint text for adapter native options.
/// </summary>
public interface ICanNativeOptionsFingerprint
{
    /// <summary>
    /// 返回稳定、确定性的配置文本，用于租约冲突检测。<br/>
    /// Returns stable, deterministic configuration text for lease conflict detection.
    /// </summary>
    string GetFingerprint();
}
