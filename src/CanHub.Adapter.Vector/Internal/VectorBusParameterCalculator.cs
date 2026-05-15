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

    /// <summary>
    /// 计算经典 CAN 位时序参数 (BRP, Tseg1, Tseg2, Sjw)，采样点 75%。<br/>
    /// Calculates classic CAN bit timing parameters (BRP, Tseg1, Tseg2, Sjw) at 75% sample point.
    /// </summary>
    public static (int Brp, int Tseg1, int Tseg2, int Sjw) CalculateClassicBitTiming(
        int targetBitrate, int clockFrequency = ClassicClockFrequency)
    {
        return CalculateBySamplePoint(
            targetBitrate,
            clockFrequency,
            divisorFactor: 2,
            maxPrescaler: 64,
            maxTseg1: 16,
            maxTseg2: 8,
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
        return CalculateBySamplePoint(
            targetBitrate,
            clockFrequency,
            divisorFactor: 1,
            maxPrescaler: 64,
            maxTseg1: 254,
            maxTseg2: 254,
            preferredBtlCycles);
    }

    /// <summary>
    /// 按采样点模型计算位时序。穷举预分频器值，选择采样点误差最小且 BTL 周期数最接近目标的配置。<br/>
    /// Calculates bit timing by sample point model. Exhausts prescaler values and selects
    /// the configuration with minimal sample point error and BTL cycles closest to target.
    /// </summary>
    private static (int Brp, int Tseg1, int Tseg2, int Sjw) CalculateBySamplePoint(
        int targetBitrate,
        int clockFrequency,
        int divisorFactor,
        int maxPrescaler,
        int maxTseg1,
        int maxTseg2,
        int preferredBtlCycles)
    {
        if (targetBitrate <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetBitrate));
        if (clockFrequency <= 0)
            throw new ArgumentOutOfRangeException(nameof(clockFrequency));

        (int Brp, int Tseg1, int Tseg2, int Sjw, double SamplePointError, int BtlCycles)? best = null;

        for (var prescaler = 1; prescaler <= maxPrescaler; prescaler++)
        {
            var denominator = targetBitrate * prescaler * divisorFactor;
            if (clockFrequency % denominator != 0)
                continue;

            var btlCycles = clockFrequency / denominator;
            if (btlCycles < 4)
                continue;

            var tseg1 = (int)(btlCycles * DefaultSamplePoint - 1);
            var tseg2 = btlCycles - 1 - tseg1;
            if (tseg1 <= 1 || tseg1 > maxTseg1 || tseg2 <= 1 || tseg2 > maxTseg2)
                continue;

            var actualSamplePoint = (1 + tseg1) / (double)btlCycles;
            var samplePointError = Math.Abs(actualSamplePoint - DefaultSamplePoint);
            var candidate = (prescaler, tseg1, tseg2, Sjw: 1, samplePointError, btlCycles);

            if (best is null ||
                candidate.samplePointError < best.Value.SamplePointError ||
                (candidate.samplePointError == best.Value.SamplePointError &&
                 Math.Abs(candidate.btlCycles - preferredBtlCycles) < Math.Abs(best.Value.BtlCycles - preferredBtlCycles)))
            {
                best = candidate;
            }
        }

        if (best is { } timing)
            return (timing.Brp, timing.Tseg1, timing.Tseg2, timing.Sjw);

        throw new CanException("vector", CanErrorCategory.ConfigurationConflict,
            $"Vector adapter cannot calculate bit timing for bitrate {targetBitrate} with clock {clockFrequency}.");
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
