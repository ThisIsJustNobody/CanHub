using CanHub.Adapter.Vector.Internal;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
public sealed class VectorChannelIndexTests
{
    [TestMethod(DisplayName = "Vector端口区分逻辑通道和驱动原生通道")]
    public void Constructor_SplitsLogicalAndNativeChannelIndex()
    {
        var port = new VectorChannelPort(
            new VectorDriver(),
            channelMask: 1UL << 7,
            logicalChannelIndex: 3);

        Assert.AreEqual(3, port.LogicalChannelIndex);
        Assert.AreEqual(3, port.ChannelIndex);
        Assert.AreEqual(7, port.NativeChannelIndex);
    }
}
