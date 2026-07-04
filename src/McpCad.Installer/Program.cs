using McpCad.Core;
using McpCad.Core.Models;
using Spectre.Console;

namespace McpCad.Installer;

public class Program
{
    public static void Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("mcp-cad").Color(Color.Cyan));
        AnsiConsole.MarkupLine("[grey]Multi-provider MCP setup — Onshape, Inventor, SolidWorks[/]");
        AnsiConsole.WriteLine();

        var providerId = RunProviderSelection();
        var credentials = RunCredentialStep(providerId);
        var agents = RunAgentRegistration();
        var serverPath = McpAgents.FindServerPath();

        if (string.IsNullOrEmpty(serverPath))
        {
            AnsiConsole.MarkupLine("[yellow]Warning: McpCad.Server not found under dist/. Config will still be saved — start the server manually.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[grey]Server: {Markup.Escape(serverPath)}[/]");
        }

        var config = InstallerWizard.BuildUserConfig(providerId, credentials, agents, serverPath);
        RunCompletion(config, serverPath);
    }

    private static string RunProviderSelection()
    {
        var choices = InstallerWizard.GetProviderChoices(InstallerWizard.IsWindows);
        var selectable = choices.Where(c => c.Selectable).ToList();
        if (selectable.Count == 1)
        {
            AnsiConsole.MarkupLine("[cyan]CAD provider[/]: [bold]Onshape[/] (desktop CAD requires Windows)");
            return selectable[0].Id;
        }

        var prompt = new SelectionPrompt<string>()
            .Title("[cyan]Select CAD provider[/]")
            .PageSize(10)
            .AddChoices(selectable.Select(c => c.Id));

        foreach (var c in choices.Where(x => !x.Selectable))
            AnsiConsole.MarkupLine($"[grey]  {Markup.Escape(c.Label)}[/]");

        return AnsiConsole.Prompt(prompt);
    }

    private static Dictionary<string, string> RunCredentialStep(string providerId)
    {
        return providerId switch
        {
            "onshape" => PromptOnshapeCredentials(),
            "inventor" => PromptInventorCredentials(),
            "solidworks" => PromptSolidWorksNote(),
            _ => new Dictionary<string, string>(),
        };
    }

    private static Dictionary<string, string> PromptOnshapeCredentials()
    {
        while (true)
        {
            var access = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Onshape Access Key[/]")
                    .PromptStyle("white")
                    .ValidationErrorMessage("[red]Access Key is required[/]")
                    .Validate(v => !string.IsNullOrWhiteSpace(v)));

            var secret = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Onshape Secret Key[/]")
                    .PromptStyle("white")
                    .Secret()
                    .ValidationErrorMessage("[red]Secret Key is required[/]")
                    .Validate(v => !string.IsNullOrWhiteSpace(v)));

            if (!InstallerWizard.ValidateOnshapeCredentials(access, secret))
            {
                AnsiConsole.MarkupLine("[red]Access Key and Secret Key are required. Try again.[/]");
                continue;
            }

            var skid = AnsiConsole.Prompt(
                new TextPrompt<string>("[grey]Secret Key ID (optional)[/]")
                    .AllowEmpty());

            var creds = new Dictionary<string, string>
            {
                ["access_key"] = access.Trim(),
                ["secret_key"] = secret.Trim(),
            };
            if (!string.IsNullOrWhiteSpace(skid))
                creds["secret_key_id"] = skid.Trim();

            return creds;
        }
    }

    private static Dictionary<string, string> PromptInventorCredentials()
    {
        var autoConnect = AnsiConsole.Confirm("[cyan]Auto-connect to running Inventor?[/]", defaultValue: true);
        return new Dictionary<string, string> { ["auto_connect"] = autoConnect ? "true" : "false" };
    }

    private static Dictionary<string, string> PromptSolidWorksNote()
    {
        AnsiConsole.MarkupLine(
            "[yellow]SolidWorks provider is planned — selection is saved for future compatibility.[/]");
        return new Dictionary<string, string>();
    }

    private static List<AgentConfig> RunAgentRegistration()
    {
        var known = InstallerWizard.KnownAgents();
        var labels = known.Select(a => a.DisplayName).ToList();

        var multi = new MultiSelectionPrompt<string>()
            .Title("[cyan]Register MCP hosts[/] (Space to toggle, Enter to confirm)")
            .PageSize(10)
            .InstructionsText("[grey](↑/↓ to move, space to toggle, enter to accept)[/]")
            .AddChoices(labels);

        foreach (var choice in known.Where(a => a.PreSelected))
            multi.Select(choice.DisplayName);

        var selected = AnsiConsole.Prompt(multi);

        var agents = new List<AgentConfig>();
        foreach (var name in selected)
        {
            if (name == "Custom URL")
            {
                var url = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]Custom agent URL[/]")
                        .ValidationErrorMessage("[red]URL required[/]")
                        .Validate(u => !string.IsNullOrWhiteSpace(u)));
                agents.Add(new AgentConfig { Name = "Custom", Url = url.Trim() });
                continue;
            }

            var urlDefault = InstallerWizard.DefaultUrlForAgent(name);
            agents.Add(new AgentConfig { Name = name, Url = urlDefault });
        }

        return agents;
    }

    private static void RunCompletion(UserConfig config, string? serverPath)
    {
        while (true)
        {
            try
            {
                UserConfigStore.Save(config);
                break;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to write config: {Markup.Escape(ex.Message)}[/]");
                if (!AnsiConsole.Confirm("Retry save?", defaultValue: true))
                    throw;
            }
        }

        if (!string.IsNullOrEmpty(serverPath))
        {
            foreach (var agent in config.Agents)
            {
                if (agent.Name is "Custom")
                    continue;
                try
                {
                    McpAgents.RegisterAgentMcpJson(agent.Name, serverPath);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(agent.Name)}: MCP JSON merge skipped — {Markup.Escape(ex.Message)}[/]");
                }
            }
        }

        AnsiConsole.MarkupLine("[green]mcp-cad configured. Restart your MCP host.[/]");
    }
}