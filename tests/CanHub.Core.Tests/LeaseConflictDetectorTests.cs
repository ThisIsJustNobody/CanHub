using CanHub;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Core.Tests;

[TestClass]
public sealed class LeaseConflictDetectorTests
{
    private sealed class FingerprintOptions(string value) : ICanNativeOptionsFingerprint
    {
        public string GetFingerprint() => $"value={value}";
    }

    [TestMethod(DisplayName = "同一端点 + 相同 CanBusParameters + 相同 NativeOptions 指纹相同")]
    public void ComputeFingerprint_SameConfig_SameFingerprint()
    {
        var ep = CanEndpoint.Parse("vector://VN1630A?channel=0");
        var opts = new CanOpenOptions
        {
            BusParameters = CanBusParameters.Classic500k,
            NativeOptions = "test",
        };

        var fp1 = LeaseConflictDetector.ComputeFingerprint(ep, opts);
        var fp2 = LeaseConflictDetector.ComputeFingerprint(ep, opts);

        Assert.IsTrue(LeaseConflictDetector.FingerprintsMatch(fp1, fp2));
    }

    [TestMethod(DisplayName = "不同 CanBusParameters 指纹不同")]
    public void ComputeFingerprint_DifferentBusParams_DifferentFingerprint()
    {
        var ep = CanEndpoint.Parse("vector://VN1630A?channel=0");
        var opts1 = new CanOpenOptions { BusParameters = CanBusParameters.Classic500k };
        var opts2 = new CanOpenOptions { BusParameters = CanBusParameters.Fd500k2M };

        var fp1 = LeaseConflictDetector.ComputeFingerprint(ep, opts1);
        var fp2 = LeaseConflictDetector.ComputeFingerprint(ep, opts2);

        Assert.IsFalse(LeaseConflictDetector.FingerprintsMatch(fp1, fp2));
    }

    [TestMethod(DisplayName = "不同 NativeOptions 指纹不同")]
    public void ComputeFingerprint_DifferentNativeOptions_DifferentFingerprint()
    {
        var ep = CanEndpoint.Parse("vector://VN1630A?channel=0");
        var opts1 = new CanOpenOptions { NativeOptions = "configA" };
        var opts2 = new CanOpenOptions { NativeOptions = "configB" };

        var fp1 = LeaseConflictDetector.ComputeFingerprint(ep, opts1);
        var fp2 = LeaseConflictDetector.ComputeFingerprint(ep, opts2);

        Assert.IsFalse(LeaseConflictDetector.FingerprintsMatch(fp1, fp2));
    }

    [TestMethod(DisplayName = "Endpoint fd/arb/data 不同不影响指纹")]
    public void ComputeFingerprint_DifferentEndpointBusParams_SameFingerprint()
    {
        var ep1 = CanEndpoint.Parse("vector://VN1630A?channel=0&fd=true&arb=500000&data=2000000");
        var ep2 = CanEndpoint.Parse("vector://VN1630A?channel=0&fd=false&arb=250000");
        var opts = new CanOpenOptions();

        var fp1 = LeaseConflictDetector.ComputeFingerprint(ep1, opts);
        var fp2 = LeaseConflictDetector.ComputeFingerprint(ep2, opts);

        Assert.IsTrue(LeaseConflictDetector.FingerprintsMatch(fp1, fp2));
    }

    [TestMethod(DisplayName = "Endpoint channel 不同指纹不同")]
    public void ComputeFingerprint_DifferentChannel_DifferentFingerprint()
    {
        var ep1 = CanEndpoint.Parse("vector://VN1630A?channel=0");
        var ep2 = CanEndpoint.Parse("vector://VN1630A?channel=1");
        var opts = new CanOpenOptions();

        var fp1 = LeaseConflictDetector.ComputeFingerprint(ep1, opts);
        var fp2 = LeaseConflictDetector.ComputeFingerprint(ep2, opts);

        Assert.IsFalse(LeaseConflictDetector.FingerprintsMatch(fp1, fp2));
    }

    [TestMethod(DisplayName = "null NativeOptions 与 非null NativeOptions 指纹不同")]
    public void ComputeFingerprint_NullVsNonNullNativeOptions_Different()
    {
        var ep = CanEndpoint.Parse("vector://VN1630A?channel=0");
        var opts1 = new CanOpenOptions();
        var opts2 = new CanOpenOptions { NativeOptions = "something" };

        var fp1 = LeaseConflictDetector.ComputeFingerprint(ep, opts1);
        var fp2 = LeaseConflictDetector.ComputeFingerprint(ep, opts2);

        Assert.IsFalse(LeaseConflictDetector.FingerprintsMatch(fp1, fp2));
    }

    [TestMethod(DisplayName = "UnsupportedParameterPolicy 不同指纹不同")]
    public void ComputeFingerprint_DifferentUnsupportedParameterPolicy_DifferentFingerprint()
    {
        var ep = CanEndpoint.Parse("vector://VN1630A?channel=0");
        var opts1 = new CanOpenOptions
        {
            BusParameters = new CanBusParameters
            {
                ArbitrationBitrate = 500_000,
                TerminationEnabled = true,
                UnsupportedParameterPolicy = CanBusParameterPolicy.Request,
            },
        };
        var opts2 = new CanOpenOptions
        {
            BusParameters = new CanBusParameters
            {
                ArbitrationBitrate = 500_000,
                TerminationEnabled = true,
                UnsupportedParameterPolicy = CanBusParameterPolicy.Require,
            },
        };

        var fp1 = LeaseConflictDetector.ComputeFingerprint(ep, opts1);
        var fp2 = LeaseConflictDetector.ComputeFingerprint(ep, opts2);

        Assert.IsFalse(LeaseConflictDetector.FingerprintsMatch(fp1, fp2));
    }

    [TestMethod(DisplayName = "结构化NativeOptions内容不同指纹不同")]
    public void ComputeFingerprint_DifferentDictionaryNativeOptions_DifferentFingerprint()
    {
        var ep = CanEndpoint.Parse("vector://VN1630A?channel=0");
        var opts1 = new CanOpenOptions { NativeOptions = new Dictionary<string, string> { ["mode"] = "a" } };
        var opts2 = new CanOpenOptions { NativeOptions = new Dictionary<string, string> { ["mode"] = "b" } };

        var fp1 = LeaseConflictDetector.ComputeFingerprint(ep, opts1);
        var fp2 = LeaseConflictDetector.ComputeFingerprint(ep, opts2);

        Assert.IsFalse(LeaseConflictDetector.FingerprintsMatch(fp1, fp2));
    }

    [TestMethod(DisplayName = "实现指纹接口的NativeOptions使用稳定文本")]
    public void ComputeFingerprint_FingerprintInterface_UsesStableContent()
    {
        var ep = CanEndpoint.Parse("vector://VN1630A?channel=0");
        var opts1 = new CanOpenOptions { NativeOptions = new FingerprintOptions("a") };
        var opts2 = new CanOpenOptions { NativeOptions = new FingerprintOptions("a") };

        var fp1 = LeaseConflictDetector.ComputeFingerprint(ep, opts1);
        var fp2 = LeaseConflictDetector.ComputeFingerprint(ep, opts2);

        Assert.IsTrue(LeaseConflictDetector.FingerprintsMatch(fp1, fp2));
    }

    [TestMethod(DisplayName = "未指定channel与channel=0指纹相同")]
    public void ComputeFingerprint_NullChannelAndZeroChannel_SameFingerprint()
    {
        var ep1 = CanEndpoint.Parse("vector://VN1630A");
        var ep2 = CanEndpoint.Parse("vector://VN1630A?channel=0");
        var opts = new CanOpenOptions();

        var fp1 = LeaseConflictDetector.ComputeFingerprint(ep1, opts);
        var fp2 = LeaseConflictDetector.ComputeFingerprint(ep2, opts);

        Assert.IsTrue(LeaseConflictDetector.FingerprintsMatch(fp1, fp2));
    }
}
