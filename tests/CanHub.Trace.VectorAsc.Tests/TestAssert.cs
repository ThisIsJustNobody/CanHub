namespace CanHub.Trace.VectorAsc.Tests;

internal static class TestAssert
{
    public static TException Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }

        throw new AssertFailedException($"Expected exception of type {typeof(TException).Name}.");
    }
}
