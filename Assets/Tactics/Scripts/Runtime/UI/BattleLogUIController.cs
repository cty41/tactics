using System;
using System.Collections.Generic;
using Tactics.Runtime.BattleLog;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tactics.Runtime.UI
{
    /// <summary>
    /// Controller for displaying battle logs in the UI.
    /// </summary>
    public class BattleLogUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _contentPanel;
        [SerializeField] private TMP_Text _logText;

        [Header("Configuration")]
        [SerializeField] private BattleLogUIConfig _config;

        [Header("Entry Settings")]
        [SerializeField] private float _entrySpacing = 5f;
        [SerializeField] private bool _useTimestamp = true;

        private List<string> _logEntries = new List<string>();
        private List<Color> _entryColors = new List<Color>();

        private void Awake()
        {
            if (_logText == null)
            {
                _logText = GetComponentInChildren<TMP_Text>(true);
            }

            if (_scrollRect == null)
            {
                _scrollRect = GetComponentInChildren<ScrollRect>(true);
            }

            if (_contentPanel == null && _scrollRect != null)
            {
                _contentPanel = _scrollRect.content;
            }
        }

        private void OnEnable()
        {
            BattleLogger.OnLogToUI += HandleLogEntry;
        }

        private void OnDisable()
        {
            BattleLogger.OnLogToUI -= HandleLogEntry;
        }

        private void Start()
        {
            if (_config == null)
            {
                // Try to load from Resources
                _config = Resources.Load<BattleLogUIConfig>("BattleLogUIConfig");
            }

            ClearDisplay();
        }

        private void HandleLogEntry(BattleLogData data)
        {
            if (data == null)
                return;

            AddLogEntry(data);
        }

        private void AddLogEntry(BattleLogData data)
        {
            string displayString = FormatLogEntry(data);
            Color color = GetColorForType(data.ActionType);

            _logEntries.Add(displayString);
            _entryColors.Add(color);

            // Enforce max entries
            if (_config != null && _logEntries.Count > _config.MaxEntries)
            {
                _logEntries.RemoveAt(0);
                _entryColors.RemoveAt(0);
            }

            UpdateDisplay();

            // Auto-scroll to bottom
            if (_config != null && _config.AutoScroll)
            {
                ScrollToBottom();
            }
        }

        private string FormatLogEntry(BattleLogData data)
        {
            string timestamp = "";
            if (_useTimestamp && (_config == null || _config.ShowTimestamp))
            {
                timestamp = $"[{DateTime.Now:HH:mm:ss}] ";
            }

            return $"{timestamp}{data.GetDisplayString()}";
        }

        private Color GetColorForType(BattleActionType type)
        {
            if (_config == null)
                return Color.white;

            switch (type)
            {
                case BattleActionType.Attack:
                    return _config.AttackColor;
                case BattleActionType.Damage:
                    return _config.DamageColor;
                case BattleActionType.Destroy:
                    return _config.DestroyColor;
                case BattleActionType.Skill:
                    return _config.SkillColor;
                case BattleActionType.TurnStart:
                case BattleActionType.TurnEnd:
                    return _config.TurnColor;
                default:
                    return Color.white;
            }
        }

        private void UpdateDisplay()
        {
            if (_logText == null)
                return;

            // Build colored text using TextMesh Pro rich text
            string fullText = "";
            for (int i = 0; i < _logEntries.Count; i++)
            {
                string entry = _logEntries[i];
                Color color = _entryColors[i];
                string colorHex = ColorUtility.ToHtmlStringRGB(color);
                fullText += $"<color=#{colorHex}>{entry}</color>\n";
            }

            _logText.text = fullText;
        }

        private void ScrollToBottom()
        {
            if (_scrollRect == null || _contentPanel == null)
                return;

            // Schedule scroll for end of frame to ensure layout is updated
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
        }

        /// <summary>
        /// Clears all log entries from the display.
        /// </summary>
        public void ClearDisplay()
        {
            _logEntries.Clear();
            _entryColors.Clear();
            UpdateDisplay();
        }

        /// <summary>
        /// Sets the configuration for the battle log UI.
        /// </summary>
        public void SetConfig(BattleLogUIConfig config)
        {
            _config = config;
        }
    }
}
