using System.Globalization;

namespace CanHub.Trace.VectorAsc;

/// <summary>
/// Vector ASC 文件读取器。<br/>
/// Reader for Vector ASC files.
/// </summary>
public static class VectorAscReader
{
    private const DateTimeStyles DateParseStyles = DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces;

    private static readonly string[] DateFormats =
    [
        "ddd MMM d H:m:ss.FFFFFFF yyyy",
        "ddd MMM d H:m:ss yyyy",
        "ddd MMM d h:m:ss.FFFFFFF tt yyyy",
        "ddd MMM d h:m:ss tt yyyy",
        "ddd MMM dd HH:mm:ss.fff yyyy",
        "ddd MMM dd HH:mm:ss yyyy",
        "ddd MMM dd hh:mm:ss.fff tt yyyy",
        "ddd MMM dd hh:mm:ss tt yyyy",
        "ddd MMM dd HH:mm:ss.fff tt yyyy",
        "ddd MMM dd HH:mm:ss tt yyyy"
    ];

    /// <summary>
    /// 从字符串读取 ASC 内容。<br/>
    /// Reads ASC content from a string.
    /// </summary>
    public static VectorAscFile ReadText(string text, VectorAscReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var reader = new StringReader(text);
        return Read(reader, options);
    }

    /// <summary>
    /// 从文件读取 ASC 内容。<br/>
    /// Reads ASC content from a file.
    /// </summary>
    public static VectorAscFile ReadFile(string path, VectorAscReadOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var reader = File.OpenText(path);
        return Read(reader, options);
    }

    /// <summary>
    /// 从文件流式读取 ASC 帧。诊断通过 <see cref="VectorAscReadOptions.DiagnosticSink"/> 上报。<br/>
    /// Streams ASC frames from a file. Diagnostics are reported through <see cref="VectorAscReadOptions.DiagnosticSink"/>.
    /// </summary>
    public static IEnumerable<VectorAscFrame> ReadFileFrames(string path, VectorAscReadOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ReadFileFramesIterator(path, options);
    }

    /// <summary>
    /// 从文本读取器流式读取 ASC 帧。诊断通过 <see cref="VectorAscReadOptions.DiagnosticSink"/> 上报。<br/>
    /// Streams ASC frames from a text reader. Diagnostics are reported through <see cref="VectorAscReadOptions.DiagnosticSink"/>.
    /// </summary>
    public static IEnumerable<VectorAscFrame> ReadFrames(TextReader reader, VectorAscReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadFramesIterator(reader, options ?? new VectorAscReadOptions());
    }

    /// <summary>
    /// 从文本读取器读取 ASC 内容。<br/>
    /// Reads ASC content from a text reader.
    /// </summary>
    public static VectorAscFile Read(TextReader reader, VectorAscReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        options ??= new VectorAscReadOptions();

        var numericBase = VectorAscNumericBase.Hex;
        var timestampFormat = VectorAscTimestampFormat.Absolute;
        var internalEventsLogged = false;
        string? rawDateText = null;
        DateTimeOffset? fileDate = null;
        string? rawTriggerBlockText = null;
        DateTimeOffset? triggerBlockStart = null;
        var frames = new List<VectorAscFrame>();
        var diagnostics = new List<VectorAscDiagnostic>();

        var lineNumber = 0;
        var lastTimestamp = TimeSpan.Zero;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith("date ", StringComparison.OrdinalIgnoreCase))
            {
                rawDateText = trimmed[5..].Trim();
                fileDate = ParseDateOrReport(rawDateText, lineNumber, options, diagnostics);
                continue;
            }

            if (trimmed.StartsWith("base ", StringComparison.OrdinalIgnoreCase))
            {
                ParseBaseLine(trimmed, ref numericBase, ref timestampFormat);
                continue;
            }

            if (trimmed.Equals("internal events logged", StringComparison.OrdinalIgnoreCase))
            {
                internalEventsLogged = true;
                continue;
            }

            if (trimmed.Equals("no internal events logged", StringComparison.OrdinalIgnoreCase))
            {
                internalEventsLogged = false;
                continue;
            }

            if (StartsWithTriggerBlock(trimmed))
            {
                rawTriggerBlockText = ExtractTriggerBlockText(trimmed);
                if (!string.IsNullOrWhiteSpace(rawTriggerBlockText))
                {
                    triggerBlockStart = ParseDateOrReport(rawTriggerBlockText, lineNumber, options, diagnostics);
                }

                continue;
            }

            if (trimmed.Equals("End TriggerBlock", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsStartOfMeasurement(trimmed))
            {
                continue;
            }

            var diagnosticCountBeforeFrame = diagnostics.Count;
            if (TryParseFrameLine(
                    trimmed,
                    lineNumber,
                    numericBase,
                    timestampFormat,
                    ref lastTimestamp,
                    options,
                    diagnostics,
                    out var frame))
            {
                frames.Add(frame);
                continue;
            }

            if (diagnostics.Count == diagnosticCountBeforeFrame)
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.UnsupportedLine,
                    $"Unsupported ASC line: {trimmed}",
                    strictOnly: false);
            }
        }

        return new VectorAscFile(
            numericBase,
            timestampFormat,
            internalEventsLogged,
            rawDateText,
            fileDate,
            rawTriggerBlockText,
            triggerBlockStart,
            frames,
            diagnostics);
    }

    private static IEnumerable<VectorAscFrame> ReadFileFramesIterator(string path, VectorAscReadOptions? options)
    {
        using var reader = File.OpenText(path);
        foreach (var frame in ReadFrames(reader, options))
        {
            yield return frame;
        }
    }

    private static IEnumerable<VectorAscFrame> ReadFramesIterator(TextReader reader, VectorAscReadOptions options)
    {
        var numericBase = VectorAscNumericBase.Hex;
        var timestampFormat = VectorAscTimestampFormat.Absolute;
        var diagnostics = new List<VectorAscDiagnostic>();

        var lineNumber = 0;
        var lastTimestamp = TimeSpan.Zero;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            diagnostics.Clear();

            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith("date ", StringComparison.OrdinalIgnoreCase))
            {
                _ = ParseDateOrReport(trimmed[5..].Trim(), lineNumber, options, diagnostics);
                continue;
            }

            if (trimmed.StartsWith("base ", StringComparison.OrdinalIgnoreCase))
            {
                ParseBaseLine(trimmed, ref numericBase, ref timestampFormat);
                continue;
            }

            if (trimmed.Equals("internal events logged", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("no internal events logged", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("End TriggerBlock", StringComparison.OrdinalIgnoreCase) ||
                IsStartOfMeasurement(trimmed))
            {
                continue;
            }

            if (StartsWithTriggerBlock(trimmed))
            {
                var rawTriggerBlockText = ExtractTriggerBlockText(trimmed);
                if (!string.IsNullOrWhiteSpace(rawTriggerBlockText))
                {
                    _ = ParseDateOrReport(rawTriggerBlockText, lineNumber, options, diagnostics);
                }

                continue;
            }

            var diagnosticCountBeforeFrame = diagnostics.Count;
            if (TryParseFrameLine(
                    trimmed,
                    lineNumber,
                    numericBase,
                    timestampFormat,
                    ref lastTimestamp,
                    options,
                    diagnostics,
                    out var frame))
            {
                yield return frame;
                continue;
            }

            if (diagnostics.Count == diagnosticCountBeforeFrame)
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.UnsupportedLine,
                    $"Unsupported ASC line: {trimmed}",
                    strictOnly: false);
            }
        }
    }

    private static void ParseBaseLine(
        string trimmed,
        ref VectorAscNumericBase numericBase,
        ref VectorAscTimestampFormat timestampFormat)
    {
        var tokens = SplitTokens(trimmed);
        if (tokens.Length >= 2)
        {
            numericBase = tokens[1].Equals("dec", StringComparison.OrdinalIgnoreCase)
                ? VectorAscNumericBase.Decimal
                : VectorAscNumericBase.Hex;
        }

        for (var i = 2; i + 1 < tokens.Length; i++)
        {
            if (tokens[i].Equals("timestamps", StringComparison.OrdinalIgnoreCase))
            {
                timestampFormat = tokens[i + 1].Equals("relative", StringComparison.OrdinalIgnoreCase)
                    ? VectorAscTimestampFormat.Relative
                    : VectorAscTimestampFormat.Absolute;
                return;
            }
        }
    }

    private static DateTimeOffset? ParseDateOrReport(
        string text,
        int lineNumber,
        VectorAscReadOptions options,
        List<VectorAscDiagnostic> diagnostics)
    {
        if (TryParseDate(text, out var parsed))
        {
            return parsed;
        }

        var diagnostic = new VectorAscDiagnostic(
            lineNumber,
            VectorAscDiagnosticCodes.UnparseableDate,
            $"Unable to parse ASC date '{text}'.");

        if (options.Strict)
        {
            throw new FormatException(diagnostic.Message);
        }

        diagnostics.Add(diagnostic);
        options.DiagnosticSink?.Invoke(diagnostic);
        return null;
    }

    private static bool TryParseDate(string text, out DateTimeOffset value)
    {
        if (TryParseChineseVectorDate(text, out value))
        {
            return true;
        }

        foreach (var candidate in GetDateCandidates(text))
        {
            if (DateTimeOffset.TryParseExact(
                    candidate,
                    DateFormats,
                    CultureInfo.InvariantCulture,
                    DateParseStyles,
                    out value))
            {
                return true;
            }

            if (DateTimeOffset.TryParse(
                    candidate,
                    CultureInfo.InvariantCulture,
                    DateParseStyles,
                    out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static IEnumerable<string> GetDateCandidates(string text)
    {
        yield return text;

        var normalized = NormalizeDateText(text);
        if (!normalized.Equals(text, StringComparison.Ordinal))
        {
            yield return normalized;
        }
    }

    private static string NormalizeDateText(string text)
    {
        var tokens = SplitTokens(text);
        if (tokens.Length == 0)
        {
            return text;
        }

        var changed = false;
        for (var i = 0; i < tokens.Length; i++)
        {
            var normalized = NormalizeDateToken(tokens[i]);
            if (!normalized.Equals(tokens[i], StringComparison.Ordinal))
            {
                tokens[i] = normalized;
                changed = true;
            }
        }

        return changed ? string.Join(' ', tokens) : text;
    }

    private static string NormalizeDateToken(string token) =>
        token switch
        {
            "am" => "AM",
            "pm" => "PM",
            "Die" => "Tue",
            "Mit" => "Wed",
            "Don" => "Thu",
            "Fre" => "Fri",
            "Sam" => "Sat",
            "Son" => "Sun",
            "Okt" => "Oct",
            "Dez" => "Dec",
            "Mai" => "May",
            "M\u00E4r" => "Mar",
            "M\uFFFDr" => "Mar",
            _ => token
        };

    private static bool TryParseChineseVectorDate(string text, out DateTimeOffset value)
    {
        var tokens = SplitTokens(text);
        var firstDateToken = tokens.Length > 0 && tokens[0].StartsWith('周') ? 1 : 0;
        var dateTokenCount = tokens.Length - firstDateToken;
        if (dateTokenCount is not (4 or 5))
        {
            value = default;
            return false;
        }

        var monthToken = tokens[firstDateToken];
        if (!monthToken.EndsWith('月') ||
            !int.TryParse(monthToken[..^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var month) ||
            !int.TryParse(tokens[firstDateToken + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var day) ||
            !TryParseClockTime(tokens[firstDateToken + 2], out var hour, out var minute, out var second, out var ticks))
        {
            value = default;
            return false;
        }

        string yearToken;
        if (dateTokenCount == 5)
        {
            var meridiem = tokens[firstDateToken + 3];
            if (meridiem == "下午" && hour < 12)
            {
                hour += 12;
            }
            else if (meridiem == "上午" && hour == 12)
            {
                hour = 0;
            }
            else if (meridiem is not ("上午" or "下午"))
            {
                value = default;
                return false;
            }

            yearToken = tokens[firstDateToken + 4];
        }
        else
        {
            yearToken = tokens[firstDateToken + 3];
        }

        if (!int.TryParse(yearToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
        {
            value = default;
            return false;
        }

        try
        {
            var localDate = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified).AddTicks(ticks);
            value = new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = default;
            return false;
        }
    }

    private static bool TryParseClockTime(
        string text,
        out int hour,
        out int minute,
        out int second,
        out long ticks)
    {
        hour = 0;
        minute = 0;
        second = 0;
        ticks = 0;

        var parts = text.Split(':');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out hour) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minute))
        {
            return false;
        }

        var secondParts = parts[2].Split('.', 2);
        if (!int.TryParse(secondParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out second))
        {
            return false;
        }

        if (secondParts.Length == 2 && secondParts[1].Length > 0)
        {
            var fractionText = secondParts[1].Length > 7
                ? secondParts[1][..7]
                : secondParts[1].PadRight(7, '0');
            if (!int.TryParse(fractionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fractionTicks))
            {
                return false;
            }

            ticks = fractionTicks;
        }

        return hour is >= 0 and <= 23 &&
               minute is >= 0 and <= 59 &&
               second is >= 0 and <= 59;
    }

    private static bool StartsWithTriggerBlock(string trimmed)
    {
        var tokens = SplitTokens(trimmed);
        return tokens.Length >= 2 &&
               tokens[0].Equals("Begin", StringComparison.OrdinalIgnoreCase) &&
               tokens[1].Equals("Triggerblock", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractTriggerBlockText(string trimmed)
    {
        var first = trimmed.IndexOfAny([' ', '\t']);
        if (first < 0)
        {
            return null;
        }

        var afterFirst = trimmed[first..].TrimStart();
        var second = afterFirst.IndexOfAny([' ', '\t']);
        if (second < 0)
        {
            return null;
        }

        return afterFirst[second..].TrimStart();
    }

    private static bool IsStartOfMeasurement(string trimmed)
    {
        var tokens = SplitTokens(trimmed);
        return tokens.Length >= 4 &&
               double.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
               tokens[1].Equals("Start", StringComparison.OrdinalIgnoreCase) &&
               tokens[2].Equals("of", StringComparison.OrdinalIgnoreCase) &&
               tokens[3].Equals("measurement", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseFrameLine(
        string trimmed,
        int lineNumber,
        VectorAscNumericBase numericBase,
        VectorAscTimestampFormat timestampFormat,
        ref TimeSpan lastTimestamp,
        VectorAscReadOptions options,
        List<VectorAscDiagnostic> diagnostics,
        out VectorAscFrame frame)
    {
        frame = default!;
        var tokens = SplitTokens(trimmed);
        if (tokens.Length < 2 ||
            !double.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var timestampSeconds))
        {
            return false;
        }

        var timestamp = SecondsToTimestamp(timestampSeconds);
        if (timestampFormat == VectorAscTimestampFormat.Relative)
        {
            timestamp += lastTimestamp;
        }

        if (tokens[1].Equals("CANFD", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseCanFdFrameLine(
                tokens,
                lineNumber,
                timestamp,
                numericBase,
                options,
                diagnostics,
                ref lastTimestamp,
                out frame);
        }

        if (!TryParseIntToken(tokens[1], VectorAscNumericBase.Decimal, out var ascChannel) || ascChannel <= 0)
        {
            AddDiagnosticOrThrow(
                options,
                diagnostics,
                lineNumber,
                VectorAscDiagnosticCodes.UnsupportedLine,
                $"Unsupported ASC bus type or channel '{tokens[1]}'.",
                strictOnly: false);
            return false;
        }

        var channelIndex = ascChannel - 1;
        try
        {
            if (tokens.Length >= 3 && tokens[2].Equals("ErrorFrame", StringComparison.OrdinalIgnoreCase))
            {
                frame = new VectorAscFrame
                {
                    LineNumber = lineNumber,
                    Timestamp = timestamp,
                    ChannelIndex = channelIndex,
                    Direction = CanFrameDirection.Receive,
                    ObservationKind = CanFrameObservationKind.Bus,
                    EventFlags = CanFrameEventFlags.ErrorResponse,
                    Frame = CanFrame.CreateError()
                };
                lastTimestamp = timestamp;
                return true;
            }

            if (tokens.Length < 6)
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.MalformedLine,
                    "Classic CAN row must contain timestamp, channel, ID, direction, frame type, and DLC.");
                return false;
            }

            var id = ParseCanId(tokens[2], numericBase);
            var direction = ParseDirection(tokens[3]);
            var observationKind = DirectionToObservationKind(direction);
            var frameType = tokens[4];

            if (frameType.Equals("r", StringComparison.OrdinalIgnoreCase))
            {
                var dlc = tokens.Length >= 6 && TryParseIntToken(tokens[5], numericBase, out var parsedDlc)
                    ? parsedDlc
                    : 0;
                if (dlc is < 0 or > CanFrame.MaxClassicPayloadLength)
                {
                    AddDiagnosticOrThrow(
                        options,
                        diagnostics,
                        lineNumber,
                        VectorAscDiagnosticCodes.MalformedLine,
                        $"Classic remote frame DLC {dlc} is outside 0..8.");
                    return false;
                }

                frame = new VectorAscFrame
                {
                    LineNumber = lineNumber,
                    Timestamp = timestamp,
                    ChannelIndex = channelIndex,
                    Direction = direction,
                    ObservationKind = observationKind,
                    Frame = CanFrame.CreateRemote(id, (byte)dlc)
                };
                lastTimestamp = timestamp;
                return true;
            }

            if (!frameType.Equals("d", StringComparison.OrdinalIgnoreCase))
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.UnsupportedLine,
                    $"Unsupported Classic CAN frame type '{frameType}'.",
                    strictOnly: false);
                return false;
            }

            if (!TryParseIntToken(tokens[5], numericBase, out var dlcValue) ||
                dlcValue is < 0 or > CanFrame.MaxClassicPayloadLength)
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.MalformedLine,
                    $"Classic data frame DLC '{tokens[5]}' is outside 0..8.");
                return false;
            }

            if (tokens.Length < 6 + dlcValue)
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.MalformedLine,
                    $"Classic data frame declares {dlcValue} bytes but only {tokens.Length - 6} are present.");
                return false;
            }

            Span<byte> payload = stackalloc byte[CanFrame.MaxClassicPayloadLength];
            for (var i = 0; i < dlcValue; i++)
            {
                if (!TryParseIntToken(tokens[6 + i], numericBase, out var byteValue) ||
                    byteValue is < 0 or > byte.MaxValue)
                {
                    AddDiagnosticOrThrow(
                        options,
                        diagnostics,
                        lineNumber,
                        VectorAscDiagnosticCodes.MalformedLine,
                        $"Invalid data byte '{tokens[6 + i]}'.");
                    return false;
                }

                payload[i] = (byte)byteValue;
            }

            frame = new VectorAscFrame
            {
                LineNumber = lineNumber,
                Timestamp = timestamp,
                ChannelIndex = channelIndex,
                Direction = direction,
                ObservationKind = observationKind,
                Frame = CanFrame.CreateData(id, payload[..dlcValue])
            };
            lastTimestamp = timestamp;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            AddDiagnosticOrThrow(
                options,
                diagnostics,
                lineNumber,
                VectorAscDiagnosticCodes.MalformedLine,
                ex.Message);
            return false;
        }
    }

    private static TimeSpan SecondsToTimestamp(double seconds) =>
        TimeSpan.FromTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond, MidpointRounding.AwayFromZero));

    private static bool TryParseCanFdFrameLine(
        string[] tokens,
        int lineNumber,
        TimeSpan timestamp,
        VectorAscNumericBase numericBase,
        VectorAscReadOptions options,
        List<VectorAscDiagnostic> diagnostics,
        ref TimeSpan lastTimestamp,
        out VectorAscFrame frame)
    {
        frame = default!;

        try
        {
            if (tokens.Length < 9)
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.MalformedLine,
                    "CAN FD row must contain timestamp, CANFD, channel, direction, ID, BRS, ESI, DLC, and data length.");
                return false;
            }

            if (!TryParseIntToken(tokens[2], VectorAscNumericBase.Decimal, out var ascChannel) || ascChannel <= 0)
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.MalformedLine,
                    $"Invalid CAN FD channel '{tokens[2]}'.");
                return false;
            }

            var channelIndex = ascChannel - 1;
            var direction = ParseDirection(tokens[3]);
            var id = ParseCanId(tokens[4], numericBase);

            string? symbolicName = null;
            var brsIndex = 5;
            if (!IsBinaryFlagToken(tokens[brsIndex]))
            {
                symbolicName = tokens[brsIndex];
                brsIndex++;
            }

            if (tokens.Length <= brsIndex + 3)
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.MalformedLine,
                    "CAN FD row is missing BRS, ESI, DLC, or data length after the identifier.");
                return false;
            }

            var brs = ParseBinaryFlag(tokens[brsIndex], "BRS");
            var esi = ParseBinaryFlag(tokens[brsIndex + 1], "ESI");

            if (!TryParseIntToken(tokens[brsIndex + 2], numericBase, out var dlc) || dlc is < 0 or > 15)
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.MalformedLine,
                    $"CAN FD DLC '{tokens[brsIndex + 2]}' is outside 0..15.");
                return false;
            }

            if (!TryParseIntToken(tokens[brsIndex + 3], VectorAscNumericBase.Decimal, out var dataLength) ||
                dataLength is < 0 or > CanFrame.MaxPayloadLength)
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.MalformedLine,
                    $"CAN FD data length '{tokens[brsIndex + 3]}' is outside 0..64.");
                return false;
            }

            if (!CanFrame.IsValidFdPayloadLength(dataLength))
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.MalformedLine,
                    $"CAN FD data length {dataLength} is not DLC-representable.");
                return false;
            }

            if (dataLength != 0 && CanFrame.DlcToLength((byte)dlc) != dataLength)
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.CanFdDlcLengthMismatch,
                    $"CAN FD DLC {dlc:X} maps to {CanFrame.DlcToLength((byte)dlc)} bytes, but row declares {dataLength} bytes.");
            }

            var payloadStart = brsIndex + 4;
            if (tokens.Length < payloadStart + dataLength)
            {
                AddDiagnosticOrThrow(
                    options,
                    diagnostics,
                    lineNumber,
                    VectorAscDiagnosticCodes.MalformedLine,
                    $"CAN FD row declares {dataLength} bytes but only {tokens.Length - payloadStart} payload tokens are present.");
                return false;
            }

            Span<byte> payload = stackalloc byte[CanFrame.MaxPayloadLength];
            for (var i = 0; i < dataLength; i++)
            {
                if (!TryParseIntToken(tokens[payloadStart + i], numericBase, out var byteValue) ||
                    byteValue is < 0 or > byte.MaxValue)
                {
                    AddDiagnosticOrThrow(
                        options,
                        diagnostics,
                        lineNumber,
                        VectorAscDiagnosticCodes.MalformedLine,
                        $"Invalid CAN FD data byte '{tokens[payloadStart + i]}'.");
                    return false;
                }

                payload[i] = (byte)byteValue;
            }

            uint? canFdFlags = null;
            var flagsIndex = payloadStart + dataLength + 2;
            if (tokens.Length > flagsIndex &&
                TryParseUIntToken(tokens[flagsIndex], VectorAscNumericBase.Hex, out var parsedFlags))
            {
                canFdFlags = parsedFlags;
                if (parsedFlags != 0)
                {
                    AddCanFdFlagDiagnostics(parsedFlags, brs, esi, lineNumber, options, diagnostics);
                }
            }

            frame = new VectorAscFrame
            {
                LineNumber = lineNumber,
                Timestamp = timestamp,
                ChannelIndex = channelIndex,
                Direction = direction,
                ObservationKind = DirectionToObservationKind(direction),
                Frame = CanFrame.CreateFdData(id, payload[..dataLength], brs, esi),
                IsCanFdLine = true,
                SymbolicName = symbolicName,
                CanFdFlags = canFdFlags
            };
            lastTimestamp = timestamp;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            AddDiagnosticOrThrow(
                options,
                diagnostics,
                lineNumber,
                VectorAscDiagnosticCodes.MalformedLine,
                ex.Message);
            return false;
        }
    }

    private static bool IsBinaryFlagToken(string token) =>
        token is "0" or "1";

    private static bool ParseBinaryFlag(string token, string name) =>
        token switch
        {
            "0" => false,
            "1" => true,
            _ => throw new FormatException($"CAN FD {name} flag must be 0 or 1, got '{token}'.")
        };

    private static void AddCanFdFlagDiagnostics(
        uint flags,
        bool brs,
        bool esi,
        int lineNumber,
        VectorAscReadOptions options,
        List<VectorAscDiagnostic> diagnostics)
    {
        var flagsDeclareFd = (flags & 0x1000) != 0 || (flags & 0x200000) != 0;
        var flagsDeclareBrs = (flags & 0x2000) != 0;
        var flagsDeclareEsi = (flags & 0x4000) != 0;

        if (!flagsDeclareFd || flagsDeclareBrs != brs || flagsDeclareEsi != esi)
        {
            AddDiagnosticOrThrow(
                options,
                diagnostics,
                lineNumber,
                VectorAscDiagnosticCodes.CanFdFlagsMismatch,
                $"CAN FD flags 0x{flags:X} disagree with BRS={Convert.ToInt32(brs)} ESI={Convert.ToInt32(esi)}.");
        }
    }

    private static CanId ParseCanId(string token, VectorAscNumericBase numericBase)
    {
        var extended = token.EndsWith('x') || token.EndsWith('X');
        var idToken = extended ? token[..^1] : token;
        if (!TryParseUIntToken(idToken, numericBase, out var value))
        {
            throw new FormatException($"Invalid CAN identifier '{token}'.");
        }

        return extended ? CanId.Extended(value) : CanId.Standard(value);
    }

    private static CanFrameDirection ParseDirection(string token) =>
        token switch
        {
            "Rx" => CanFrameDirection.Receive,
            "Tx" => CanFrameDirection.Transmit,
            _ => throw new FormatException($"Invalid frame direction '{token}'.")
        };

    private static CanFrameObservationKind DirectionToObservationKind(CanFrameDirection direction) =>
        direction == CanFrameDirection.Transmit
            ? CanFrameObservationKind.TxConfirmed
            : CanFrameObservationKind.Bus;

    private static bool TryParseIntToken(string token, VectorAscNumericBase numericBase, out int value)
    {
        if (TryParseUIntToken(token, numericBase, out var unsigned) && unsigned <= int.MaxValue)
        {
            value = (int)unsigned;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryParseUIntToken(string token, VectorAscNumericBase numericBase, out uint value)
    {
        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            token = token[2..];
        }

        var style = numericBase == VectorAscNumericBase.Hex
            ? NumberStyles.HexNumber
            : NumberStyles.Integer;
        return uint.TryParse(token, style, CultureInfo.InvariantCulture, out value);
    }

    private static void AddDiagnosticOrThrow(
        VectorAscReadOptions options,
        List<VectorAscDiagnostic> diagnostics,
        int lineNumber,
        string code,
        string message,
        bool strictOnly = true)
    {
        var diagnostic = new VectorAscDiagnostic(lineNumber, code, message);
        if (options.Strict || !strictOnly)
        {
            if (options.Strict)
            {
                throw new FormatException(message);
            }

            diagnostics.Add(diagnostic);
            options.DiagnosticSink?.Invoke(diagnostic);
            return;
        }

        diagnostics.Add(diagnostic);
        options.DiagnosticSink?.Invoke(diagnostic);
    }

    private static string[] SplitTokens(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}
