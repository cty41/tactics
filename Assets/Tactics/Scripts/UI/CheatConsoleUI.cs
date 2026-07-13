using System;
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
    /// Displays structured battle logs and accepts development-only commands.
    /// </summary>
    public sealed class CheatConsoleUI : UIControllerBase
    {
        private const int MaxLogEntries = 50;
        private const int MaxHistorySize = 50;
        private const float BottomScrollTolerance = 32f;
        private const string HistoryPrefsKey = "Tactics_CheatConsole_History";

        private readonly List<string> _commandHistory = new List<string>();
        private int _historyIndex = -1;

        private VisualElement _rootContainer;
        private ScrollView _logList;
        private TextField _commandInput;
        private Coroutine _initializeCoroutine;
        private readonly Queue<VisualElement> _logEntryPool = new Queue<VisualElement>();
        private bool _isSubscribed;
        private bool _callbacksRegistered;
        private bool _userNearBottom = true;

        private static bool IsDevelopmentBuild => Application.isEditor || Debug.isDebugBuild;

        protected override void OnShown()
        {
            if (_initializeCoroutine != null)
                StopCoroutine(_initializeCoroutine);
            _initializeCoroutine = StartCoroutine(InitializeUI());
        }

        private IEnumerator InitializeUI()
        {
            yield return null;

            var root = Ui.GetRootElement(UIManager.UIId.CheatConsole);
            if (root == null)
            {
                if (this == null || !isActiveAndEnabled)
                    yield break;

                TLog.Warning("[CheatConsoleUI] Root visual element still null after waiting. Retrying...");
                yield return null;
                root = Ui.GetRootElement(UIManager.UIId.CheatConsole);
            }

            if (root == null)
            {
                if (this == null || !isActiveAndEnabled)
                    yield break;

                TLog.Warning("[CheatConsoleUI] Failed to get root visual element after retry. ToggleConsole can retry initialization.");
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
                _rootContainer.style.height = Length.Percent(25);
                _rootContainer.style.backgroundColor = new Color(0, 0, 0, 0.85f);
                _rootContainer.style.flexDirection = FlexDirection.Column;
            }

            if (!_isSubscribed)
            {
                TBattleLog.OnLogToUI += HandleLogEntry;
                TBattleLog.OnLogsCleared += HandleLogsCleared;
                _isSubscribed = true;
            }

            ClearDisplayedLogs();
            foreach (var data in TBattleLog.GetCurrentBattleLogs())
                AddLogEntry(data, false);
            ScrollToBottom();

            if (_commandInput != null)
            {
                if (!_callbacksRegistered)
                {
                    _commandInput.RegisterCallback<NavigationSubmitEvent>(OnCommandSubmitted);
                    _commandInput.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                    _callbacksRegistered = true;
                }

                _commandInput.style.display = IsDevelopmentBuild
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _commandInput.style.height = StyleKeyword.Auto;
                _commandInput.style.fontSize = 16;
                _commandInput.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.95f);
                _commandInput.style.color = Color.white;
                LoadHistory();
                // Do not steal focus from battle input when the console is auto-shown.
            }
        }

        protected override void OnHidden()
        {
            if (_initializeCoroutine != null)
            {
                StopCoroutine(_initializeCoroutine);
                _initializeCoroutine = null;
            }

            if (_isSubscribed)
            {
                TBattleLog.OnLogToUI -= HandleLogEntry;
                TBattleLog.OnLogsCleared -= HandleLogsCleared;
                _isSubscribed = false;
            }

            if (_commandInput != null && _callbacksRegistered)
            {
                _commandInput.UnregisterCallback<NavigationSubmitEvent>(OnCommandSubmitted);
                _commandInput.UnregisterCallback<KeyDownEvent>(OnKeyDown);
                _callbacksRegistered = false;
            }

            ClearDisplayedLogs();

            if (_rootContainer != null)
                _rootContainer.style.display = DisplayStyle.None;
        }

        private void HandleLogEntry(BattleLogData data)
        {
            AddLogEntry(data, true);
        }

        private void HandleLogsCleared()
        {
            ClearDisplayedLogs();
        }

        private void AddLogEntry(BattleLogData data, bool respectScrollPosition)
        {
            if (data == null || _logList == null)
                return;

            bool shouldScroll = !respectScrollPosition || IsNearBottom();
            var label = new Label(data.GetDisplayString());
            label.AddToClassList("log-entry");
            label.style.color = GetColorForType(data.ActionType);
            AddEntryLabel(label);

            if (shouldScroll)
                ScrollToBottom();
        }

        private void AddEntryLabel(VisualElement label)
        {
            _logList.contentContainer.Add(label);
            _logEntryPool.Enqueue(label);
            TrimDisplayedEntries();
        }

        private void TrimDisplayedEntries()
        {
            while (_logEntryPool.Count > MaxLogEntries)
            {
                var oldest = _logEntryPool.Dequeue();
                oldest?.RemoveFromHierarchy();
            }
        }

        private void ClearDisplayedLogs()
        {
            while (_logEntryPool.Count > 0)
            {
                var entry = _logEntryPool.Dequeue();
                entry?.RemoveFromHierarchy();
            }

            _userNearBottom = true;
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
            if (!IsDevelopmentBuild || _commandInput == null || _logList == null)
                return;

            string command = _commandInput.value;
            if (string.IsNullOrWhiteSpace(command))
                return;

            bool isClearLog = string.Equals(command.Trim(), "clearlog", StringComparison.OrdinalIgnoreCase);

            var inputLabel = new Label($"> {command}");
            inputLabel.AddToClassList("log-entry");
            inputLabel.style.color = Color.white;
            AddEntryLabel(inputLabel);

            string result = CheatCommandManager.Instance.Execute(command);
            if (!string.IsNullOrEmpty(result))
            {
                var resultLabel = new Label(result);
                resultLabel.AddToClassList("log-entry");
                resultLabel.style.color = result.StartsWith("[Error]")
                    ? new Color(1f, 0.3f, 0.3f)
                    : new Color(0.3f, 1f, 0.3f);
                AddEntryLabel(resultLabel);
            }

            if (isClearLog)
                ClearDisplayedLogs();
            else
                ScrollToBottom();

            _commandInput.value = string.Empty;

            string lastCmd = _commandHistory.Count > 0
                ? _commandHistory[_commandHistory.Count - 1]
                : null;
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

        private bool IsNearBottom()
        {
            if (_logList == null)
                return true;

            float contentHeight = _logList.contentContainer.layout.height;
            float viewportHeight = _logList.layout.height;
            float remaining = contentHeight - viewportHeight - _logList.scrollOffset.y;
            _userNearBottom = remaining <= BottomScrollTolerance;
            return _userNearBottom;
        }

        private void ScrollToBottom()
        {
            if (_logList == null)
                return;

            _logList.scrollOffset = new Vector2(0, float.MaxValue);
            _userNearBottom = true;
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
