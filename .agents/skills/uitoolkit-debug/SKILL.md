---
name: uitoolkit-debug
description: Deep introspection of Unity UIToolkit runtime UI state. Lists UIDocuments, extracts full VisualElement debug trees, and queries individual element styles / data bindings.
---

# UIToolkit / Debug

## When to use

- Agent needs to understand what UI is currently active in the scene.
- Agent needs to debug "element not found", "wrong text/value", "style looks incorrect", or "data binding not working" issues.
- Complements `manage_ui` (`get_visual_tree`) by providing missing fields like `enabledInHierarchy`, `resolvedStyle` margins/padding/flex, control values (Toggle/Slider/Dropdown), and data binding info.

## How to Call

This tool is registered with `com.coplaydev.unity-mcp` and is auto-discovered by `CommandRegistry`.

```bash
# List all UIDocuments in loaded scenes
unity-mcp-cli run-tool uitoolkit_debug --input '{
  "action": "list_documents"
}'

# Get full debug tree for a UIDocument
unity-mcp-cli run-tool uitoolkit_debug --input '{
  "action": "get_tree",
  "target": "MyUIContainer",
  "max_depth": 20,
  "include_styles": true
}'

# Get deep detail for a single element
unity-mcp-cli run-tool uitoolkit_debug --input '{
  "action": "get_element_detail",
  "target": "MyUIContainer",
  "element_path": "root/content/settings-panel/volume-slider"
}'
```

> For complex input, save JSON to a file and use `--input-file args.json`.

## Actions

### `list_documents`

Returns an overview of every `UIDocument` currently present in loaded scenes.

**Input**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `string` | **Yes** | Must be `"list_documents"` |

**Output**

```json
{
  "success": true,
  "message": "Found 2 UIDocument(s) in loaded scenes.",
  "data": {
    "documents": [
      {
        "gameObjectName": "MainMenuUI",
        "sceneName": "MainMenu",
        "visualTreeAssetPath": "Assets/Tactics/Arts/UI/MainMenu.uxml",
        "sortingOrder": 0,
        "enabled": true,
        "rootElementName": "root",
        "rootElementBuilt": true,
        "childCount": 4
      }
    ]
  }
}
```

### `get_tree`

Recursively extracts an enhanced VisualElement tree from the specified `UIDocument`, including states, layout, control values, data bindings, and optional style summaries.

**Input**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `string` | **Yes** | Must be `"get_tree"` |
| `target` | `string` | **Yes** | GameObject name or path that carries the `UIDocument` |
| `max_depth` | `integer` | No | Max recursion depth (default `20`) |
| `include_styles` | `boolean` | No | Include `styleSummary` on each node (default `false`) |

**Output**

```json
{
  "success": true,
  "message": "Visual tree for UIDocument on MainMenuUI",
  "data": {
    "gameObject": "MainMenuUI",
    "sourceAsset": "Assets/Tactics/Arts/UI/MainMenu.uxml",
    "tree": {
      "type": "VisualElement",
      "name": "root",
      "classes": ["main-menu"],
      "enabledInHierarchy": true,
      "visible": true,
      "display": "Flex",
      "layout": { "x": 0, "y": 0, "width": 1920, "height": 1080 },
      "worldBound": { "x": 0, "y": 0, "width": 1920, "height": 1080 },
      "controlValue": null,
      "dataBinding": null,
      "children": [
        {
          "type": "Button",
          "name": "start-button",
          "classes": ["menu-button", "primary"],
          "enabledInHierarchy": true,
          "visible": true,
          "display": "Flex",
          "layout": { "x": 760, "y": 500, "width": 400, "height": 60 },
          "controlValue": { "kind": "TextElement", "text": "Start Game" },
          "children": []
        }
      ]
    }
  }
}
```

### `get_element_detail`

Deep-dive into a single VisualElement by path or name. Returns full `resolvedStyle`, data bindings, control value, and interaction properties.

**Input**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `string` | **Yes** | Must be `"get_element_detail"` |
| `target` | `string` | **Yes** | GameObject name or path that carries the `UIDocument` |
| `element_path` | `string` | **Yes** | Hierarchy path (`root/content/button`) or element `name` |

**Output**

```json
{
  "success": true,
  "message": "Detail for element 'start-button' (Button)",
  "data": {
    "type": "Button",
    "name": "start-button",
    "classes": ["menu-button", "primary"],
    "classListString": "menu-button primary",
    "enabledInHierarchy": true,
    "visible": true,
    "pickingMode": "Position",
    "focusable": true,
    "tabIndex": 0,
    "childCount": 1,
    "layout": { "x": 760, "y": 500, "width": 400, "height": 60 },
    "worldBound": { "x": 760, "y": 500, "width": 400, "height": 60 },
    "parentName": "root",
    "parentType": "VisualElement",
    "resolvedStyle": {
      "color": "#FFFFFFFF",
      "backgroundColor": "#4CAF50FF",
      "fontSize": 24,
      "unityFont": "(default)",
      "width": 400,
      "height": 60,
      "marginLeft": 0,
      "marginTop": 0,
      "marginRight": 0,
      "marginBottom": 0,
      "paddingLeft": 16,
      "paddingTop": 8,
      "paddingRight": 16,
      "paddingBottom": 8,
      "borderLeftWidth": 0,
      "borderTopWidth": 0,
      "borderRightWidth": 0,
      "borderBottomWidth": 0,
      "borderColor": "#00000000",
      "flexDirection": "Row",
      "alignItems": "Center",
      "justifyContent": "Center",
      "flexGrow": 0,
      "flexShrink": 1,
      "flexBasis": 0,
      "flexWrap": "NoWrap",
      "position": "Relative",
      "left": 0,
      "top": 0,
      "right": 0,
      "bottom": 0
    },
    "controlValue": { "kind": "TextElement", "text": "Start Game" },
    "dataBinding": null
  }
}
```

## Control Value Kinds

When a node represents an interactive control, `controlValue` is populated:

| Control type | `kind` | Fields |
|--------------|--------|--------|
| `TextField` | `"TextField"` | `value` |
| `IntegerField` | `"IntegerField"` | `value` |
| `FloatField` | `"FloatField"` | `value` |
| `Toggle` | `"Toggle"` | `value` |
| `Slider` | `"Slider"` | `value`, `lowValue`, `highValue` |
| `SliderInt` | `"SliderInt"` | `value`, `lowValue`, `highValue` |
| `DropdownField` | `"DropdownField"` | `value`, `choices` |
| `ProgressBar` | `"ProgressBar"` | `value`, `lowValue`, `highValue` |
| `Label` / `Button` / any `TextElement` | `"TextElement"` | `text` |

## Data Binding

If the element uses UIToolkit data binding, `dataBinding` contains:

| Field | Description |
|-------|-------------|
| `dataSourceType` | Full name of the bound data source type |
| `dataSourcePath` | Property path string (e.g. `"player.health"`) |
| `dataSource` | `{ type, toString }` summary of the actual source object |
| `bindingPath` | For `PropertyField`, the serialized property path |

## Error Responses

Common error codes:

- `action is required` — Missing `action` field.
- `Could not find target GameObject: X` — `target` did not resolve to any GameObject.
- `GameObject X has no UIDocument component.` — Target exists but has no UI.
- `Element 'X' not found in UIDocument on Y.` — `element_path` did not match any element; response includes `suggestedNames`.
