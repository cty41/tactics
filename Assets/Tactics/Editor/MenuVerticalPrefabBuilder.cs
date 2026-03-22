#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds Assets/Tactics/Arts/UI/Menu.prefab from uibg + uibtn_orange (Fantasy palette, 4 menu rows).
/// </summary>
public static class MenuVerticalPrefabBuilder
{
    private const string UibgPath = "Assets/Tactics/Arts/UI/uibg.prefab";
    private const string UibtnPath = "Assets/Tactics/Arts/UI/uibtn_orange.prefab";
    private const string OutputPath = "Assets/Tactics/Arts/UI/Menu.prefab";
    private const string RowPrefabPath = "Assets/Tactics/Arts/UI/uibtn_menu_row.prefab";

    [MenuItem("Tactics/UI/Build Vertical Menu Prefab")]
    public static void BuildMenuPrefab()
    {
        var rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UibgPath);
        var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UibtnPath);
        if (rootPrefab == null || btnPrefab == null)
        {
            Debug.LogError("MenuVerticalPrefabBuilder: missing uibg or uibtn_orange prefab.");
            return;
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(rootPrefab);
        root.name = "Menu";

        var bg = root.transform.Find("Background")?.GetComponent<Image>();
        if (bg != null)
            bg.color = new Color(0f, 0f, 0f, 0.84313726f);

        var strokeGo = root.transform.Find("Stroke");
        if (strokeGo != null)
            strokeGo.gameObject.SetActive(true);

        var header = root.transform.Find("Header")?.GetComponent<TextMeshProUGUI>();
        if (header != null)
        {
            header.text = "MENU";
            header.horizontalAlignment = HorizontalAlignmentOptions.Center;
        }

        var copy = root.transform.Find("Copy");
        if (copy != null)
            copy.gameObject.SetActive(false);

        var vlg = root.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.padding = new RectOffset(32, 32, 48, 48);
            vlg.spacing = 12f;
        }

        var rtRoot = root.GetComponent<RectTransform>();
        if (rtRoot != null)
        {
            rtRoot.sizeDelta = new Vector2(320f, 520f);
            rtRoot.anchoredPosition = Vector2.zero;
        }

        var headerTf = root.transform.Find("Header");
        int headerIdx = headerTf != null ? headerTf.GetSiblingIndex() : 2;

        var sep = CreateSeparator(root.transform);
        sep.transform.SetSiblingIndex(headerIdx + 1);

        string[] labels = { "CONTINUE", "OPTIONS", "MAIN MENU", "SAVE AND QUIT" };
        int insert = headerIdx + 2;
        foreach (var label in labels)
        {
            var btnRoot = (GameObject)PrefabUtility.InstantiatePrefab(btnPrefab, root.transform);
            btnRoot.name = "MenuRow_" + label.Replace(" ", "");
            SetupMenuRow(btnRoot, label, rootPrefab);
            btnRoot.transform.SetSiblingIndex(insert++);
        }

        var first = root.transform.Find("MenuRow_CONTINUE");
        if (first != null)
            PrefabUtility.SaveAsPrefabAsset(first.gameObject, RowPrefabPath);

        PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("MenuVerticalPrefabBuilder: saved " + OutputPath);
    }

    /// <summary>For Unity batchmode: -executeMethod MenuVerticalPrefabBuilder.ExecuteBuild</summary>
    public static void ExecuteBuild()
    {
        BuildMenuPrefab();
    }

    static GameObject CreateSeparator(Transform parent)
    {
        var go = new GameObject("Separator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.97647065f, 0.5294118f, 0.29411766f, 1f);
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 2f;
        le.preferredHeight = 2f;
        le.flexibleWidth = 1f;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(0f, 2f);
        return go;
    }

    static void SetupMenuRow(GameObject btnRoot, string label, GameObject uibgPrefabAsset)
    {
        var rowVlg = btnRoot.GetComponent<VerticalLayoutGroup>();
        if (rowVlg != null)
        {
            rowVlg.padding = new RectOffset(8, 8, 8, 8);
            rowVlg.spacing = 0f;
        }

        var bg = btnRoot.transform.Find("Background")?.GetComponent<Image>();
        if (bg != null)
            bg.color = new Color(0f, 0f, 0f, 0.84313726f);

        var le = btnRoot.GetComponent<LayoutElement>();
        if (le == null)
            le = btnRoot.AddComponent<LayoutElement>();
        le.minHeight = 48f;
        le.preferredHeight = 48f;

        var bgT = btnRoot.transform.Find("Background");
        if (bgT == null)
            return;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(bgT, false);
        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        StretchFull(labelGo.GetComponent<RectTransform>());

        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.fontSize = 18f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.fontStyle = FontStyles.UpperCase;

        var sampleHeader = uibgPrefabAsset.transform.Find("Header")?.GetComponent<TextMeshProUGUI>();
        if (sampleHeader != null)
        {
            tmp.font = sampleHeader.font;
            tmp.fontSharedMaterial = sampleHeader.fontSharedMaterial;
            tmp.enableVertexGradient = sampleHeader.enableVertexGradient;
            tmp.colorGradient = sampleHeader.colorGradient;
        }
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
#endif
