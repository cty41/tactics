#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Tactics.CodeAnalysis
{
    /// <summary>
    /// Analyzes code-level dependencies between scripts.
    /// Tracks which scripts reference which other scripts through:
    /// - Using statements
    /// - Type references (base classes, interfaces, fields, parameters)
    /// </summary>
    public class DependencyTracker
    {
        private readonly SymbolIndex _symbolIndex;
        private readonly Dictionary<string, HashSet<string>> _dependencies = new();
        private readonly Dictionary<string, HashSet<string>> _referencedBy = new();

        public DependencyTracker(SymbolIndex symbolIndex)
        {
            _symbolIndex = symbolIndex;
        }

        /// <summary>
        /// Analyzes all scripts and builds dependency graph.
        /// </summary>
        public void AnalyzeDependencies()
        {
            _dependencies.Clear();
            _referencedBy.Clear();

            foreach (var symbol in _symbolIndex.AllSymbols)
            {
                if (!_dependencies.ContainsKey(symbol.FilePath))
                {
                    _dependencies[symbol.FilePath] = new HashSet<string>();
                }
            }

            foreach (var symbol in _symbolIndex.AllSymbols)
            {
                var deps = FindDependencies(symbol);
                foreach (var dep in deps)
                {
                    _dependencies[symbol.FilePath].Add(dep);

                    if (!_referencedBy.ContainsKey(dep))
                    {
                        _referencedBy[dep] = new HashSet<string>();
                    }
                    _referencedBy[dep].Add(symbol.FilePath);
                }
            }
        }

        /// <summary>
        /// Finds all dependencies of a given script.
        /// </summary>
        private HashSet<string> FindDependencies(SymbolInfo symbol)
        {
            var deps = new HashSet<string>();

            try
            {
                var fullPath = Path.Combine(Application.dataPath, symbol.FilePath.Substring(7));
                if (!File.Exists(fullPath))
                {
                    return deps;
                }

                var content = File.ReadAllText(fullPath);

                // 1. Find referenced types from using namespaces
                foreach (var usingNs in symbol.UsingNamespaces)
                {
                    foreach (var otherSymbol in _symbolIndex.AllSymbols)
                    {
                        if (otherSymbol.Namespace == usingNs || otherSymbol.Namespace.StartsWith(usingNs + "."))
                        {
                            if (content.Contains(otherSymbol.TypeName))
                            {
                                deps.Add(otherSymbol.FilePath);
                            }
                        }
                    }
                }

                // 2. Find base type references
                if (!string.IsNullOrEmpty(symbol.BaseTypeName))
                {
                    var baseSymbols = _symbolIndex.GetSymbolsByFullName(symbol.BaseTypeName);
                    foreach (var baseSymbol in baseSymbols)
                    {
                        deps.Add(baseSymbol.FilePath);
                    }
                }

                // 3. Find interface references
                foreach (var interfaceName in symbol.InterfaceNames)
                {
                    var interfaceSymbols = _symbolIndex.GetSymbolsByFullName(interfaceName);
                    foreach (var interfaceSymbol in interfaceSymbols)
                    {
                        deps.Add(interfaceSymbol.FilePath);
                    }
                }

                // 4. Scan for any type name references in the code
                foreach (var otherSymbol in _symbolIndex.AllSymbols)
                {
                    if (otherSymbol.FilePath == symbol.FilePath) continue;
                    
                    var pattern = $@"\b{Regex.Escape(otherSymbol.TypeName)}\b";
                    if (Regex.IsMatch(content, pattern))
                    {
                        deps.Add(otherSymbol.FilePath);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DependencyTracker] Failed to analyze {symbol.FilePath}: {e.Message}");
            }

            return deps;
        }

        /// <summary>
        /// Gets all scripts that depend on the given script.
        /// </summary>
        public HashSet<string> GetReferencedBy(string filePath)
        {
            return _referencedBy.TryGetValue(filePath, out var refs) ? refs : new HashSet<string>();
        }

        /// <summary>
        /// Gets all scripts that the given script depends on.
        /// </summary>
        public HashSet<string> GetDependencies(string filePath)
        {
            return _dependencies.TryGetValue(filePath, out var deps) ? deps : new HashSet<string>();
        }

        /// <summary>
        /// Gets all scripts that are transitively referenced starting from the seed set.
        /// </summary>
        public HashSet<string> GetTransitiveReferences(HashSet<string> seedPaths)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<string>(seedPaths);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (visited.Contains(current)) continue;
                visited.Add(current);

                foreach (var dep in GetDependencies(current))
                {
                    if (!visited.Contains(dep))
                    {
                        queue.Enqueue(dep);
                    }
                }
            }

            return visited;
        }

        /// <summary>
        /// Returns all scripts that are not referenced by any other script.
        /// </summary>
        public List<string> GetLeafScripts()
        {
            return _symbolIndex.AllSymbols
                .Where(s => !_referencedBy.ContainsKey(s.FilePath) || _referencedBy[s.FilePath].Count == 0)
                .Select(s => s.FilePath)
                .ToList();
        }
    }
}
