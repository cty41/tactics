#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Tactics.CodeAnalysis
{
    /// <summary>
    /// Manages symbol index for all C# types in the project.
    /// Maps fully qualified type names to their source file paths.
    /// </summary>
    public class SymbolIndex
    {
        private readonly Dictionary<string, List<SymbolInfo>> _symbolsByFullName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SymbolInfo> _symbolsByFilePath = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _projectNamespaces = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<SymbolInfo> AllSymbols => _symbolsByFilePath.Values;
        public IReadOnlyCollection<string> ProjectNamespaces => _projectNamespaces;

        public void Clear()
        {
            _symbolsByFullName.Clear();
            _symbolsByFilePath.Clear();
            _projectNamespaces.Clear();
        }

        public void AddSymbol(SymbolInfo symbol)
        {
            if (!_symbolsByFullName.TryGetValue(symbol.FullName, out var list))
            {
                list = new List<SymbolInfo>();
                _symbolsByFullName[symbol.FullName] = list;
            }
            list.Add(symbol);

            _symbolsByFilePath[symbol.FilePath] = symbol;

            if (!string.IsNullOrEmpty(symbol.Namespace))
            {
                _projectNamespaces.Add(symbol.Namespace);
            }
        }

        public List<SymbolInfo> GetSymbolsByFullName(string fullName)
        {
            return _symbolsByFullName.TryGetValue(fullName, out var list) ? list : new List<SymbolInfo>();
        }

        public SymbolInfo? GetSymbolByFilePath(string filePath)
        {
            return _symbolsByFilePath.TryGetValue(filePath, out var symbol) ? symbol : null;
        }

        public bool IsProjectType(string namespaceOrTypeName)
        {
            if (string.IsNullOrEmpty(namespaceOrTypeName))
                return false;

            // Check if it's a namespace we know
            if (_projectNamespaces.Any(ns => namespaceOrTypeName.StartsWith(ns + ".") || namespaceOrTypeName == ns))
                return true;

            // Check if it's a type name we know
            if (_symbolsByFullName.ContainsKey(namespaceOrTypeName))
                return true;

            return false;
        }

        /// <summary>
        /// Extracts type name from a potential generic type like List&lt;T&gt;
        /// </summary>
        public static string ExtractTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeName;

            var genericIndex = typeName.IndexOf('<');
            if (genericIndex > 0)
            {
                return typeName.Substring(0, genericIndex);
            }

            return typeName;
        }
    }

    /// <summary>
    /// Represents a C# type symbol extracted from source code.
    /// </summary>
    public class SymbolInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string FullName => string.IsNullOrEmpty(Namespace) ? TypeName : $"{Namespace}.{TypeName}";
        public SymbolKind Kind { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsStatic { get; set; }
        public bool IsInterface => Kind == SymbolKind.Interface;
        public bool IsClass => Kind == SymbolKind.Class;
        public bool IsStruct => Kind == SymbolKind.Struct;
        public bool IsEnum => Kind == SymbolKind.Enum;
        public string? BaseTypeName { get; set; }
        public List<string> InterfaceNames { get; set; } = new();
        public List<string> UsingNamespaces { get; set; } = new();
    }

    public enum SymbolKind
    {
        Class,
        Interface,
        Struct,
        Enum
    }
}
