using System.Runtime.InteropServices;
using CanHub.Adapter.Zlg.Internal;

namespace CanHub.Adapter.Zlg.Tests;

[TestClass]
public sealed class ZlgNativeLayoutTests
{
    [TestMethod(DisplayName = "ZLG native struct sizes match verified probe facts")]
    public void NativeStructSizes_MatchVerifiedProbeFacts()
    {
        Assert.AreEqual(16, Marshal.SizeOf<NativeCanFrame>());
        Assert.AreEqual(72, Marshal.SizeOf<NativeCanFdFrame>());
        Assert.AreEqual(20, Marshal.SizeOf<NativeTransmitData>());
        Assert.AreEqual(76, Marshal.SizeOf<NativeTransmitFdData>());
        Assert.AreEqual(24, Marshal.SizeOf<NativeReceiveData>());
        Assert.AreEqual(80, Marshal.SizeOf<NativeReceiveFdData>());
        Assert.AreEqual(88, Marshal.SizeOf<NativeCanFdData>());
        Assert.AreEqual(16, Marshal.SizeOf<NativeErrorData>());
        Assert.AreEqual(100, Marshal.SizeOf<NativeDataObject>());
        Assert.AreEqual(79, Marshal.SizeOf<NativeDeviceInfo>());
        Assert.AreEqual(32, Marshal.SizeOf<NativeChannelInitConfig>());
    }
}
