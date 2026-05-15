using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanAdapterManifestTests
{
    [TestMethod(DisplayName = "构造函数正确设置所有属性")]
    public void Constructor_SetsAllProperties()
    {
        var schemes = new[] { "usb", "pcie" };
        var manifest = new CanAdapterManifest("zlg-usb", "ZLG USB-CAN", schemes);

        Assert.AreEqual("zlg-usb", manifest.AdapterId);
        Assert.AreEqual("ZLG USB-CAN", manifest.DisplayName);
        CollectionAssert.AreEqual(schemes, manifest.EndpointSchemes.ToArray());
    }

    [TestMethod(DisplayName = "空方案列表构造函数成功")]
    public void Constructor_EmptySchemes_Succeeds()
    {
        var manifest = new CanAdapterManifest("vector", "Vector", Array.Empty<string>());

        Assert.AreEqual("vector", manifest.AdapterId);
        Assert.AreEqual("Vector", manifest.DisplayName);
        Assert.IsEmpty(manifest.EndpointSchemes);
    }

    [TestMethod(DisplayName = "端点方案列表为只读")]
    public void EndpointSchemes_IsReadOnly()
    {
        var manifest = new CanAdapterManifest("test", "Test", new[] { "a" });
        Assert.IsInstanceOfType<IReadOnlyList<string>>(manifest.EndpointSchemes);
    }

    [TestMethod(DisplayName = "适配器ID为null时抛出异常")]
    public void Constructor_NullAdapterId_Throws()
    {
        TestAssert.Throws<ArgumentNullException>(
            () => new CanAdapterManifest(null!, "Display", Array.Empty<string>()));
    }

    [TestMethod(DisplayName = "显示名称为null时抛出异常")]
    public void Constructor_NullDisplayName_Throws()
    {
        TestAssert.Throws<ArgumentNullException>(
            () => new CanAdapterManifest("id", null!, Array.Empty<string>()));
    }

    [TestMethod(DisplayName = "端点方案为null时抛出异常")]
    public void Constructor_NullEndpointSchemes_Throws()
    {
        TestAssert.Throws<ArgumentNullException>(
            () => new CanAdapterManifest("id", "Display", null!));
    }

    [TestMethod]
    public void Constructor_WithNewFields_SetsProperties()
    {
        var caps = new[] { new CanCapability("can-fd", true) };
        var manifest = new CanAdapterManifest(
            "zlg", "ZLG USBCANFD", new[] { "zlg" },
            platform: "windows",
            exclusivity: ExclusivityModel.DeviceLevel,
            capabilities: caps,
            supportsChannelScan: true);

        Assert.AreEqual("windows", manifest.Platform);
        Assert.AreEqual(ExclusivityModel.DeviceLevel, manifest.Exclusivity);
        Assert.HasCount(1, manifest.Capabilities);
        Assert.AreEqual("can-fd", manifest.Capabilities[0].Name);
        Assert.IsTrue(manifest.SupportsChannelScan);
    }

    [TestMethod]
    public void Constructor_DefaultValues()
    {
        var manifest = new CanAdapterManifest("test", "Test", new[] { "test" });

        Assert.AreEqual("cross-platform", manifest.Platform);
        Assert.AreEqual(ExclusivityModel.None, manifest.Exclusivity);
        Assert.IsEmpty(manifest.Capabilities);
        Assert.IsFalse(manifest.SupportsChannelScan);
    }

    [TestMethod]
    public void Constructor_NullCapabilities_DefaultsToEmpty()
    {
        var manifest = new CanAdapterManifest("test", "Test", new[] { "test" },
            capabilities: null);

        Assert.IsEmpty(manifest.Capabilities);
    }

    [TestMethod]
    public void Constructor_DefensivelyCopiesAndHidesMutableCollections()
    {
        var schemes = new[] { "virtual" };
        var caps = new[] { new CanCapability("classic-can", true) };

        var manifest = new CanAdapterManifest("test", "Test", schemes, capabilities: caps);
        schemes[0] = "mutated";
        caps[0] = new CanCapability("can-fd", false);

        Assert.AreEqual("virtual", manifest.EndpointSchemes[0]);
        Assert.AreEqual("classic-can", manifest.Capabilities[0].Name);
        Assert.IsNotInstanceOfType<string[]>(manifest.EndpointSchemes);
        Assert.IsNotInstanceOfType<CanCapability[]>(manifest.Capabilities);
    }
}
