using CanHub.Core;

namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// CAN 总线波特率 → BTR 寄存器参数计算。<br/>
/// CAN bus bitrate to BTR register parameter calculation.
/// </summary>
internal static class VectorBusParameterCalculator
{
    private const double DefaultSamplePoint = 0.75;
    private const int ClassicClockFrequency = 16_000_000;
    private const int FdClockFrequency = 80_000_000;
    private const int FdArbitrationPreferredBtlCycles = 8;
    private const int FdDataPreferredBtlCycles = 40;
    private static readonly CanBitTimingConstraints s_classicConstraints = new()
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

    private static readonly CanBitTimingConstraints s_fdConstraints = new()
    {
        PrescalerMinimum = 1,
        PrescalerMaximum = 64,
        PrescalerIncrement = 1,
        TimeQuantaPerBitMinimum = 4,
        TimeSegment1Minimum = 1,
        TimeSegment1Maximum = 254,
        TimeSegment2Minimum = 1,
        TimeSegment2Maximum = 254,
        SynchronizationJumpWidthMaximum = 128,
    };

    /// <summary>
    /// 计算经典 CAN 位时序参数 (BRP, Tseg1, Tseg2, Sjw)，采样点 75%。<br/>
    /// Calculates classic CAN bit timing parameters (BRP, Tseg1, Tseg2, Sjw) at 75% sample point.
    /// </summary>
    public static (int Brp, int Tseg1, int Tseg2, int Sjw) CalculateClassicBitTiming(
        int targetBitrate, int clockFrequency = ClassicClockFrequency)
    {
        return CalculateVectorTiming(
            targetBitrate,
            clockFrequency,
            clockDivisor: 2,
            constraints: s_classicConstraints,
            preferredBtlCycles: 16);
    }

    /// <summary>
    /// 计算 CAN FD 仲裁段位时序参数，时钟 80 MHz，目标 8 BTL 周期。<br/>
    /// Calculates CAN FD arbitration bit timing at 80 MHz clock, targeting 8 BTL cycles.
    /// </summary>
    public static (int Brp, int Tseg1, int Tseg2, int Sjw) CalculateFdArbitrationBitTiming(
        int targetBitrate, int clockFrequency = FdClockFrequency)
    {
        return CalculateFdBitTiming(targetBitrate, clockFrequency, FdArbitrationPreferredBtlCycles);
    }

    /// <summary>
    /// 计算 CAN FD 数据段位时序参数，时钟 80 MHz，目标 40 BTL 周期。<br/>
    /// Calculates CAN FD data bit timing at 80 MHz clock, targeting 40 BTL cycles.
    /// </summary>
    public static (int Brp, int Tseg1, int Tseg2, int Sjw) CalculateFdDataBitTiming(
        int targetBitrate, int clockFrequency = FdClockFrequency)
    {
        return CalculateFdBitTiming(targetBitrate, clockFrequency, FdDataPreferredBtlCycles);
    }

    /// <summary>
    /// 计算 CAN FD 位时序参数（通用）。<br/>
    /// Calculates CAN FD bit timing parameters (generic).
    /// </summary>
    public static (int Brp, int Tseg1, int Tseg2, int Sjw) CalculateFdBitTiming(
        int targetBitrate,
        int clockFrequency = FdClockFrequency,
        int preferredBtlCycles = FdDataPreferredBtlCycles)
    {
        return CalculateVectorTiming(
            targetBitrate,
            clockFrequency,
            clockDivisor: 1,
            constraints: s_fdConstraints,
            preferredBtlCycles);
    }

    /// <summary>
    /// 使用 Core 通用计算器和 Vector 控制器约束计算位时序。<br/>
    /// Calculates bit timing through the Core generic calculator and Vector controller constraints.
    /// </summary>
    private static (int Brp, int Tseg1, int Tseg2, int Sjw) CalculateVectorTiming(
        int targetBitrate,
        int clockFrequency,
        int clockDivisor,
        CanBitTimingConstraints constraints,
        int preferredBtlCycles)
    {
        try
        {
            var timing = CanBitTimingCalculator.Calculate(new CanBitTimingCalculationRequest
            {
                TargetBitrate = targetBitrate,
                ClockFrequency = clockFrequency,
                ClockDivisor = clockDivisor,
                SamplePoint = DefaultSamplePoint,
                PreferredTimeQuantaPerBit = preferredBtlCycles,
                SynchronizationJumpWidth = 1,
                MaximumBitrateError = 0,
                Constraints = constraints,
            });

            return (timing.Prescaler, timing.TimeSegment1, timing.TimeSegment2, timing.SynchronizationJumpWidth);
        }
        catch (CanException ex) when (ex.Category == CanErrorCategory.ConfigurationConflict)
        {
            throw new CanException("vector", CanErrorCategory.ConfigurationConflict,
                $"Vector adapter cannot calculate bit timing for bitrate {targetBitrate} with clock {clockFrequency}.",
                ex);
        }
    }

    /// <summary>
    /// 判断波特率是否在 Vector 适配器支持的范围内。
    /// Classic: 5k-1M，FD: 100k-12M。<br/>
    /// Checks whether the bitrate is within the range supported by the Vector adapter.
    /// Classic: 5k-1M, FD: 100k-12M.
    /// </summary>
    public static bool IsValidBitrate(int bitrate, bool isFd)
    {
        if (bitrate <= 0) return false;
        if (isFd)
            return bitrate >= 100_000 && bitrate <= 12_000_000;
        else
            return bitrate >= 5_000 && bitrate <= 1_000_000;
    }
}
