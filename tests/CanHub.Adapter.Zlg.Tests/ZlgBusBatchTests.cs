using System.Reflection;
using CanHub.Adapter.Zlg.Internal;

namespace CanHub.Adapter.Zlg.Tests;

[TestClass]
public sealed class ZlgBusBatchTests
{
    [TestMethod(DisplayName = "SendBatchAsync empty batch returns empty result")]
    public async Task SendBatchAsync_EmptyBatch_ReturnsEmptyResult()
    {
        var entry = CreateSyntheticLeaseEntry();
        await using var bus = new ZlgBus(entry, static lease => lease.Dispose(), static (lease, _) => lease.DisposeAsync());

        var results = await bus.SendBatchAsync(ReadOnlyMemory<CanFrame>.Empty, ct: TestContext.CancellationToken);

        Assert.AreEqual(0, results.Length);
    }

    public TestContext TestContext { get; set; } = null!;

    private static ZlgChannelLeaseEntry CreateSyntheticLeaseEntry()
    {
        var capabilities = ZlgDeviceTypeMap.Resolve("USBCANFD_200U");
        var deviceInfo = new ZlgDeviceInfo(
            (ZlgDeviceType)capabilities.DeviceTypeId,
            DeviceIndex: 0,
            HardwareVersion: "0.0",
            FirmwareVersion: "0.0",
            DriverVersion: "0.0",
            InterfaceVersion: "0.0",
            IrqNumber: 0,
            CanChannelCount: (byte)capabilities.DefaultChannelCount,
            SerialNumber: "synthetic",
            HardwareType: capabilities.EndpointName);

        var deviceConstructor = typeof(ZlgDeviceLeaseEntry).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(ZlgDeviceKey),
                typeof(ZlgDeviceCapabilities),
                typeof(nint),
                typeof(ZlgDeviceInfo),
                typeof(bool),
            ],
            modifiers: null)!;
        var device = (ZlgDeviceLeaseEntry)deviceConstructor.Invoke(
        [
            new ZlgDeviceKey(capabilities.DeviceTypeId, DeviceIndex: 0),
            capabilities,
            nint.Zero,
            deviceInfo,
            true,
        ]);

        var coreAssembly = typeof(CanHubRegistry).Assembly;
        var sequenceGeneratorType = coreAssembly.GetType("CanHub.Core.CanSequenceGenerator", throwOnError: true)!;
        var hubType = coreAssembly.GetType("CanHub.Core.FrameBroadcastHub", throwOnError: true)!;
        var sequenceGenerator = Activator.CreateInstance(sequenceGeneratorType, nonPublic: true)!;
        var hub = Activator.CreateInstance(hubType, sequenceGenerator)!;

        return (ZlgChannelLeaseEntry)Activator.CreateInstance(
            typeof(ZlgChannelLeaseEntry),
            [
                new ZlgChannelKey(capabilities.DeviceTypeId, DeviceIndex: 0, ChannelIndex: 0),
                device,
                nint.Zero,
                hub,
                new byte[32],
                false,
                ZlgTransmitType.Single,
                "Synthetic ZLG lease",
            ])!;
    }
}
