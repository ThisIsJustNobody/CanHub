using CanHub.Adapter.Zlg.Internal;

namespace CanHub.Adapter.Zlg.Tests;

[TestClass]
public sealed class ZlgNativeContractTests
{
    [TestMethod(DisplayName = "Public ZLG enums keep verified native values")]
    public void PublicEnums_KeepVerifiedNativeValues()
    {
        Assert.AreEqual(0, ToInt32(ZlgWorkMode.Normal));
        Assert.AreEqual(1, ToInt32(ZlgWorkMode.NotAck));
        Assert.AreEqual(2, ToInt32(ZlgWorkMode.SelfAck));
        Assert.AreEqual(3, ToInt32(ZlgWorkMode.NotRetry));

        Assert.AreEqual(0u, ToUInt32(ZlgTransmitType.Normal));
        Assert.AreEqual(1u, ToUInt32(ZlgTransmitType.Single));
        Assert.AreEqual(2u, ToUInt32(ZlgTransmitType.SelfAck));
        Assert.AreEqual(3u, ToUInt32(ZlgTransmitType.SingleSelfAck));
    }

    [TestMethod(DisplayName = "Native exceptions are mapped to CanException AdapterError")]
    public void NativeExceptionMapper_MapsToCanException()
    {
        var api = ZlgExceptionMapper.ToCanException(new ZlgApiException("ZCAN_Test", ZlgStatus.Error));
        var missingDll = ZlgExceptionMapper.ToCanException(new DllNotFoundException("missing zlgcan.dll"));

        Assert.AreEqual("zlg", api.AdapterId);
        Assert.AreEqual(CanErrorCategory.AdapterError, api.Category);
        Assert.IsInstanceOfType<ZlgApiException>(api.InnerException);
        Assert.AreEqual("zlg", missingDll.AdapterId);
        Assert.AreEqual(CanErrorCategory.AdapterError, missingDll.Category);
        Assert.IsInstanceOfType<DllNotFoundException>(missingDll.InnerException);
    }

    [TestMethod(DisplayName = "Native scan diagnostics carry mapped hint and details")]
    public void NativeExceptionMapper_MapsToScanDiagnostic()
    {
        var diagnostic = ZlgExceptionMapper.ToScanDiagnostic(
            new ZlgApiException("ZCAN_GetDeviceInf", ZlgStatus.Error, "deviceIndex=1"));

        Assert.AreEqual("zlg", diagnostic.AdapterId);
        Assert.AreEqual(CanErrorCategory.AdapterError, diagnostic.Category);
        Assert.AreEqual((int)ZlgStatus.Error, diagnostic.NativeErrorCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostic.Hint));
        Assert.AreEqual("ZCAN_GetDeviceInf", diagnostic.Details["nativeFunction"]);
        Assert.AreEqual(((uint)ZlgStatus.Error).ToString(), diagnostic.Details["vendorCode"]);
        Assert.AreEqual("deviceIndex=1", diagnostic.Details["nativeDetail"]);
    }

    [TestMethod(DisplayName = "Platform support failures are mapped at the native boundary")]
    public void NativeExceptionMapper_TreatsPlatformNotSupportedAsNativeBoundary()
    {
        var exception = new PlatformNotSupportedException("ZLG zlgcan.dll probing is supported only on Windows.");

        Assert.IsTrue(ZlgExceptionMapper.IsNativeBoundaryException(exception));

        var mapped = ZlgExceptionMapper.ToCanException(exception);
        Assert.AreEqual("zlg", mapped.AdapterId);
        Assert.AreEqual(CanErrorCategory.AdapterError, mapped.Category);
        Assert.IsInstanceOfType<PlatformNotSupportedException>(mapped.InnerException);
    }

    [TestMethod(DisplayName = "ZLG native loader does not mutate PATH")]
    public void NativeLoader_Load_DoesNotMutatePath()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("ZLG native loader is Windows-only.");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CANHUB_TEST_ZLG")))
            Assert.Inconclusive("Skipping native loader test: CANHUB_TEST_ZLG is not set.");

        var before = Environment.GetEnvironmentVariable("PATH");

        _ = ZlgNativeLoader.LoadZlgCan();

        var after = Environment.GetEnvironmentVariable("PATH");
        Assert.AreEqual(before, after);
    }

    private static int ToInt32<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        Convert.ToInt32(value);

    private static uint ToUInt32<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        Convert.ToUInt32(value);
}
