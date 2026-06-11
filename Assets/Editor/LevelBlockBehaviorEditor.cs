using Jam;
using UnityEditor;
using UnityEngine;

public enum LevelBlockEditTool
{
    Edit,
    Pivot,
    HorizontalBounds,
    VerticalBounds
}

[CustomEditor(typeof(BlockBehavior))]
public class LevelBlockBehaviorEditor : Editor
{

    const int PreviewCellSize = 32;


    SerializedProperty _meshRendererProperty;


    void OnEnable()
    {
        _meshRendererProperty = serializedObject.FindProperty("_meshRenderer");
    }


    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        BlockBehavior block = (BlockBehavior)target;

        DrawScriptField(block);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawReadOnlySizeFields(block);
                DrawReadOnlyPivotFields(block);
                EditorGUILayout.LabelField("ActivePoints", block.ActivePoints.ToString());
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(block.Width * PreviewCellSize + 4)))
            {
                DrawPreview(block, PreviewCellSize);

                if (GUILayout.Button("Edit"))
                    LevelBlockShapePopup.Open(block);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(_meshRendererProperty);

        serializedObject.ApplyModifiedProperties();
    }


    static void DrawScriptField(BlockBehavior block)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            MonoScript script = MonoScript.FromMonoBehaviour(block);
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        }
    }


    void DrawReadOnlySizeFields(BlockBehavior block)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            DrawVector2IntRow("Size", block.Width, block.Height, 1);
        }
    }


    void DrawReadOnlyPivotFields(BlockBehavior block)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            DrawVector2IntRow("Pivot", block.Pivot.x, block.Pivot.y, 0);
        }
    }


    static Vector2Int DrawVector2IntRow(string label, int x, int y, int minValue)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);
            GUILayout.Label("X", GUILayout.Width(12));
            x = EditorGUILayout.IntField(x, GUILayout.Width(70));
            GUILayout.Label("Y", GUILayout.Width(12));
            y = EditorGUILayout.IntField(y, GUILayout.Width(70));
        }

        return new Vector2Int(Mathf.Max(minValue, x), Mathf.Max(minValue, y));
    }


    public static void DrawPreview(BlockBehavior block, int cellSize)
    {
        float width = block.Width * cellSize;
        float height = block.Height * cellSize;
        Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
        LevelBlockShapeDrawer.Draw(rect, block, cellSize, false, LevelBlockEditTool.Edit);
    }
}

public class LevelBlockShapePopup : EditorWindow
{

    const int MinSize = 1;


    const int MaxSize = 12;


    const int CellSize = 100;


    BlockBehavior _block;


    LevelBlockEditTool _tool;


    public static void Open(BlockBehavior block)
    {
        LevelBlockShapePopup window = CreateInstance<LevelBlockShapePopup>();
        window.titleContent = new GUIContent("Edit Level Figure");
        window._block = block;
        window.ResizeWindow();
        window.ShowUtility();
    }


    void OnGUI()
    {
        if (_block == null)
        {
            Close();
            return;
        }

        DrawSizeFields();
        DrawGrid();
        DrawTools();
    }


    void DrawSizeFields()
    {
        EditorGUILayout.LabelField("Size", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("X", GUILayout.Width(14));
            int width = EditorGUILayout.IntField(_block.Width, GUILayout.Width(100));
            GUILayout.Label("Y", GUILayout.Width(14));
            int height = EditorGUILayout.IntField(_block.Height, GUILayout.Width(100));

            if (EditorGUI.EndChangeCheck())
            {
                width = Mathf.Clamp(width, MinSize, MaxSize);
                height = Mathf.Clamp(height, MinSize, MaxSize);

                Undo.RecordObject(_block, "Change Block Size");
                _block.SetSize(width, height);
                EditorUtility.SetDirty(_block);
                ResizeWindow();
            }
        }

        EditorGUILayout.LabelField("ActivePoints", _block.ActivePoints.ToString());
    }


    void DrawGrid()
    {
        Rect gridRect = GUILayoutUtility.GetRect(_block.Width * CellSize, _block.Height * CellSize,
            GUILayout.ExpandWidth(false));
        LevelBlockShapeDrawer.Draw(gridRect, _block, CellSize, true, _tool);

        Event currentEvent = Event.current;
        if ((currentEvent.type == EventType.MouseDown || currentEvent.type == EventType.MouseDrag) &&
            gridRect.Contains(currentEvent.mousePosition))
        {
            int x = Mathf.FloorToInt((currentEvent.mousePosition.x - gridRect.x) / CellSize);
            int y = Mathf.FloorToInt((currentEvent.mousePosition.y - gridRect.y) / CellSize);
            ApplyCellClick(x, y);
            currentEvent.Use();
        }
    }


    void DrawTools()
    {
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawToolButton("Edit", LevelBlockEditTool.Edit);
            DrawToolButton("Pivot", LevelBlockEditTool.Pivot);
            DrawToolButton("Bounds (hor)", LevelBlockEditTool.HorizontalBounds);
            DrawToolButton("Bounds (ver)", LevelBlockEditTool.VerticalBounds);
        }
    }


    void DrawToolButton(string label, LevelBlockEditTool tool)
    {
        bool isSelected = _tool == tool;
        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = isSelected ? new Color(0f, 0.55f, 0.55f) : previousColor;

        if (GUILayout.Button(label, EditorStyles.miniButton))
            _tool = tool;

        GUI.backgroundColor = previousColor;
    }


    void ApplyCellClick(int x, int y)
    {
        Undo.RecordObject(_block, "Edit Level Figure");

        switch (_tool)
        {
            case LevelBlockEditTool.Edit:
                _block.SetCellOccupied(x, y, !_block.IsCellOccupied(x, y));
                break;

            case LevelBlockEditTool.Pivot:
                _block.SetPivot(new Vector2Int(x, y));
                break;

            case LevelBlockEditTool.HorizontalBounds:
                _block.SetHorizontalBoundsCell(x, y, !_block.IsCellInHorizontalBounds(x, y));
                break;

            case LevelBlockEditTool.VerticalBounds:
                _block.SetVerticalBoundsCell(x, y, !_block.IsCellInVerticalBounds(x, y));
                break;
        }

        EditorUtility.SetDirty(_block);
        Repaint();
    }


    void ResizeWindow()
    {
        if (_block == null)
            return;

        Vector2 size = new(Mathf.Max(276, _block.Width * CellSize + 20),
            Mathf.Max(220, _block.Height * CellSize + 150));
        minSize = size;
        maxSize = size;
    }
}

static class LevelBlockShapeDrawer
{

    static readonly Color FilledColor = new(1f, 0f, 0f);


    static readonly Color EmptyColor = new(0.52f, 0.52f, 0.50f);


    static readonly Color GridLineColor = Color.white;


    static readonly Color PivotColor = new(0f, 0f, 1f);


    static readonly Color HorizontalBoundsColor = new(0f, 0.85f, 1f);


    static readonly Color VerticalBoundsColor = new(1f, 0.85f, 0f);


    public static void Draw(Rect rect, BlockBehavior block, int cellSize, bool drawToolHints,
        LevelBlockEditTool activeTool)
    {
        for (int y = 0; y < block.Height; y++)
        {
            for (int x = 0; x < block.Width; x++)
            {
                Rect cellRect = new(rect.x + x * cellSize, rect.y + y * cellSize, cellSize, cellSize);
                EditorGUI.DrawRect(cellRect, block.IsCellOccupied(x, y) ? FilledColor : EmptyColor);

                Handles.color = GridLineColor;
                Handles.DrawAAPolyLine(2f,
                    new Vector3(cellRect.xMin, cellRect.yMin),
                    new Vector3(cellRect.xMax, cellRect.yMin),
                    new Vector3(cellRect.xMax, cellRect.yMax),
                    new Vector3(cellRect.xMin, cellRect.yMax),
                    new Vector3(cellRect.xMin, cellRect.yMin));

                if (block.Pivot.x == x && block.Pivot.y == y)
                    DrawBorder(cellRect, PivotColor, Mathf.Max(3, cellSize / 14));

                if (drawToolHints && activeTool == LevelBlockEditTool.HorizontalBounds &&
                    block.IsCellInHorizontalBounds(x, y))
                    DrawInsetBorder(cellRect, HorizontalBoundsColor, 5f, 3f);

                if (drawToolHints && activeTool == LevelBlockEditTool.VerticalBounds &&
                    block.IsCellInVerticalBounds(x, y))
                    DrawInsetBorder(cellRect, VerticalBoundsColor, 9f, 3f);
            }
        }
    }


    static void DrawBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }


    static void DrawInsetBorder(Rect rect, Color color, float inset, float thickness)
    {
        DrawBorder(new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f), color,
            thickness);
    }
}
