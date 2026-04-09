#nullable enable
using System;
using System.Collections.Generic;

namespace Tactics.CodeAnalysis
{
    /// <summary>
    /// C# type symbol extracted from Roslyn AST analysis.
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
        public bool IsPartial { get; set; }
        public bool IsInterface => Kind == SymbolKind.Interface;
        public bool IsClass => Kind == SymbolKind.Class;
        public bool IsStruct => Kind == SymbolKind.Struct;
        public bool IsEnum => Kind == SymbolKind.Enum;
        public string? ParentTypeName { get; set; }
        public string? BaseTypeName { get; set; }
        public List<string> InterfaceNames { get; set; } = new();
        public List<string> ExternalUsingNamespaces { get; set; } = new();
        public List<string> ReferencedTypeNames { get; set; } = new();
    }

    /// <summary>
    /// External package dependency detected via using statements.
    /// </summary>
    public class ExternalDependencyInfo
    {
        public string NamespaceName { get; set; } = string.Empty;
        public List<string> UsedBy { get; set; } = new();
    }

    /// <summary>
    /// Special attribute entry point (e.g. [MenuItem], [CreateAssetMenu]).
    /// </summary>
    public class SpecialAttributeInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
    }

    /// <summary>
    /// Asset (scene/prefab) -> script reference mapping.
    /// </summary>
    public class AssetScriptReference
    {
        public string AssetPath { get; set; } = string.Empty;
        public string AssetType => AssetPath.EndsWith(".unity") ? "Scene" : AssetPath.EndsWith(".prefab") ? "Prefab" : "Asset";
        public string ScriptPath { get; set; } = string.Empty;
        public string ScriptGuid { get; set; } = string.Empty;
    }

    public enum SymbolKind
    {
        Class,
        Interface,
        Struct,
        Enum
    }
}
