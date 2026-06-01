using Spectre.Console;

namespace McpCad.Installer;

public class Program
{
    private const string StatePath = "scripts/tui/state.json";
    private static State _state = new();
    private static McpAgent[] _agents = [];
    private static int _selectedIdx;
    private static string _status = "";

    public static void Main(string[] args)
    {
        _state = State.Load(StatePath);
        _agents = McpAgents.All(_state);

        while (true)
        {
            Console.Clear();
            AnsiConsole.Write(new FigletText("mcp-cad").Color(Color.Cyan));
            AnsiConsole.MarkupLine("[grey]MCP server installer — Autodesk Inventor[/]");
            AnsiConsole.MarkupLine("[grey]j/k  move   Space  toggle   Enter  install   q  quit[/]");
            AnsiConsole.WriteLine();

            for (int i = 0; i < _agents.Length; i++)
            {
                var agent = _agents[i];
                var check = agent.Selected ? "[green][[x]][/]" : "[[ ]]";
                var cursor = i == _selectedIdx ? "[cyan]>[/]" : " ";
                var name = i == _selectedIdx ? $"[bold cyan]{agent.Name}[/]" : agent.Name;
                AnsiConsole.MarkupLine($"  {cursor} {check} {name,-12} [grey]{agent.Description}[/]");
            }

            AnsiConsole.WriteLine();
            if (!string.IsNullOrEmpty(_status))
                AnsiConsole.MarkupLine(_status);

            var key = Console.ReadKey(intercept: true);
            _status = "";
            switch (key.Key)
            {
                case ConsoleKey.J or ConsoleKey.DownArrow:
                    _selectedIdx = (_selectedIdx + 1) % _agents.Length;
                    break;
                case ConsoleKey.K or ConsoleKey.UpArrow:
                    _selectedIdx = (_selectedIdx - 1 + _agents.Length) % _agents.Length;
                    break;
                case ConsoleKey.Spacebar:
                    _agents[_selectedIdx].Selected = !_agents[_selectedIdx].Selected;
                    break;
                case ConsoleKey.Enter:
                    RunAgent(_agents[_selectedIdx]);
                    break;
                case ConsoleKey.Q or ConsoleKey.Escape:
                    return;
            }
        }
    }

    private static void RunAgent(McpAgent agent)
    {
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
