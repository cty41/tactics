using System;
using System.Collections.Generic;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// SkillGraph 受控描述协议。
    /// Agent 输出此结构，由 SpecCompiler 编译为 SkillGraphAsset。
    /// </summary>
    [Serializable]
    public class SkillGraphSpec
    {
        public string DisplayName;
        public string Description;
        public List<SkillNodeSpec> Nodes = new();
        public List<SkillEdgeSpec> Edges = new();
    }

    [Serializable]
    public class SkillNodeSpec
    {
        public string Id;
        public string Type;
        public Dictionary<string, object> Parameters = new();
    }

    [Serializable]
    public class SkillEdgeSpec
    {
        public string Source;
        public string Target;
        public string Port;
    }
}
