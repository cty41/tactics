using System.Collections;
using Tactics.Flow.Roguelike;
using Tactics.Roguelike;
using Tactics.Roster;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    public sealed class SlotSelectUIController : UIControllerBase
    {
        private enum SlotMode
        {
            NewGame,
            LoadGame
        }

        private static SlotMode s_mode = SlotMode.NewGame;

        private VisualElement _slotList;
        private Label _titleLabel;
        private Label _messageLabel;
        private Button _backButton;
        private Button _confirmButton;
        private Button _cancelButton;
        private int _pendingOverwriteSlot = -1;
        private bool _wired;

        public static async System.Threading.Tasks.Task ShowForNewGameAsync()
        {
            s_mode = SlotMode.NewGame;
            await UIManager.Instance.ShowAsync(UIManager.UIId.SlotSelect);
        }

        public static async System.Threading.Tasks.Task ShowForLoadGameAsync()
        {
            s_mode = SlotMode.LoadGame;
            await UIManager.Instance.ShowAsync(UIManager.UIId.SlotSelect);
        }

        protected override void OnShown()
        {
            if (_wired) return;
            StartCoroutine(WireDelayed());
        }

        protected override void OnHidden()
        {
            Unwire();
        }

        private IEnumerator WireDelayed()
        {
            yield return null;
            Wire();
        }

        private void Wire()
        {
            var root = Ui.GetRootElement(UIManager.UIId.SlotSelect);
            if (root == null)
            {
                TLog.Warning("[SlotSelectUIController] Could not get root visual element.");
                return;
            }

            _titleLabel = root.Q<Label>("SlotTitle");
            _slotList = root.Q<VisualElement>("SlotList");
            _messageLabel = root.Q<Label>("MessageLabel");
            _backButton = root.Q<Button>("BackButton");
            _confirmButton = root.Q<Button>("ConfirmOverwriteButton");
            _cancelButton = root.Q<Button>("CancelOverwriteButton");

            if (_backButton != null) _backButton.clicked += OnBackClicked;
            if (_confirmButton != null) _confirmButton.clicked += OnConfirmOverwriteClicked;
            if (_cancelButton != null) _cancelButton.clicked += ClearOverwritePrompt;

            _wired = true;
            Refresh();
        }

        private void Unwire()
        {
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
            if (_confirmButton != null) _confirmButton.clicked -= OnConfirmOverwriteClicked;
            if (_cancelButton != null) _cancelButton.clicked -= ClearOverwritePrompt;
            _wired = false;
        }

        private void Refresh()
        {
            if (_titleLabel != null)
                _titleLabel.text = s_mode == SlotMode.NewGame ? "NEW GAME" : "LOAD GAME";

            ClearOverwritePrompt();
            _slotList?.Clear();

            for (int i = 0; i < PlayerAdventureStateStore.SlotCount; i++)
            {
                var summary = PlayerAdventureStateStore.GetSlotSummary(i);
                _slotList?.Add(CreateSlotButton(summary));
            }
        }

        private Button CreateSlotButton(SaveSlotSummary summary)
        {
            var button = new Button(() => OnSlotClicked(summary))
            {
                name = $"SlotButton{summary.SlotIndex + 1}",
                text = BuildSlotText(summary)
            };
            button.AddToClassList("slot-button");
            if (s_mode == SlotMode.LoadGame && !summary.HasSave)
                button.SetEnabled(false);
            return button;
        }

        private static string BuildSlotText(SaveSlotSummary summary)
        {
            string title = $"SLOT {summary.SlotIndex + 1}";
            if (!summary.HasSave)
                return $"{title}\nEMPTY";
            if (summary.IsCorrupted)
                return $"{title}\nCORRUPTED";
            return $"{title}\nParty {summary.ActivePartyCount}/{summary.RosterCount}  Gold {summary.Gold}";
        }

        private void OnSlotClicked(SaveSlotSummary summary)
        {
            if (s_mode == SlotMode.LoadGame)
            {
                if (!summary.HasSave || summary.IsCorrupted)
                    return;
                StartCoroutine(OpenSlotCoroutine(summary.SlotIndex, false));
                return;
            }

            if (summary.HasSave)
            {
                _pendingOverwriteSlot = summary.SlotIndex;
                if (_messageLabel != null)
                    _messageLabel.text = $"Overwrite Slot {summary.SlotIndex + 1}?";
                SetOverwriteButtonsVisible(true);
                return;
            }

            StartCoroutine(OpenSlotCoroutine(summary.SlotIndex, true));
        }

        private void OnConfirmOverwriteClicked()
        {
            if (_pendingOverwriteSlot < 0)
                return;

            StartCoroutine(OpenSlotCoroutine(_pendingOverwriteSlot, true));
        }

        private IEnumerator OpenSlotCoroutine(int slotIndex, bool createNew)
        {
            if (createNew)
            {
                PlayerAdventureStateStore.CreateNew(slotIndex);
                RoguelikeMapRuntimeState.ClearAll();
                PlayerPrefs.DeleteKey(RoguelikeMapUIController.MapPlayerPrefsKey);
                PlayerPrefs.Save();
            }
            else
            {
                PlayerAdventureStateStore.SetActiveSlotIndex(slotIndex);
                PlayerAdventureStateStore.LoadRepairAndSave(slotIndex);
            }

            UIManager.Instance.Hide(UIManager.UIId.SlotSelect);
            UIManager.Instance.Hide(UIManager.UIId.Home);
            var task = RoguelikeFlowCoordinator.Instance.OpenMapAsync();
            while (!task.IsCompleted)
                yield return null;
        }

        private void OnBackClicked()
        {
            UIManager.Instance.Hide(UIManager.UIId.SlotSelect);
            UIManager.Instance.Show(UIManager.UIId.Home);
        }

        private void ClearOverwritePrompt()
        {
            _pendingOverwriteSlot = -1;
            if (_messageLabel != null)
                _messageLabel.text = string.Empty;
            SetOverwriteButtonsVisible(false);
        }

        private void SetOverwriteButtonsVisible(bool visible)
        {
            if (_confirmButton != null)
                _confirmButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_cancelButton != null)
                _cancelButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
