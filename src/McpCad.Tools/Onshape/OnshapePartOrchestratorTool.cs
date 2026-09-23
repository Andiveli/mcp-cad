using McpCad.Tools.Onshape.Orchestration;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpCad.Tools.Onshape;

public partial class OnshapeSkillTools
{
    [McpServerTool]
    public CallToolResult onshape_orchestrate_part(string address_json, string plan_json)
    {
        try
        {
            var (address, plan) = OnshapePartPlanParser.Parse(address_json, plan_json);
            var validated = OnshapePartPlanValidator.Validate(address, plan);
            var bridge = _partModelingContextFactory is null ? null : new OnshapePartPlanContextBridge(_partModelingContextFactory);
            var state = bridge is not null && bridge.TryExecute(validated, out var bridged)
                ? bridged
                : new OnshapePartPlanExecutor(provider).Execute(validated);
            return OnshapeOrchestrationResultBuilder.Build(state);
        }
        catch (ArgumentException exception)
        {
            return OnshapeOrchestrationResultBuilder.PreflightError(
                new OrchestrationError(PreflightCode(exception.Message), exception.Message));
        }
    }

    private static string PreflightCode(string message)
    {
        var separator = message.IndexOfAny([' ', ':']);
        return separator > 0 ? message[..separator] : "INVALID_PLAN_SCHEMA";
    }
}
