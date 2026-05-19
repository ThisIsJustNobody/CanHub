using System.Globalization;
using System.Text;

namespace CanHub.Trace.VectorAsc;

/// <summary>
/// Vector ASC 文件写入器。<br/>
/// Writer for Vector ASC files.
/// </summary>
public static class VectorAscWriter
{
    /// <summary>
    /// 将帧记录写入 ASC 字符串。<br/>
    /// Writes frame records to an ASC string.
    /// </summary>
    public static string WriteText(IEnumerable<VectorAscFrame> frames, VectorAscWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var builder = new StringBuilder();
        using var writer = new StringWriter(builder, CultureInfo.InvariantCulture);
        Write(writer, frames, options);
        return builder.ToString();
    }

    /// <summary>
    /// 将帧记录写入 ASC 文件。<br/>
    /// Writes frame records to an ASC file.
    /// </summary>
    public static void WriteFile(string path, IEnumerable<VectorAscFrame> frames, VectorAscWriteOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
        Write(writer, frames, options);
    }

    /// <summary>
    /// 将帧记录写入文本写入器。<br/>
    /// Writes frame records to a text writer.
    /// </summary>
    public static void Write(TextWriter writer, IEnumerable<VectorAscFrame> frames, VectorAscWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(frames);
        options ??= new VectorAscWriteOptions();

        var startTime = options.StartTime ?? DateTimeOffset.Now;
        var formattedDate = FormatDate(startTime);

        writer.Write("date ");
        writer.WriteLine(formattedDate);
        writer.Write("base ");
        writer.Write(options.NumericBase == VectorAscNumericBase.Decimal ? "dec" : "hex");
        writer.Write("  timestamps ");
        writer.WriteLine(options.TimestampFormat == VectorAscTimestampFormat.Relative ? "relative" : "absolute");
        writer.WriteLine("internal events logged");
        writer.Write("Begin Triggerblock ");
        writer.WriteLine(formattedDate);
        writer.WriteLine(" 0.000000 Start of measurement");

        var previousTimestamp = TimeSpan.Zero;
        foreach (var frame in frames)
        {
            var outputTimestamp = frame.Timestamp;
            if (options.TimestampFormat == VectorAscTimestampFormat.Relative)
            {
                if (frame.Timestamp < previousTimestamp)
                {
                    throw new ArgumentException("Relative ASC output requires non-decreasing frame timestamps.", nameof(frames));
                }

                outputTimestamp = frame.Timestamp - previousTimestamp;
                previousTimestamp = frame.Timestamp;
            }

            WriteFrame(writer, frame, options, outputTimestamp);
        }

        writer.WriteLine("End TriggerBlock");
    }

    private static void WriteFrame(
        TextWriter writer,
        VectorAscFrame record,
        VectorAscWriteOptions options,
        TimeSpan outputTimestamp)
    {
        if (record.ChannelIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(record), record.ChannelIndex, "Channel index must not be negative.");
        }

        var channel = record.ChannelIndex + 1;
        var direction = record.Direction == CanFrameDirection.Transmit ? "Tx" : "Rx";
        var timestamp = FormatTimestamp(outputTimestamp);

        if (record.Frame.Flags.HasFlag(CanFrameFlags.FD) || record.IsCanFdLine)
        {
            WriteCanFdFrame(writer, timestamp, channel, direction, record, options);
            return;
        }

        switch (record.Frame.Kind)
        {
            case CanFrameKind.Data:
                writer.Write(timestamp);
                writer.Write(' ');
                writer.Write(channel.ToString(CultureInfo.InvariantCulture));
                writer.Write("  ");
                writer.Write(FormatId(record.Frame.Id, options).PadRight(15));
                writer.Write(' ');
                writer.Write(direction.PadRight(4));
                writer.Write(" d ");
                writer.Write(FormatNumber(record.Frame.Dlc, options));
                WritePayload(writer, record.Frame, options);
                writer.WriteLine();
                break;
            case CanFrameKind.Remote:
                writer.Write(timestamp);
                writer.Write(' ');
                writer.Write(channel.ToString(CultureInfo.InvariantCulture));
                writer.Write("  ");
                writer.Write(FormatId(record.Frame.Id, options).PadRight(15));
                writer.Write(' ');
                writer.Write(direction.PadRight(4));
                writer.Write(" r ");
                writer.WriteLine(FormatNumber(record.Frame.Dlc, options));
                break;
            case CanFrameKind.Error:
                writer.Write(timestamp);
                writer.Write(' ');
                writer.Write(channel.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("  ErrorFrame");
                break;
            default:
                throw new ArgumentException($"Unsupported ASC frame kind '{record.Frame.Kind}'.", nameof(record));
        }
    }

    private static void WriteCanFdFrame(
        TextWriter writer,
        string timestamp,
        int channel,
        string direction,
        VectorAscFrame record,
        VectorAscWriteOptions options)
    {
        var frame = record.Frame;
        if (frame.Kind != CanFrameKind.Data)
        {
            throw new ArgumentException("CAN FD ASC output supports data frames only.", nameof(record));
        }

        var brs = frame.Flags.HasFlag(CanFrameFlags.BRS);
        var esi = frame.Flags.HasFlag(CanFrameFlags.ESI);
        var flags = brs ? 0x303000u : 0x200000u;
        if (esi) flags |= 0x4000u;
        if (record.Direction == CanFrameDirection.Transmit) flags |= 0x40u;

        writer.Write(timestamp);
        writer.Write(" CANFD ");
        writer.Write(channel.ToString(CultureInfo.InvariantCulture).PadLeft(3));
        writer.Write(' ');
        writer.Write(direction.PadRight(4));
        writer.Write(' ');
        writer.Write(FormatId(frame.Id, options).PadLeft(8));
        writer.Write("  ");
        writer.Write(string.Empty.PadLeft(32));
        writer.Write(' ');
        writer.Write(brs ? '1' : '0');
        writer.Write(' ');
        writer.Write(esi ? '1' : '0');
        writer.Write(' ');
        writer.Write(FormatDlc(frame.Dlc, options));
        writer.Write(' ');
        writer.Write(frame.Length.ToString(CultureInfo.InvariantCulture).PadLeft(2));
        WritePayload(writer, frame, options);
        writer.Write("        0    0 ");
        writer.Write(flags.ToString("X", CultureInfo.InvariantCulture).PadLeft(8));
        writer.WriteLine("        0        0        0        0        0");
    }

    private static void WritePayload(TextWriter writer, CanFrame frame, VectorAscWriteOptions options)
    {
        Span<byte> payload = stackalloc byte[CanFrame.MaxPayloadLength];
        frame.CopyPayloadTo(payload);
        for (var i = 0; i < frame.Length; i++)
        {
            writer.Write(' ');
            writer.Write(FormatNumber(payload[i], options).PadLeft(2, '0'));
        }
    }

    private static string FormatId(CanId id, VectorAscWriteOptions options)
    {
        var value = FormatNumber(id.Value, options);
        return id.IsExtended ? value + "x" : value;
    }

    private static string FormatNumber(uint value, VectorAscWriteOptions options) =>
        options.NumericBase == VectorAscNumericBase.Decimal
            ? value.ToString(CultureInfo.InvariantCulture)
            : value.ToString("X", CultureInfo.InvariantCulture);

    private static string FormatDlc(byte dlc, VectorAscWriteOptions options) =>
        options.NumericBase == VectorAscNumericBase.Decimal
            ? dlc.ToString(CultureInfo.InvariantCulture)
            : dlc.ToString("x", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(TimeSpan timestamp) =>
        timestamp.TotalSeconds.ToString("0.000000", CultureInfo.InvariantCulture).PadLeft(9);

    private static string FormatDate(DateTimeOffset date) =>
        date.ToString("ddd MMM dd HH:mm:ss.fff yyyy", CultureInfo.InvariantCulture);
}
