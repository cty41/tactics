using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Tactics.Common.Testing.Gameplay;
using Tactics.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tactics.Tests.PlayMode
{
    public sealed class BattleSpeedControlUiTests
    {
        private const string BattleUxmlPath = "Assets/Tactics/Arts/UI/Battle.uxml";
        private const string PanelSettingsPath = "Assets/Tactics/UIToolkit/PanelSettings.asset";

        private GameObject _uiRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            UIManager.Instance.Destroy(UIManager.UIId.Battle);
            yield return null;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BattleUxmlPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            Assert.That(tree, Is.Not.Null, $"Missing production Battle UXML at {BattleUxmlPath}.");
            Assert.That(panelSettings, Is.Not.Null, $"Missing production PanelSettings at {PanelSettingsPath}.");

            _uiRoot = new GameObject("BattleSpeedControlUiTests_Root");
            _uiRoot.SetActive(false);
            var document = _uiRoot.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = tree;
            UIManager.Instance.RegisterTestUI(UIManager.UIId.Battle, document);

            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UIManager.Instance.Destroy(UIManager.UIId.Battle);
            if (_uiRoot != null)
                Object.DestroyImmediate(_uiRoot);

            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PostBattleRecoveryBeat_StopsWhileGameIsPaused()
        {
            var controller = _uiRoot.GetComponent<BattleUIController>();
            Assert.That(controller, Is.Not.Null);

            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Quadruple);
            GameTimeService.Pause();
            var recovery = controller.ShowPostBattleRecoveryAsync(
                System.Array.Empty<Tactics.Common.Units.IUnit>());
            var speedButton = UIManager.Instance.GetRootElement(UIManager.UIId.Battle)?
                .Q<Button>("BattleSpeedButton");
            Assert.That(speedButton, Is.Not.Null);
            Assert.That(speedButton.style.display.value, Is.EqualTo(DisplayStyle.None));

            yield return new WaitForSecondsRealtime(0.9f);
            Assert.That(recovery.IsCompleted, Is.False, "Post-battle recovery beat must stop while gameplay is paused.");

            GameTimeService.Resume();
            var deadline = Time.realtimeSinceStartup + 1f;
            while (!recovery.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(recovery.IsCompletedSuccessfully, Is.True, recovery.Exception?.ToString());

            UIManager.Instance.Hide(UIManager.UIId.Battle);
            var showTask = UIManager.Instance.ShowAsync(UIManager.UIId.Battle);
            yield return new WaitUntil(() => showTask.IsCompleted);
            yield return null;
            yield return null;

            speedButton = UIManager.Instance.GetRootElement(UIManager.UIId.Battle)?
                .Q<Button>("BattleSpeedButton");
            Assert.That(speedButton, Is.Not.Null);
            Assert.That(
                speedButton.style.display.value,
                Is.EqualTo(DisplayStyle.Flex),
                "A cached Battle UI must restore the speed control for the next encounter.");
        }

        [UnityTest]
        public IEnumerator PostBattleRecoveryBeat_CancelsWhenBattleUiIsDestroyed()
        {
            var controller = _uiRoot.GetComponent<BattleUIController>();
            Assert.That(controller, Is.Not.Null);

            GameTimeService.Pause();
            var recovery = controller.ShowPostBattleRecoveryAsync(
                System.Array.Empty<Tactics.Common.Units.IUnit>());

            Object.DestroyImmediate(_uiRoot);
            _uiRoot = null;
            var deadline = Time.realtimeSinceStartup + 1f;
            while (!recovery.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(
                recovery.IsCanceled,
                Is.True,
                "Destroying the Battle UI must cancel its paused recovery delay.");
        }

        [UnityTest]
        public IEnumerator CachedBattleUi_ReentryRefreshesTextAndKeepsSingleSubscription()
        {
            UIManager.Instance.Hide(UIManager.UIId.Battle);
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Double);

            var showTask = UIManager.Instance.ShowAsync(UIManager.UIId.Battle);
            yield return new WaitUntil(() => showTask.IsCompleted);
            yield return null;
            yield return null;

            var button = UIManager.Instance.GetRootElement(UIManager.UIId.Battle)?.Q<Button>("BattleSpeedButton");
            Assert.That(button, Is.Not.Null);
            Assert.That(button.text, Is.EqualTo("⚙ 2×"));

            ClickThroughProductionAdapter(button);
            Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Quadruple));
            Assert.That(button.text, Is.EqualTo("⚙ 4×"));

            UIManager.Instance.Hide(UIManager.UIId.Battle);
            showTask = UIManager.Instance.ShowAsync(UIManager.UIId.Battle);
            yield return new WaitUntil(() => showTask.IsCompleted);
            yield return null;
            yield return null;

            button = UIManager.Instance.GetRootElement(UIManager.UIId.Battle)?.Q<Button>("BattleSpeedButton");
            Assert.That(button, Is.Not.Null);
            Assert.That(button.text, Is.EqualTo("⚙ 4×"));

            ClickThroughProductionAdapter(button);
            Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Half));
            Assert.That(button.text, Is.EqualTo("⚙ 0.5×"));
        }

        private static void ClickThroughProductionAdapter(Button button)
        {
            var invokeField = typeof(UiGameplayStepAdapter).GetField(
                "ClickableInvokeMethod",
                BindingFlags.Static | BindingFlags.NonPublic);
            var invokeMethod = invokeField?.GetValue(null) as MethodInfo;
            Assert.That(invokeMethod, Is.Not.Null, "UI adapter click activation method must be available.");

            using var clickEvent = ClickEvent.GetPooled();
            clickEvent.target = button;
            invokeMethod.Invoke(button.clickable, new object[] { clickEvent });
        }

        [UnityTest]
        public IEnumerator ProductionButton_ClicksCycleOneTwoFourHalfOne()
        {
            var root = UIManager.Instance.GetRootElement(UIManager.UIId.Battle);
            var button = root?.Q<Button>("BattleSpeedButton");
            Assert.That(button, Is.Not.Null, "Battle.uxml must expose the production speed button.");
            Assert.That(button.text, Is.EqualTo("⚙ 1×"));

            var expected = new[]
            {
                (GamePlaybackSpeed.Double, "⚙ 2×", 2f),
                (GamePlaybackSpeed.Quadruple, "⚙ 4×", 4f),
                (GamePlaybackSpeed.Half, "⚙ 0.5×", 0.5f),
                (GamePlaybackSpeed.Normal, "⚙ 1×", 1f)
            };

            foreach (var (speed, text, scale) in expected)
            {
                ClickThroughProductionAdapter(button);
                yield return null;

                Assert.That(button.text, Is.EqualTo(text));
                Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(speed));
                Assert.That(Time.timeScale, Is.EqualTo(scale));
            }
        }
    }
}
