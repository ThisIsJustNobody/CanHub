namespace CanHub.Core;

/// <summary>
/// CAN 位时序硬件约束。不同控制器通过这些范围描述 BRP、TSEG 和 SJW 的可编程能力。<br/>
/// CAN bit timing hardware constraints. Different controllers describe their programmable BRP,
/// TSEG, and SJW capabilities through these ranges.
/// </summary>
public sealed class CanBitTimingConstraints
{
    /// <summary>同步段长度（Tq），经典 CAN/CAN FD 通常为 1。<br/>Synchronization segment length in Tq; usually 1 for Classic CAN/CAN FD.</summary>
    public int SynchronizationSegment { get; init; } = 1;

    /// <summary>BRP 最小值。<br/>Minimum bitrate prescaler.</summary>
    public int PrescalerMinimum { get; init; } = 1;

    /// <summary>BRP 最大值。<br/>Maximum bitrate prescaler.</summary>
    public int PrescalerMaximum { get; init; } = 1;

    /// <summary>BRP 步进。<br/>Bitrate prescaler increment.</summary>
    public int PrescalerIncrement { get; init; } = 1;

    /// <summary>TSEG1 最小值（Tq）。<br/>Minimum TSEG1 in Tq.</summary>
    public int TimeSegment1Minimum { get; init; } = 1;

    /// <summary>TSEG1 最大值（Tq）。<br/>Maximum TSEG1 in Tq.</summary>
    public int TimeSegment1Maximum { get; init; } = 1;

    /// <summary>TSEG2 最小值（Tq）。<br/>Minimum TSEG2 in Tq.</summary>
    public int TimeSegment2Minimum { get; init; } = 1;

    /// <summary>TSEG2 最大值（Tq）。<br/>Maximum TSEG2 in Tq.</summary>
    public int TimeSegment2Maximum { get; init; } = 1;

    /// <summary>SJW 最小值（Tq）。<br/>Minimum synchronization jump width in Tq.</summary>
    public int SynchronizationJumpWidthMinimum { get; init; } = 1;

    /// <summary>SJW 最大值（Tq）。<br/>Maximum synchronization jump width in Tq.</summary>
    public int SynchronizationJumpWidthMaximum { get; init; } = 1;

    internal void Validate()
    {
        ValidatePositive(SynchronizationSegment, nameof(SynchronizationSegment));
        ValidateRange(PrescalerMinimum, PrescalerMaximum, nameof(PrescalerMinimum), nameof(PrescalerMaximum));
        ValidatePositive(PrescalerIncrement, nameof(PrescalerIncrement));
        ValidateRange(TimeSegment1Minimum, TimeSegment1Maximum, nameof(TimeSegment1Minimum), nameof(TimeSegment1Maximum));
        ValidateRange(TimeSegment2Minimum, TimeSegment2Maximum, nameof(TimeSegment2Minimum), nameof(TimeSegment2Maximum));
        ValidateRange(SynchronizationJumpWidthMinimum, SynchronizationJumpWidthMaximum,
            nameof(SynchronizationJumpWidthMinimum), nameof(SynchronizationJumpWidthMaximum));
    }

    private static void ValidatePositive(int value, string paramName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(paramName, value, "CAN bit timing constraints must be positive.");
    }

    private static void ValidateRange(int minimum, int maximum, string minimumName, string maximumName)
    {
        ValidatePositive(minimum, minimumName);
        ValidatePositive(maximum, maximumName);
        if (minimum > maximum)
            throw new ArgumentException($"'{minimumName}' must be less than or equal to '{maximumName}'.");
    }
}

/// <summary>
/// CAN 位时序计算请求。<br/>CAN bit timing calculation request.
/// </summary>
public sealed class CanBitTimingCalculationRequest
{
    /// <summary>目标波特率（bps）。<br/>Target bitrate in bits per second.</summary>
    public int TargetBitrate { get; init; }

    /// <summary>CAN 控制器输入时钟频率（Hz）。<br/>CAN controller input clock frequency in Hz.</summary>
    public int ClockFrequency { get; init; }

    /// <summary>
    /// 控制器固定时钟分频。多数控制器为 1；部分经典 CAN BTR 模型为 2。<br/>
    /// Fixed controller clock divisor. Most controllers use 1; some Classic CAN BTR models use 2.
    /// </summary>
    public int ClockDivisor { get; init; } = 1;

    /// <summary>目标采样点，范围为 0..1；null 时使用 CiA 推荐 NRZ 采样点。<br/>Target sample point from 0 to 1; null uses CiA-recommended NRZ defaults.</summary>
    public double? SamplePoint { get; init; }

    /// <summary>偏好的每 bit Tq 总数；用于在同等误差候选中择优。<br/>Preferred total Tq per bit; used as a tie-breaker among equal-error candidates.</summary>
    public int? PreferredTimeQuantaPerBit { get; init; }

    /// <summary>显式 SJW（Tq）；null 时计算安全默认值。<br/>Explicit SJW in Tq; null calculates a safe default.</summary>
    public int? SynchronizationJumpWidth { get; init; }

    /// <summary>最大允许波特率相对误差，例如 0.005 表示 0.5%。<br/>Maximum allowed relative bitrate error, for example 0.005 means 0.5%.</summary>
    public double MaximumBitrateError { get; init; } = 0.005;

    /// <summary>控制器时序约束。<br/>Controller timing constraints.</summary>
    public CanBitTimingConstraints Constraints { get; init; } = new();
}

/// <summary>
/// CAN 位时序计算结果。<br/>CAN bit timing calculation result.
/// </summary>
public readonly record struct CanBitTimingCalculationResult
{
    /// <summary>创建 CAN 位时序计算结果。<br/>Creates a CAN bit timing calculation result.</summary>
    public CanBitTimingCalculationResult(
        int prescaler,
        int timeSegment1,
        int timeSegment2,
        int synchronizationJumpWidth,
        int requestedBitrate,
        int actualBitrate,
        int timeQuantaPerBit,
        double requestedSamplePoint,
        double actualSamplePoint,
        double bitrateError,
        double samplePointError,
        double timeQuantumNanoseconds)
    {
        Prescaler = prescaler;
        TimeSegment1 = timeSegment1;
        TimeSegment2 = timeSegment2;
        SynchronizationJumpWidth = synchronizationJumpWidth;
        RequestedBitrate = requestedBitrate;
        ActualBitrate = actualBitrate;
        TimeQuantaPerBit = timeQuantaPerBit;
        RequestedSamplePoint = requestedSamplePoint;
        ActualSamplePoint = actualSamplePoint;
        BitrateError = bitrateError;
        SamplePointError = samplePointError;
        TimeQuantumNanoseconds = timeQuantumNanoseconds;
    }

    /// <summary>BRP 预分频值。<br/>Bitrate prescaler value.</summary>
    public int Prescaler { get; }

    /// <summary>TSEG1（Tq）。<br/>TSEG1 in Tq.</summary>
    public int TimeSegment1 { get; }

    /// <summary>TSEG2（Tq）。<br/>TSEG2 in Tq.</summary>
    public int TimeSegment2 { get; }

    /// <summary>SJW（Tq）。<br/>Synchronization jump width in Tq.</summary>
    public int SynchronizationJumpWidth { get; }

    /// <summary>请求的目标波特率（bps）。<br/>Requested target bitrate in bits per second.</summary>
    public int RequestedBitrate { get; }

    /// <summary>计算得到的实际波特率（bps，四舍五入到整数）。<br/>Calculated actual bitrate in bits per second, rounded to integer.</summary>
    public int ActualBitrate { get; }

    /// <summary>每 bit 的总 Tq 数，包含同步段。<br/>Total Tq per bit including the synchronization segment.</summary>
    public int TimeQuantaPerBit { get; }

    /// <summary>请求的采样点。<br/>Requested sample point.</summary>
    public double RequestedSamplePoint { get; }

    /// <summary>实际采样点。<br/>Actual sample point.</summary>
    public double ActualSamplePoint { get; }

    /// <summary>波特率相对误差。<br/>Relative bitrate error.</summary>
    public double BitrateError { get; }

    /// <summary>采样点绝对误差。<br/>Absolute sample point error.</summary>
    public double SamplePointError { get; }

    /// <summary>单个 Tq 的纳秒数。<br/>Nanoseconds per Tq.</summary>
    public double TimeQuantumNanoseconds { get; }
}

/// <summary>
/// CAN 位时序计算器。按输入时钟、BRP/TSEG/SJW 约束和目标采样点计算通用位时序。<br/>
/// CAN bit timing calculator. Calculates generic timing from input clock, BRP/TSEG/SJW
/// constraints, and target sample point.
/// </summary>
public static class CanBitTimingCalculator
{
    private const double ComparisonTolerance = 0.000000000001;

    /// <summary>
    /// 按 CiA 推荐的 NRZ 规则获取默认采样点：≤500k 为 87.5%，≤800k 为 80%，更高为 75%。<br/>
    /// Gets the CiA-recommended NRZ default sample point: 87.5% up to 500k, 80% up to 800k,
    /// and 75% above that.
    /// </summary>
    public static double GetRecommendedNrzSamplePoint(int bitrate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bitrate);
        if (bitrate > 800_000)
            return 0.75;
        if (bitrate > 500_000)
            return 0.8;
        return 0.875;
    }

    /// <summary>
    /// 计算 CAN 位时序。结果优先选择最低波特率误差，再选择最低采样点误差，最后按偏好 Tq 数择优。<br/>
    /// Calculates CAN bit timing. Results prefer the lowest bitrate error, then the lowest sample
    /// point error, and finally the preferred Tq count when supplied.
    /// </summary>
    public static CanBitTimingCalculationResult Calculate(CanBitTimingCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var constraints = request.Constraints;
        var targetSamplePoint = request.SamplePoint ?? GetRecommendedNrzSamplePoint(request.TargetBitrate);
        var minimumTimeQuanta = constraints.SynchronizationSegment +
                                constraints.TimeSegment1Minimum +
                                constraints.TimeSegment2Minimum;
        var maximumTimeQuanta = constraints.SynchronizationSegment +
                                constraints.TimeSegment1Maximum +
                                constraints.TimeSegment2Maximum;

        Candidate? best = null;
        for (var prescaler = constraints.PrescalerMinimum;
             prescaler <= constraints.PrescalerMaximum;
             prescaler += constraints.PrescalerIncrement)
        {
            for (var timeQuantaPerBit = minimumTimeQuanta; timeQuantaPerBit <= maximumTimeQuanta; timeQuantaPerBit++)
            {
                var denominator = (long)request.ClockDivisor * prescaler * timeQuantaPerBit;
                var actualBitrate = request.ClockFrequency / (double)denominator;
                var bitrateError = Math.Abs(actualBitrate - request.TargetBitrate) / request.TargetBitrate;
                if (bitrateError > request.MaximumBitrateError + ComparisonTolerance)
                    continue;

                foreach (var timeSegment1 in EnumerateTimeSegment1Candidates(timeQuantaPerBit, targetSamplePoint, constraints))
                {
                    var timeSegment2 = timeQuantaPerBit - constraints.SynchronizationSegment - timeSegment1;
                    if (!IsValidSegments(timeSegment1, timeSegment2, constraints))
                        continue;

                    var actualSamplePoint = (constraints.SynchronizationSegment + timeSegment1) /
                                            (double)timeQuantaPerBit;
                    var samplePointError = Math.Abs(actualSamplePoint - targetSamplePoint);
                    var candidate = new Candidate(
                        prescaler,
                        timeSegment1,
                        timeSegment2,
                        timeQuantaPerBit,
                        (int)Math.Round(actualBitrate, MidpointRounding.AwayFromZero),
                        bitrateError,
                        actualSamplePoint,
                        samplePointError,
                        TimeQuantumNanoseconds: 1_000_000_000d * request.ClockDivisor * prescaler / request.ClockFrequency);

                    if (best is null || IsBetter(candidate, best.Value, request.PreferredTimeQuantaPerBit))
                        best = candidate;
                }
            }
        }

        if (best is not { } timing)
        {
            throw new CanException("core", CanErrorCategory.ConfigurationConflict,
                $"Cannot calculate CAN bit timing for bitrate {request.TargetBitrate} with clock {request.ClockFrequency} Hz.");
        }

        var sjw = ResolveSynchronizationJumpWidth(request.SynchronizationJumpWidth, timing, constraints);
        return new CanBitTimingCalculationResult(
            timing.Prescaler,
            timing.TimeSegment1,
            timing.TimeSegment2,
            sjw,
            request.TargetBitrate,
            timing.ActualBitrate,
            timing.TimeQuantaPerBit,
            targetSamplePoint,
            timing.ActualSamplePoint,
            timing.BitrateError,
            timing.SamplePointError,
            timing.TimeQuantumNanoseconds);
    }

    private static void ValidateRequest(CanBitTimingCalculationRequest request)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.TargetBitrate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ClockFrequency);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ClockDivisor);
        if (request.SamplePoint is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(request.SamplePoint), request.SamplePoint, "Sample point must be greater than 0 and less than 1.");
        if (request.MaximumBitrateError < 0)
            throw new ArgumentOutOfRangeException(nameof(request.MaximumBitrateError), request.MaximumBitrateError, "Maximum bitrate error cannot be negative.");
        if (request.PreferredTimeQuantaPerBit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.PreferredTimeQuantaPerBit), request.PreferredTimeQuantaPerBit, "Preferred Tq per bit must be positive.");
        if (request.SynchronizationJumpWidth is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.SynchronizationJumpWidth), request.SynchronizationJumpWidth, "SJW must be positive.");

        ArgumentNullException.ThrowIfNull(request.Constraints);
        request.Constraints.Validate();
    }

    private static IEnumerable<int> EnumerateTimeSegment1Candidates(
        int timeQuantaPerBit,
        double targetSamplePoint,
        CanBitTimingConstraints constraints)
    {
        var idealTimeSegment1 = targetSamplePoint * timeQuantaPerBit - constraints.SynchronizationSegment;
        var floor = (int)Math.Floor(idealTimeSegment1);
        var ceiling = (int)Math.Ceiling(idealTimeSegment1);
        var maxFromTseg2Min = timeQuantaPerBit - constraints.SynchronizationSegment - constraints.TimeSegment2Minimum;
        var minFromTseg2Max = timeQuantaPerBit - constraints.SynchronizationSegment - constraints.TimeSegment2Maximum;

        var rawCandidates = new[]
        {
            floor,
            ceiling,
            floor - 1,
            ceiling + 1,
            constraints.TimeSegment1Minimum,
            constraints.TimeSegment1Maximum,
            maxFromTseg2Min,
            minFromTseg2Max,
        };

        var emitted = new int[rawCandidates.Length];
        var emittedCount = 0;
        foreach (var rawCandidate in rawCandidates)
        {
            var candidate = Math.Clamp(rawCandidate, constraints.TimeSegment1Minimum, constraints.TimeSegment1Maximum);
            if (emitted.AsSpan(0, emittedCount).Contains(candidate))
                continue;
            emitted[emittedCount++] = candidate;
            yield return candidate;
        }
    }

    private static bool IsValidSegments(int timeSegment1, int timeSegment2, CanBitTimingConstraints constraints)
    {
        return timeSegment1 >= constraints.TimeSegment1Minimum &&
               timeSegment1 <= constraints.TimeSegment1Maximum &&
               timeSegment2 >= constraints.TimeSegment2Minimum &&
               timeSegment2 <= constraints.TimeSegment2Maximum;
    }

    private static bool IsBetter(Candidate candidate, Candidate best, int? preferredTimeQuantaPerBit)
    {
        if (IsLower(candidate.BitrateError, best.BitrateError))
            return true;
        if (IsLower(best.BitrateError, candidate.BitrateError))
            return false;

        if (IsLower(candidate.SamplePointError, best.SamplePointError))
            return true;
        if (IsLower(best.SamplePointError, candidate.SamplePointError))
            return false;

        if (preferredTimeQuantaPerBit is { } preferred)
        {
            var candidatePreferredDistance = Math.Abs(candidate.TimeQuantaPerBit - preferred);
            var bestPreferredDistance = Math.Abs(best.TimeQuantaPerBit - preferred);
            if (candidatePreferredDistance < bestPreferredDistance)
                return true;
            if (candidatePreferredDistance > bestPreferredDistance)
                return false;
        }

        if (candidate.TimeQuantaPerBit > best.TimeQuantaPerBit)
            return true;
        if (candidate.TimeQuantaPerBit < best.TimeQuantaPerBit)
            return false;

        return candidate.Prescaler < best.Prescaler;
    }

    private static bool IsLower(double left, double right) => left < right - ComparisonTolerance;

    private static int ResolveSynchronizationJumpWidth(
        int? requestedSynchronizationJumpWidth,
        Candidate timing,
        CanBitTimingConstraints constraints)
    {
        if (requestedSynchronizationJumpWidth is { } explicitSjw)
        {
            if (!IsValidSynchronizationJumpWidth(explicitSjw, timing, constraints))
            {
                throw new CanException("core", CanErrorCategory.ConfigurationConflict,
                    $"CAN SJW {explicitSjw} is outside the allowed range for the calculated timing.");
            }

            return explicitSjw;
        }

        var defaultSjw = Math.Max(
            constraints.SynchronizationJumpWidthMinimum,
            Math.Min(
                Math.Min(timing.TimeSegment1, timing.TimeSegment2 / 2),
                constraints.SynchronizationJumpWidthMaximum));

        if (defaultSjw > timing.TimeSegment2)
            defaultSjw = timing.TimeSegment2;

        return defaultSjw;
    }

    private static bool IsValidSynchronizationJumpWidth(
        int synchronizationJumpWidth,
        Candidate timing,
        CanBitTimingConstraints constraints)
    {
        return synchronizationJumpWidth >= constraints.SynchronizationJumpWidthMinimum &&
               synchronizationJumpWidth <= constraints.SynchronizationJumpWidthMaximum &&
               synchronizationJumpWidth <= timing.TimeSegment1 &&
               synchronizationJumpWidth <= timing.TimeSegment2;
    }

    private readonly record struct Candidate(
        int Prescaler,
        int TimeSegment1,
        int TimeSegment2,
        int TimeQuantaPerBit,
        int ActualBitrate,
        double BitrateError,
        double ActualSamplePoint,
        double SamplePointError,
        double TimeQuantumNanoseconds);
}
