using System;
using System.Collections.Generic;
using Jam;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class LevelEditor : EditorWindow
{
    private const int MinGridSize = 1;
    private const int MaxGridSize = 20;
    private const int GridCellSize = 26;
    private const string LevelFolderPath = "Assets/_Project/LevelEditor";
    private const string BlockDragKey = "LevelEditorBlockPayload";
    private const string RuntimePreviewRootName = "[LevelEditor Runtime Preview]";

    private static readonly Dictionary<BlockColor, Material> ColorMaterials = new Dictionary<BlockColor, Material>();

    private ObjectField _levelAsset;
    private IntegerField _levelID;
    private IntegerField _gridX;
    private IntegerField _gridY;
    private VisualElement _gridHolder;
    private VisualElement _gridPreview;
    private VisualElement _levelPanel;
    private VisualElement _materialsPanel;
    private VisualElement _materialFields;

    private readonly List<LevelBlockData> _placedBlocks = new List<LevelBlockData>();
    private int _selectedBlockIndex = -1;
    private int _draggingBlockIndex = -1;
    private Vector2Int _dragCellOffset;
    private GameObject _runtimePreviewRoot;

    [MenuItem("LevelEditor/LevelEditor")]
    public static void ShowWindow()
    {
        GetWindow<LevelEditor>("LevelEditor");
    }

    public void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        root.Clear();

        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Editor/LevelEditor.uxml");

        if (visualTree == null)
            return;

        visualTree.CloneTree(root);

        _levelAsset = root.Q<ObjectField>("levelAsset");
        _levelID = root.Q<IntegerField>("levelID");
        _gridX = root.Q<IntegerField>("gridX");
        _gridY = root.Q<IntegerField>("gridY");
        _gridHolder = root.Q<VisualElement>("gridHolder");
        _gridPreview = root.Q<VisualElement>("gridPreview");
        _levelPanel = root.Q<VisualElement>("levelPanel");
        _materialsPanel = root.Q<VisualElement>("materialsPanel");
        _materialFields = root.Q<VisualElement>("materialFields");

        _levelAsset.objectType = typeof(LevelData);
        _levelAsset.allowSceneObjects = false;
        _levelID.value = Mathf.Max(1, _levelID.value);
        _gridX.value = Mathf.Clamp(_gridX.value, MinGridSize, MaxGridSize);
        _gridY.value = Mathf.Clamp(_gridY.value, MinGridSize, MaxGridSize);

        _levelAsset.RegisterValueChangedCallback(evt => LoadLevel(evt.newValue as LevelData));
        _gridX.RegisterValueChangedCallback(_ => RebuildGridPreview());
        _gridY.RegisterValueChangedCallback(_ => RebuildGridPreview());

        _gridPreview.focusable = true;
        _gridPreview.RegisterCallback<DragUpdatedEvent>(OnGridDragUpdated);
        _gridPreview.RegisterCallback<DragPerformEvent>(OnGridDragPerform);
        _gridPreview.RegisterCallback<PointerDownEvent>(OnGridPointerDown);
        _gridPreview.RegisterCallback<PointerMoveEvent>(OnGridPointerMove);
        _gridPreview.RegisterCallback<PointerUpEvent>(OnGridPointerUp);
        _gridPreview.RegisterCallback<KeyDownEvent>(OnGridKeyDown);

        root.Q<Button>("levelTab").clicked += () => ShowTab(true);
        root.Q<Button>("materialsTab").clicked += () => ShowTab(false);
        root.Q<Button>("addBlock").clicked += AddBlock;
        root.Q<Button>("initLevel").clicked += InitLevel;

        BuildMaterialFields();
        _materialFields.RegisterCallback<DragUpdatedEvent>(OnMaterialsDragUpdated);
        _materialFields.RegisterCallback<DragPerformEvent>(OnMaterialsDragPerform);
        RebuildGridPreview();
    }

    private void OnDisable()
    {
        ClearRuntimePreview(true);
    }

    public static Material GetMaterial(BlockColor blockColor)
    {
        return ColorMaterials.TryGetValue(blockColor, out Material material) ? material : null;
    }

    private void ShowTab(bool showLevel)
    {
        _levelPanel.EnableInClassList("hidden", !showLevel);
        _materialsPanel.EnableInClassList("hidden", showLevel);
    }

    private void BuildMaterialFields()
    {
        _materialFields.Clear();

        foreach (BlockColor blockColor in Enum.GetValues(typeof(BlockColor)))
        {
            if (!ColorMaterials.ContainsKey(blockColor))
                ColorMaterials.Add(blockColor, FindMaterialForColor(blockColor));

            ObjectField field = new ObjectField(blockColor.ToString())
            {
                objectType = typeof(Material),
                allowSceneObjects = false,
                value = GetMaterial(blockColor)
            };

            field.RegisterValueChangedCallback(evt =>
            {
                ColorMaterials[blockColor] = evt.newValue as Material;
                RefreshPlacedBlockMaterials(blockColor);
                RebuildGridPreview();
            });
            _materialFields.Add(field);
        }
    }

    private static Material FindMaterialForColor(BlockColor blockColor)
    {
        Material existingMaterial = GetMaterial(blockColor);
        if (existingMaterial != null)
            return existingMaterial;

        string[] guids = AssetDatabase.FindAssets($"{blockColor} t:Material", new[] { "Assets/_Project/Resources/Material" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && string.Equals(material.name, blockColor.ToString(), StringComparison.OrdinalIgnoreCase))
                return material;
        }

        return null;
    }

    private void OnMaterialsDragUpdated(DragUpdatedEvent evt)
    {
        DragAndDrop.visualMode = HasDraggedMaterials() ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
        evt.StopPropagation();
    }

    private void OnMaterialsDragPerform(DragPerformEvent evt)
    {
        int mappedCount = 0;

        foreach (Object objectReference in DragAndDrop.objectReferences)
        {
            Material material = objectReference as Material;
            if (material == null)
                continue;

            foreach (BlockColor blockColor in Enum.GetValues(typeof(BlockColor)))
            {
                if (!string.Equals(material.name, blockColor.ToString(), StringComparison.OrdinalIgnoreCase))
                    continue;

                ColorMaterials[blockColor] = material;
                RefreshPlacedBlockMaterials(blockColor);
                mappedCount++;
                break;
            }
        }

        if (mappedCount > 0)
        {
            DragAndDrop.AcceptDrag();
            BuildMaterialFields();
            RebuildGridPreview();
            Debug.Log($"Mapped {mappedCount} material(s) to BlockColor enum.");
        }
        else
        {
            Debug.LogWarning("No dragged material matched a BlockColor enum name.");
        }

        evt.StopPropagation();
    }

    private static bool HasDraggedMaterials()
    {
        foreach (Object objectReference in DragAndDrop.objectReferences)
        {
            if (objectReference is Material)
                return true;
        }

        return false;
    }

    private void InitLevel()
    {
        int level = Mathf.Max(1, _levelID.value);
        int gridX = Mathf.Clamp(_gridX.value, MinGridSize, MaxGridSize);
        int gridY = Mathf.Clamp(_gridY.value, MinGridSize, MaxGridSize);

        _levelID.SetValueWithoutNotify(level);
        _gridX.SetValueWithoutNotify(gridX);
        _gridY.SetValueWithoutNotify(gridY);

        LevelData selectedLevel = _levelAsset.value as LevelData;
        if (selectedLevel != null)
        {
            Undo.RecordObject(selectedLevel, "Update Level");
            selectedLevel.Init(level, new Vector2Int(gridX, gridY));
            selectedLevel.SetBlocks(_placedBlocks);
            EditorUtility.SetDirty(selectedLevel);
            AssetDatabase.SaveAssets();
            Selection.activeObject = selectedLevel;
            Debug.Log($"Updated level: {AssetDatabase.GetAssetPath(selectedLevel)}");
            return;
        }

        EnsureLevelFolderExists();
        string assetName = $"level_{level}";
        string assetPath = $"{LevelFolderPath}/{assetName}.asset";

        if (AssetDatabase.LoadAssetAtPath<LevelData>(assetPath) != null)
        {
            Debug.Log($"Da co level: {assetPath}");
            return;
        }

        LevelData levelData = CreateInstance<LevelData>();
        levelData.Init(level, new Vector2Int(gridX, gridY));
        levelData.SetBlocks(_placedBlocks);

        AssetDatabase.CreateAsset(levelData, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = levelData;
        Debug.Log($"Created level: {assetPath}");
    }

    private void LoadLevel(LevelData levelData)
    {
        _placedBlocks.Clear();
        _selectedBlockIndex = -1;
        _draggingBlockIndex = -1;

        if (levelData == null)
        {
            RebuildGridPreview();
            return;
        }

        _levelID.SetValueWithoutNotify(levelData.LevelId);
        _gridX.SetValueWithoutNotify(Mathf.Clamp(levelData.GridSize.x, MinGridSize, MaxGridSize));
        _gridY.SetValueWithoutNotify(Mathf.Clamp(levelData.GridSize.y, MinGridSize, MaxGridSize));

        foreach (LevelBlockData blockData in levelData.Blocks)
        {
            if (blockData == null || blockData.Prefab == null)
                continue;

            _placedBlocks.Add(new LevelBlockData(
                blockData.Prefab,
                blockData.GridPosition,
                blockData.Rotation,
                blockData.BlockColor,
                blockData.Material));
        }

        RebuildGridPreview();
    }

    private void AddBlock()
    {
        LevelBlockPickerPopup.Open();
    }

    private static void EnsureLevelFolderExists()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            AssetDatabase.CreateFolder("Assets", "_Project");

        if (!AssetDatabase.IsValidFolder(LevelFolderPath))
            AssetDatabase.CreateFolder("Assets/_Project", "LevelEditor");
    }

    private void RebuildGridPreview()
    {
        if (_gridPreview == null)
            return;

        _gridPreview.Clear();

        int width = Mathf.Clamp(_gridX.value, MinGridSize, MaxGridSize);
        int height = Mathf.Clamp(_gridY.value, MinGridSize, MaxGridSize);

        _gridPreview.style.width = width * GridCellSize;
        _gridPreview.style.height = height * GridCellSize;

        for (int y = 0; y < height; y++)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            for (int x = 0; x < width; x++)
            {
                VisualElement cell = new VisualElement();
                cell.style.width = GridCellSize;
                cell.style.height = GridCellSize;
                cell.style.backgroundColor = GetGridCellColor(x, y);
                cell.style.borderTopWidth = 1;
                cell.style.borderBottomWidth = 1;
                cell.style.borderLeftWidth = 1;
                cell.style.borderRightWidth = 1;

                Color borderColor = new Color(0.38f, 0.42f, 0.46f);
                cell.style.borderTopColor = borderColor;
                cell.style.borderBottomColor = borderColor;
                cell.style.borderLeftColor = borderColor;
                cell.style.borderRightColor = borderColor;
                row.Add(cell);
            }

            _gridPreview.Add(row);
        }

        RebuildRuntimePreview();
    }

    private void OnGridDragUpdated(DragUpdatedEvent evt)
    {
        LevelBlockDragPayload payload = GetDraggedBlockPayload();
        DragAndDrop.visualMode = payload != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
        evt.StopPropagation();
    }

    private void OnGridDragPerform(DragPerformEvent evt)
    {
        LevelBlockDragPayload payload = GetDraggedBlockPayload();
        if (payload == null)
            return;

        Vector2Int gridPosition = ToGridPosition(evt.localMousePosition);
        if (!TryAddBlock(payload, gridPosition))
            return;

        DragAndDrop.AcceptDrag();
        evt.StopPropagation();
    }

    private void OnGridPointerDown(PointerDownEvent evt)
    {
        _gridPreview.Focus();

        Vector2Int gridPosition = ToGridPosition(_gridPreview.WorldToLocal(evt.position));
        int blockIndex = FindPlacedBlockIndexAt(gridPosition);
        _selectedBlockIndex = blockIndex;
        _draggingBlockIndex = blockIndex;

        if (blockIndex >= 0)
        {
            _dragCellOffset = gridPosition - _placedBlocks[blockIndex].GridPosition;
            _gridPreview.CapturePointer(evt.pointerId);
        }

        RebuildGridPreview();
        evt.StopPropagation();
    }

    private void OnGridPointerMove(PointerMoveEvent evt)
    {
        if (_draggingBlockIndex < 0 || _draggingBlockIndex >= _placedBlocks.Count)
            return;

        Vector2Int gridPosition = ToGridPosition(_gridPreview.WorldToLocal(evt.position)) - _dragCellOffset;
        LevelBlockData currentBlock = _placedBlocks[_draggingBlockIndex];
        if (currentBlock.GridPosition == gridPosition)
            return;

        LevelBlockData movedBlock = CloneBlock(currentBlock, gridPosition, currentBlock.Rotation);
        if (!CanPlaceBlock(movedBlock, _draggingBlockIndex))
            return;

        _placedBlocks[_draggingBlockIndex] = movedBlock;
        RebuildGridPreview();
        evt.StopPropagation();
    }

    private void OnGridPointerUp(PointerUpEvent evt)
    {
        _draggingBlockIndex = -1;
        if (_gridPreview.HasPointerCapture(evt.pointerId))
            _gridPreview.ReleasePointer(evt.pointerId);

        evt.StopPropagation();
    }

    private void OnGridKeyDown(KeyDownEvent evt)
    {
        if (_selectedBlockIndex < 0 || _selectedBlockIndex >= _placedBlocks.Count)
            return;

        if (evt.keyCode == KeyCode.D)
        {
            _placedBlocks.RemoveAt(_selectedBlockIndex);
            _selectedBlockIndex = -1;
            _draggingBlockIndex = -1;
            RebuildGridPreview();
            evt.StopPropagation();
            return;
        }

        return;
    }

    private bool TryAddBlock(LevelBlockDragPayload payload, Vector2Int gridPosition)
    {
        LevelBlockData newBlock = new LevelBlockData(payload.Prefab, gridPosition, 0, payload.BlockColor, payload.Material);
        if (!CanPlaceBlock(newBlock, -1))
        {
            Debug.LogWarning($"Cannot place block '{payload.Prefab.name}' at {gridPosition}. Block is outside the grid or overlaps another block.");
            return false;
        }

        _placedBlocks.Add(newBlock);
        _selectedBlockIndex = _placedBlocks.Count - 1;
        RebuildGridPreview();
        return true;
    }

    private bool CanPlaceBlock(LevelBlockData blockData, int ignoreIndex)
    {
        BlockBehavior block = GetBlockBehavior(blockData.Prefab);
        if (block == null)
            return false;

        int gridWidth = Mathf.Clamp(_gridX.value, MinGridSize, MaxGridSize);
        int gridHeight = Mathf.Clamp(_gridY.value, MinGridSize, MaxGridSize);
        int rotation = NormalizeRotation(blockData.Rotation);
        Vector2Int rotatedSize = GetRotatedSize(block, rotation);

        for (int y = 0; y < rotatedSize.y; y++)
        {
            for (int x = 0; x < rotatedSize.x; x++)
            {
                if (!IsRotatedCellOccupied(block, x, y, rotation))
                    continue;

                Vector2Int cellPosition = ToLevelCell(blockData.GridPosition, block, x, y, rotation);
                if (cellPosition.x < 0 || cellPosition.x >= gridWidth || cellPosition.y < 0 || cellPosition.y >= gridHeight)
                    return false;

                if (IsGridCellOccupied(cellPosition.x, cellPosition.y, ignoreIndex))
                    return false;
            }
        }

        return true;
    }

    private Color GetGridCellColor(int x, int y)
    {
        int blockIndex = FindPlacedBlockIndexAt(new Vector2Int(x, y));
        if (blockIndex < 0)
            return new Color(0.18f, 0.2f, 0.22f);

        LevelBlockData blockData = _placedBlocks[blockIndex];
        return GetMaterialColor(blockData.Material, GetFallbackColor(blockData.BlockColor));
    }

    private bool IsGridCellOccupied(int gridX, int gridY, int ignoreIndex)
    {
        return FindPlacedBlockIndexAt(new Vector2Int(gridX, gridY), ignoreIndex) >= 0;
    }

    private int FindPlacedBlockIndexAt(Vector2Int gridPosition, int ignoreIndex = -1)
    {
        for (int i = _placedBlocks.Count - 1; i >= 0; i--)
        {
            if (i == ignoreIndex)
                continue;

            LevelBlockData placedBlock = _placedBlocks[i];
            BlockBehavior block = GetBlockBehavior(placedBlock.Prefab);
            if (block == null)
                continue;

            int rotation = NormalizeRotation(placedBlock.Rotation);
            Vector2Int rotatedSize = GetRotatedSize(block, rotation);

            for (int y = 0; y < rotatedSize.y; y++)
            {
                for (int x = 0; x < rotatedSize.x; x++)
                {
                    if (!IsRotatedCellOccupied(block, x, y, rotation))
                        continue;

                    if (ToLevelCell(placedBlock.GridPosition, block, x, y, rotation) == gridPosition)
                        return i;
                }
            }
        }

        return -1;
    }

    private static LevelBlockData CloneBlock(LevelBlockData source, Vector2Int gridPosition, int rotation)
    {
        return new LevelBlockData(source.Prefab, gridPosition, rotation, source.BlockColor, source.Material);
    }

    private static BlockBehavior GetBlockBehavior(GameObject prefab)
    {
        return prefab != null ? prefab.GetComponentInChildren<BlockBehavior>() : null;
    }

    private static Vector2Int ToLevelCell(Vector2Int gridPosition, BlockBehavior block, int blockX, int blockY, int rotation)
    {
        Vector2Int pivot = GetRotatedPivot(block, rotation);
        return new Vector2Int(gridPosition.x + blockX - pivot.x, gridPosition.y + blockY - pivot.y);
    }

    private static Vector2Int ToGridPosition(Vector2 localMousePosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(localMousePosition.x / GridCellSize),
            Mathf.FloorToInt(localMousePosition.y / GridCellSize));
    }

    private static Vector2Int GetRotatedSize(BlockBehavior block, int rotation)
    {
        return NormalizeRotation(rotation) % 2 == 0
            ? new Vector2Int(block.Width, block.Height)
            : new Vector2Int(block.Height, block.Width);
    }

    private static Vector2Int GetRotatedPivot(BlockBehavior block, int rotation)
    {
        switch (NormalizeRotation(rotation))
        {
            case 1:
                return new Vector2Int(block.Height - 1 - block.Pivot.y, block.Pivot.x);
            case 2:
                return new Vector2Int(block.Width - 1 - block.Pivot.x, block.Height - 1 - block.Pivot.y);
            case 3:
                return new Vector2Int(block.Pivot.y, block.Width - 1 - block.Pivot.x);
            default:
                return block.Pivot;
        }
    }

    private static bool IsRotatedCellOccupied(BlockBehavior block, int x, int y, int rotation)
    {
        switch (NormalizeRotation(rotation))
        {
            case 1:
                return block.IsCellOccupied(y, block.Height - 1 - x);
            case 2:
                return block.IsCellOccupied(block.Width - 1 - x, block.Height - 1 - y);
            case 3:
                return block.IsCellOccupied(block.Width - 1 - y, x);
            default:
                return block.IsCellOccupied(x, y);
        }
    }

    private static int NormalizeRotation(int rotation)
    {
        return ((rotation % 4) + 4) % 4;
    }

    private static Color GetMaterialColor(Material material, Color fallback)
    {
        if (material == null)
            return fallback;

        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");

        return material.HasProperty("_Color") ? material.GetColor("_Color") : fallback;
    }

    private static Color GetFallbackColor(BlockColor blockColor)
    {
        switch (blockColor)
        {
            case BlockColor.Yellow:
                return new Color(1f, 0.86f, 0.12f);
            case BlockColor.Blue:
                return new Color(0.12f, 0.42f, 1f);
            case BlockColor.Pink:
                return new Color(1f, 0.25f, 0.68f);
            default:
                return new Color(0.9f, 0.22f, 0.18f);
        }
    }

    private void RebuildRuntimePreview()
    {
        ClearRuntimePreview(true);

        if (_placedBlocks.Count == 0)
            return;

        _runtimePreviewRoot = new GameObject(RuntimePreviewRootName);
        _runtimePreviewRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        foreach (LevelBlockData blockData in _placedBlocks)
            CreateRuntimePreviewBlock(blockData);

        SceneView.RepaintAll();
    }

    private void CreateRuntimePreviewBlock(LevelBlockData blockData)
    {
        if (blockData.Prefab == null)
            return;

        GameObject previewObject = PrefabUtility.InstantiatePrefab(blockData.Prefab) as GameObject;
        if (previewObject == null)
            previewObject = Instantiate(blockData.Prefab);

        previewObject.name = $"Preview_{blockData.Prefab.name}";
        SetRuntimePreviewHideFlags(previewObject);
        previewObject.transform.SetParent(_runtimePreviewRoot.transform);
        previewObject.transform.position = ToRuntimeWorldPosition(blockData);

        ApplyRuntimePreviewMaterial(previewObject, blockData.Material);
    }

    private void RefreshPlacedBlockMaterials(BlockColor blockColor)
    {
        Material material = GetMaterial(blockColor);

        for (int i = 0; i < _placedBlocks.Count; i++)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData.BlockColor != blockColor)
                continue;

            _placedBlocks[i] = new LevelBlockData(blockData.Prefab, blockData.GridPosition, blockData.Rotation, blockData.BlockColor, material);
        }
    }

    private static Vector3 ToRuntimeWorldPosition(LevelBlockData blockData)
    {
        return new Vector3(blockData.GridPosition.x, 0f, -blockData.GridPosition.y);
    }

    private static void ApplyRuntimePreviewMaterial(GameObject previewObject, Material material)
    {
        if (material == null)
            return;

        Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
            renderer.sharedMaterial = material;
    }

    private static void SetRuntimePreviewHideFlags(GameObject previewObject)
    {
        HideFlags hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        previewObject.hideFlags = hideFlags;

        Transform[] children = previewObject.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
            child.gameObject.hideFlags = hideFlags;
    }

    private void ClearRuntimePreview(bool includeSceneOrphan)
    {
        if (_runtimePreviewRoot != null)
        {
            DestroyImmediate(_runtimePreviewRoot);
            _runtimePreviewRoot = null;
        }

        if (!includeSceneOrphan)
            return;

        GameObject orphanRoot = GameObject.Find(RuntimePreviewRootName);
        if (orphanRoot != null)
            DestroyImmediate(orphanRoot);
    }

    private static LevelBlockDragPayload GetDraggedBlockPayload()
    {
        if (DragAndDrop.GetGenericData(BlockDragKey) is LevelBlockDragPayload payload && GetBlockBehavior(payload.Prefab) != null)
            return payload;

        foreach (Object objectReference in DragAndDrop.objectReferences)
        {
            if (objectReference is GameObject gameObject && GetBlockBehavior(gameObject) != null)
                return new LevelBlockDragPayload(gameObject, BlockColor.Red, GetMaterial(BlockColor.Red));
        }

        return null;
    }

    public static void StartBlockDrag(GameObject prefab, BlockColor blockColor, Material material)
    {
        LevelBlockDragPayload payload = new LevelBlockDragPayload(prefab, blockColor, material);
        DragAndDrop.PrepareStartDrag();
        DragAndDrop.objectReferences = new Object[] { prefab };
        DragAndDrop.SetGenericData(BlockDragKey, payload);
        DragAndDrop.StartDrag(prefab.name);
    }

    private sealed class LevelBlockDragPayload
    {
        public readonly GameObject Prefab;
        public readonly BlockColor BlockColor;
        public readonly Material Material;

        public LevelBlockDragPayload(GameObject prefab, BlockColor blockColor, Material material)
        {
            Prefab = prefab;
            BlockColor = blockColor;
            Material = material;
        }
    }
}

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
    private Vector2 _scrollPosition;
    private List<GameObject> _blockPrefabs;
    private BlockColor _selectedColor;
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
        EditorGUILayout.LabelField("Drag a block into the level grid", EditorStyles.boldLabel);
        _selectedColor = (BlockColor)EditorGUILayout.EnumPopup("Color", _selectedColor);

        Material material = LevelEditor.GetMaterial(_selectedColor);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Material", material, typeof(Material), false);
        }

        if (_blockPrefabs == null || _blockPrefabs.Count == 0)
        {
            EditorGUILayout.HelpBox("No prefab with BlockBehavior was found under Assets/_Project/Resources/Prefab.", MessageType.Info);
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
        EditorGUILayout.LabelField(prefab.name, _blockTitleStyle, GUILayout.Height(itemHeight), GUILayout.Width(labelWidth));

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
    }
}
