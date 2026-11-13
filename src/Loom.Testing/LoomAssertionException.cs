namespace Loom.Testing;

public sealed class LoomAssertionException : Exception
{
    public LoomAssertionException(string message)
        : base(message)
    {
    }

    public LoomAssertionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public LoomAssertionException()
    {
    }
}
