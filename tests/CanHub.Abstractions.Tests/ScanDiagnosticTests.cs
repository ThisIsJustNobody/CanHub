namespace CanHub.Abstractions.Tests;

[TestClass]
public sealed class ScanDiagnosticTests
{
    [TestMethod]
    public void Constructor_SetsProperties()
    {
        var diag = new ScanDiagnostic(
            CanErrorCategory.AdapterError,
            "OpenDevice failed",
            unchecked((int)0xFFFFFFFC),
            CanRecoverability.Fatal);

        Assert.AreEqual(CanErrorCategory.AdapterError, diag.Category);
        Assert.AreEqual("OpenDevice failed", diag.Message);
        Assert.AreEqual(-4, diag.NativeErrorCode);
        Assert.AreEqual(CanRecoverability.Fatal, diag.Recoverability);
    }

    [TestMethod]
    public void Constructor_NullNativeErrorCode()
    {
        var diag = new ScanDiagnostic(
            CanErrorCategory.AdapterNotFound,
            "No device");

        Assert.IsNull(diag.NativeErrorCode);
    }

    [TestMethod]
    public void Constructor_DefaultAdapterId_IsWildcard()
    {
        var diag = new ScanDiagnostic(
            CanErrorCategory.AdapterNotFound,
            "No device");

        Assert.AreEqual("*", diag.AdapterId);
    }

    [TestMethod]
    public void Constructor_NullAdapterId_ThrowsArgumentException()
    {
        TestAssert.Throws<ArgumentException>(() =>
            new ScanDiagnostic(
                CanErrorCategory.AdapterNotFound,
                "No device",
                adapterId: null!));
    }

    [TestMethod]
    public void Constructor_NullMessage_ThrowsArgumentException()
    {
        TestAssert.Throws<ArgumentException>(() =>
            new ScanDiagnostic(
                CanErrorCategory.AdapterNotFound,
                null!));
    }

    [TestMethod]
    public void Constructor_WithEndpoint_StoresEndpoint()
    {
        var diag = new ScanDiagnostic(
            CanErrorCategory.InvalidEndpoint,
            "Bad URI",
            endpoint: "virtual://bad",
            hint: "Use channelIndex",
            details: new Dictionary<string, string>
            {
                ["parameter"] = "channelIndex",
            });

        Assert.AreEqual("virtual://bad", diag.Endpoint);
        Assert.AreEqual("Use channelIndex", diag.Hint);
        Assert.AreEqual("channelIndex", diag.Details["parameter"]);
    }

    [TestMethod]
    public void Constructor_WhitespaceEndpoint_NormalizesToNull()
    {
        var diag = new ScanDiagnostic(
            CanErrorCategory.InvalidEndpoint,
            "Bad URI",
            endpoint: "   ");

        Assert.IsNull(diag.Endpoint);
    }
}
