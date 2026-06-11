
using System.Collections.Generic;
using Jam;
using UnityEditor;
using UnityEngine;

public class LevelBlockPickerPopup : EditorWindow
{

    private const int BlockColumnCount = 2;

    private const float BlockColumnGap = 2f;

    private const float BlockRowGap = 2f;

    private const float BlockListHorizontalPadding = 24f;

    private const float BlockItemMinWidth = 90f;

    private const int PreviewCellSize = 8;

    private const float PreviewCellGap = 1f;

    private const float BlockItemMinHeight = 28f;

    private const float BlockItemPadding = 2f;

    private const float BlockPreviewGap = 6f;

    private static readonly Color BlockIconColor = new Color(0.36f, 0.88f, 1f);

    private static readonly Color PivotDotColor = new Color(1f, 0.18f, 0.18f);

    private static readonly Color PivotDotOutlineColor = Color.white;

    private Vector2 _scrollPosition;

    private List<GameObject> _blockPrefabs;

    private TypeBlockColor _selectedColor;

    private GUIStyle _blockTitleStyle;


    public static void Open()
    {
        LevelBlockPickerPopup window = CreateInstance<LevelBlockPickerPopup>();
        window.titleContent = new GUIContent("Blocks");
        window.minSize = new Vector2(220, 260);
        window.ShowUtility();
    }


    private void OnEnable()
    {
        _blockPrefabs = FindBlockPrefabs();
        _blockTitleStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
            fontSize = 10
        };
    }


    private void OnGUI()
    {
        EditorGUILayout.LabelField("Kéo block vào grid", EditorStyles.boldLabel);
        _selectedColor = (TypeBlockColor)EditorGUILayout.EnumPopup("Color", _selectedColor);

        Material material = LevelEditor.GetMaterial(_selectedColor);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Material", material, typeof(Material), false);
        }

        if (_blockPrefabs == null || _blockPrefabs.Count == 0)
        {
            return;
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        float availableWidth = Mathf.Max(0f, position.width - BlockListHorizontalPadding);
        float blockItemWidth = Mathf.Max(
            BlockItemMinWidth,
            Mathf.Floor((availableWidth - BlockColumnGap) / BlockColumnCount));

        for (int i = 0; i < _blockPrefabs.Count; i += BlockColumnCount)
        {
            GameObject leftPrefab = _blockPrefabs[i];
            GameObject rightPrefab = i + 1 < _blockPrefabs.Count ? _blockPrefabs[i + 1] : null;
            float rowHeight = Mathf.Max(GetBlockItemHeight(leftPrefab), GetBlockItemHeight(rightPrefab));

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawBlockItem(leftPrefab, material, rowHeight, blockItemWidth);

                GUILayout.Space(BlockColumnGap);

                if (rightPrefab != null)
                    DrawBlockItem(rightPrefab, material, rowHeight, blockItemWidth);
                else
                    GUILayout.Space(blockItemWidth);
            }

            GUILayout.Space(BlockRowGap);
        }

        EditorGUILayout.EndScrollView();
    }


    private static List<GameObject> FindBlockPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Resources/Prefab" });
        List<GameObject> prefabs = new List<GameObject>();

        foreach (string prefabGuid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponentInChildren<BlockBehavior>() != null)
                prefabs.Add(prefab);
        }

        prefabs.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return prefabs;
    }


    private static float GetBlockItemHeight(GameObject prefab)
    {
        BlockBehavior block = prefab != null ? prefab.GetComponentInChildren<BlockBehavior>() : null;
        if (block == null)
            return 0f;

        return Mathf.Max(BlockItemMinHeight, GetPreviewHeight(block) + BlockItemPadding * 2f);
    }


    private void DrawBlockItem(GameObject prefab, Material material, float itemHeight, float itemWidth)
    {
        BlockBehavior block = prefab.GetComponentInChildren<BlockBehavior>();
        if (block == null)
            return;

        Rect itemRect = EditorGUILayout.BeginHorizontal(
            EditorStyles.helpBox,
            GUILayout.Height(itemHeight),
            GUILayout.Width(itemWidth),
            GUILayout.MinWidth(itemWidth),
            GUILayout.MaxWidth(itemWidth));

        float previewWidth = GetPreviewWidth(block) + BlockItemPadding * 2f;
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(previewWidth), GUILayout.Height(itemHeight)))
        {
            GUILayout.FlexibleSpace();
            Rect previewRect = GUILayoutUtility.GetRect(
                GetPreviewWidth(block),
                GetPreviewHeight(block),
                GUILayout.Width(GetPreviewWidth(block)),
                GUILayout.Height(GetPreviewHeight(block)));
            DrawBlockIconPreview(block, previewRect);
            GUILayout.FlexibleSpace();
        }

        GUILayout.Space(BlockPreviewGap);

        float labelWidth = Mathf.Max(1f, itemWidth - previewWidth - BlockPreviewGap - 12f);
        EditorGUILayout.LabelField(prefab.name, _blockTitleStyle, GUILayout.Height(itemHeight),
            GUILayout.Width(labelWidth));

        EditorGUILayout.EndHorizontal();

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.MouseDrag && itemRect.Contains(currentEvent.mousePosition))
        {
            LevelEditor.StartBlockDrag(prefab, _selectedColor, material);
            currentEvent.Use();
        }
    }


    private static float GetPreviewWidth(BlockBehavior block)
    {
        return block.Width * PreviewCellSize + Mathf.Max(0, block.Width - 1) * PreviewCellGap;
    }


    private static float GetPreviewHeight(BlockBehavior block)
    {
        return block.Height * PreviewCellSize + Mathf.Max(0, block.Height - 1) * PreviewCellGap;
    }


    private static void DrawBlockIconPreview(BlockBehavior block, Rect previewRect)
    {
        for (int y = 0; y < block.Height; y++)
        {
            for (int x = 0; x < block.Width; x++)
            {
                if (!block.IsCellOccupied(x, y))
                    continue;

                Rect cellRect = new Rect(
                    previewRect.x + x * (PreviewCellSize + PreviewCellGap),
                    previewRect.y + y * (PreviewCellSize + PreviewCellGap),
                    PreviewCellSize,
                    PreviewCellSize);
                EditorGUI.DrawRect(cellRect, BlockIconColor);
            }
        }

        DrawPivotDot(block, previewRect);
    }


    private static void DrawPivotDot(BlockBehavior block, Rect previewRect)
    {
        Vector2Int pivot = block.Pivot;
        if (pivot.x < 0 || pivot.x >= block.Width || pivot.y < 0 || pivot.y >= block.Height)
            return;

        Vector2 center = new Vector2(
            previewRect.x + pivot.x * (PreviewCellSize + PreviewCellGap) + PreviewCellSize * 0.5f,
            previewRect.y + pivot.y * (PreviewCellSize + PreviewCellGap) + PreviewCellSize * 0.5f);

        Handles.color = PivotDotOutlineColor;
        Handles.DrawSolidDisc(center, Vector3.forward, PreviewCellSize * 0.43f);
        Handles.color = PivotDotColor;
        Handles.DrawSolidDisc(center, Vector3.forward, PreviewCellSize * 0.3f);
    }
}
