using System.Globalization;

namespace CanHub.Adapter.Vector.Tests;

internal static class VectorEcuTestSettings
{
    public const string OptInVariable = "CANHUB_TEST_VECTOR_ECU";

    private const string DeviceVariable = "CANHUB_TEST_VECTOR_ECU_DEVICE";
    private const string DeviceIndexVariable = "CANHUB_TEST_VECTOR_ECU_DEVICE_INDEX";
    private const string ChannelIndexVariable = "CANHUB_TEST_VECTOR_ECU_CHANNEL_INDEX";
    private const string RequestIdVariable = "CANHUB_TEST_VECTOR_ECU_REQUEST_ID";
    private const string ResponseIdVariable = "CANHUB_TEST_VECTOR_ECU_RESPONSE_ID";

    public static void RequireOptIn()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
            Assert.Inconclusive($"Skipping ECU bench test: set {OptInVariable}=1 to run.");
    }

    public static VectorEcuTarget GetTarget() => new(
        GetStringEnvironment(DeviceVariable, "VN5610A"),
        GetIntEnvironment(DeviceIndexVariable, 0),
        GetIntEnvironment(ChannelIndexVariable, 2),
        GetStandardCanIdEnvironment(RequestIdVariable, 0x7DCu),
        GetStandardCanIdEnvironment(ResponseIdVariable, 0x7DDu));

    private static string GetStringEnvironment(string variableName, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static int GetIntEnvironment(string variableName, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
            return parsed;

        Assert.Inconclusive($"Invalid {variableName} value '{value}'. Use a non-negative integer.");
        return defaultValue;
    }

    private static uint GetStandardCanIdEnvironment(string variableName, uint defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var span = value.AsSpan();
        var style = NumberStyles.Integer;
        if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            span = span[2..];
            style = NumberStyles.HexNumber;
        }

        if (uint.TryParse(span, style, CultureInfo.InvariantCulture, out var parsed) && parsed <= 0x7FF)
            return parsed;

        Assert.Inconclusive($"Invalid {variableName} value '{value}'. Use a standard CAN ID in decimal or 0x-prefixed hex.");
        return defaultValue;
    }
}

internal readonly record struct VectorEcuTarget(
    string Device,
    int DeviceIndex,
    int ChannelIndex,
    uint RequestId,
    uint ResponseId)
{
    public string Endpoint => $"vector://{Device}?deviceIndex={DeviceIndex}&channelIndex={ChannelIndex}";
}
