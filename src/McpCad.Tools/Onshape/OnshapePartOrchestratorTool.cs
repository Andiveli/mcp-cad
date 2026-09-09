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
            var state = new OnshapePartPlanExecutor(provider).Execute(validated);
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
