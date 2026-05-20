using CanHub.Adapter.Vector.Internal;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
public sealed class VectorChannelScanMappingTests
{
    [TestMethod]
    public void FromChannelConfig_CanFdChannelBuildsOpenableEndpoint()
    {
        var native = new XLClass.xl_channel_config
        {
            hwType = XLDefine.XL_HardwareType.XL_HWTYPE_VN5610A,
            hwIndex = 0,
            hwChannel = 2,
            channelIndex = 5,
            channelBusCapabilities = XLDefine.XL_BusCapabilities.XL_BUS_COMPATIBLE_CAN,
            channelCapabilities = XLDefine.XL_ChannelCapabilities.XL_CHANNEL_FLAG_CANFD_ISO_SUPPORT,
            isOnBus = 0,
        };

        var channel = VectorChannelScanMapper.FromChannelConfig(native);

        Assert.AreEqual("vector", channel.AdapterId);
        Assert.AreEqual("VN5610A", channel.DeviceName);
        Assert.AreEqual(0, channel.DeviceIndex);
        Assert.AreEqual(2, channel.ChannelIndex);
        Assert.AreEqual(5, channel.NativeChannelIndex);
        Assert.AreEqual("vector://VN5610A?deviceIndex=0&channelIndex=2", channel.Endpoint);
        Assert.AreEqual("vector://VN5610A?channelIndex=2&deviceIndex=0", channel.CanonicalEndpoint);
        Assert.AreEqual("Vector", channel.VendorName);
        Assert.AreEqual("VN5610A:0", channel.HardwareId);
        Assert.AreEqual("Vector VN5610A #0 CH2", channel.DisplayName);
        Assert.AreEqual(CanBusParameters.Classic500k, channel.RecommendedBusParameters);
        Assert.AreEqual(CanChannelAvailability.Available, channel.Availability);
        Assert.IsTrue(channel.CanOpen);
        Assert.IsTrue(channel.Capabilities.Any(c => c.Name == "classic-can"));
        Assert.IsTrue(channel.Capabilities.Any(c => c.Name == "can-fd"));
    }

    [TestMethod]
    public void FromChannelConfig_ActiveCanChannelRemainsOpenable()
    {
        var native = new XLClass.xl_channel_config
        {
            hwType = XLDefine.XL_HardwareType.XL_HWTYPE_VN1630,
            hwIndex = 1,
            hwChannel = 0,
            channelIndex = 8,
            channelBusCapabilities = XLDefine.XL_BusCapabilities.XL_BUS_COMPATIBLE_CAN,
            isOnBus = 1,
            connectedBusType = XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN,
        };

        var channel = VectorChannelScanMapper.FromChannelConfig(native);

        Assert.AreEqual("VN1630", channel.DeviceName);
        Assert.AreEqual(1, channel.DeviceIndex);
        Assert.AreEqual(CanChannelAvailability.Active, channel.Availability);
        Assert.AreEqual("vector://VN1630?deviceIndex=1&channelIndex=0", channel.Endpoint);
        Assert.IsTrue(channel.CanOpen);
    }

    [TestMethod]
    public void FromChannelConfig_NonCanChannelIsUnsupportedWithoutEndpoint()
    {
        var native = new XLClass.xl_channel_config
        {
            hwType = XLDefine.XL_HardwareType.XL_HWTYPE_VN5610A,
            hwIndex = 0,
            hwChannel = 0,
            channelIndex = 3,
            channelBusCapabilities = XLDefine.XL_BusCapabilities.XL_BUS_COMPATIBLE_ETHERNET,
        };

        var channel = VectorChannelScanMapper.FromChannelConfig(native);

        Assert.AreEqual(CanChannelAvailability.Unsupported, channel.Availability);
        Assert.IsNull(channel.Endpoint);
        Assert.IsFalse(channel.CanOpen);
        Assert.IsNotNull(channel.Diagnostic);
        Assert.AreEqual("vector", channel.Diagnostic.AdapterId);
    }
}
