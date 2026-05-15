namespace CanHub.Abstractions.Tests;

[TestClass]
public sealed class IChannelLeaseTests
{
    [TestMethod]
    public void Interface_HasExpectedMembers()
    {
        var type = typeof(IChannelLease);

        Assert.IsTrue(typeof(IAsyncDisposable).IsAssignableFrom(type));

        var channelIndex = type.GetProperty(nameof(IChannelLease.ChannelIndex));
        Assert.IsNotNull(channelIndex);
        Assert.AreEqual(typeof(int), channelIndex.PropertyType);

        var referenceCount = type.GetProperty(nameof(IChannelLease.ReferenceCount));
        Assert.IsNotNull(referenceCount);
        Assert.AreEqual(typeof(int), referenceCount.PropertyType);

        var activateAsync = type.GetMethod(nameof(IChannelLease.ActivateAsync));
        Assert.IsNotNull(activateAsync);
    }

    [TestMethod]
    public void FakeChannelLease_ImplementsInterface()
    {
        var lease = new FakeChannelLease(0);

        Assert.AreEqual(0, lease.ChannelIndex);
        Assert.AreEqual(0, lease.ReferenceCount);
    }

    private sealed class FakeChannelLease : IChannelLease
    {
        public int ChannelIndex { get; }
        public int ReferenceCount { get; private set; }

        public FakeChannelLease(int channelIndex) => ChannelIndex = channelIndex;

        public ValueTask ActivateAsync(CanOpenContext context, CancellationToken ct = default)
        {
            ReferenceCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DeactivateAsync(CancellationToken ct = default)
        {
            ReferenceCount--;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            ReferenceCount = Math.Max(0, ReferenceCount - 1);
            return ValueTask.CompletedTask;
        }
    }
}
