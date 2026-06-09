using Spectre.Console;

namespace McpCad.Installer;

public class Program
{
    private const string StatePath = "scripts/tui/state.json";
    private static State _state = new();
    private static McpAgent[] _agents = [];
    private static int _selectedIdx;
    private static string _status = "";

    // Electric orange for the TUI
    private const string ElectricOrange = "bold #FF5F00";

    // Simple ASCII name only (as before the banner image request)
    private static readonly string[] LogoLines = new[]
    {
        " __  __  ____  ____     ____    _    ____ ",
        "|  \\/  |/ ___||  _ \\   / ___|  / \\  |  _ \\ ",
        "| |\\/| | |    | |_) | | |     / _ \\ | | | |",
        "| |  | | |___ |  __/  | |___ / ___ \\| |_| |",
        "|_|  |_|\\____||_|      \\____/_/   \\_\\____/ ",
        "",
        "                 MCP-CAD"
    };

    public static void Main(string[] args)
    {
        _state = State.Load(StatePath);
        _agents = McpAgents.All(_state);
        AutoDetectAgents(_agents);

        int totalItems = _agents.Length + 2;
        int continueIndex = _agents.Length;
        int exitIndex = _agents.Length + 1;

        bool inSelection = true;

        while (inSelection)
        {
            Console.Clear();
            RenderLogo();
            AnsiConsole.MarkupLine("[grey]MCP server installer — AI Agents for CAD (Mechanical + Electronic)[/]");
            AnsiConsole.MarkupLine("[grey]j/k or arrows: move  |  Space or Enter on agent: toggle  |  Enter on button: activate  |  q: quit[/]");
            AnsiConsole.WriteLine();

            for (int i = 0; i < _agents.Length; i++)
            {
                var agent = _agents[i];
                var check = agent.Selected ? "[green][[x]][/]" : "[[ ]]";
                var cursor = i == _selectedIdx ? "[cyan]>[/]" : " ";
                var name = i == _selectedIdx ? $"[bold cyan]{agent.Name}[/]" : agent.Name;
                AnsiConsole.MarkupLine($"  {cursor} {check} {name,-12} [grey]{agent.Description}[/]");
            }

            // Bottom action buttons - very explicit for "dumb user" safety
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]What do you want to do?[/]");

            bool onContinue = _selectedIdx == continueIndex;
            bool onExit = _selectedIdx == exitIndex;

            // IMPORTANT: literal [ and ] must be escaped as [[ and ]] when inside markup strings
            string continueBtn = onContinue
                ? "[bold white on blue]> [[ Continue ]] (install all selected agents)[/]"
                : "  [[ Continue ]] (install all selected agents)";
            string exitBtn = onExit
                ? "[bold white on blue]> [[ Exit ]][/]"
                : "  [[ Exit ]]";

            AnsiConsole.MarkupLine(continueBtn);
            AnsiConsole.MarkupLine(exitBtn);

            AnsiConsole.WriteLine();
            if (!string.IsNullOrEmpty(_status))
                AnsiConsole.MarkupLine(_status);

            var key = Console.ReadKey(intercept: true);
            _status = "";
            switch (key.Key)
            {
                case ConsoleKey.J or ConsoleKey.DownArrow:
                    _selectedIdx = (_selectedIdx + 1) % totalItems;
                    break;
                case ConsoleKey.K or ConsoleKey.UpArrow:
                    _selectedIdx = (_selectedIdx - 1 + totalItems) % totalItems;
                    break;
                case ConsoleKey.Spacebar:
                    if (_selectedIdx < _agents.Length)
                    {
                        _agents[_selectedIdx].Selected = !_agents[_selectedIdx].Selected;
                    }
                    break;
                case ConsoleKey.Enter:
                    if (_selectedIdx < _agents.Length)
                    {
                        // Allow Enter to also toggle agent (makes it obvious)
                        _agents[_selectedIdx].Selected = !_agents[_selectedIdx].Selected;
                    }
                    else if (_selectedIdx == continueIndex)
                    {
                        var selected = _agents.Where(a => a.Selected).ToArray();
                        if (selected.Length == 0)
                        {
                            _status = "[yellow]No agents selected. Move up and use Space to toggle some.[/]";
                        }
                        else
                        {
                            InstallSelected(selected);
                            inSelection = false; // move to success screen
                        }
                    }
                    else if (_selectedIdx == exitIndex)
                    {
                        return;
                    }
                    break;
                case ConsoleKey.Q or ConsoleKey.Escape:
                    return;
            }
        }
    }

    private static void RenderLogo()
    {
        foreach (var line in LogoLines)
        {
            if (line.Contains("MCP-CAD"))
            {
                AnsiConsole.MarkupLine($"[{ElectricOrange}]{line}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[grey]{line}[/]");
            }
        }
        AnsiConsole.WriteLine();
    }

    private static void AutoDetectAgents(McpAgent[] agents)
    {
        foreach (var agent in agents)
        {
            if (string.IsNullOrEmpty(agent.ConfigPath))
                continue;

            var dir = Path.GetDirectoryName(agent.ConfigPath);
            bool agentPresent = !string.IsNullOrEmpty(dir) &&
                                (Directory.Exists(dir) || File.Exists(agent.ConfigPath));

            if (agentPresent)
            {
                agent.Selected = true;
            }
        }
    }

    private static void InstallSelected(McpAgent[] selected)
    {
        var results = new List<(string Name, bool Success, string Message)>();

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(new Style(foreground: Color.FromHex("#FF5F00")))
            .Start($"Configuring {selected.Length} agent(s)...", ctx =>
            {
                foreach (var agent in selected)
                {
                    ctx.Status($"Installing for {agent.Name}...");
                    try
                    {
                        var path = agent.Run?.Invoke(_state, agent) ?? "unknown";
                        _state.LastAgent = agent.Name;
                        results.Add((agent.Name, true, path));
                    }
                    catch (Exception ex)
                    {
                        results.Add((agent.Name, false, ex.Message));
                    }
                }
                _state.Save(StatePath);
            });

        ShowSuccessScreen(results);
    }

    private static void ShowSuccessScreen(List<(string Name, bool Success, string Message)> results)
    {
        Console.Clear();
        RenderLogo();

        AnsiConsole.Write(new Rule($"[{ElectricOrange}]✓ Installation Complete[/]").RuleStyle(ElectricOrange));

        var successCount = results.Count(r => r.Success);
        var failCount = results.Count - successCount;

        var summary = successCount > 0
            ? $"[green]✓ Successfully configured {successCount} agent(s) with the MCP-CAD server[/]"
            : "[red]Some configurations had issues[/]";

        AnsiConsole.MarkupLine(summary);
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.FromHex("#FF5F00"))
            .AddColumn("Agent")
            .AddColumn("Status")
            .AddColumn("Details");

        foreach (var (name, success, msg) in results)
        {
            var status = success ? "[green]OK[/]" : "[red]FAIL[/]";
            table.AddRow(name, status, msg.Length > 60 ? msg[..60] + "..." : msg);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (failCount == 0)
        {
            AnsiConsole.MarkupLine("[grey]Next steps: Restart your AI agent(s) / TUI so the mcp-cad server is loaded.[/]");
            AnsiConsole.MarkupLine("[grey]The server will then be available for AI-driven mechanical and electronic CAD workflows.[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[{ElectricOrange}]Press Enter to exit...[/]");

        // Wait specifically for Enter (or any key, but message says Enter)
        while (Console.ReadKey(intercept: true).Key != ConsoleKey.Enter)
        {
            // keep waiting for Enter
        }
    }

    private static void RunAgent(McpAgent agent)
    {
        // Legacy single-run kept for compatibility if needed elsewhere
        try
        {
            var path = agent.Run?.Invoke(_state, agent) ?? "unknown";
            _status = $"[green]{agent.Name}: OK ({path})[/]";
            _state.LastAgent = agent.Name;
            _state.Save(StatePath);
        }
        catch (Exception ex)
        {
            _status = $"[red]{agent.Name}: FAIL — {ex.Message}[/]";
        }
    }
}
