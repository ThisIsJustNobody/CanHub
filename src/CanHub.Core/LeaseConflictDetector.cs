using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanHub.Core;

/// <summary>
/// 租约冲突检测器。使用 SHA256 指纹比较检测配置冲突，防止同一设备被多个进程重复打开。<br/>
/// Lease conflict detector. Uses SHA256 fingerprint comparison to detect configuration conflicts and prevent the same device from being opened by multiple processes.
/// </summary>
public static class LeaseConflictDetector
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    /// 计算配置指纹。用于比较两个 CAN 打开请求是否针对同一设备使用相同参数。<br/>
    /// Computes a configuration fingerprint for comparing whether two CAN open requests target the same device with identical parameters.
    /// </summary>
    /// <remarks>
    /// 指纹由三部分组成：<br/>
    /// (1) 定位器信息（scheme + device + channel），<br/>
    /// (2) CAN 总线参数（比特率、采样点等），<br/>
    /// (3) 原生选项（适配器特定配置）。<br/>
    /// 三部分分别哈希后组合，再次 SHA256 哈希生成最终指纹。<br/>
    /// The fingerprint consists of three parts:<br/>
    /// (1) locator info (scheme + device + channel),<br/>
    /// (2) CAN bus parameters (bitrate, sample points, etc.),<br/>
    /// (3) native options (adapter-specific configuration).<br/>
    /// Each part is hashed separately, combined, then SHA256-hashed again to produce the final fingerprint.
    /// </remarks>
    public static byte[] ComputeFingerprint(
        CanEndpoint endpoint,
        CanOpenOptions options,
        string? canonicalLocator = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(options);
        options.BusParameters.Validate();

        // (1) 定位信息：scheme + device + channel（不受旧总线 query 参数影响）
        var locator = canonicalLocator ?? $"{endpoint.Scheme}://{endpoint.Device}?channel={endpoint.Channel ?? 0}";
        var locatorBytes = Encoding.UTF8.GetBytes(locator);
        var hash1 = SHA256.HashData(locatorBytes);

        // (2) CanBusParameters 规范化序列化
        var busParamsBytes = SerializeBusParameters(options.BusParameters);
        var hash2 = SHA256.HashData(busParamsBytes);

        // (3) NativeOptions
        var nativeBytes = SerializeNativeOptions(options.NativeOptions);
        var hash3 = SHA256.HashData(nativeBytes);

        var combined = new byte[hash1.Length + hash2.Length + hash3.Length];
        Buffer.BlockCopy(hash1, 0, combined, 0, hash1.Length);
        Buffer.BlockCopy(hash2, 0, combined, hash1.Length, hash2.Length);
        Buffer.BlockCopy(hash3, 0, combined, hash1.Length + hash2.Length, hash3.Length);
        return SHA256.HashData(combined);
    }

    /// <summary>
    /// 使用恒定时间比较两个指纹是否匹配，防止时序攻击。<br/>
    /// Compares two fingerprints using constant-time comparison to prevent timing attacks.
    /// </summary>
    public static bool FingerprintsMatch(byte[] existing, byte[] candidate)
    {
        return CryptographicOperations.FixedTimeEquals(existing, candidate);
    }

    private static byte[] SerializeBusParameters(CanBusParameters bp)
    {
        var sb = new StringBuilder();
        sb.Append(bp.IsFd); sb.Append('|');
        sb.Append(bp.ArbitrationBitrate); sb.Append('|');
        sb.Append(bp.DataBitrate); sb.Append('|');
        sb.Append(bp.IsNonIsoFd); sb.Append('|');
        sb.Append(bp.ArbitrationTseg1); sb.Append('|');
        sb.Append(bp.ArbitrationTseg2); sb.Append('|');
        sb.Append(bp.ArbitrationSjw); sb.Append('|');
        sb.Append(bp.DataTseg1); sb.Append('|');
        sb.Append(bp.DataTseg2); sb.Append('|');
        sb.Append(bp.DataSjw); sb.Append('|');
        sb.Append(bp.TerminationEnabled); sb.Append('|');
        sb.Append(bp.AckOff); sb.Append('|');
        sb.Append(bp.SelfAck); sb.Append('|');
        sb.Append(bp.UnsupportedParameterPolicy);
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] SerializeNativeOptions(object? nativeOptions)
    {
        if (nativeOptions is null)
            return Array.Empty<byte>();

        var typeName = nativeOptions.GetType().AssemblyQualifiedName ?? nativeOptions.GetType().FullName ?? nativeOptions.GetType().Name;
        string payload = nativeOptions switch
        {
            ICanNativeOptionsFingerprint fingerprint => fingerprint.GetFingerprint(),
            string text => text,
            IReadOnlyDictionary<string, string> dictionary => SerializeStringDictionary(dictionary),
            IEnumerable<KeyValuePair<string, string>> pairs => SerializeStringPairs(pairs),
            _ => JsonSerializer.Serialize(nativeOptions, nativeOptions.GetType(), s_jsonOptions),
        };

        return Encoding.UTF8.GetBytes($"{typeName}|{payload}");
    }

    private static string SerializeStringDictionary(IReadOnlyDictionary<string, string> dictionary) =>
        SerializeStringPairs(dictionary);

    private static string SerializeStringPairs(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var sb = new StringBuilder();
        foreach (var pair in pairs.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(Uri.EscapeDataString(pair.Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(pair.Value));
            sb.Append(';');
        }
        return sb.ToString();
    }
}
