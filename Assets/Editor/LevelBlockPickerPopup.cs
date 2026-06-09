
using System.Collections.Generic;
using Jam;
using UnityEditor;
using UnityEngine;

public class LevelBlockPickerPopup : EditorWindow
{
    // So cot hien thi prefab block trong popup.
    private const int BlockColumnCount = 2;
    // Khoang cach ngang giua hai item block.
    private const float BlockColumnGap = 2f;
    // Khoang cach doc giua cac hang item block.
    private const float BlockRowGap = 2f;
    // Padding ngang de tinh chieu rong vung list block.
    private const float BlockListHorizontalPadding = 24f;
    // Chieu rong toi thieu cua mot item block.
    private const float BlockItemMinWidth = 90f;
    // Kich thuoc moi o nho trong preview shape.
    private const int PreviewCellSize = 8;
    // Khoang cach giua cac o preview shape.
    private const float PreviewCellGap = 1f;
    // Chieu cao toi thieu cua item block.
    private const float BlockItemMinHeight = 28f;
    // Padding ben trong item block.
    private const float BlockItemPadding = 2f;
    // Khoang cach giua preview shape va ten prefab.
    private const float BlockPreviewGap = 6f;
    // Mau ve icon preview cua block trong popup.
    private static readonly Color BlockIconColor = new Color(0.36f, 0.88f, 1f);
    // Mau cham danh dau pivot tren preview block.
    private static readonly Color PivotDotColor = new Color(1f, 0.18f, 0.18f);
    // Mau vien giup cham pivot noi bat tren ca o sang va nen toi.
    private static readonly Color PivotDotOutlineColor = Color.white;
    // Vi tri scroll hien tai cua danh sach block.
    private Vector2 _scrollPosition;
    // Danh sach prefab block tim duoc trong project.
    private List<GameObject> _blockPrefabs;
    // Mau block dang duoc chon de gan vao payload drag.
    private BlockColor _selectedColor;
    // Style ve ten prefab block trong item.
    private GUIStyle _blockTitleStyle;

    /// <summary>
    /// Mở popup danh sách prefab block để kéo thả vào level editor.
    /// </summary>
    public static void Open()
    {
        LevelBlockPickerPopup window = CreateInstance<LevelBlockPickerPopup>();
        window.titleContent = new GUIContent("Blocks");
        window.minSize = new Vector2(220, 260);
        window.ShowUtility();
    }

    /// <summary>
    /// Nạp danh sách prefab block và chuẩn bị style chữ khi popup được bật.
    /// </summary>
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

    /// <summary>
    /// Vẽ giao diện chọn màu, material và danh sách block có thể kéo thả.
    /// </summary>
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Kéo block vào grid", EditorStyles.boldLabel);
        _selectedColor = (BlockColor)EditorGUILayout.EnumPopup("Color", _selectedColor);

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

    /// <summary>
    /// Tìm tất cả prefab có BlockBehavior trong thư mục prefab của project.
    /// </summary>
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

    /// <summary>
    /// Tính chiều cao item block dựa trên kích thước preview của prefab.
    /// </summary>
    private static float GetBlockItemHeight(GameObject prefab)
    {
        BlockBehavior block = prefab != null ? prefab.GetComponentInChildren<BlockBehavior>() : null;
        if (block == null)
            return 0f;

        return Mathf.Max(BlockItemMinHeight, GetPreviewHeight(block) + BlockItemPadding * 2f);
    }

    /// <summary>
    /// Vẽ một item block trong popup và bắt đầu drag khi người dùng kéo item.
    /// </summary>
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

    /// <summary>
    /// Tính chiều rộng preview icon dựa trên số ô ngang của block.
    /// </summary>
    private static float GetPreviewWidth(BlockBehavior block)
    {
        return block.Width * PreviewCellSize + Mathf.Max(0, block.Width - 1) * PreviewCellGap;
    }

    /// <summary>
    /// Tính chiều cao preview icon dựa trên số ô dọc của block.
    /// </summary>
    private static float GetPreviewHeight(BlockBehavior block)
    {
        return block.Height * PreviewCellSize + Mathf.Max(0, block.Height - 1) * PreviewCellGap;
    }

    /// <summary>
    /// Vẽ icon block dạng lưới nhỏ chỉ gồm các ô đang occupied.
    /// </summary>
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

    /// <summary>
    /// Ve cham nho tai cell pivot de biet diem neo cua block khi keo vao grid.
    /// </summary>
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
