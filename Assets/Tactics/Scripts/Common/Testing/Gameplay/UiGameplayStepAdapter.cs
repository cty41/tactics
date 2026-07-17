using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Tactics.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.Common.Testing.Gameplay
{
    public sealed class UiGameplayStepAdapter : IGameplayStepAdapter
    {
        private const string UiAdapterName = "UI";
        private static readonly MethodInfo ClickableInvokeMethod = typeof(Clickable).GetMethod(
            "Invoke",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public string AdapterName => UiAdapterName;

        public bool CanExecute(ExecutableScenarioAction action)
        {
            return action.Kind is "openUI"
                or "closeUI"
                or "clickElement"
                or "setText"
                or "setElementEnabled";
        }

        public async Task<GameplayStepResult> ExecuteAsync(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            try
            {
                switch (action.Kind)
                {
                    case "openUI":
                        return await OpenUI(context, action);
                    case "closeUI":
                        return CloseUI(context, action);
                    case "clickElement":
                        return ClickElement(context, action);
                    case "setText":
                        return SetText(context, action);
                    case "setElementEnabled":
                        return SetElementEnabled(context, action);
                    default:
                        return GameplayStepResult.Fail(UiAdapterName, action.Kind, $"Unsupported UI action '{action.Kind}'.");
                }
            }
            catch (Exception ex)
            {
                return GameplayStepResult.Fail(UiAdapterName, action.Kind, ex.Message);
            }
        }

        public bool CanAssert(ExecutableScenarioAssertion assertion)
        {
            return assertion.Kind is "elementVisible"
                or "elementText"
                or "elementEnabled"
                or "elementExists";
        }

        public Task<GameplayAssertionResult> AssertAsync(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            try
            {
                GameplayAssertionResult result = assertion.Kind switch
                {
                    "elementVisible" => AssertElementVisible(context, assertion),
                    "elementText" => AssertElementText(context, assertion),
                    "elementEnabled" => AssertElementEnabled(context, assertion),
                    "elementExists" => AssertElementExists(context, assertion),
                    _ => GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, $"Unsupported UI assertion '{assertion.Kind}'.")
                };

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult(GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, ex.Message));
            }
        }

        public ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request)
        {
            var data = new JObject();
            var elementName = request.Target;

            if (!string.IsNullOrWhiteSpace(elementName))
            {
                var element = FindElement(context, elementName);
                if (element != null)
                {
                    data["element"] = elementName;
                    data["visible"] = element.style.display != DisplayStyle.None;
                    data["enabled"] = element.enabledSelf;
                    data["text"] = GetElementText(element);
                }
            }

            return new ProbeSnapshot
            {
                Adapter = UiAdapterName,
                Kind = request.Kind,
                Target = request.Target,
                Data = data
            };
        }

        private static async Task<GameplayStepResult> OpenUI(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string uiId = action.Parameters["uiId"]?.ToString();
            if (string.IsNullOrWhiteSpace(uiId))
                return GameplayStepResult.Fail(UiAdapterName, action.Kind, "openUI requires uiId.");

            if (!Enum.TryParse<UIManager.UIId>(uiId, true, out var parsedUiId))
                return GameplayStepResult.Fail(UiAdapterName, action.Kind, $"Unknown UI ID '{uiId}'.");

            await UIManager.Instance.ShowAsync(parsedUiId);
            context.CurrentUiId = parsedUiId;

            return GameplayStepResult.Pass(UiAdapterName, action.Kind, $"Opened UI '{uiId}'.");
        }

        private static GameplayStepResult CloseUI(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string uiId = action.Parameters["uiId"]?.ToString();
            if (string.IsNullOrWhiteSpace(uiId))
            {
                // Close current UI
                if (context.CurrentUiId.HasValue)
                {
                    UIManager.Instance.Hide(context.CurrentUiId.Value);
                    context.CurrentUiId = null;
                    return GameplayStepResult.Pass(UiAdapterName, action.Kind, "Closed current UI.");
                }
                return GameplayStepResult.Fail(UiAdapterName, action.Kind, "No current UI to close.");
            }

            if (!Enum.TryParse<UIManager.UIId>(uiId, true, out var parsedUiId))
                return GameplayStepResult.Fail(UiAdapterName, action.Kind, $"Unknown UI ID '{uiId}'.");

            UIManager.Instance.Hide(parsedUiId);
            if (context.CurrentUiId == parsedUiId)
                context.CurrentUiId = null;

            return GameplayStepResult.Pass(UiAdapterName, action.Kind, $"Closed UI '{uiId}'.");
        }

        private static GameplayStepResult ClickElement(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string elementName = action.Parameters["elementName"]?.ToString();
            if (string.IsNullOrWhiteSpace(elementName))
                return GameplayStepResult.Fail(UiAdapterName, action.Kind, "clickElement requires elementName.");

            var element = FindElement(context, elementName);
            if (element == null)
                return GameplayStepResult.Fail(UiAdapterName, action.Kind, $"Element '{elementName}' not found.");

            using var clickEvent = ClickEvent.GetPooled();
            clickEvent.target = element;
            if (element is Button button && button.clickable != null && ClickableInvokeMethod != null)
                ClickableInvokeMethod.Invoke(button.clickable, new object[] { clickEvent });
            else
                element.SendEvent(clickEvent);

            string elementKind = element is Button ? "button" : "element";
            return GameplayStepResult.Pass(UiAdapterName, action.Kind, $"Clicked {elementKind} '{elementName}'.");
        }

        private static GameplayStepResult SetText(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string elementName = action.Parameters["elementName"]?.ToString();
            if (string.IsNullOrWhiteSpace(elementName))
                return GameplayStepResult.Fail(UiAdapterName, action.Kind, "setText requires elementName.");

            string text = action.Parameters["text"]?.ToString();
            if (text == null)
                return GameplayStepResult.Fail(UiAdapterName, action.Kind, "setText requires text parameter.");

            var element = FindElement(context, elementName);
            if (element == null)
                return GameplayStepResult.Fail(UiAdapterName, action.Kind, $"Element '{elementName}' not found.");

            if (element is Label label)
            {
                label.text = text;
                return GameplayStepResult.Pass(UiAdapterName, action.Kind, $"Set text of '{elementName}' to '{text}'.");
            }

            if (element is TextField textField)
            {
                textField.value = text;
                return GameplayStepResult.Pass(UiAdapterName, action.Kind, $"Set text of '{elementName}' to '{text}'.");
            }

            return GameplayStepResult.Fail(UiAdapterName, action.Kind, $"Element '{elementName}' does not support text.");
        }

        private static GameplayStepResult SetElementEnabled(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string elementName = action.Parameters["elementName"]?.ToString();
            if (string.IsNullOrWhiteSpace(elementName))
                return GameplayStepResult.Fail(UiAdapterName, action.Kind, "setElementEnabled requires elementName.");

            bool enabled = action.Parameters["enabled"]?.ToObject<bool>() ?? true;

            var element = FindElement(context, elementName);
            if (element == null)
                return GameplayStepResult.Fail(UiAdapterName, action.Kind, $"Element '{elementName}' not found.");

            element.SetEnabled(enabled);
            return GameplayStepResult.Pass(UiAdapterName, action.Kind, $"Set '{elementName}' enabled={enabled}.");
        }

        private static GameplayAssertionResult AssertElementVisible(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string elementName = assertion.Target;
            if (string.IsNullOrWhiteSpace(elementName))
                return GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, "elementVisible requires target element name.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? true;

            var element = FindElement(context, elementName);
            if (element == null)
                return GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, $"Element '{elementName}' not found.");

            bool actual = element.style.display != DisplayStyle.None;
            return actual == expected
                ? GameplayAssertionResult.Pass(UiAdapterName, assertion.Kind, $"Element '{elementName}' visible={actual}")
                : GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, $"Expected Element '{elementName}' visible={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertElementText(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string elementName = assertion.Target;
            if (string.IsNullOrWhiteSpace(elementName))
                return GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, "elementText requires target element name.");

            string expected = assertion.Expected?.ToString();
            if (expected == null)
                return GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, "elementText requires expected text.");

            var element = FindElement(context, elementName);
            if (element == null)
                return GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, $"Element '{elementName}' not found.");

            string actual = GetElementText(element);
            return actual == expected
                ? GameplayAssertionResult.Pass(UiAdapterName, assertion.Kind, $"Element '{elementName}' text='{actual}'")
                : GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, $"Expected Element '{elementName}' text='{expected}', actual='{actual}'.");
        }

        private static GameplayAssertionResult AssertElementEnabled(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string elementName = assertion.Target;
            if (string.IsNullOrWhiteSpace(elementName))
                return GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, "elementEnabled requires target element name.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? true;

            var element = FindElement(context, elementName);
            if (element == null)
                return GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, $"Element '{elementName}' not found.");

            bool actual = element.enabledSelf;
            return actual == expected
                ? GameplayAssertionResult.Pass(UiAdapterName, assertion.Kind, $"Element '{elementName}' enabled={actual}")
                : GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, $"Expected Element '{elementName}' enabled={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertElementExists(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string elementName = assertion.Target;
            if (string.IsNullOrWhiteSpace(elementName))
                return GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, "elementExists requires target element name.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? true;

            var element = FindElement(context, elementName);
            bool actual = element != null;
            return actual == expected
                ? GameplayAssertionResult.Pass(UiAdapterName, assertion.Kind, $"Element '{elementName}' exists={actual}")
                : GameplayAssertionResult.Fail(UiAdapterName, assertion.Kind, $"Expected Element '{elementName}' exists={expected}, actual={actual}.");
        }

        private static VisualElement FindElement(GameplayRuntimeContext context, string elementName)
        {
            // Support hierarchical selectors: "parent >> child"
            string[] parts = elementName.Split(new[] { " >> " }, StringSplitOptions.None);

            // Try to find element in current UI
            if (context.CurrentUiId.HasValue)
            {
                var uiDoc = GetUiDocument(context.CurrentUiId.Value);
                if (uiDoc?.rootVisualElement != null)
                {
                    var element = FindByPath(uiDoc.rootVisualElement, parts);
                    if (element != null)
                        return element;
                }
            }

            // Try to find in all active UIDocuments
            var uiDocs = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var doc in uiDocs)
            {
                if (doc.rootVisualElement != null)
                {
                    var element = FindByPath(doc.rootVisualElement, parts);
                    if (element != null)
                        return element;
                }
            }

            return null;
        }

        private static VisualElement FindByPath(VisualElement root, string[] pathParts)
        {
            if (pathParts.Length == 1)
                return root.Q(pathParts[0]);

            var current = root.Q(pathParts[0]);
            if (current == null) return null;

            for (int i = 1; i < pathParts.Length; i++)
            {
                current = current.Q(pathParts[i]);
                if (current == null) return null;
            }

            return current;
        }

        private static UIDocument GetUiDocument(UIManager.UIId uiId)
        {
            string uiName = uiId.ToString();
            var uiDocs = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var doc in uiDocs)
            {
                if (doc.gameObject.name.Contains(uiName))
                    return doc;
            }
            return null;
        }

        private static string GetElementText(VisualElement element)
        {
            if (element is Label label)
                return label.text;
            if (element is Button button)
                return button.text;
            if (element is TextField textField)
                return textField.value;
            return string.Empty;
        }
    }
}
