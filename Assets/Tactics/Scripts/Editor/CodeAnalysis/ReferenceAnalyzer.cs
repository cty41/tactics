#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Tactics.CodeAnalysis
{
    /// <summary>
    /// Scans C# files and extracts symbol information using regex-based parsing.
    /// </summary>
    public class ReferenceAnalyzer
    {
        private static readonly string SearchDirectory = "Assets/Tactics/Scripts";

        private readonly SymbolIndex _symbolIndex = new();
        private readonly List<SpecialAttributeInfo> _specialAttributes = new();
        private readonly List<string> _allScriptPaths = new();

        public SymbolIndex SymbolIndex => _symbolIndex;
        public IReadOnlyList<SpecialAttributeInfo> SpecialAttributes => _specialAttributes;
        public IReadOnlyList<string> AllScriptPaths => _allScriptPaths;

        // Regex patterns
        private static readonly Regex NamespaceRegex = new(@"namespace\s+([\w.]+)", RegexOptions.Compiled);
        private static readonly Regex ClassRegex = new(@"(public|private|protected|internal)?\s*(abstract|sealed|static)?\s*class\s+(\w+)(\s*:\s*([\w\s,.<>]+))?", RegexOptions.Compiled);
        private static readonly Regex InterfaceRegex = new(@"(public|private|protected|internal)?\s*interface\s+(\w+)(\s*:\s*([\w\s,.<>]+))?", RegexOptions.Compiled);
        private static readonly Regex StructRegex = new(@"(public|private|protected|internal)?\s*(readonly)?\s*struct\s+(\w+)", RegexOptions.Compiled);
        private static readonly Regex EnumRegex = new(@"(public|private|protected|internal)?\s*enum\s+(\w+)", RegexOptions.Compiled);
        private static readonly Regex UsingRegex = new(@"using\s+([\w.]+);", RegexOptions.Compiled);
        private static readonly Regex RuntimeInitAttributeRegex = new(@"\[RuntimeInitializeOnLoadMethod", RegexOptions.Compiled);
        private static readonly Regex CreateAssetMenuRegex = new(@"\[CreateAssetMenu", RegexOptions.Compiled);
        private static readonly Regex MenuItemAttributeRegex = new(@"\[MenuItem\s*\(", RegexOptions.Compiled);
        private static readonly Regex InitializeOnLoadRegex = new(@"\[InitializeOnLoad", RegexOptions.Compiled);

        public void ScanAllScripts()
        {
            _symbolIndex.Clear();
            _allScriptPaths.Clear();

            var fullPath = Path.Combine(Application.dataPath, "Tactics", "Scripts");
            if (!Directory.Exists(fullPath))
            {
                Debug.LogError($"[ReferenceAnalyzer] Directory not found: {fullPath}");
                return;
            }

            var files = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                // Convert absolute path to relative: "D:\project\Assets\Tactics\Scripts\xx.cs" -> "Assets/Tactics/Scripts/xx.cs"
                var relativePath = file.Replace(Application.dataPath, "Assets").Replace('\\', '/');
                _allScriptPaths.Add(relativePath);
                ProcessFile(relativePath, file);
            }

            Debug.Log($"[ReferenceAnalyzer] Scanned {_allScriptPaths.Count} script files, found {_symbolIndex.AllSymbols.Count} symbols");
        }

        private void ProcessFile(string relativePath, string fullPath)
        {
            try
            {
                var content = File.ReadAllText(fullPath);
                var fileName = Path.GetFileNameWithoutExtension(relativePath);

                var namespaceMatch = NamespaceRegex.Match(content);
                var namespaceName = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : string.Empty;

                var usings = UsingRegex.Matches(content)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .ToList();

                foreach (Match match in ClassRegex.Matches(content))
                {
                    var symbol = new SymbolInfo
                    {
                        FileName = fileName,
                        FilePath = relativePath,
                        Namespace = namespaceName,
                        TypeName = match.Groups[3].Value,
                        Kind = SymbolKind.Class,
                        IsAbstract = match.Groups[2].Success && match.Groups[2].Value.Contains("abstract"),
                        IsStatic = match.Groups[2].Success && match.Groups[2].Value.Contains("static"),
                        UsingNamespaces = usings
                    };

                    if (match.Groups[5].Success)
                    {
                        var baseTypes = ParseBaseTypes(match.Groups[5].Value);
                        if (baseTypes.Count > 0)
                        {
                            symbol.BaseTypeName = baseTypes[0];
                            symbol.InterfaceNames.AddRange(baseTypes.Skip(1));
                        }
                    }

                    _symbolIndex.AddSymbol(symbol);
                }

                foreach (Match match in InterfaceRegex.Matches(content))
                {
                    var symbol = new SymbolInfo
                    {
                        FileName = fileName,
                        FilePath = relativePath,
                        Namespace = namespaceName,
                        TypeName = match.Groups[2].Value,
                        Kind = SymbolKind.Interface,
                        UsingNamespaces = usings
                    };

                    if (match.Groups[4].Success)
                    {
                        symbol.InterfaceNames.AddRange(ParseBaseTypes(match.Groups[4].Value));
                    }

                    _symbolIndex.AddSymbol(symbol);
                }

                foreach (Match match in StructRegex.Matches(content))
                {
                    _symbolIndex.AddSymbol(new SymbolInfo
                    {
                        FileName = fileName,
                        FilePath = relativePath,
                        Namespace = namespaceName,
                        TypeName = match.Groups[3].Value,
                        Kind = SymbolKind.Struct,
                        UsingNamespaces = usings
                    });
                }

                foreach (Match match in EnumRegex.Matches(content))
                {
                    _symbolIndex.AddSymbol(new SymbolInfo
                    {
                        FileName = fileName,
                        FilePath = relativePath,
                        Namespace = namespaceName,
                        TypeName = match.Groups[2].Value,
                        Kind = SymbolKind.Enum,
                        UsingNamespaces = usings
                    });
                }

                CheckSpecialAttributes(content, relativePath, namespaceName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReferenceAnalyzer] Failed to process file {relativePath}: {e.Message}");
            }
        }

        private void CheckSpecialAttributes(string content, string filePath, string namespaceName)
        {
            if (RuntimeInitAttributeRegex.IsMatch(content))
            {
                _specialAttributes.Add(new SpecialAttributeInfo { FilePath = filePath, AttributeName = "RuntimeInitializeOnLoadMethod", Description = "Auto-initialized at runtime" });
            }
            if (CreateAssetMenuRegex.IsMatch(content))
            {
                _specialAttributes.Add(new SpecialAttributeInfo { FilePath = filePath, AttributeName = "CreateAssetMenu", Description = "Creates ScriptableObject asset" });
            }
            if (MenuItemAttributeRegex.IsMatch(content))
            {
                _specialAttributes.Add(new SpecialAttributeInfo { FilePath = filePath, AttributeName = "MenuItem", Description = "Editor menu command" });
            }
            if (InitializeOnLoadRegex.IsMatch(content))
            {
                _specialAttributes.Add(new SpecialAttributeInfo { FilePath = filePath, AttributeName = "InitializeOnLoad", Description = "Editor initialization" });
            }
        }

        private static List<string> ParseBaseTypes(string baseTypesStr)
        {
            var types = new List<string>();
            var parts = baseTypesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var typeName = part.Trim();
                if (!string.IsNullOrEmpty(typeName))
                {
                    var genericIndex = typeName.IndexOf('<');
                    if (genericIndex > 0) typeName = typeName.Substring(0, genericIndex);
                    types.Add(typeName);
                }
            }
            return types;
        }
    }

    public class SpecialAttributeInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
