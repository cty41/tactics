using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.MCP
{
    [McpForUnityTool("generate_skill_graph_spec")]
    public static class GenerateSkillGraphSpecTool
    {
        public static object HandleCommand(JObject @params)
        {
            string text = @params["text"]?.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return new ErrorResponse("text parameter is required.");

            try
            {
                var result = SkillGraphCliHelper.RunCli("generate-skill-graph-spec", $"-t \"{SkillGraphCliHelper.EscapeArg(text)}\"");
                return JObject.Parse(result);
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Failed to generate spec: {ex.Message}");
            }
        }
    }

    [McpForUnityTool("generate_skill_graph_spec_from_answers")]
    public static class GenerateSkillGraphSpecFromAnswersTool
    {
        public static object HandleCommand(JObject @params)
        {
            string answersJson = @params["answers"]?.ToString();
            if (string.IsNullOrWhiteSpace(answersJson))
                return new ErrorResponse("answers parameter is required.");

            try
            {
                var answers = JObject.Parse(answersJson);
                string tempPath = Path.Combine(Application.dataPath, "..", "_temp_answers.json");
                File.WriteAllText(tempPath, answers.ToString());

                string relPath = Path.GetRelativePath(
                    Path.Combine(Application.dataPath, ".."),
                    tempPath).Replace('\\', '/');

                var result = SkillGraphCliHelper.RunCli("generate-skill-graph-spec-answers", $"-a \"{relPath}\"");
                File.Delete(tempPath);

                return JObject.Parse(result);
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Failed to generate spec from answers: {ex.Message}");
            }
        }
    }

    [McpForUnityTool("generate_gameplay_test_spec")]
    public static class GenerateGameplayTestSpecTool
    {
        public static object HandleCommand(JObject @params)
        {
            string text = @params["text"]?.ToString();
            string outputPath = @params["outputPath"]?.ToString();

            if (string.IsNullOrWhiteSpace(text))
                return new ErrorResponse("text parameter is required.");

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = $"Tests/gameplay-specs/{SkillGraphCliHelper.GenerateFileName(text)}.gameplay-test.md";

            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", outputPath);
                string dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var result = SkillGraphCliHelper.RunCli("generate-spec", $"-t \"{SkillGraphCliHelper.EscapeArg(text)}\" -o \"{outputPath}\"");

                if (File.Exists(fullPath))
                {
                    string content = File.ReadAllText(fullPath);
                    return new SuccessResponse("Generated gameplay test spec", new JObject
                    {
                        ["path"] = outputPath,
                        ["content"] = content
                    });
                }

                return JObject.Parse(result);
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Failed to generate gameplay test spec: {ex.Message}");
            }
        }
    }

    [McpForUnityTool("validate_skill_graph_spec")]
    public static class ValidateSkillGraphSpecTool
    {
        public static object HandleCommand(JObject @params)
        {
            string specPath = @params["specPath"]?.ToString();
            if (string.IsNullOrWhiteSpace(specPath))
                return new ErrorResponse("specPath parameter is required.");

            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", specPath);
                if (!File.Exists(fullPath))
                    return new ErrorResponse($"Spec file not found: {specPath}");

                var result = SkillGraphCliHelper.RunCli("validate-spec", $"-s \"{specPath}\"");
                return JObject.Parse(result);
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Failed to validate spec: {ex.Message}");
            }
        }
    }

    [McpForUnityTool("apply_skill_graph_spec")]
    public static class ApplySkillGraphSpecTool
    {
        public static object HandleCommand(JObject @params)
        {
            string specPath = @params["specPath"]?.ToString();
            string graphPath = @params["graphPath"]?.ToString();

            if (string.IsNullOrWhiteSpace(specPath))
                return new ErrorResponse("specPath parameter is required.");
            if (string.IsNullOrWhiteSpace(graphPath))
                return new ErrorResponse("graphPath parameter is required.");

            try
            {
                string fullSpecPath = Path.Combine(Application.dataPath, "..", specPath);
                if (!File.Exists(fullSpecPath))
                    return new ErrorResponse($"Spec file not found: {specPath}");

                // Read spec content
                string specContent = File.ReadAllText(fullSpecPath);
                var facadeType = FindLoadedType("Tactics.Editor.SkillGraphEditor.SkillGraphMcpFacade");
                var specType = FindLoadedType("Tactics.Common.Skills.Graph.SkillGraphSpec");
                if (facadeType == null || specType == null)
                    return new ErrorResponse("SkillGraph editor runtime types are not loaded.");

                var spec = JObject.Parse(specContent).ToObject(specType);
                if (spec == null)
                    return new ErrorResponse("SkillGraphSpec JSON is invalid.");

                var applyMethod = facadeType.GetMethod(
                    "ApplySpec",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null,
                    new[] { typeof(string), specType },
                    null);
                if (applyMethod == null)
                    return new ErrorResponse("ApplySpec method not found.");

                var result = applyMethod.Invoke(null, new object[] { graphPath, spec });
                var resultType = result?.GetType();
                bool success = resultType?.GetField("Success")?.GetValue(result) is bool successValue && successValue;
                bool isValid = resultType?.GetField("IsValid")?.GetValue(result) is bool validValue && validValue;

                return new SuccessResponse("SkillGraph spec applied", new JObject
                {
                    ["graphPath"] = graphPath,
                    ["success"] = success,
                    ["isValid"] = isValid
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Failed to apply spec: {ex.Message}");
            }
        }

        private static Type FindLoadedType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }
    }

    internal static class SkillGraphCliHelper
    {
        internal static string RunCli(string command, string args)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string cliPath = Path.Combine(projectRoot, "Tools", "gameplay-test-spec", "dist", "src", "cli.js");

            if (!File.Exists(cliPath))
                throw new FileNotFoundException($"CLI not found at {cliPath}. Run 'npm run build' in Tools/gameplay-test-spec/.");

            var psi = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{cliPath}\" {command} {args}",
                WorkingDirectory = projectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
                throw new Exception($"CLI error (exit {process.ExitCode}): {stderr}");

            return stdout;
        }

        internal static string EscapeArg(string arg)
        {
            return arg.Replace("\"", "\\\"");
        }

        internal static string GenerateFileName(string text)
        {
            string name = text.Split(new[] { ',', '，', '。', ' ' }, 2)[0].Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrEmpty(name) ? "new-skill" : name.ToLowerInvariant().Replace(' ', '-');
        }
    }
}
