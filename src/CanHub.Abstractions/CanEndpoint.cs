using System.Collections.ObjectModel;

namespace CanHub;

/// <summary>
/// 解析后的 CAN 适配器端点，格式为 URI：scheme://device?channelIndex=0&amp;key=value。
/// Scheme 按小写规范化，Device 保留原始大小写，channelIndex 从查询参数中提取；旧 channel 参数作为兼容别名。<br/>
/// Parsed CAN adapter endpoint in the URI format: scheme://device?channelIndex=0&amp;key=value.
/// Scheme is normalized to lowercase, Device preserves original casing, channelIndex is extracted from query parameters;
/// the legacy channel parameter is accepted as a compatibility alias.
/// </summary>
public sealed class CanEndpoint : IEquatable<CanEndpoint>
{
    /// <summary>端点方案（小写规范化）。<br/>Endpoint scheme (lowercase normalized).</summary>
    public string Scheme { get; }

    /// <summary>设备标识（URI 的 host 部分）。<br/>Device identifier (the URI host portion).</summary>
    public string Device { get; }

    /// <summary>查询参数（已移除 channelIndex/channel 键）。<br/>Query parameters (channelIndex/channel keys removed).</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>通道索引（来自 ?channelIndex=N 或旧 ?channel=N，非负整数），未指定时为 null。<br/>Channel index (from ?channelIndex=N or legacy ?channel=N, non-negative integer), null when not specified.</summary>
    public int? ChannelIndex { get; }

    /// <summary>通道索引兼容别名。建议新代码使用 <see cref="ChannelIndex"/>。<br/>Compatibility alias for the channel index. New code should use <see cref="ChannelIndex"/>.</summary>
    public int? Channel => ChannelIndex;

    private CanEndpoint(string scheme, string device, IReadOnlyDictionary<string, string> parameters, int? channelIndex)
    {
        Scheme = scheme;
        Device = device;
        Parameters = parameters;
        ChannelIndex = channelIndex;
    }

    /// <summary>
    /// 创建规范化的 CAN 适配器端点。<br/>
    /// Creates a canonical CAN adapter endpoint.
    /// </summary>
    /// <param name="scheme">端点方案。<br/>Endpoint scheme.</param>
    /// <param name="device">设备标识。<br/>Device identifier.</param>
    /// <param name="channelIndex">通道索引；未指定时不写入通道查询参数。<br/>Channel index; omitted from the query when null.</param>
    /// <param name="parameters">额外查询参数；不能包含 channel 或 channelIndex。<br/>Additional query parameters; must not include channel or channelIndex.</param>
    /// <returns>规范化端点。<br/>The canonical endpoint.</returns>
    /// <exception cref="CanException">参数无效、包含保留通道参数或重复参数时抛出。<br/>Thrown when parameters are invalid, contain reserved channel keys, or duplicate keys.</exception>
    public static CanEndpoint Create(
        string scheme,
        string device,
        int? channelIndex = null,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(scheme))
            throw new CanException("*", CanErrorCategory.InvalidEndpoint, "端点方案不能为空。");
        if (string.IsNullOrWhiteSpace(device))
            throw new CanException("*", CanErrorCategory.InvalidEndpoint, "端点设备不能为空。");
        if (channelIndex is < 0)
            throw new CanException("*", CanErrorCategory.InvalidEndpoint, "通道索引必须为非负整数。");

        var copiedParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                if (string.IsNullOrWhiteSpace(key))
                    throw new CanException("*", CanErrorCategory.InvalidEndpoint, "端点查询参数名不能为空。");
                if (string.Equals(key, "channel", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "channelIndex", StringComparison.OrdinalIgnoreCase))
                {
                    throw new CanException(
                        "*",
                        CanErrorCategory.InvalidEndpoint,
                        $"端点查询参数 '{key}' 是保留参数，请使用 {nameof(channelIndex)}。");
                }
                if (!copiedParameters.TryAdd(key, value ?? string.Empty))
                    throw new CanException("*", CanErrorCategory.InvalidEndpoint, $"端点 URI 包含重复的查询参数: '{key}'");
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.Append(scheme.Trim()).Append("://").Append(device.Trim());

        var hasQuery = channelIndex.HasValue || copiedParameters.Count > 0;
        if (hasQuery)
            sb.Append('?');

        var first = true;
        if (channelIndex.HasValue)
        {
            sb.Append("channelIndex=").Append(channelIndex.Value);
            first = false;
        }

        if (copiedParameters.Count > 0)
        {
            var keys = new string[copiedParameters.Count];
            int idx = 0;
            foreach (var key in copiedParameters.Keys) keys[idx++] = key;
            Array.Sort(keys, StringComparer.OrdinalIgnoreCase);

            foreach (var key in keys)
            {
                if (!first) sb.Append('&');
                sb.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(copiedParameters[key]));
                first = false;
            }
        }

        return Parse(sb.ToString());
    }

    /// <summary>
    /// 将 URI 字符串解析为 <see cref="CanEndpoint"/>。<br/>
    /// Parses a URI string into a <see cref="CanEndpoint"/>.
    /// </summary>
    /// <param name="uri">端点 URI，格式为 scheme://device[?channelIndex=N&amp;key=value...]，兼容旧 channel 参数。<br/>Endpoint URI in the format scheme://device[?channelIndex=N&amp;key=value...], with legacy channel compatibility.</param>
    /// <returns>解析后的端点。<br/>The parsed endpoint.</returns>
    /// <exception cref="CanException">URI 为空、格式无效、设备为空、包含片段、重复参数或通道号无效时抛出。<br/>Thrown when URI is empty, has invalid format, empty device, contains a fragment, has duplicate parameters, or invalid channel number.</exception>
    public static CanEndpoint Parse(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new CanException(
                "*",
                CanErrorCategory.InvalidEndpoint,
                "端点 URI 不能为空。");
        }

        var trimmed = uri.Trim();

        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            throw new CanException(
                "*",
                CanErrorCategory.InvalidEndpoint,
                $"端点 URI 必须使用 scheme://device 格式: '{uri}'");
        }

        // 预检：片段字符不能出现在原始输入中
        if (trimmed.Contains('#'))
        {
            throw new CanException(
                "*",
                CanErrorCategory.InvalidEndpoint,
                $"端点 URI 不允许包含片段（#）: '{uri}'");
        }

        // 验证 URI 格式
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed))
        {
            throw new CanException(
                "*",
                CanErrorCategory.InvalidEndpoint,
                $"无效的端点 URI 格式: '{uri}'",
                new UriFormatException($"无法解析 URI: '{uri}'"));
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            throw new CanException(
                "*",
                CanErrorCategory.InvalidEndpoint,
                $"端点 URI 不允许包含用户信息: '{uri}'");
        }

        if (!parsed.IsDefaultPort)
        {
            throw new CanException(
                "*",
                CanErrorCategory.InvalidEndpoint,
                $"端点 URI 不允许包含端口: '{uri}'");
        }

        if (!string.IsNullOrEmpty(parsed.AbsolutePath) && parsed.AbsolutePath != "/")
        {
            throw new CanException(
                "*",
                CanErrorCategory.InvalidEndpoint,
                $"端点 URI 不允许包含路径: '{uri}'");
        }

        var scheme = parsed.Scheme.ToLowerInvariant();

        // 从原始输入中提取设备（host），保留大小写
        // Uri.Host 会小写化 host，但规范要求保留设备原始大小写
        var deviceStart = trimmed.IndexOf("://", StringComparison.Ordinal) + 3;
        var queryOrEnd = trimmed.Length;
        var qIdx = trimmed.IndexOf('?', deviceStart);
        if (qIdx >= 0) queryOrEnd = qIdx;
        var pathIdx = trimmed.IndexOf('/', deviceStart);
        if (pathIdx >= 0 && pathIdx < queryOrEnd) queryOrEnd = pathIdx;
        var device = trimmed[deviceStart..queryOrEnd];

        if (string.IsNullOrEmpty(device))
        {
            throw new CanException(
                "*",
                CanErrorCategory.InvalidEndpoint,
                $"端点 URI 设备（host）不能为空: '{uri}'");
        }

        // 从原始输入中解析查询参数（System.Uri 会去重，所以必须从原始字符串解析）
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int? channelIndex = null;

        if (qIdx >= 0)
        {
            var rawQuery = trimmed[(qIdx + 1)..];
            var pairs = rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries);

            foreach (var pair in pairs)
            {
                try
                {
                    ValidatePercentEncoding(pair);
                    var equalsIdx = pair.IndexOf('=');
                    string key;
                    string value;

                    if (equalsIdx >= 0)
                    {
                        key = Uri.UnescapeDataString(pair[..equalsIdx]);
                        value = Uri.UnescapeDataString(pair[(equalsIdx + 1)..]);
                    }
                    else
                    {
                        key = Uri.UnescapeDataString(pair);
                        value = string.Empty;
                    }

                    // 使用独立的 seenKeys 集合检测重复，因为 channel 键会从 parameters 中移除
                    if (!seenKeys.Add(key))
                    {
                        throw new CanException(
                            "*",
                            CanErrorCategory.InvalidEndpoint,
                            $"端点 URI 包含重复的查询参数: '{key}'");
                    }

                    if (string.Equals(key, "channelIndex", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "channel", StringComparison.OrdinalIgnoreCase))
                    {
                        var parsedChannelIndex = ParseChannelIndexValue(value, key);
                        if (channelIndex.HasValue && channelIndex.Value != parsedChannelIndex)
                            throw new CanException(
                                "*",
                                CanErrorCategory.InvalidEndpoint,
                                $"端点 URI 中 channel 与 channelIndex 不一致: channelIndex={channelIndex.Value}, {key}={parsedChannelIndex}");

                        channelIndex = parsedChannelIndex;
                    }
                    else
                    {
                        parameters[key] = value;
                    }
                }
                catch (UriFormatException ex)
                {
                    throw new CanException(
                        "*",
                        CanErrorCategory.InvalidEndpoint,
                        $"端点 URI 查询参数包含无效转义序列: '{pair}'",
                        ex);
                }
            }
        }

        return new CanEndpoint(scheme, device, new ReadOnlyDictionary<string, string>(parameters), channelIndex);
    }

    /// <summary>判断两个端点是否相等（结构比较，参数顺序无关）。<br/>Determines whether two endpoints are equal (structural comparison, parameter order independent).</summary>
    public bool Equals(CanEndpoint? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Scheme == other.Scheme
            && Device == other.Device
            && ChannelIndex == other.ChannelIndex
            && Parameters.Count == other.Parameters.Count
            && Parameters.All(p =>
                other.Parameters.TryGetValue(p.Key, out var otherValue)
                && p.Value == otherValue);
    }

    /// <inheritdoc cref="Equals(CanHub.CanEndpoint?)"/>
    public override bool Equals(object? obj) => Equals(obj as CanEndpoint);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Scheme);
        hash.Add(Device);
        hash.Add(ChannelIndex);

        // 排序参数键以保证哈希一致性，避免 LINQ OrderBy 分配
        if (Parameters.Count > 0)
        {
            var keys = new string[Parameters.Count];
            int i = 0;
            foreach (var key in Parameters.Keys) keys[i++] = key;
            Array.Sort(keys, StringComparer.OrdinalIgnoreCase);

            foreach (var key in keys)
            {
                hash.Add(StringComparer.OrdinalIgnoreCase.GetHashCode(key));
                hash.Add(Parameters[key]);
            }
        }

        return hash.ToHashCode();
    }

    /// <summary>判断两个端点是否相等。<br/>Determines whether two endpoints are equal.</summary>
    public static bool operator ==(CanEndpoint? left, CanEndpoint? right) => Equals(left, right);

    /// <summary>判断两个端点是否不等。<br/>Determines whether two endpoints are not equal.</summary>
    public static bool operator !=(CanEndpoint? left, CanEndpoint? right) => !Equals(left, right);

    /// <summary>返回规范化的端点 URI 字符串表示。<br/>Returns a canonical endpoint URI string representation.</summary>
    public override string ToString()
    {
        // 排序参数键以保证输出一致性，避免 LINQ OrderBy 分配
        string[]? sortedKeys = null;
        if (Parameters.Count > 0)
        {
            sortedKeys = new string[Parameters.Count];
            int idx = 0;
            foreach (var key in Parameters.Keys) sortedKeys[idx++] = key;
            Array.Sort(sortedKeys, StringComparer.OrdinalIgnoreCase);
        }

        var sb = new System.Text.StringBuilder();
        sb.Append(Scheme).Append("://").Append(Device);

        bool hasQuery = ChannelIndex.HasValue || (sortedKeys?.Length > 0);
        if (hasQuery) sb.Append('?');

        bool first = true;
        if (ChannelIndex.HasValue)
        {
            sb.Append("channelIndex=").Append(ChannelIndex.Value);
            first = false;
        }

        if (sortedKeys != null)
        {
            foreach (var key in sortedKeys)
            {
                if (!first) sb.Append('&');
                sb.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(Parameters[key]));
                first = false;
            }
        }

        return sb.ToString();
    }

    private static void ValidatePercentEncoding(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '%')
                continue;

            if (i + 2 >= value.Length ||
                !Uri.IsHexDigit(value[i + 1]) ||
                !Uri.IsHexDigit(value[i + 2]))
            {
                throw new UriFormatException("Invalid percent-encoding in endpoint query.");
            }

            i += 2;
        }
    }

    private static int ParseChannelIndexValue(string value, string key)
    {
        if (!int.TryParse(value, out var channelValue) || channelValue < 0)
        {
            throw new CanException(
                "*",
                CanErrorCategory.InvalidEndpoint,
                $"通道索引参数 '{key}' 必须为非负整数，但收到: '{value}'");
        }

        return channelValue;
    }
}
