using CanHub.Adapter.Zlg.Tests.Support;

namespace CanHub.Adapter.Zlg.Tests.Hardware;

[TestClass]
[TestCategory("Hardware")]
public sealed class ZlgScanHardwareTests : ZlgCanHubHardwareTestBase
{
    [TestMethod(DisplayName = "ZLG scan finds two USBCANFD_200U devices")]
    public async Task Scan_FindsTwoUsbCanFd200UDevices()
    {
        RequireZlgHardware();

        var provider = new ZlgAdapterProvider();
        var scan = await provider.ScanAsync(
            new ScanOptions { MinDepth = (int)Math.Max(Env.ScanDepth, 2) },
            TestContext.CancellationToken);

        foreach (var channel in scan.Channels)
        {
            TestContext.WriteLine(
                $"{channel.Endpoint}: {channel.DeviceName}, native CH={channel.NativeChannelIndex}, canOpen={channel.CanOpen}");
        }

        Assert.IsTrue(scan.Channels.Count(channel => channel.DeviceIndex == Env.Device0Index) >= 2);
        Assert.IsTrue(scan.Channels.Count(channel => channel.DeviceIndex == Env.Device1Index) >= 2);
        Assert.IsTrue(scan.Channels.All(channel => channel.AdapterId == "zlg"));
        Assert.IsTrue(scan.Channels.All(channel => channel.Endpoint?.StartsWith("zlg://USBCANFD_200U", StringComparison.Ordinal) == true));
    }
}
