using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.Editor.MCP
{
    /// <summary>
    /// UIToolkit debug tool for com.coplaydev.unity-mcp.
    /// Provides enhanced runtime introspection of UIDocument / VisualElement trees,
    /// complementing the existing manage_ui get_visual_tree with missing debugging fields.
    /// </summary>
    [McpForUnityTool("uitoolkit_debug", AutoRegister = false, Group = "ui")]
    public static class Tool_UIToolkitDebug
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse("action is required");
            }

            try
            {
                var p = new ToolParams(@params);

                switch (action)
                {
                    case "list_documents":
                        return ListDocuments(p);

                    case "get_tree":
                        return GetTree(p);

                    case "get_element_detail":
                        return GetElementDetail(p);

                    case "ping":
                        return new SuccessResponse("pong", new { tool = "uitoolkit_debug", version = "1.0" });

                    default:
                        return new ErrorResponse($"Unknown action: {action}");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        #region list_documents

        private static object ListDocuments(ToolParams p)
        {
            var docs = Resources.FindObjectsOfTypeAll<UIDocument>();
            var result = new List<object>();

            foreach (var doc in docs)
            {
                if (doc == null) continue;

                var go = doc.gameObject;
                if (go == null) continue;

                // Skip prefab assets (only scene instances)
                if (string.IsNullOrEmpty(go.scene.name)) continue;

                string assetPath = doc.visualTreeAsset != null
                    ? AssetDatabase.GetAssetPath(doc.visualTreeAsset)
                    : null;

                var root = doc.rootVisualElement;
                result.Add(new
                {
                    gameObjectName = go.name,
                    sceneName = go.scene.name,
                    visualTreeAssetPath = assetPath,
                    sortingOrder = doc.sortingOrder,
                    enabled = doc.enabled,
                    rootElementName = root?.name,
                    rootElementBuilt = root != null,
                    childCount = root?.childCount ?? 0,
                });
            }

            return new SuccessResponse(
                $"Found {result.Count} UIDocument(s) in loaded scenes.",
                new { documents = result }
            );
        }

        #endregion

        #region get_tree

        private static object GetTree(ToolParams p)
        {
            var (uiDoc, error) = ResolveUIDocument(p);
            if (error != null) return error;

            int maxDepth = p.GetInt("max_depth") ?? 20;
            bool includeStyles = p.GetBool("include_styles");

            var root = uiDoc.rootVisualElement;
            if (root == null)
            {
                return new SuccessResponse(
                    $"UIDocument on {uiDoc.gameObject.name} has no visual tree (not yet built).",
                    new
                    {
                        gameObject = uiDoc.gameObject.name,
                        sourceAsset = uiDoc.visualTreeAsset != null
                            ? AssetDatabase.GetAssetPath(uiDoc.visualTreeAsset)
                            : null,
                        tree = (object)null
                    }
                );
            }

            var tree = SerializeVisualElement(root, 0, maxDepth, includeStyles);

            return new SuccessResponse(
                $"Visual tree for UIDocument on {uiDoc.gameObject.name}",
                new
                {
                    gameObject = uiDoc.gameObject.name,
                    sourceAsset = uiDoc.visualTreeAsset != null
                        ? AssetDatabase.GetAssetPath(uiDoc.visualTreeAsset)
                        : null,
                    tree
                }
            );
        }

        private static object SerializeVisualElement(VisualElement element, int depth, int maxDepth, bool includeStyles)
        {
            var result = new Dictionary<string, object>
            {
                ["type"] = element.GetType().Name,
                ["name"] = element.name ?? "",
                ["classes"] = new List<string>(element.GetClasses()),
                ["enabledInHierarchy"] = element.enabledInHierarchy,
                ["visible"] = element.visible,
            };

            // Display style (resolvedStyle does not expose display, use style.display.value)
            try
            {
                result["display"] = element.style.display.value.ToString();
            }
            catch
            {
                // ignore if not accessible
            }

            // Layout
            var layout = element.layout;
            result["layout"] = new
            {
                x = layout.x,
                y = layout.y,
                width = layout.width,
                height = layout.height,
            };

            // World bound (optional, may differ from layout)
            try
            {
                var wb = element.worldBound;
                result["worldBound"] = new { x = wb.x, y = wb.y, width = wb.width, height = wb.height };
            }
            catch
            {
                // ignore
            }

            // Control values
            var controlValue = ExtractControlValue(element);
            if (controlValue != null)
            {
                result["controlValue"] = controlValue;
            }

            // Data binding
            var binding = ExtractDataBinding(element);
            if (binding != null)
            {
                result["dataBinding"] = binding;
            }

            // Styles (optional or partial)
            if (includeStyles)
            {
                var style = ExtractStyleSummary(element);
                if (style != null && ((Dictionary<string, object>)style).Count > 0)
                {
                    result["styleSummary"] = style;
                }
            }

            // Children
            if (depth < maxDepth && element.childCount > 0)
            {
                var children = new List<object>();
                foreach (var child in element.Children())
                {
                    children.Add(SerializeVisualElement(child, depth + 1, maxDepth, includeStyles));
                }
                result["children"] = children;
            }
            else if (element.childCount > 0)
            {
                result["childCount"] = element.childCount;
                result["truncated"] = true;
            }

            return result;
        }

        #endregion

        #region get_element_detail

        private static object GetElementDetail(ToolParams p)
        {
            var (uiDoc, error) = ResolveUIDocument(p);
            if (error != null) return error;

            var targetResult = p.GetRequired("element_path", "'element_path' parameter is required.");
            var targetError = targetResult.GetOrError(out string elementPath);
            if (targetError != null) return targetError;

            var root = uiDoc.rootVisualElement;
            if (root == null)
            {
                return new ErrorResponse($"UIDocument on {uiDoc.gameObject.name} has no visual tree (not yet built).");
            }

            var element = FindElementByPath(root, elementPath);
            if (element == null)
            {
                // Suggest available names
                var availableNames = CollectElementNames(root);
                return new ErrorResponse(
                    $"Element '{elementPath}' not found in UIDocument on {uiDoc.gameObject.name}.",
                    new { suggestedNames = availableNames.Take(20).ToList() }
                );
            }

            var detail = SerializeElementDetail(element);
            return new SuccessResponse(
                $"Detail for element '{element.name ?? "(unnamed)"}' ({element.GetType().Name})",
                detail
            );
        }

        private static object SerializeElementDetail(VisualElement element)
        {
            var result = new Dictionary<string, object>
            {
                ["type"] = element.GetType().Name,
                ["name"] = element.name ?? "",
                ["classes"] = new List<string>(element.GetClasses()),
                ["enabledInHierarchy"] = element.enabledInHierarchy,
                ["visible"] = element.visible,
                ["pickingMode"] = element.pickingMode.ToString(),
                ["focusable"] = element.focusable,
                ["tabIndex"] = element.tabIndex,
                ["childCount"] = element.childCount,
            };

            try { result["display"] = element.style.display.value.ToString(); } catch { }

            // Layout
            var layout = element.layout;
            result["layout"] = new
            {
                x = layout.x,
                y = layout.y,
                width = layout.width,
                height = layout.height,
            };

            try
            {
                var wb = element.worldBound;
                result["worldBound"] = new { x = wb.x, y = wb.y, width = wb.width, height = wb.height };
            }
            catch { }

            // Parent
            result["parentName"] = element.parent?.name ?? "(root)";
            result["parentType"] = element.parent?.GetType().Name ?? "none";

            // Full resolved style
            var style = ExtractFullResolvedStyle(element);
            if (style != null && style.Count > 0)
            {
                result["resolvedStyle"] = style;
            }

            // Control value
            var controlValue = ExtractControlValue(element);
            if (controlValue != null)
            {
                result["controlValue"] = controlValue;
            }

            // Data binding
            var binding = ExtractDataBinding(element);
            if (binding != null)
            {
                result["dataBinding"] = binding;
            }

            // USS class list string
            result["classListString"] = string.Join(" ", element.GetClasses());

            return result;
        }

        #endregion

        #region Helpers

        private static (UIDocument doc, object error) ResolveUIDocument(ToolParams p)
        {
            var targetResult = p.GetRequired("target");
            var targetError = targetResult.GetOrError(out string target);
            if (targetError != null) return (null, targetError);

            var goInstruction = new JObject { ["find"] = target };
            GameObject go = ObjectResolver.Resolve(goInstruction, typeof(GameObject)) as GameObject;
            if (go == null)
            {
                return (null, new ErrorResponse($"Could not find target GameObject: {target}"));
            }

            var uiDoc = go.GetComponent<UIDocument>();
            if (uiDoc == null)
            {
                return (null, new ErrorResponse($"GameObject {go.name} has no UIDocument component."));
            }

            return (uiDoc, null);
        }

        private static VisualElement FindElementByPath(VisualElement root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
                return null;

            // Try exact hierarchy path first (slash-separated)
            if (path.Contains('/'))
            {
                var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var current = root;
                foreach (var part in parts)
                {
                    if (current == null) return null;
                    var next = current.Children().FirstOrDefault(c =>
                        string.Equals(c.name, part, StringComparison.Ordinal));
                    if (next == null)
                    {
                        // fallback: try case-insensitive
                        next = current.Children().FirstOrDefault(c =>
                            string.Equals(c.name, part, StringComparison.OrdinalIgnoreCase));
                    }
                    current = next;
                }
                if (current != null) return current;
            }

            // Try exact name match across the whole tree
            var exact = root.Query<VisualElement>(name: path).First();
            if (exact != null) return exact;

            // Fallback: case-insensitive name match
            var lowerPath = path.ToLowerInvariant();
            var all = root.Query<VisualElement>().ToList();
            return all.FirstOrDefault(e =>
                (e.name ?? "").ToLowerInvariant() == lowerPath);
        }

        private static List<string> CollectElementNames(VisualElement root)
        {
            var names = new List<string>();
            if (root == null) return names;
            var all = root.Query<VisualElement>().ToList();
            foreach (var e in all)
            {
                if (!string.IsNullOrEmpty(e.name))
                    names.Add(e.name);
            }
            return names;
        }

        private static object ExtractControlValue(VisualElement element)
        {
            switch (element)
            {
                case TextField tf:
                    return new { kind = "TextField", value = tf.value };
                case IntegerField intf:
                    return new { kind = "IntegerField", value = intf.value };
                case FloatField ff:
                    return new { kind = "FloatField", value = ff.value };
                case Toggle toggle:
                    return new { kind = "Toggle", value = toggle.value };
                case Slider slider:
                    return new { kind = "Slider", value = slider.value, lowValue = slider.lowValue, highValue = slider.highValue };
                case SliderInt sliderInt:
                    return new { kind = "SliderInt", value = sliderInt.value, lowValue = sliderInt.lowValue, highValue = sliderInt.highValue };
                case DropdownField dropdown:
                    return new { kind = "DropdownField", value = dropdown.value, choices = dropdown.choices?.ToList() };
                case ProgressBar pb:
                    return new { kind = "ProgressBar", value = pb.value, lowValue = pb.lowValue, highValue = pb.highValue };
                case TextElement textEl when !string.IsNullOrEmpty(textEl.text):
                    return new { kind = "TextElement", text = textEl.text };
                default:
                    return null;
            }
        }

        private static object ExtractDataBinding(VisualElement element)
        {
            if (element == null) return null;

            var result = new Dictionary<string, object>();

            if (element.dataSourceType != null)
            {
                result["dataSourceType"] = element.dataSourceType.FullName;
            }

            if (element.dataSourcePath != null)
            {
                result["dataSourcePath"] = element.dataSourcePath.ToString();
            }

            if (element.dataSource != null)
            {
                result["dataSource"] = new
                {
                    type = element.dataSource.GetType().FullName,
                    toString = element.dataSource.ToString(),
                };
            }

            // PropertyField binding path (if applicable)
            if (element is UnityEditor.UIElements.PropertyField pf)
            {
                try
                {
                    result["bindingPath"] = pf.bindingPath;
                }
                catch { }
            }

            return result.Count > 0 ? result : null;
        }

        private static Dictionary<string, object> ExtractStyleSummary(VisualElement element)
        {
            var style = new Dictionary<string, object>();
            var rs = element.resolvedStyle;

            if (rs.width > 0) style["width"] = rs.width;
            if (rs.height > 0) style["height"] = rs.height;
            if (rs.color != Color.clear) style["color"] = ColorToHex(rs.color);
            if (rs.backgroundColor != Color.clear) style["backgroundColor"] = ColorToHex(rs.backgroundColor);
            if (rs.fontSize > 0) style["fontSize"] = rs.fontSize;

            // Margins
            if (rs.marginLeft > 0 || rs.marginTop > 0 || rs.marginRight > 0 || rs.marginBottom > 0)
            {
                style["margin"] = new { left = rs.marginLeft, top = rs.marginTop, right = rs.marginRight, bottom = rs.marginBottom };
            }

            // Padding
            if (rs.paddingLeft > 0 || rs.paddingTop > 0 || rs.paddingRight > 0 || rs.paddingBottom > 0)
            {
                style["padding"] = new { left = rs.paddingLeft, top = rs.paddingTop, right = rs.paddingRight, bottom = rs.paddingBottom };
            }

            // Flex basics
            try
            {
                style["flexDirection"] = element.resolvedStyle.flexDirection.ToString();
            }
            catch { }

            try
            {
                style["alignItems"] = element.resolvedStyle.alignItems.ToString();
            }
            catch { }

            try
            {
                style["justifyContent"] = element.resolvedStyle.justifyContent.ToString();
            }
            catch { }

            return style;
        }

        private static Dictionary<string, object> ExtractFullResolvedStyle(VisualElement element)
        {
            var style = new Dictionary<string, object>();
            var rs = element.resolvedStyle;

            // Colors
            style["color"] = ColorToHex(rs.color);
            style["backgroundColor"] = ColorToHex(rs.backgroundColor);

            // Font
            style["fontSize"] = rs.fontSize;
            style["unityFont"] = rs.unityFont?.name ?? "(default)";

            // Dimensions
            style["width"] = rs.width;
            style["height"] = rs.height;
            style["minWidth"] = rs.minWidth;
            style["minHeight"] = rs.minHeight;
            style["maxWidth"] = rs.maxWidth;
            style["maxHeight"] = rs.maxHeight;

            // Margins
            style["marginLeft"] = rs.marginLeft;
            style["marginTop"] = rs.marginTop;
            style["marginRight"] = rs.marginRight;
            style["marginBottom"] = rs.marginBottom;

            // Padding
            style["paddingLeft"] = rs.paddingLeft;
            style["paddingTop"] = rs.paddingTop;
            style["paddingRight"] = rs.paddingRight;
            style["paddingBottom"] = rs.paddingBottom;

            // Borders
            style["borderLeftWidth"] = rs.borderLeftWidth;
            style["borderTopWidth"] = rs.borderTopWidth;
            style["borderRightWidth"] = rs.borderRightWidth;
            style["borderBottomWidth"] = rs.borderBottomWidth;
            style["borderColor"] = ColorToHex(rs.borderLeftColor);

            // Flex
            style["flexDirection"] = rs.flexDirection.ToString();
            style["alignItems"] = rs.alignItems.ToString();
            style["justifyContent"] = rs.justifyContent.ToString();
            style["flexGrow"] = rs.flexGrow;
            style["flexShrink"] = rs.flexShrink;
            style["flexBasis"] = rs.flexBasis.value;
            style["flexWrap"] = rs.flexWrap.ToString();

            // Position
            style["position"] = rs.position.ToString();
            style["left"] = rs.left;
            style["top"] = rs.top;
            style["right"] = rs.right;
            style["bottom"] = rs.bottom;

            return style;
        }

        private static string ColorToHex(Color c)
        {
            return $"#{ColorUtility.ToHtmlStringRGBA(c)}";
        }

        #endregion
    }
}
