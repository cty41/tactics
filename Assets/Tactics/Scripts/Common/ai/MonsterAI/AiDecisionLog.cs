using System.Collections.Generic;
using System.Text;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// AI 决策日志。
    /// 记录决策过程，用于调试和可解释性。
    /// </summary>
    public class AiDecisionLog
    {
        private readonly List<LogEntry> _entries = new();
        private readonly bool _verbose;

        public AiDecisionLog(bool verbose = false)
        {
            _verbose = verbose;
        }

        /// <summary>
        /// 添加信息日志。
        /// </summary>
        public void Info(string message)
        {
            _entries.Add(new LogEntry(LogType.Info, message));
            if (_verbose)
            {
                TLog.Info($"[AI] {message}");
            }
        }

        /// <summary>
        /// 添加规则过滤日志。
        /// </summary>
        public void RuleFiltered(string intentName, string ruleName, string reason)
        {
            _entries.Add(new LogEntry(LogType.RuleFiltered, $"[{intentName}] Filtered by rule '{ruleName}': {reason}"));
            if (_verbose)
            {
                TLog.Info($"[AI] [{intentName}] Filtered by rule '{ruleName}': {reason}");
            }
        }

        /// <summary>
        /// 添加评分日志（原始值 + 曲线值 + 加权值）。
        /// </summary>
        public void ScoreAdded(string intentName, string scoreName, float rawValue, float curveValue, float weightedValue)
        {
            _entries.Add(new LogEntry(LogType.Score,
                $"[{intentName}] Score '{scoreName}': raw={rawValue:F2} curve={curveValue:F2} weighted={weightedValue:F2}"));
            _scoreData.Add(new ScoreData
            {
                IntentName = intentName,
                ScoreName = scoreName,
                RawValue = rawValue,
                CurveValue = curveValue,
                WeightedValue = weightedValue
            });
            if (_verbose)
            {
                TLog.Info($"[AI] [{intentName}] Score '{scoreName}': raw={rawValue:F2} curve={curveValue:F2} weighted={weightedValue:F2}");
            }
        }

        /// <summary>
        /// 添加评分日志（旧版兼容）。
        /// </summary>
        public void ScoreAdded(string intentName, string scoreName, float value, float weight)
        {
            ScoreAdded(intentName, scoreName, value, value, value * weight);
        }

        /// <summary>获取结构化评分数据（用于热力图/图表可视化）</summary>
        public IReadOnlyList<ScoreData> GetScoreData() => _scoreData;

        [System.Serializable]
        public class ScoreData
        {
            public string IntentName;
            public string ScoreName;
            public float RawValue;
            public float CurveValue;
            public float WeightedValue;
        }

        private readonly List<ScoreData> _scoreData = new();

        /// <summary>
        /// 添加最终选择日志。
        /// </summary>
        public void FinalSelection(IntentCandidate selected)
        {
            _entries.Add(new LogEntry(LogType.FinalSelection, $"Selected: {selected.IntentType} (Score: {selected.TotalScore:F2})"));
            TLog.Info($"[AI] Selected: {selected.IntentType} (Score: {selected.TotalScore:F2})");
        }

        /// <summary>
        /// 添加候选列表日志。
        /// </summary>
        public void CandidateList(List<IntentCandidate> candidates)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Candidate intents:");
            foreach (var candidate in candidates)
            {
                string status = candidate.PassedRules ? "PASS" : $"FAIL: {candidate.RuleFailureReason}";
                sb.AppendLine($"  - {candidate.IntentType}: Score={candidate.TotalScore:F2}, Status={status}");
            }
            _entries.Add(new LogEntry(LogType.CandidateList, sb.ToString()));
            if (_verbose)
            {
                TLog.Info($"[AI] {sb}");
            }
        }

        /// <summary>
        /// 获取所有日志条目。
        /// </summary>
        public IReadOnlyList<LogEntry> GetEntries()
        {
            return _entries;
        }

        /// <summary>
        /// 获取格式化的日志文本。
        /// </summary>
        public string GetFormattedLog()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== AI Decision Log ===");
            foreach (var entry in _entries)
            {
                sb.AppendLine($"[{entry.Type}] {entry.Message}");
            }
            sb.AppendLine("=======================");
            return sb.ToString();
        }

        /// <summary>
        /// 清空日志。
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
        }

        public class LogEntry
        {
            public LogType Type { get; }
            public string Message { get; }

            public LogEntry(LogType type, string message)
            {
                Type = type;
                Message = message;
            }
        }

        public enum LogType
        {
            Info,
            RuleFiltered,
            Score,
            FinalSelection,
            CandidateList
        }
    }
}
