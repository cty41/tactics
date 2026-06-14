using System;
using System.Diagnostics;
using System.IO;
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
