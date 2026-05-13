using System.Collections;
using System.Collections.Generic;
using Tactics.Cheats;
using Tactics.Runtime.BattleLog;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    /// <summary>
    /// UI Toolkit controller for the in-game cheat/debug console.
    /// Displays battle logs and accepts command input via a bottom text field.
    /// </summary>
    public sealed class CheatConsoleUI : UIControllerBase
    {
        private const int MaxLogEntries = 50;
        private const int MaxHistorySize = 50;
        private const string HistoryPrefsKey = "Tactics_CheatConsole_History";

        private readonly List<string> _commandHistory = new List<string>();
        private int _historyIndex = -1;

        private VisualElement _rootContainer;
        private ScrollView _logList;
        private TextField _commandInput;
        private readonly Queue<VisualElement> _logEntryPool = new Queue<VisualElement>();

        protected override void OnShown()
        {
            StartCoroutine(InitializeUI());
        }

        private IEnumerator InitializeUI()
        {
            yield return null;

            var root = Ui.GetRootElement(UIManager.UIId.CheatConsole);
            if (root == null)
            {
                TLog.Warning("[CheatConsoleUI] Root visual element still null after waiting. Retrying...");
                yield return null;
                root = Ui.GetRootElement(UIManager.UIId.CheatConsole);
            }

            if (root == null)
            {
                TLog.Error("[CheatConsoleUI] Failed to get root visual element after retry.");
                yield break;
            }

            _rootContainer = root.Q<VisualElement>("CheatConsoleRoot");
            _logList = root.Q<ScrollView>("LogList");
            _commandInput = root.Q<TextField>("CommandInput");

            if (_rootContainer != null)
            {
                _rootContainer.style.display = DisplayStyle.Flex;
                _rootContainer.style.position = Position.Absolute;
                _rootContainer.style.top = 0;
                _rootContainer.style.left = 0;
                _rootContainer.style.right = 0;
                _rootContainer.style.height = Length.Percent(35);
                _rootContainer.style.backgroundColor = new Color(0, 0, 0, 0.85f);
                _rootContainer.style.flexDirection = FlexDirection.Column;
            }

            TBattleLog.OnLogToUI += HandleLogEntry;

            if (_commandInput != null)
            {
                _commandInput.RegisterCallback<NavigationSubmitEvent>(OnCommandSubmitted);
                _commandInput.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                _commandInput.style.height = StyleKeyword.Auto;
                _commandInput.style.fontSize = 16;
                _commandInput.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.95f);
                _commandInput.style.color = Color.white;
                _commandInput.Focus();
                LoadHistory();
            }
        }

        protected override void OnHidden()
        {
            TBattleLog.OnLogToUI -= HandleLogEntry;

            if (_commandInput != null)
            {
                _commandInput.UnregisterCallback<NavigationSubmitEvent>(OnCommandSubmitted);
                _commandInput.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            }

            if (_rootContainer != null)
                _rootContainer.style.display = DisplayStyle.None;
        }

        private void HandleLogEntry(BattleLogData data)
        {
            if (data == null || _logList == null)
                return;

            var label = new Label(data.GetDisplayString());
            label.AddToClassList("log-entry");
            label.style.color = GetColorForType(data.ActionType);

            _logList.contentContainer.Add(label);
            _logEntryPool.Enqueue(label);

            while (_logEntryPool.Count > MaxLogEntries)
            {
                var oldest = _logEntryPool.Dequeue();
                if (oldest != null)
                    oldest.RemoveFromHierarchy();
            }

            ScrollToBottom();
        }

        private void OnCommandSubmitted(NavigationSubmitEvent evt)
        {
            SubmitCommand();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                evt.StopImmediatePropagation();
                SubmitCommand();
            }
            else if (evt.keyCode == KeyCode.UpArrow)
            {
                evt.StopPropagation();
                NavigateHistory(-1);
            }
            else if (evt.keyCode == KeyCode.DownArrow)
            {
                evt.StopPropagation();
                NavigateHistory(1);
            }
            else
            {
                _historyIndex = -1;
            }
        }

        private void SubmitCommand()
        {
            if (_commandInput == null)
                return;

            string command = _commandInput.value;
            if (string.IsNullOrWhiteSpace(command))
                return;

            var inputLabel = new Label($"> {command}");
            inputLabel.AddToClassList("log-entry");
            inputLabel.style.color = Color.white;

            _logList.contentContainer.Add(inputLabel);
            _logEntryPool.Enqueue(inputLabel);

            string result = CheatCommandManager.Instance.Execute(command);
            if (!string.IsNullOrEmpty(result))
            {
                var resultLabel = new Label(result);
                resultLabel.AddToClassList("log-entry");
                resultLabel.style.color = result.StartsWith("[Error]") ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.3f);
                _logList.contentContainer.Add(resultLabel);
                _logEntryPool.Enqueue(resultLabel);
            }

            while (_logEntryPool.Count > MaxLogEntries)
            {
                var oldest = _logEntryPool.Dequeue();
                if (oldest != null)
                    oldest.RemoveFromHierarchy();
            }

            ScrollToBottom();

            _commandInput.value = string.Empty;

            // 添加到历史记录（去重）
            string lastCmd = _commandHistory.Count > 0 ? _commandHistory[_commandHistory.Count - 1] : null;
            if (lastCmd == command)
                _commandHistory.RemoveAt(_commandHistory.Count - 1);
            _commandHistory.Add(command);
            if (_commandHistory.Count > MaxHistorySize)
                _commandHistory.RemoveAt(0);
            _historyIndex = -1;
            SaveHistory();

            _commandInput.Focus();
        }

        private void SaveHistory()
        {
            if (_commandHistory.Count == 0) return;
            PlayerPrefs.SetString(HistoryPrefsKey, string.Join("|", _commandHistory));
        }

        private void LoadHistory()
        {
            if (!PlayerPrefs.HasKey(HistoryPrefsKey)) return;
            string saved = PlayerPrefs.GetString(HistoryPrefsKey);
            var commands = saved.Split('|');
            _commandHistory.Clear();
            foreach (var cmd in commands)
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                    _commandHistory.Add(cmd);
            }
            while (_commandHistory.Count > MaxHistorySize)
                _commandHistory.RemoveAt(0);
        }

        private void NavigateHistory(int direction)
        {
            if (_commandInput == null || _commandHistory.Count == 0)
                return;

            // 首次 ArrowUp（direction=-1）：从最新命令开始
            if (_historyIndex < 0 && direction < 0)
                _historyIndex = _commandHistory.Count;

            _historyIndex += direction;

            if (_historyIndex < 0)
            {
                _historyIndex = -1;
                _commandInput.value = string.Empty;
                return;
            }

            if (_historyIndex >= _commandHistory.Count)
            {
                _historyIndex = _commandHistory.Count - 1;
                return;
            }

            _commandInput.value = _commandHistory[_historyIndex];
            _commandInput.SelectRange(_commandInput.value.Length, _commandInput.value.Length);
        }

        private void ScrollToBottom()
        {
            if (_logList == null)
                return;

            _logList.scrollOffset = new Vector2(0, float.MaxValue);
        }

        private static Color GetColorForType(BattleActionType type)
        {
            return type switch
            {
                BattleActionType.Attack => new Color(1f, 0.65f, 0f),
                BattleActionType.Skill => new Color(0.58f, 0.44f, 0.86f),
                BattleActionType.TurnStart => new Color(1f, 0.84f, 0f),
                BattleActionType.TurnEnd => new Color(1f, 0.84f, 0f),
                BattleActionType.Damage => new Color(0.39f, 0.58f, 0.93f),
                BattleActionType.Destroy => new Color(0.55f, 0f, 0f),
                BattleActionType.Heal => new Color(0.2f, 1f, 0.2f),
                BattleActionType.Buff => new Color(1f, 0.4f, 0f),
                _ => Color.white,
            };
        }
    }
}
