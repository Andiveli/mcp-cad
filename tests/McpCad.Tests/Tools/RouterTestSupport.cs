using McpCad.Core;

namespace McpCad.Tests.Tools;

/// <summary>Routes all resolution to a single <see cref="IMechanicalCadProvider"/> for legacy unit tests.</summary>
internal sealed class MechanicalRouterAdapter(IMechanicalCadProvider mechanical, string providerName = "inventor")
    : IProviderRouter
{
    public string CurrentProvider { get; set; } = providerName;

    public IReadOnlyCollection<string> AvailableProviders { get; } =
        new[] { providerName }.ToList().AsReadOnly();

    public IDisposable PushProvider(string provider)
    {
        var prior = CurrentProvider;
        CurrentProvider = provider;
        return new Scope(() => CurrentProvider = prior);
    }

    public ICadProvider Resolve(string provider) => mechanical;

    private sealed class Scope(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}