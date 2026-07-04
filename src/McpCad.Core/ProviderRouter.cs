using McpCad.Core.Exceptions;
using McpCad.Core.Models;

namespace McpCad.Core;

public sealed class ProviderRouter : IProviderRouter
{
    private static readonly AsyncLocal<CadExecutionContext?> ContextHolder = new();

    private readonly IReadOnlyDictionary<string, Lazy<ICadProvider>> _registry;
    private readonly string _defaultProvider;
    private readonly UserConfig? _userConfig;

    public ProviderRouter(
        IReadOnlyDictionary<string, Lazy<ICadProvider>> registry,
        string defaultProvider,
        UserConfig? userConfig = null)
    {
        _registry = registry;
        _defaultProvider = NormalizeName(defaultProvider);
        _userConfig = userConfig;
    }

    public string CurrentProvider
    {
        get
        {
            var ctx = ContextHolder.Value;
            if (!string.IsNullOrWhiteSpace(ctx?.CurrentProvider))
                return ctx.CurrentProvider;
            return _defaultProvider;
        }
    }

    public IReadOnlyCollection<string> AvailableProviders => _registry.Keys.ToList().AsReadOnly();

    public IDisposable PushProvider(string provider)
    {
        var name = NormalizeName(provider);
        if (!_registry.ContainsKey(name))
            throw new ProviderNotFoundException(name, _registry.Keys);

        var prior = ContextHolder.Value;
        var next = new CadExecutionContext
        {
            CurrentProvider = name,
            PriorContext = prior,
            SelectedProvider = prior?.SelectedProvider,
        };
        ContextHolder.Value = next;
        return new ProviderScope(prior);
    }

    public ICadProvider Resolve(string provider)
    {
        var name = NormalizeName(provider);
        if (!_registry.TryGetValue(name, out var lazy))
            throw new ProviderNotFoundException(name, _registry.Keys);

        if (_userConfig?.Providers.TryGetValue(name, out var cfg) == true && !cfg.Enabled)
            throw new ProviderDisabledException(name);

        try
        {
            return lazy.Value;
        }
        catch (Exception ex) when (ex is not ProviderDisabledException and not ProviderNotFoundException)
        {
            throw new ProviderConnectionException($"Failed to connect {name}: {ex.Message}", ex);
        }
    }

    private static string NormalizeName(string provider) =>
        provider.Trim().ToLowerInvariant();

    private sealed class ProviderScope : IDisposable
    {
        private readonly CadExecutionContext? _prior;
        private bool _disposed;

        public ProviderScope(CadExecutionContext? prior) => _prior = prior;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            ContextHolder.Value = _prior;
        }
    }
}