namespace McpCad.Core.Exceptions;

public sealed class ProviderNotFoundException : Exception
{
    public string Provider { get; }
    public IReadOnlyCollection<string> Available { get; }

    public ProviderNotFoundException(string provider, IEnumerable<string> available)
        : base($"Unknown provider '{provider}'. Available: {string.Join(", ", available)}")
    {
        Provider = provider;
        Available = available.ToList().AsReadOnly();
    }
}