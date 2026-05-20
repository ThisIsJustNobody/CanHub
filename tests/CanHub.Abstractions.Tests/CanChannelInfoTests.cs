namespace CanHub.Abstractions.Tests;

[TestClass]
public sealed class CanChannelInfoTests
{
    [TestMethod]
    public void Constructor_SetsChannelProperties()
    {
        var caps = new[] { new CanCapability("can-fd", false) };
        var channel = new CanChannelInfo(
            adapterId: "vector",
            deviceName: "VN5610A",
            deviceIndex: 0,
            channelIndex: 2,
            nativeChannelIndex: 5,
            endpoint: "vector://VN5610A?deviceIndex=0&channel=2",
            availability: CanChannelAvailability.Available,
            capabilities: caps);

        Assert.AreEqual("vector", channel.AdapterId);
        Assert.AreEqual("VN5610A", channel.DeviceName);
        Assert.AreEqual(0, channel.DeviceIndex);
        Assert.AreEqual(2, channel.ChannelIndex);
        Assert.AreEqual(5, channel.NativeChannelIndex);
        Assert.AreEqual("vector://VN5610A?deviceIndex=0&channel=2", channel.Endpoint);
        Assert.AreEqual("vector://VN5610A?channelIndex=2&deviceIndex=0", channel.CanonicalEndpoint);
        Assert.AreEqual("vector://VN5610A?channelIndex=2&deviceIndex=0", channel.ChannelId);
        Assert.AreEqual("VN5610A Channel 2", channel.DisplayName);
        Assert.AreEqual(CanChannelAvailability.Available, channel.Availability);
        Assert.IsTrue(channel.CanOpen);
        Assert.HasCount(1, channel.Capabilities);
        Assert.IsNull(channel.Diagnostic);
    }

    [TestMethod]
    public void Constructor_ActiveChannelWithEndpointCanStillOpen()
    {
        var channel = new CanChannelInfo(
            adapterId: "vector",
            deviceName: "VN5610A",
            deviceIndex: 0,
            channelIndex: 3,
            nativeChannelIndex: 6,
            endpoint: "vector://VN5610A?deviceIndex=0&channel=3",
            availability: CanChannelAvailability.Active);

        Assert.IsTrue(channel.CanOpen);
        Assert.AreEqual(CanChannelAvailability.Active, channel.Availability);
    }

    [TestMethod]
    public void Constructor_UnsupportedChannelWithoutEndpointCannotOpen()
    {
        var diag = new ScanDiagnostic(
            CanErrorCategory.InvalidEndpoint,
            "Channel is not CAN-compatible.",
            adapterId: "vector");

        var channel = new CanChannelInfo(
            adapterId: "vector",
            deviceName: "VN5610A",
            deviceIndex: 0,
            channelIndex: 0,
            nativeChannelIndex: 0,
            endpoint: null,
            availability: CanChannelAvailability.Unsupported,
            diagnostic: diag);

        Assert.IsFalse(channel.CanOpen);
        Assert.AreSame(diag, channel.Diagnostic);
    }

    [TestMethod]
    public void ScanResult_DefaultsToEmptyCollections()
    {
        var result = new CanChannelScanResult();

        Assert.IsEmpty(result.Channels);
        Assert.IsEmpty(result.Diagnostics);
    }

    [TestMethod]
    public void Constructor_NullAdapterId_ThrowsArgumentException()
    {
        TestAssert.Throws<ArgumentException>(() =>
            new CanChannelInfo(
                adapterId: null!,
                deviceName: "VN5610A",
                deviceIndex: 0,
                channelIndex: 0,
                nativeChannelIndex: null,
                endpoint: null,
                availability: CanChannelAvailability.Available));
    }

    [TestMethod]
    public void Constructor_NullDeviceName_ThrowsArgumentException()
    {
        TestAssert.Throws<ArgumentException>(() =>
            new CanChannelInfo(
                adapterId: "vector",
                deviceName: null!,
                deviceIndex: 0,
                channelIndex: 0,
                nativeChannelIndex: null,
                endpoint: null,
                availability: CanChannelAvailability.Available));
    }

    [TestMethod]
    public void Constructor_NegativeDeviceIndex_ThrowsArgumentOutOfRangeException()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(() =>
            new CanChannelInfo(
                adapterId: "vector",
                deviceName: "VN5610A",
                deviceIndex: -1,
                channelIndex: 0,
                nativeChannelIndex: null,
                endpoint: null,
                availability: CanChannelAvailability.Available));
    }

    [TestMethod]
    public void Constructor_NegativeChannelIndex_ThrowsArgumentOutOfRangeException()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(() =>
            new CanChannelInfo(
                adapterId: "vector",
                deviceName: "VN5610A",
                deviceIndex: 0,
                channelIndex: -1,
                nativeChannelIndex: null,
                endpoint: null,
                availability: CanChannelAvailability.Available));
    }

    [TestMethod]
    public void Constructor_NegativeNativeChannelIndex_ThrowsArgumentOutOfRangeException()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(() =>
            new CanChannelInfo(
                adapterId: "vector",
                deviceName: "VN5610A",
                deviceIndex: 0,
                channelIndex: 0,
                nativeChannelIndex: -1,
                endpoint: null,
                availability: CanChannelAvailability.Available));
    }

    [TestMethod]
    public void Constructor_ErrorAvailability_WithEndpoint_CannotOpen()
    {
        var channel = new CanChannelInfo(
            adapterId: "vector",
            deviceName: "VN5610A",
            deviceIndex: 0,
            channelIndex: 0,
            nativeChannelIndex: 0,
            endpoint: "vector://VN5610A?channel=0",
            availability: CanChannelAvailability.Error);

        Assert.IsFalse(channel.CanOpen);
    }

    [TestMethod]
    public void Constructor_DefensivelyCopiesAndHidesCapabilitiesArray()
    {
        var caps = new[] { new CanCapability("classic-can", true) };
        var channel = new CanChannelInfo(
            adapterId: "virtual",
            deviceName: "bench",
            deviceIndex: 0,
            channelIndex: 0,
            nativeChannelIndex: 0,
            endpoint: "virtual://bench?channel=0",
            availability: CanChannelAvailability.Available,
            capabilities: caps);

        caps[0] = new CanCapability("can-fd", false);

        Assert.AreEqual("classic-can", channel.Capabilities[0].Name);
        Assert.IsNotInstanceOfType<CanCapability[]>(channel.Capabilities);
    }

    [TestMethod]
    public void Constructor_WithUiMetadata_SetsMetadataProperties()
    {
        var recommended = CanBusParameters.Fd500k2M;

        var channel = new CanChannelInfo(
            adapterId: "zlg",
            deviceName: "USBCANFD_200U",
            deviceIndex: 1,
            channelIndex: 0,
            nativeChannelIndex: 0,
            endpoint: "zlg://USBCANFD_200U?deviceIndex=1&channelIndex=0",
            availability: CanChannelAvailability.Available,
            vendorName: "ZLG",
            hardwareId: "USBCANFD_200U:1",
            serialNumber: "ABC123",
            displayName: "ZLG USBCANFD_200U #1 CH0",
            channelId: "zlg:USBCANFD_200U:1:0",
            recommendedBusParameters: recommended);

        Assert.AreEqual("ZLG", channel.VendorName);
        Assert.AreEqual("USBCANFD_200U:1", channel.HardwareId);
        Assert.AreEqual("ABC123", channel.SerialNumber);
        Assert.AreEqual("ZLG USBCANFD_200U #1 CH0", channel.DisplayName);
        Assert.AreEqual("zlg:USBCANFD_200U:1:0", channel.ChannelId);
        Assert.AreSame(recommended, channel.RecommendedBusParameters);
    }
}
