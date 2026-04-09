#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Tactics.CodeAnalysis
{
    /// <summary>
    /// Uses Roslyn C# AST parser to analyze script references with precision.
    /// Replaces regex-based parsing with semantic-aware type resolution.
    /// </summary>
    public class RoslynCodeAnalyzer
    {
        private const string SearchDirectory = "Assets/Tactics/Scripts";

        // Project namespaces that are "internal" to this project
        private static readonly string[] ProjectNamespacePrefixes = { "Tactics", "TbsFramework" };

        private readonly Dictionary<string, List<SymbolInfo>> _symbolsByFullName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SymbolInfo> _symbolsByFilePath = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<SymbolInfo> _allSymbols = new();
        private readonly List<SpecialAttributeInfo> _specialAttributes = new();
        private readonly List<string> _allScriptPaths = new();

        // Dependency graph: filePath -> set of filePaths it references
        private readonly Dictionary<string, HashSet<string>> _referencedBy = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _dependenciesOf = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<SymbolInfo> AllSymbols => _allSymbols;
        public IReadOnlyList<string> AllScriptPaths => _allScriptPaths;
        public IReadOnlyList<SpecialAttributeInfo> SpecialAttributes => _specialAttributes;

        /// <summary>
        /// Scans all C# files using Roslyn AST parsing.
        /// </summary>
        public void ScanAllScripts()
        {
            _symbolsByFullName.Clear();
            _symbolsByFilePath.Clear();
            _allSymbols.Clear();
            _specialAttributes.Clear();
            _allScriptPaths.Clear();
            _referencedBy.Clear();
            _dependenciesOf.Clear();

            var fullPath = Path.Combine(Application.dataPath, "Tactics", "Scripts");
            if (!Directory.Exists(fullPath))
            {
                Debug.LogError($"[RoslynCodeAnalyzer] Directory not found: {fullPath}");
                return;
            }

            var files = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);

            // Phase 1: Parse all files and collect symbols + type references
            var parsedFiles = new List<ParsedFile>();
            foreach (var file in files)
            {
                var relativePath = file.Replace(Application.dataPath, "Assets").Replace('\\', '/');
                _allScriptPaths.Add(relativePath);
                _dependenciesOf[relativePath] = new HashSet<string>();
            }

            foreach (var file in files)
            {
                var relativePath = file.Replace(Application.dataPath, "Assets").Replace('\\', '/');
                try
                {
                    var content = File.ReadAllText(file);
                    var parsed = ParseFileWithRoslyn(relativePath, content);
                    parsedFiles.Add(parsed);

                    foreach (var symbol in parsed.Symbols)
                    {
                        AddSymbol(symbol);
                    }
                    _specialAttributes.AddRange(parsed.SpecialAttributes);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RoslynCodeAnalyzer] Failed to parse {relativePath}: {e.Message}");
                }
            }

            // Phase 2: Resolve type references to file paths
            foreach (var parsed in parsedFiles)
            {
                foreach (var referredTypeName in parsed.ReferredTypeNames)
                {
                    var targetFiles = ResolveTypeToFile(referredTypeName, parsed.FilePath);
                    foreach (var targetFile in targetFiles)
                    {
                        if (targetFile != parsed.FilePath)
                        {
                            _dependenciesOf[parsed.FilePath].Add(targetFile);

                            if (!_referencedBy.ContainsKey(targetFile))
                                _referencedBy[targetFile] = new HashSet<string>();
                            _referencedBy[targetFile].Add(parsed.FilePath);
                        }
                    }
                }
            }

            Debug.Log($"[RoslynCodeAnalyzer] Scanned {_allScriptPaths.Count} files, found {_allSymbols.Count} symbols, {_specialAttributes.Count} special attributes");
        }

        #region Roslyn Parsing

        private ParsedFile ParseFileWithRoslyn(string relativePath, string content)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(content, path: relativePath);
            var root = syntaxTree.GetRoot();

            var symbols = new List<SymbolInfo>();
            var specialAttributes = new List<SpecialAttributeInfo>();
            var referredTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Namespace handling
            var namespaceDeclarations = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().ToList();
            string? defaultNamespace = null;

            if (namespaceDeclarations.Count == 0)
            {
                // File-level namespace (C# 10 file-scoped namespace)
                var fileScopedNs = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
                if (fileScopedNs != null)
                {
                    defaultNamespace = fileScopedNs.Name.ToString();
                }
            }

            foreach (var nsDecl in namespaceDeclarations)
            {
                var ns = nsDecl.Name.ToString();
                ExtractTypes(nsDecl, ns, relativePath, symbols, specialAttributes, referredTypeNames);
            }

            var fileScoped = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
            if (fileScoped != null)
            {
                ExtractTypes(fileScoped, fileScoped.Name.ToString(), relativePath, symbols, specialAttributes, referredTypeNames);
            }

            // Top-level types (no namespace)
            var topLevelTypes = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Where(t => t.Parent is CompilationUnitSyntax);
            foreach (var typeDecl in topLevelTypes)
            {
                ExtractSingleType(typeDecl, string.Empty, relativePath, symbols, specialAttributes, referredTypeNames);
            }

            // Collect using statements for external dependency tracking
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
                .Select(u => u.Name?.ToString())
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            // Collect all external usings
            foreach (var symbol in symbols)
            {
                foreach (var u in usings)
                {
                    if (!IsProjectNamespace(u!))
                    {
                        if (!symbol.ExternalUsingNamespaces.Contains(u!))
                            symbol.ExternalUsingNamespaces.Add(u);
                    }
                }
            }

            // Also collect the referred names from attributes like typeof(), GetComponent<T>()
            CollectTypeReferencesFromExpressions(root, referredTypeNames);

            return new ParsedFile
            {
                FilePath = relativePath,
                Symbols = symbols,
                SpecialAttributes = specialAttributes,
                ReferredTypeNames = referredTypeNames
            };
        }

        private void ExtractTypes(BaseNamespaceDeclarationSyntax nsDecl, string ns, string filePath,
            List<SymbolInfo> symbols, List<SpecialAttributeInfo> specialAttributes, HashSet<string> referredTypeNames)
        {
            // Direct children that are type declarations
            foreach (var child in nsDecl.Members)
            {
                if (child is BaseTypeDeclarationSyntax typeDecl)
                {
                    ExtractSingleType(typeDecl, ns, filePath, symbols, specialAttributes, referredTypeNames);
                }
            }
        }

        private void ExtractSingleType(BaseTypeDeclarationSyntax typeDecl, string ns, string filePath,
            List<SymbolInfo> symbols, List<SpecialAttributeInfo> specialAttributes, HashSet<string> referredTypeNames,
            string? parentTypeName = null)
        {
            var typeKind = typeDecl switch
            {
                ClassDeclarationSyntax _ => SymbolKind.Class,
                InterfaceDeclarationSyntax _ => SymbolKind.Interface,
                StructDeclarationSyntax _ => SymbolKind.Struct,
                EnumDeclarationSyntax _ => SymbolKind.Enum,
                _ => SymbolKind.Class
            };

            var typeName = typeDecl.Identifier.Text;
            var fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
            if (!string.IsNullOrEmpty(parentTypeName))
                fullName = $"{parentTypeName}.{typeName}";

            var symbol = new SymbolInfo
            {
                FileName = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath,
                Namespace = ns,
                TypeName = typeName,
                Kind = typeKind,
                ParentTypeName = parentTypeName
            };

            // Modifiers
            var modifiers = typeDecl.Modifiers.Select(m => m.Text).ToHashSet();
            symbol.IsAbstract = modifiers.Contains("abstract");
            symbol.IsStatic = modifiers.Contains("static");
            symbol.IsPartial = modifiers.Contains("partial");

            // Base types and interfaces
            if (typeDecl.BaseList != null)
            {
                var baseTypes = typeDecl.BaseList.Types.Select(t => t.Type.ToString().Trim())
                    .Where(t => !string.IsNullOrEmpty(t)).ToList();

                for (int i = 0; i < baseTypes.Count; i++)
                {
                    var baseType = StripGeneric(baseTypes[i]);
                    if (typeKind == SymbolKind.Interface)
                    {
                        // All base types for interfaces are other interfaces
                        symbol.InterfaceNames.Add(baseType);
                    }
                    else if (i == 0)
                    {
                        // First base type for class/struct is the base class (could be System.Object)
                        symbol.BaseTypeName = baseType;
                    }
                    else
                    {
                        symbol.InterfaceNames.Add(baseType);
                    }
                }

                // Track references to all base types
                foreach (var bt in baseTypes)
                {
                    referredTypeNames.Add(StripGeneric(bt));
                }
            }

            // Collect type references from member declarations
            CollectTypeReferencesFromMembers(typeDecl, referredTypeNames);

            // Special attributes on the type itself
            CollectSpecialAttributes(typeDecl, filePath, ns, typeName, specialAttributes);

            symbols.Add(symbol);

            // Recursively process nested types
            var nestedTypes = typeDecl.ChildNodes().OfType<BaseTypeDeclarationSyntax>();
            foreach (var nested in nestedTypes)
            {
                ExtractSingleType(nested, ns, filePath, symbols, specialAttributes, referredTypeNames, typeName);
            }
        }

        private void CollectTypeReferencesFromMembers(BaseTypeDeclarationSyntax typeDecl, HashSet<string> referredTypeNames)
        {
            // Fields
            foreach (var fieldDecl in typeDecl.ChildNodes().OfType<FieldDeclarationSyntax>())
            {
                var typeStr = fieldDecl.Declaration.Type.ToString();
                referredTypeNames.Add(StripGeneric(typeStr));
            }

            // Properties
            foreach (var propDecl in typeDecl.ChildNodes().OfType<PropertyDeclarationSyntax>())
            {
                var typeStr = propDecl.Type.ToString();
                referredTypeNames.Add(StripGeneric(typeStr));
            }

            // Methods
            foreach (var methodDecl in typeDecl.ChildNodes().OfType<MethodDeclarationSyntax>())
            {
                var returnType = methodDecl.ReturnType.ToString();
                referredTypeNames.Add(StripGeneric(returnType));

                foreach (var param in methodDecl.ParameterList.Parameters)
                {
                    var paramType = param.Type?.ToString();
                    if (!string.IsNullOrEmpty(paramType))
                        referredTypeNames.Add(StripGeneric(paramType!));
                }
            }

            // Generic type parameter constraints
            foreach (var constraint in typeDecl.ChildNodes().OfType<TypeParameterConstraintClauseSyntax>())
            {
                foreach (var constraintType in constraint.Constraints)
                {
                    referredTypeNames.Add(StripGeneric(constraintType.ToString()));
                }
            }

            // Attributes - collect attribute types
            foreach (var attrList in typeDecl.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var attrName = attr.Name.ToString();
                    referredTypeNames.Add(StripGeneric(attrName));
                }
            }
        }

        private void CollectTypeReferencesFromExpressions(SyntaxNode root, HashSet<string> referredTypeNames)
        {
            // typeof(T)
            foreach (var typeofExpr in root.DescendantNodes().OfType<TypeOfExpressionSyntax>())
            {
                referredTypeNames.Add(StripGeneric(typeofExpr.Type.ToString()));
            }

            // new TypeName()
            foreach (var objCreation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                referredTypeNames.Add(StripGeneric(objCreation.Type.ToString()));
            }

            // var x = (TypeName)y - cast expressions
            foreach (var cast in root.DescendantNodes().OfType<CastExpressionSyntax>())
            {
                referredTypeNames.Add(StripGeneric(cast.Type.ToString()));
            }

            // as TypeName
            foreach (var asExpr in root.DescendantNodes().OfType<BinaryExpressionSyntax>())
            {
                if (asExpr.IsKind(SyntaxKind.AsExpression))
                {
                    referredTypeNames.Add(StripGeneric(asExpr.Right.ToString()));
                }
            }

            // is TypeName
            foreach (var isPattern in root.DescendantNodes().OfType<IsPatternExpressionSyntax>())
            {
                if (isPattern.Pattern is DeclarationPatternSyntax dp)
                {
                    referredTypeNames.Add(StripGeneric(dp.Type.ToString()));
                }
            }

            // Method invocation with generic type arguments: GetComponent<Transform>()
            foreach (var invoke in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invoke.Expression is GenericNameSyntax generic)
                {
                    foreach (var typeArg in generic.TypeArgumentList.Arguments)
                    {
                        referredTypeNames.Add(StripGeneric(typeArg.ToString()));
                    }
                }
            }

            // Generic local function declarations
            foreach (var localFunc in root.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
            {
                var retType = localFunc.ReturnType.ToString();
                referredTypeNames.Add(StripGeneric(retType));
                foreach (var param in localFunc.ParameterList.Parameters)
                {
                    var pType = param.Type?.ToString();
                if (!string.IsNullOrEmpty(pType))
                    referredTypeNames.Add(StripGeneric(pType!));
                }
            }
        }

        private void CollectSpecialAttributes(BaseTypeDeclarationSyntax typeDecl, string filePath, string ns, string typeName, List<SpecialAttributeInfo> specialAttributes)
        {
            foreach (var attrList in typeDecl.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var attrName = attr.Name.ToString();
                    if (attrName == "CreateAssetMenu" || attrName == "CreateAssetMenuAttribute")
                    {
                        specialAttributes.Add(new SpecialAttributeInfo
                        {
                            FilePath = filePath,
                            AttributeName = "CreateAssetMenu",
                            Description = "Creates ScriptableObject asset",
                            ClassName = typeName,
                            Namespace = ns
                        });
                    }
                    if (attrName == "InitializeOnLoad" || attrName == "InitializeOnLoadAttribute")
                    {
                        specialAttributes.Add(new SpecialAttributeInfo
                        {
                            FilePath = filePath,
                            AttributeName = "InitializeOnLoad",
                            Description = "Editor initialization",
                            ClassName = typeName,
                            Namespace = ns
                        });
                    }
                }
            }

            // Check methods for [RuntimeInitializeOnLoadMethod] and [MenuItem]
            foreach (var methodDecl in typeDecl.ChildNodes().OfType<MethodDeclarationSyntax>())
            {
                foreach (var attrList in methodDecl.AttributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        var attrName = attr.Name.ToString();
                        if (attrName == "RuntimeInitializeOnLoadMethod" || attrName == "RuntimeInitializeOnLoadMethodAttribute")
                        {
                            specialAttributes.Add(new SpecialAttributeInfo
                            {
                                FilePath = filePath,
                                AttributeName = "RuntimeInitializeOnLoadMethod",
                                Description = "Auto-initialized at runtime",
                                ClassName = typeName,
                                Namespace = ns
                            });
                        }
                        if (attrName == "MenuItem" || attrName == "MenuItemAttribute")
                        {
                            specialAttributes.Add(new SpecialAttributeInfo
                            {
                                FilePath = filePath,
                                AttributeName = "MenuItem",
                                Description = "Editor menu command",
                                ClassName = typeName,
                                Namespace = ns
                            });
                        }
                    }
                }
            }
        }

        #endregion

        #region Symbol Index

        private void AddSymbol(SymbolInfo symbol)
        {
            if (!_symbolsByFullName.TryGetValue(symbol.FullName, out var list))
            {
                list = new List<SymbolInfo>();
                _symbolsByFullName[symbol.FullName] = list;
            }
            list.Add(symbol);

            // Only store primary (non-nested) type in the file->symbol map
            if (string.IsNullOrEmpty(symbol.ParentTypeName))
            {
                _symbolsByFilePath[symbol.FilePath] = symbol;
            }

            _allSymbols.Add(symbol);
        }

        /// <summary>
        /// Resolves a type name to file path(s).
        /// Handles simple names (via using), fully qualified names, and partial matches.
        /// </summary>
        private HashSet<string> ResolveTypeToFile(string typeName, string sourceFilePath)
        {
            var results = new HashSet<string>();

            // Strip "Attribute" suffix for attribute types
            var resolvedName = typeName;
            if (typeName.EndsWith("Attribute"))
            {
                // Try both with and without suffix
                var shortName = typeName.Substring(0, typeName.Length - "Attribute".Length);
                foreach (var candidate in TryResolve(shortName, sourceFilePath))
                    results.Add(candidate);
            }

            foreach (var candidate in TryResolve(resolvedName, sourceFilePath))
                results.Add(candidate);

            return results;
        }

        private IEnumerable<string> TryResolve(string typeName, string sourceFilePath)
        {
            if (string.IsNullOrEmpty(typeName)) yield break;

            // Strip generic parameters
            typeName = StripGeneric(typeName);

            // Direct full name match
            if (_symbolsByFullName.TryGetValue(typeName, out var exactMatch))
            {
                foreach (var s in exactMatch) yield return s.FilePath;
                yield break;
            }

            // Short name match - need to find the correct namespace
            // Check if source file has a primary type
            if (_symbolsByFilePath.TryGetValue(sourceFilePath, out var sourceSymbol))
            {
                var sourceNs = sourceSymbol.Namespace;

                // Try same namespace first
                var fullName = $"{sourceNs}.{typeName}";
                if (_symbolsByFullName.TryGetValue(fullName, out var nsMatch))
                {
                    foreach (var s in nsMatch) yield return s.FilePath;
                    yield break;
                }

                // Try sub-namespaces
                var nsPrefix = sourceNs + ".";
                foreach (var kvp in _symbolsByFullName)
                {
                    if (kvp.Key.StartsWith(nsPrefix) && kvp.Key.EndsWith("." + typeName) &&
                        !kvp.Key.Substring(nsPrefix.Length).Contains("."))
                    {
                        // This is a direct child namespace match, probably not what we want
                    }
                }
            }

            // Short name match across all known symbols (with project namespace filter)
            foreach (var symbol in _allSymbols)
            {
                if (symbol.TypeName == typeName)
                {
                    yield return symbol.FilePath;
                }
            }
        }

        /// <summary>
        /// Gets all scripts transitively referenced from the seed set.
        /// </summary>
        public HashSet<string> GetTransitiveReferences(HashSet<string> seedPaths)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();

            foreach (var seed in seedPaths)
                queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (visited.Contains(current)) continue;
                visited.Add(current);

                if (_dependenciesOf.TryGetValue(current, out var deps))
                {
                    foreach (var dep in deps)
                    {
                        if (!visited.Contains(dep))
                            queue.Enqueue(dep);
                    }
                }
            }

            return visited;
        }

        /// <summary>
        /// Gets the dependency chain for each referenced script.
        /// </summary>
        public List<DependencyChainEntry> BuildDependencyChains(HashSet<string> referencedPaths)
        {
            var chains = new List<DependencyChainEntry>();
            foreach (var path in referencedPaths.OrderBy(p => p))
            {
                var dependsOn = _dependenciesOf.TryGetValue(path, out var deps) ? deps.ToList() : new List<string>();
                var referencedBy = _referencedBy.TryGetValue(path, out var refs) ? refs.ToList() : new List<string>();

                chains.Add(new DependencyChainEntry
                {
                    FilePath = path,
                    DependsOn = dependsOn,
                    ReferencedBy = referencedBy
                });
            }
            return chains;
        }

        public List<string> GetLeafScripts()
        {
            return _allSymbols
                .Where(s => string.IsNullOrEmpty(s.ParentTypeName))
                .Where(s => !_referencedBy.ContainsKey(s.FilePath) || _referencedBy[s.FilePath].Count == 0)
                .Select(s => s.FilePath)
                .ToList();
        }

        public bool IsProjectNamespace(string ns)
        {
            if (string.IsNullOrEmpty(ns)) return false;
            return ProjectNamespacePrefixes.Any(p => ns == p || ns.StartsWith(p + "."));
        }

        private static string StripGeneric(string typeName)
        {
            var idx = typeName.IndexOf('<');
            return idx > 0 ? typeName.Substring(0, idx).Trim() : typeName;
        }

        #endregion
    }

    #region Helper Types

    internal class ParsedFile
    {
        public string FilePath { get; set; } = string.Empty;
        public List<SymbolInfo> Symbols { get; set; } = new();
        public List<SpecialAttributeInfo> SpecialAttributes { get; set; } = new();
        public HashSet<string> ReferredTypeNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class DependencyChainEntry
    {
        public string FilePath { get; set; } = string.Empty;
        public List<string> DependsOn { get; set; } = new();
        public List<string> ReferencedBy { get; set; } = new();
    }

    #endregion
}
