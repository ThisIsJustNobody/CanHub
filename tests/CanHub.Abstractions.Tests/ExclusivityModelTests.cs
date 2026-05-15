namespace CanHub.Abstractions.Tests;

[TestClass]
public sealed class ExclusivityModelTests
{
    [TestMethod]
    [DataRow(ExclusivityModel.None, (byte)0)]
    [DataRow(ExclusivityModel.DeviceLevel, (byte)1)]
    [DataRow(ExclusivityModel.ChannelLevel, (byte)2)]
    public void Values_HaveExpectedByteValues(ExclusivityModel model, byte expected)
    {
        Assert.AreEqual(expected, (byte)model);
    }

    [TestMethod]
    public void Default_IsNone()
    {
        var model = default(ExclusivityModel);
        Assert.AreEqual(ExclusivityModel.None, model);
    }
}
