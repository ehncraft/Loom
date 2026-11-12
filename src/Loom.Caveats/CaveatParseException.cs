namespace Loom.Caveats;

public sealed class CaveatParseException : Exception
{
    public CaveatParseException(string message)
        : base(message)
    {
    }

    public CaveatParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public CaveatParseException()
    {
    }
}
