using CanHub.Core;

namespace CanHub.Core.Tests;

[TestClass]
public sealed class CanBitTimingCalculatorTests
{
    private static readonly CanBitTimingConstraints VectorClassicConstraints = new()
    {
        PrescalerMinimum = 1,
        PrescalerMaximum = 64,
        PrescalerIncrement = 1,
        TimeSegment1Minimum = 1,
        TimeSegment1Maximum = 16,
        TimeSegment2Minimum = 1,
        TimeSegment2Maximum = 8,
        SynchronizationJumpWidthMaximum = 4,
    };

    private static readonly CanBitTimingConstraints VectorFdConstraints = new()
    {
        PrescalerMinimum = 1,
        PrescalerMaximum = 64,
        PrescalerIncrement = 1,
        TimeSegment1Minimum = 1,
        TimeSegment1Maximum = 254,
        TimeSegment2Minimum = 1,
        TimeSegment2Maximum = 254,
        SynchronizationJumpWidthMaximum = 128,
    };

    [TestMethod(DisplayName = "按控制器约束计算 Vector Classic 500k 的既有时序")]
    public void Calculate_VectorClassic500k_ReturnsExistingTiming()
    {
        var timing = CanBitTimingCalculator.Calculate(new CanBitTimingCalculationRequest
        {
            TargetBitrate = 500_000,
            ClockFrequency = 16_000_000,
            ClockDivisor = 2,
            SamplePoint = 0.75,
            PreferredTimeQuantaPerBit = 16,
            SynchronizationJumpWidth = 1,
            MaximumBitrateError = 0,
            Constraints = VectorClassicConstraints,
        });

        Assert.AreEqual(1, timing.Prescaler);
        Assert.AreEqual(11, timing.TimeSegment1);
        Assert.AreEqual(4, timing.TimeSegment2);
        Assert.AreEqual(1, timing.SynchronizationJumpWidth);
        Assert.AreEqual(16, timing.TimeQuantaPerBit);
        Assert.AreEqual(500_000, timing.ActualBitrate);
        Assert.AreEqual(0.75, timing.ActualSamplePoint, 0.000001);
    }

    [TestMethod(DisplayName = "同一计算器可用于 CAN FD 数据段 2M")]
    public void Calculate_VectorFdData2M_ReturnsExistingTiming()
    {
        var timing = CanBitTimingCalculator.Calculate(new CanBitTimingCalculationRequest
        {
            TargetBitrate = 2_000_000,
            ClockFrequency = 80_000_000,
            SamplePoint = 0.75,
            PreferredTimeQuantaPerBit = 40,
            SynchronizationJumpWidth = 1,
            MaximumBitrateError = 0,
            Constraints = VectorFdConstraints,
        });

        Assert.AreEqual(1, timing.Prescaler);
        Assert.AreEqual(29, timing.TimeSegment1);
        Assert.AreEqual(10, timing.TimeSegment2);
        Assert.AreEqual(1, timing.SynchronizationJumpWidth);
        Assert.AreEqual(40, timing.TimeQuantaPerBit);
        Assert.AreEqual(2_000_000, timing.ActualBitrate);
    }

    [TestMethod(DisplayName = "未指定采样点时使用 CiA 推荐 NRZ 采样点")]
    public void GetRecommendedNrzSamplePoint_UsesCiAProfile()
    {
        Assert.AreEqual(0.875, CanBitTimingCalculator.GetRecommendedNrzSamplePoint(500_000), 0.000001);
        Assert.AreEqual(0.8, CanBitTimingCalculator.GetRecommendedNrzSamplePoint(800_000), 0.000001);
        Assert.AreEqual(0.75, CanBitTimingCalculator.GetRecommendedNrzSamplePoint(1_000_000), 0.000001);
    }

    [TestMethod(DisplayName = "非整除目标波特率可按误差容忍选择最近解")]
    public void Calculate_NonExactBitrateWithinTolerance_ReturnsNearestTiming()
    {
        var timing = CanBitTimingCalculator.Calculate(new CanBitTimingCalculationRequest
        {
            TargetBitrate = 333_000,
            ClockFrequency = 16_000_000,
            SamplePoint = 0.875,
            MaximumBitrateError = 0.005,
            Constraints = new CanBitTimingConstraints
            {
                PrescalerMinimum = 1,
                PrescalerMaximum = 64,
                PrescalerIncrement = 1,
                TimeSegment1Minimum = 1,
                TimeSegment1Maximum = 16,
                TimeSegment2Minimum = 1,
                TimeSegment2Maximum = 8,
                SynchronizationJumpWidthMaximum = 4,
            },
        });

        Assert.AreEqual(333_333, timing.ActualBitrate);
        Assert.IsTrue(timing.BitrateError <= 0.005);
        Assert.AreEqual(0.875, timing.RequestedSamplePoint, 0.000001);
    }

    [TestMethod(DisplayName = "默认 SJW 不超过 TSEG1、TSEG2 和控制器上限")]
    public void Calculate_DefaultSynchronizationJumpWidth_ClampsToSafeRange()
    {
        var timing = CanBitTimingCalculator.Calculate(new CanBitTimingCalculationRequest
        {
            TargetBitrate = 2_000_000,
            ClockFrequency = 80_000_000,
            SamplePoint = 0.75,
            PreferredTimeQuantaPerBit = 40,
            MaximumBitrateError = 0,
            Constraints = VectorFdConstraints,
        });

        Assert.AreEqual(5, timing.SynchronizationJumpWidth);
    }

    [TestMethod(DisplayName = "超过允许误差时抛出配置冲突")]
    public void Calculate_NoTimingWithinTolerance_ThrowsCanException()
    {
        try
        {
            CanBitTimingCalculator.Calculate(new CanBitTimingCalculationRequest
            {
                TargetBitrate = 123_456,
                ClockFrequency = 16_000_000,
                SamplePoint = 0.875,
                MaximumBitrateError = 0,
                Constraints = VectorClassicConstraints,
            });
            Assert.Fail("Expected CanException.");
        }
        catch (CanException ex)
        {
            Assert.AreEqual(CanErrorCategory.ConfigurationConflict, ex.Category);
        }
    }
}
