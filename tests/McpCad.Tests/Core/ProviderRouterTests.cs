using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;

namespace McpCad.Tests.Core;

public class ProviderRouterTests : IDisposable
{
    private sealed class CountingCadProvider : ICadProvider
    {
        public static int InstanceCount;

        public CountingCadProvider()
        {
            Interlocked.Increment(ref InstanceCount);
        }

        public Dictionary<string, object?> Connect() => new() { ["success"] = true };
        public Dictionary<string, object?> Disconnect() => new() { ["success"] = true };
        public Dictionary<string, object?> Health() => new() { ["success"] = true };
        public Dictionary<string, object?> DocOpen(string path) => new();
        public Dictionary<string, object?> DocNewPart(string template = "") => new();
        public Dictionary<string, object?> DocNewAssembly(string template = "") => new();
        public Dictionary<string, object?> DocSave() => new();
        public Dictionary<string, object?> DocSaveAs(string path) => new();
        public Dictionary<string, object?> DocClose(bool save = true) => new();
        public Dictionary<string, object?> ExportStep(string path, Dictionary<string, object?>? options = null) => new();
        public Dictionary<string, object?> ExportStl(string path, Dictionary<string, object?>? options = null) => new();
        public Dictionary<string, object?> ExportPdf(string path, Dictionary<string, object?>? options = null) => new();
        public Dictionary<string, object?> ExportDxf(string path, Dictionary<string, object?>? options = null) => new();
    }

    public ProviderRouterTests()
    {
        CountingCadProvider.InstanceCount = 0;
        CadExecutionContext.ResetForTests();
    }

    public void Dispose() => CadExecutionContext.ResetForTests();

    private static ProviderRouter CreateRouter(
        string defaultProvider = "onshape",
        UserConfig? userConfig = null,
        Action<Dictionary<string, Lazy<ICadProvider>>>? configure = null)
    {
        var registry = new Dictionary<string, Lazy<ICadProvider>>(StringComparer.OrdinalIgnoreCase)
        {
            ["inventor"] = new Lazy<ICadProvider>(() => new CountingCadProvider(), LazyThreadSafetyMode.ExecutionAndPublication),
            ["onshape"] = new Lazy<ICadProvider>(() => new CountingCadProvider(), LazyThreadSafetyMode.ExecutionAndPublication),
            ["solidworks"] = new Lazy<ICadProvider>(() => new CountingCadProvider(), LazyThreadSafetyMode.ExecutionAndPublication),
        };
        configure?.Invoke(registry);
        return new ProviderRouter(registry, defaultProvider, userConfig);
    }

    [Fact]
    public void PushProvider_Sets_CurrentProvider()
    {
        var router = CreateRouter(defaultProvider: "onshape");

        using (router.PushProvider("inventor"))
        {
            router.CurrentProvider.Should().Be("inventor");
        }
    }

    [Fact]
    public void PushProvider_Dispose_Restores_Prior_Provider()
    {
        var router = CreateRouter(defaultProvider: "onshape");

        using (router.PushProvider("inventor"))
        {
            router.CurrentProvider.Should().Be("inventor");
        }

        router.CurrentProvider.Should().Be("onshape");
    }

    [Fact]
    public void Concurrent_AsyncLocal_Isolates_CurrentProvider()
    {
        var router = CreateRouter(defaultProvider: "onshape");
        var gate = new ManualResetEventSlim(false);
        string? a = null;
        string? b = null;

        var taskA = Task.Run(() =>
        {
            using (router.PushProvider("inventor"))
            {
                gate.Wait();
                a = router.CurrentProvider;
            }
        });

        var taskB = Task.Run(() =>
        {
            using (router.PushProvider("onshape"))
            {
                gate.Set();
                b = router.CurrentProvider;
            }
        });

        Task.WaitAll(taskA, taskB);

        a.Should().Be("inventor");
        b.Should().Be("onshape");
    }

    [Fact]
    public void CurrentProvider_Uses_Default_From_Config_When_No_Push()
    {
        var router = CreateRouter(defaultProvider: "inventor");

        router.CurrentProvider.Should().Be("inventor");
    }

    [Fact]
    public void Resolve_Returns_Registered_Provider()
    {
        var router = CreateRouter();

        var provider = router.Resolve("inventor");

        provider.Should().NotBeNull();
        provider.Should().BeAssignableTo<ICadProvider>();
    }

    [Fact]
    public void Resolve_Unknown_Throws_ProviderNotFoundException()
    {
        var router = CreateRouter();

        var act = () => router.Resolve("unknown");

        act.Should().Throw<ProviderNotFoundException>()
            .Which.Provider.Should().Be("unknown");
    }

    [Fact]
    public void Resolve_Disabled_Throws_ProviderDisabledException()
    {
        var userConfig = new UserConfig
        {
            Providers =
            {
                ["inventor"] = new ProviderConfig { Enabled = false },
                ["onshape"] = new ProviderConfig { Enabled = true },
            },
        };
        var router = CreateRouter(userConfig: userConfig);

        var act = () => router.Resolve("inventor");

        act.Should().Throw<ProviderDisabledException>()
            .Which.Provider.Should().Be("inventor");
    }

    [Fact]
    public void AvailableProviders_Lists_Registered_Names()
    {
        var router = CreateRouter();

        router.AvailableProviders.Should().BeEquivalentTo("inventor", "onshape", "solidworks");
    }

    [Fact]
    public void Concurrent_First_Access_Creates_Single_Instance()
    {
        CountingCadProvider.InstanceCount = 0;
        var registry = new Dictionary<string, Lazy<ICadProvider>>
        {
            ["inventor"] = new Lazy<ICadProvider>(
                () => new CountingCadProvider(),
                LazyThreadSafetyMode.ExecutionAndPublication),
        };
        var router = new ProviderRouter(registry, "inventor");

        Parallel.For(0, 8, _ => router.Resolve("inventor"));

        CountingCadProvider.InstanceCount.Should().Be(1);
    }

    [Fact]
    public void Unused_Provider_Never_Constructed()
    {
        CountingCadProvider.InstanceCount = 0;
        var router = CreateRouter();

        router.Resolve("onshape");

        CountingCadProvider.InstanceCount.Should().Be(1);
    }

    [Fact]
    public void PushProvider_Unknown_Throws_ProviderNotFoundException()
    {
        var router = CreateRouter();

        var act = () => router.PushProvider("fusion360");

        act.Should().Throw<ProviderNotFoundException>();
    }

    [Fact]
    public void Resolve_When_Lazy_Factory_Throws_Wraps_ProviderConnectionException()
    {
        var registry = new Dictionary<string, Lazy<ICadProvider>>
        {
            ["inventor"] = new Lazy<ICadProvider>(() => throw new InvalidOperationException("COM unavailable")),
        };
        var router = new ProviderRouter(registry, "inventor");

        var act = () => router.Resolve("inventor");

        act.Should().Throw<ProviderConnectionException>()
            .WithInnerException<InvalidOperationException>();
    }
}
