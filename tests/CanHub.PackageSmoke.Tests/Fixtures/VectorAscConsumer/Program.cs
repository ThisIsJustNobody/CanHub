using CanHub;
using CanHub.Trace.VectorAsc;

var records = new[]
{
    new VectorAscFrame
    {
        Timestamp = TimeSpan.FromMilliseconds(1),
        ChannelIndex = 0,
        Direction = CanFrameDirection.Receive,
        ObservationKind = CanFrameObservationKind.Bus,
        Frame = CanFrame.CreateData(CanId.Standard(0x123), [0x01, 0x02, 0x03])
    },
    new VectorAscFrame
    {
        Timestamp = TimeSpan.FromMilliseconds(2),
        ChannelIndex = 0,
        Direction = CanFrameDirection.Transmit,
        ObservationKind = CanFrameObservationKind.TxConfirmed,
        Frame = CanFrame.CreateFdData(CanId.Extended(0x18DAF110), [0x10, 0x14, 0x62, 0xF1, 0x90], bitRateSwitch: true),
        IsCanFdLine = true
    }
};

var text = VectorAscWriter.WriteText(records, new VectorAscWriteOptions
{
    StartTime = new DateTimeOffset(2026, 5, 19, 13, 0, 0, TimeSpan.Zero)
});
var parsed = VectorAscReader.ReadText(text, new VectorAscReadOptions { Strict = true });

Require(parsed.Frames.Count == 2, "ASC roundtrip did not preserve frame count.");
Require(parsed.Frames[0].Frame.Id == CanId.Standard(0x123), "Classic CAN identifier was not preserved.");
Require(parsed.Frames[1].Frame.Id == CanId.Extended(0x18DAF110), "CAN FD identifier was not preserved.");
Require(parsed.Frames[1].Frame.Flags.HasFlag(CanFrameFlags.FD), "CAN FD flag was not preserved.");

var frameEvent = VectorAscCanHubConversion.ToFrameEvent(parsed.Frames[1]);
Require(frameEvent.Frame == parsed.Frames[1].Frame, "CanHub conversion did not preserve the frame.");

RequireFile("CanHub.Abstractions.dll");
RequireFile("CanHub.Trace.VectorAsc.dll");

Console.WriteLine("vector-asc-ok");

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void RequireFile(string relativePath)
{
    var path = Path.Combine(AppContext.BaseDirectory, relativePath);
    if (!File.Exists(path))
        throw new FileNotFoundException($"Expected file was not copied to output: {relativePath}", path);
}
