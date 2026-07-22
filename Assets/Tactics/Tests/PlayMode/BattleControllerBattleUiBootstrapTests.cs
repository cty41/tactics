using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class BattleControllerBattleUiBootstrapTests
    {
        private GameObject _managerRoot;
        private GameObject _battleRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_battleRoot != null)
            {
                Object.DestroyImmediate(_battleRoot);
                _battleRoot = null;
            }

            if (_managerRoot != null)
            {
                Object.DestroyImmediate(_managerRoot);
                _managerRoot = null;
            }

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattleUI_WaitsForBootstrap_AndShows()
        {
            LogAssert.ignoreFailingMessages = true;

            _managerRoot = new GameObject("TestGameAssetManager");
            var manager = _managerRoot.AddComponent<GameAssetManager>();
            manager.gameObject.SetActive(false);
            SetAutoInitialize(manager, false);
            manager.gameObject.SetActive(true);

            _battleRoot = new GameObject("TestBattleController");
            var controller = _battleRoot.AddComponent<BattleController>();
            SetStartImmediately(controller, false);

            var awake = typeof(BattleController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            awake?.Invoke(controller, null);

            _ = controller.StartBattleAsync();

            for (int i = 0; i < 5; i++)
                yield return null;

            SetEditorManifestlessInitialized(manager, true);

            for (int i = 0; i < 10; i++)
                yield return null;

            Assert.IsTrue(controller.IsBattleActive, "Battle should still become active even when bootstrap is delayed.");
        }

        [Test]
        public void RemoveUnit_BeforeUnitManagerInitialization_IsSafeNoOp()
        {
            _battleRoot = new GameObject("TestBattleController_PreInitRemove");
            var controller = _battleRoot.AddComponent<BattleController>();
            SetStartImmediately(controller, false);

            Assert.DoesNotThrow(() => controller.RemoveUnit(null));
        }

        [UnityTest]
        public IEnumerator BattleUI_FailsGracefully_WhenBootstrapTimesOut()
        {
            LogAssert.ignoreFailingMessages = true;

            _battleRoot = new GameObject("TestBattleController");
            var controller = _battleRoot.AddComponent<BattleController>();
            SetStartImmediately(controller, false);

            var awake = typeof(BattleController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            awake?.Invoke(controller, null);

            _ = controller.StartBattleAsync();

            for (int i = 0; i < 15; i++)
                yield return null;

            Assert.IsTrue(controller.IsBattleActive, "Battle should remain active even when Battle UI bootstrap times out.");
        }

        private static void SetStartImmediately(BattleController controller, bool value)
        {
            typeof(BattleController)
                .GetField("_startImmediatelly", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, value);
        }

        private static void SetAutoInitialize(GameAssetManager manager, bool value)
        {
            typeof(GameAssetManager)
                .GetField("_autoInitializeOnAwake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(manager, value);
        }

        private static void SetEditorManifestlessInitialized(GameAssetManager manager, bool value)
        {
            typeof(GameAssetManager)
                .GetField("_editorManifestlessInitialized", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(manager, value);
        }
    }
}
