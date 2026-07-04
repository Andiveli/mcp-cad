namespace McpCad.Core.Exceptions;

public sealed class ProviderDisabledException : Exception
{
    public string Provider { get; }

    public ProviderDisabledException(string provider)
        : base($"Provider '{provider}' is not enabled in config. Enable it under providers.{provider}.enabled.")
    {
        Provider = provider;
    }
}