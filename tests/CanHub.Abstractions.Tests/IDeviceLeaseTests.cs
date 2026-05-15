namespace CanHub.Abstractions.Tests;

[TestClass]
public sealed class IDeviceLeaseTests
{
    [TestMethod]
    public void Interface_HasExpectedMembers()
    {
        var type = typeof(IDeviceLease);

        Assert.IsTrue(typeof(IAsyncDisposable).IsAssignableFrom(type));

        var deviceId = type.GetProperty(nameof(IDeviceLease.DeviceId));
        Assert.IsNotNull(deviceId);
        Assert.AreEqual(typeof(string), deviceId.PropertyType);

        var isOpen = type.GetProperty(nameof(IDeviceLease.IsOpen));
        Assert.IsNotNull(isOpen);
        Assert.AreEqual(typeof(bool), isOpen.PropertyType);

        var openAsync = type.GetMethod(nameof(IDeviceLease.OpenAsync));
        Assert.IsNotNull(openAsync);
    }

    [TestMethod]
    public void FakeDeviceLease_ImplementsInterface()
    {
        var lease = new FakeDeviceLease("dev-0");

        Assert.AreEqual("dev-0", lease.DeviceId);
        Assert.IsFalse(lease.IsOpen);
    }

    private sealed class FakeDeviceLease : IDeviceLease
    {
        public string DeviceId { get; }
        public bool IsOpen { get; private set; }

        public FakeDeviceLease(string deviceId) => DeviceId = deviceId;

        public ValueTask OpenAsync(CanOpenContext context, CancellationToken ct = default)
        {
            IsOpen = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsOpen = false;
            return ValueTask.CompletedTask;
        }
    }
}
