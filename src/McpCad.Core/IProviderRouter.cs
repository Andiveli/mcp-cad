namespace McpCad.Core;

/// <summary>Runtime dispatch layer: resolves CAD backends per tool call.</summary>
public interface IProviderRouter
{
    string CurrentProvider { get; }

    IReadOnlyCollection<string> AvailableProviders { get; }

    IDisposable PushProvider(string provider);

    ICadProvider Resolve(string provider);
}