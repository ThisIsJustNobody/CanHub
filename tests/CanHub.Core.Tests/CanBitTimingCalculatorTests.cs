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
        TimeQuantaPerBitMinimum = 8,
        TimeQuantaPerBitMaximum = 25,
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
        TimeQuantaPerBitMinimum = 4,
        TimeQuantaPerBitMaximum = 160,
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

    [TestMethod(DisplayName = "指定采样点时返回多组按误差排序的候选结果")]
    public void CalculateCandidates_WithRequestedSamplePoint_ReturnsOrderedCandidateSet()
    {
        var candidates = CanBitTimingCalculator.CalculateCandidates(
            new CanBitTimingCalculationRequest
            {
                TargetBitrate = 500_000,
                ClockFrequency = 16_000_000,
                ClockDivisor = 2,
                SamplePoint = 0.8,
                PreferredTimeQuantaPerBit = 16,
                SynchronizationJumpWidth = 1,
                MaximumBitrateError = 0,
                Constraints = VectorClassicConstraints,
            },
            maximumResults: 4);

        Assert.AreEqual(4, candidates.Count);
        Assert.AreEqual(1, candidates[0].Prescaler);
        Assert.AreEqual(12, candidates[0].TimeSegment1);
        Assert.AreEqual(3, candidates[0].TimeSegment2);
        Assert.AreEqual(16, candidates[0].TimeQuantaPerBit);
        Assert.AreEqual(0.8125, candidates[0].ActualSamplePoint, 0.000001);

        Assert.AreEqual(1, candidates[1].Prescaler);
        Assert.AreEqual(11, candidates[1].TimeSegment1);
        Assert.AreEqual(4, candidates[1].TimeSegment2);
        Assert.AreEqual(16, candidates[1].TimeQuantaPerBit);

        for (var i = 1; i < candidates.Count; i++)
        {
            Assert.IsTrue(candidates[i - 1].BitrateError <= candidates[i].BitrateError + 0.000001);
            if (Math.Abs(candidates[i - 1].BitrateError - candidates[i].BitrateError) <= 0.000001)
                Assert.IsTrue(candidates[i - 1].SamplePointError <= candidates[i].SamplePointError + 0.000001);
        }
    }

    [TestMethod(DisplayName = "最佳解等于候选列表第一项")]
    public void Calculate_ReturnsFirstCandidate()
    {
        var request = new CanBitTimingCalculationRequest
        {
            TargetBitrate = 500_000,
            ClockFrequency = 16_000_000,
            ClockDivisor = 2,
            SamplePoint = 0.8,
            PreferredTimeQuantaPerBit = 16,
            SynchronizationJumpWidth = 1,
            MaximumBitrateError = 0,
            Constraints = VectorClassicConstraints,
        };

        var best = CanBitTimingCalculator.Calculate(request);
        var candidates = CanBitTimingCalculator.CalculateCandidates(request, maximumResults: 1);

        Assert.AreEqual(best, candidates[0]);
    }

    [TestMethod(DisplayName = "Vector FD 500k/75% 候选包含参考表中的精确匹配行")]
    public void CalculateCandidates_VectorFd500k75Percent_ContainsReferenceRows()
    {
        var candidates = CanBitTimingCalculator.CalculateCandidates(
            new CanBitTimingCalculationRequest
            {
                TargetBitrate = 500_000,
                ClockFrequency = 80_000_000,
                SamplePoint = 0.75,
                MaximumBitrateError = 0,
                Constraints = VectorFdConstraints,
            },
            maximumResults: 64);

        AssertContainsTiming(candidates, btlCycles: 8, tseg1: 5, tseg2: 2, prescaler: 20);
        AssertContainsTiming(candidates, btlCycles: 16, tseg1: 11, tseg2: 4, prescaler: 10);
        AssertContainsTiming(candidates, btlCycles: 20, tseg1: 14, tseg2: 5, prescaler: 8);
        AssertContainsTiming(candidates, btlCycles: 32, tseg1: 23, tseg2: 8, prescaler: 5);
        AssertContainsTiming(candidates, btlCycles: 40, tseg1: 29, tseg2: 10, prescaler: 4);
        AssertContainsTiming(candidates, btlCycles: 80, tseg1: 59, tseg2: 20, prescaler: 2);
        AssertContainsTiming(candidates, btlCycles: 160, tseg1: 119, tseg2: 40, prescaler: 1);
    }

    [TestMethod(DisplayName = "Vector FD 2M/75% 候选不因 SJW 大于 TSEG2 过滤参考行")]
    public void CalculateCandidates_VectorFd2M75Percent_IncludesRowsWithSmallTseg2()
    {
        var candidates = CanBitTimingCalculator.CalculateCandidates(
            new CanBitTimingCalculationRequest
            {
                TargetBitrate = 2_000_000,
                ClockFrequency = 80_000_000,
                SamplePoint = 0.75,
                MaximumBitrateError = 0,
                Constraints = VectorFdConstraints,
            },
            maximumResults: 32);

        AssertContainsTiming(candidates, btlCycles: 4, tseg1: 2, tseg2: 1, prescaler: 10);
        AssertContainsTiming(candidates, btlCycles: 8, tseg1: 5, tseg2: 2, prescaler: 5);
        AssertContainsTiming(candidates, btlCycles: 20, tseg1: 14, tseg2: 5, prescaler: 2);
        AssertContainsTiming(candidates, btlCycles: 40, tseg1: 29, tseg2: 10, prescaler: 1);
    }

    [TestMethod(DisplayName = "候选扫描不使用显式 SJW 过滤时序行")]
    public void CalculateCandidates_ExplicitSjw_DoesNotFilterCandidateRows()
    {
        var candidates = CanBitTimingCalculator.CalculateCandidates(
            new CanBitTimingCalculationRequest
            {
                TargetBitrate = 2_000_000,
                ClockFrequency = 80_000_000,
                SamplePoint = 0.75,
                SynchronizationJumpWidth = 10,
                MaximumBitrateError = 0,
                Constraints = VectorFdConstraints,
            },
            maximumResults: 32);

        AssertContainsTiming(candidates, btlCycles: 4, tseg1: 2, tseg2: 1, prescaler: 10);
    }

    [TestMethod(DisplayName = "Vector FD 2M/80% 候选包含参考表中的精确匹配行")]
    public void CalculateCandidates_VectorFd2M80Percent_ContainsReferenceRows()
    {
        var candidates = CanBitTimingCalculator.CalculateCandidates(
            new CanBitTimingCalculationRequest
            {
                TargetBitrate = 2_000_000,
                ClockFrequency = 80_000_000,
                SamplePoint = 0.8,
                MaximumBitrateError = 0,
                Constraints = VectorFdConstraints,
            },
            maximumResults: 32);

        AssertContainsTiming(candidates, btlCycles: 5, tseg1: 3, tseg2: 1, prescaler: 8);
        AssertContainsTiming(candidates, btlCycles: 10, tseg1: 7, tseg2: 2, prescaler: 4);
        AssertContainsTiming(candidates, btlCycles: 40, tseg1: 31, tseg2: 8, prescaler: 1);
    }

    [TestMethod(DisplayName = "Vector FD 500k/80% 候选包含参考表中的精确匹配行")]
    public void CalculateCandidates_VectorFd500k80Percent_ContainsReferenceRows()
    {
        var candidates = CanBitTimingCalculator.CalculateCandidates(
            new CanBitTimingCalculationRequest
            {
                TargetBitrate = 500_000,
                ClockFrequency = 80_000_000,
                SamplePoint = 0.8,
                MaximumBitrateError = 0,
                Constraints = VectorFdConstraints,
            },
            maximumResults: 64);

        AssertContainsTiming(candidates, btlCycles: 5, tseg1: 3, tseg2: 1, prescaler: 32);
        AssertContainsTiming(candidates, btlCycles: 160, tseg1: 127, tseg2: 32, prescaler: 1);
    }

    [TestMethod(DisplayName = "Vector FD 500k/87.5% 候选包含物理公式一致的参考行")]
    public void CalculateCandidates_VectorFd500k875Percent_ContainsReferenceRows()
    {
        var candidates = CanBitTimingCalculator.CalculateCandidates(
            new CanBitTimingCalculationRequest
            {
                TargetBitrate = 500_000,
                ClockFrequency = 80_000_000,
                SamplePoint = 0.875,
                MaximumBitrateError = 0,
                Constraints = VectorFdConstraints,
            },
            maximumResults: 64);

        AssertContainsTiming(candidates, btlCycles: 8, tseg1: 6, tseg2: 1, prescaler: 20);
        AssertContainsTiming(candidates, btlCycles: 16, tseg1: 13, tseg2: 2, prescaler: 10);
        AssertContainsTiming(candidates, btlCycles: 80, tseg1: 69, tseg2: 10, prescaler: 2);
        AssertContainsTiming(candidates, btlCycles: 160, tseg1: 139, tseg2: 20, prescaler: 1);
    }

    [TestMethod(DisplayName = "Vector FD 500k/96.3% 候选包含参考表中的精确匹配行")]
    public void CalculateCandidates_VectorFd500k963Percent_ContainsReferenceRows()
    {
        var candidates = CanBitTimingCalculator.CalculateCandidates(
            new CanBitTimingCalculationRequest
            {
                TargetBitrate = 500_000,
                ClockFrequency = 80_000_000,
                SamplePoint = 0.963,
                MaximumBitrateError = 0,
                Constraints = VectorFdConstraints,
            },
            maximumResults: 64);

        AssertContainsTiming(candidates, btlCycles: 80, tseg1: 76, tseg2: 3, prescaler: 2);
        AssertContainsTiming(candidates, btlCycles: 160, tseg1: 153, tseg2: 6, prescaler: 1);
    }

    [TestMethod(DisplayName = "Vector FD 2M/52.5% 候选包含参考表中的精确匹配行")]
    public void CalculateCandidates_VectorFd2M525Percent_ContainsReferenceRows()
    {
        var candidates = CanBitTimingCalculator.CalculateCandidates(
            new CanBitTimingCalculationRequest
            {
                TargetBitrate = 2_000_000,
                ClockFrequency = 80_000_000,
                SamplePoint = 0.525,
                MaximumBitrateError = 0,
                Constraints = VectorFdConstraints,
            },
            maximumResults: 32);

        AssertContainsTiming(candidates, btlCycles: 40, tseg1: 20, tseg2: 19, prescaler: 1);
    }

    [TestMethod(DisplayName = "Vector Classic 500k/50% 候选匹配 BTR 表且排除 4 TQ")]
    public void CalculateCandidates_VectorClassic500k50Percent_MatchesBtrRows()
    {
        var candidates = CanBitTimingCalculator.CalculateCandidates(
            new CanBitTimingCalculationRequest
            {
                TargetBitrate = 500_000,
                ClockFrequency = 16_000_000,
                ClockDivisor = 2,
                SamplePoint = 0.5,
                SynchronizationJumpWidth = 1,
                MaximumBitrateError = 0,
                Constraints = VectorClassicConstraints,
            },
            maximumResults: 16);

        AssertContainsTiming(candidates, btlCycles: 8, tseg1: 3, tseg2: 4, prescaler: 2);
        AssertContainsTiming(candidates, btlCycles: 16, tseg1: 7, tseg2: 8, prescaler: 1);
        Assert.IsFalse(candidates.Any(static candidate => candidate.TimeQuantaPerBit == 4));
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

    private static void AssertContainsTiming(
        IReadOnlyList<CanBitTimingCalculationResult> candidates,
        int btlCycles,
        int tseg1,
        int tseg2,
        int prescaler)
    {
        Assert.IsTrue(
            candidates.Any(candidate =>
                candidate.TimeQuantaPerBit == btlCycles &&
                candidate.TimeSegment1 == tseg1 &&
                candidate.TimeSegment2 == tseg2 &&
                candidate.Prescaler == prescaler),
            $"Expected timing BTL={btlCycles}, TSEG1={tseg1}, TSEG2={tseg2}, Prescaler={prescaler}.");
    }
}
