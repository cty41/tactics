#if UNITY_EDITOR
using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Tactics.Common.Skills.Graph;
using Tactics.EditorTools;

namespace Tactics.Editor.MCP
{
    [McpForUnityTool("list_presentation_graphs")]
    public static class ListPresentationGraphsTool
    {
        public static object HandleCommand(JObject @params) =>
            new SuccessResponse("Presentation graphs listed.", PresentationAuthoringFacade.ListGraphs());
    }

    [McpForUnityTool("get_presentation_graph")]
    public static class GetPresentationGraphTool
    {
        public static object HandleCommand(JObject @params) => Execute(() =>
            PresentationAuthoringFacade.GetGraph(@params.Value<string>("graphPath")));

        private static object Execute(Func<JObject> action)
        {
            try { return new SuccessResponse("Presentation graph loaded.", action()); }
            catch (Exception exception) { return new ErrorResponse(exception.Message); }
        }
    }

    [McpForUnityTool("validate_presentation_changeset")]
    public static class ValidatePresentationChangeSetTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                if (@params["changeSets"] is JArray batch)
                    return new SuccessResponse(
                        "Presentation ChangeSet batch validated without writes.",
                        PresentationAuthoringFacade.ValidateChangeSets(batch));
                return new SuccessResponse(
                    "Presentation ChangeSet validated without writes.",
                    PresentationAuthoringFacade.ValidateChangeSet(@params));
            }
            catch (Exception exception) { return new ErrorResponse(exception.Message); }
        }
    }

    [McpForUnityTool("apply_presentation_changeset")]
    public static class ApplyPresentationChangeSetTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                if (@params["changeSets"] is JArray batch)
                    return new SuccessResponse(
                        "Presentation ChangeSet batch applied atomically.",
                        PresentationAuthoringFacade.ApplyChangeSets(batch));
                return new SuccessResponse(
                    "Presentation ChangeSet applied atomically.",
                    PresentationAuthoringFacade.ApplyChangeSet(@params));
            }
            catch (Exception exception) { return new ErrorResponse(exception.Message); }
        }
    }

    [McpForUnityTool("preview_presentation")]
    public static class PreviewPresentationTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                return new SuccessResponse(
                    "Presentation preview rendered.",
                    PresentationAuthoringFacade.Preview(
                        @params.Value<string>("graphPath"),
                        @params));
            }
            catch (Exception exception) { return new ErrorResponse(exception.Message); }
        }
    }
}
#endif
