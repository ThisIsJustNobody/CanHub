using CanHub.Adapter.Vector.Internal;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
public sealed class VectorBusParameterCalculatorTests
{
    [TestMethod]
    public void CalculateClassicBitTiming_500Kbps()
    {
        var (brp, tseg1, tseg2, sjw) = VectorBusParameterCalculator.CalculateClassicBitTiming(500_000);
        Assert.AreEqual(1, brp);
        Assert.AreEqual(11, tseg1);
        Assert.AreEqual(4, tseg2);
        Assert.AreEqual(1, sjw);
        Assert.AreEqual(16, 1 + tseg1 + tseg2);
    }

    [TestMethod]
    public void CalculateClassicBitTiming_250Kbps()
    {
        var (brp, tseg1, tseg2, sjw) = VectorBusParameterCalculator.CalculateClassicBitTiming(250_000);
        Assert.IsTrue(brp > 0);
        Assert.IsTrue(tseg1 >= 1 && tseg1 <= 16);
        Assert.IsTrue(tseg2 >= 1 && tseg2 <= 8);
        var totalTq = 1 + tseg1 + tseg2;
        Assert.IsTrue(totalTq >= 8 && totalTq <= 25);
    }

    [TestMethod]
    public void CalculateFdBitTiming_2Mbps()
    {
        var (brp, tseg1, tseg2, sjw) = VectorBusParameterCalculator.CalculateFdDataBitTiming(2_000_000);
        Assert.AreEqual(1, brp);
        Assert.AreEqual(29, tseg1);
        Assert.AreEqual(10, tseg2);
        Assert.AreEqual(1, sjw);
    }

    [TestMethod]
    public void CalculateFdArbitrationBitTiming_500Kbps()
    {
        var (brp, tseg1, tseg2, sjw) = VectorBusParameterCalculator.CalculateFdArbitrationBitTiming(500_000);
        Assert.AreEqual(20, brp);
        Assert.AreEqual(5, tseg1);
        Assert.AreEqual(2, tseg2);
        Assert.AreEqual(1, sjw);
    }

    [TestMethod]
    public void CreateClassicChipParams_UsesCalculatedDefaults()
    {
        var busParams = new CanBusParameters { ArbitrationBitrate = 250_000 };
        var (_, expectedTseg1, expectedTseg2, expectedSjw) =
            VectorBusParameterCalculator.CalculateClassicBitTiming(busParams.ArbitrationBitrate);

        var chipParams = VectorChannelPort.CreateClassicChipParams(busParams, vectorOptions: null);

        Assert.AreEqual((uint)busParams.ArbitrationBitrate, chipParams.bitrate);
        Assert.AreEqual((byte)expectedTseg1, chipParams.tseg1);
        Assert.AreEqual((byte)expectedTseg2, chipParams.tseg2);
        Assert.AreEqual((byte)expectedSjw, chipParams.sjw);
    }

    [TestMethod]
    public void CreateClassicChipParams_ExplicitTimingOverridesCalculatedDefaults()
    {
        var busParams = new CanBusParameters
        {
            ArbitrationBitrate = 500_000,
            ArbitrationTseg1 = 10,
            ArbitrationTseg2 = 3,
            ArbitrationSjw = 2,
        };

        var chipParams = VectorChannelPort.CreateClassicChipParams(busParams, vectorOptions: null);

        Assert.AreEqual((byte)10, chipParams.tseg1);
        Assert.AreEqual((byte)3, chipParams.tseg2);
        Assert.AreEqual((byte)2, chipParams.sjw);
    }

    [TestMethod]
    public void CreateCanFdConfiguration_Fd500k2M_UsesVectorCompatibleDefaults()
    {
        var fdConfig = VectorChannelPort.CreateCanFdConfiguration(CanBusParameters.Fd500k2M);

        Assert.AreEqual(500_000u, fdConfig.arbitrationBitRate);
        Assert.AreEqual(1u, fdConfig.sjwAbr);
        Assert.AreEqual(5u, fdConfig.tseg1Abr);
        Assert.AreEqual(2u, fdConfig.tseg2Abr);
        Assert.AreEqual(2_000_000u, fdConfig.dataBitRate);
        Assert.AreEqual(1u, fdConfig.sjwDbr);
        Assert.AreEqual(29u, fdConfig.tseg1Dbr);
        Assert.AreEqual(10u, fdConfig.tseg2Dbr);
    }

    [TestMethod]
    public void CreateCanFdConfiguration_UsesCalculatedDefaults()
    {
        var busParams = new CanBusParameters
        {
            IsFd = true,
            ArbitrationBitrate = 500_000,
            DataBitrate = 4_000_000,
        };
        var (_, expectedArbTseg1, expectedArbTseg2, expectedArbSjw) =
            VectorBusParameterCalculator.CalculateFdArbitrationBitTiming(busParams.ArbitrationBitrate);
        var (_, expectedDataTseg1, expectedDataTseg2, expectedDataSjw) =
            VectorBusParameterCalculator.CalculateFdDataBitTiming(busParams.DataBitrate.Value);

        var fdConfig = VectorChannelPort.CreateCanFdConfiguration(busParams);

        Assert.AreEqual((uint)busParams.ArbitrationBitrate, fdConfig.arbitrationBitRate);
        Assert.AreEqual((uint)expectedArbTseg1, fdConfig.tseg1Abr);
        Assert.AreEqual((uint)expectedArbTseg2, fdConfig.tseg2Abr);
        Assert.AreEqual((uint)expectedArbSjw, fdConfig.sjwAbr);
        Assert.AreEqual((uint)busParams.DataBitrate.Value, fdConfig.dataBitRate);
        Assert.AreEqual((uint)expectedDataTseg1, fdConfig.tseg1Dbr);
        Assert.AreEqual((uint)expectedDataTseg2, fdConfig.tseg2Dbr);
        Assert.AreEqual((uint)expectedDataSjw, fdConfig.sjwDbr);
    }

    [TestMethod]
    public void IsValidBitrate_ClassicTrueCases()
    {
        Assert.IsTrue(VectorBusParameterCalculator.IsValidBitrate(125_000, false));
        Assert.IsTrue(VectorBusParameterCalculator.IsValidBitrate(250_000, false));
        Assert.IsTrue(VectorBusParameterCalculator.IsValidBitrate(500_000, false));
        Assert.IsTrue(VectorBusParameterCalculator.IsValidBitrate(1_000_000, false));
    }

    [TestMethod]
    public void IsValidBitrate_InvalidBitrate_ReturnsFalse()
    {
        Assert.IsFalse(VectorBusParameterCalculator.IsValidBitrate(0, false));
        Assert.IsFalse(VectorBusParameterCalculator.IsValidBitrate(-1, false));
    }
}
