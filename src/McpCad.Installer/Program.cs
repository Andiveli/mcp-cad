using Spectre.Console;

namespace McpCad.Installer;

public class Program
{
    private const string StatePath = "scripts/tui/state.json";
    private static State _state = new();
    private static McpAgent[] _agents = [];

    public static void Main(string[] args)
    {
        _state = State.Load(StatePath);
        _agents = McpAgents.All(_state);

        AnsiConsole.Write(new FigletText("mcp-cad").Color(Color.Cyan));
        AnsiConsole.MarkupLine("[grey]MCP server installer for Autodesk Inventor[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]What do you want to do?[/]")
                .AddChoices("Select MCP clients", "Run installation", "Quit"));

            switch (choice)
            {
                case "Select MCP clients":
                    SelectAgents();
                    break;
                case "Run installation":
                    RunInstallation();
                    _state.LastAgent = "install";
                    _state.Save(StatePath);
                    return;
                case "Quit":
                    return;
            }
        }
    }

    private static void SelectAgents()
    {
        var choices = _agents.Select(a => a.Label).ToList();
        choices.Add("Back");

        while (true)
        {
            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Toggle MCP clients to register[/] (Enter to toggle, Esc to go back)")
                .AddChoices(choices)
                .HighlightStyle(new Style(foreground: Color.Cyan)));

            if (selected == "Back") break;

            var idx = choices.IndexOf(selected);
            if (idx >= 0 && idx < _agents.Length)
            {
                _agents[idx].Selected = !_agents[idx].Selected;
                choices[idx] = _agents[idx].Label;
            }
        }
    }

    private static void RunInstallation()
    {
        var selected = _agents.Where(a => a.Selected).ToList();
        if (selected.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No MCP clients selected.[/]");
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Registering mcp-cad in {selected.Count} client(s)...[/]");

        foreach (var agent in selected)
        {
            AnsiConsole.Markup($"  {agent.Name}... ");
            try
            {
                var path = agent.Run?.Invoke(_state, agent) ?? "unknown";
                AnsiConsole.MarkupLine($"[green]OK[/] ({path})");
                _state.LastAgent = agent.Name;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]FAIL: {ex.Message}[/]");
            }
        }

        _state.Save(StatePath);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green bold]Installation complete![/]");
        AnsiConsole.MarkupLine("[grey]Restart your MCP client(s) to pick up the changes.[/]");
    }
}
