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
    // Kich thuoc grid nho nhat cho phep trong editor.
    private const int MinGridSize = 1;
    // Kich thuoc grid lon nhat cho phep trong editor.
    private const int MaxGridSize = 20;
    // Kich thuoc pixel cua moi o trong grid preview UI.
    private const int GridCellSize = 26;
    // Do lech wall vao trong khi tao runtime preview/prefab.
    private const float WallRuntimeInset = 0.25f;
    // Do lech corner vao trong khi tao runtime preview/prefab.
    private const float CornerRuntimeInset = 0.25f;
    // Do chong nhe giua wall va corner de khong bi ho khe.
    private const float WallCornerOverlap = 0.12f;
    // Tam trung binh cua cac object moi truong, dung de tinh huong inset fallback.
    private static Vector2 _environmentRuntimeCenter;
    // Danh dau tam moi truong da duoc tinh hay chua.
    private static bool _hasEnvironmentRuntimeCenter;

    // Thu muc luu LevelData asset va prefab level sinh ra tu editor.
    private const string LevelFolderPath = "Assets/_Project/LevelEditor";
    // Key luu payload drag block trong DragAndDrop generic data.
    private const string BlockDragKey = "LevelEditorBlockPayload";
    // Ten root tam trong scene de hien runtime preview cua level.
    private const string RuntimePreviewRootName = "[LevelEditor Runtime Preview]";
    // Duong dan prefab wall mac dinh.
    private const string WallPatch = "Assets/_Project/Resources/Prefab/Environment/Border.prefab";
    // Duong dan prefab corner mac dinh.
    private const string CornerPatch = "Assets/_Project/Resources/Prefab/Environment/Corner.prefab";
    // Duong dan prefab obstacle mac dinh.
    private const string ObstaclePatch = "Assets/_Project/Resources/Prefab/Environment/Inner Obstacle.prefab";
    // Duong dan prefab tile nen mac dinh.
    private const string TilePatch = "Assets/_Project/Resources/Prefab/Environment/Inner Tile.prefab";

    // Bang map mau logic sang Material de gan cho block trong editor/runtime preview.
    private static readonly Dictionary<TypeBlockColor, Material> ColorMaterials = new Dictionary<TypeBlockColor, Material>();
    // Prefab wall hien dang duoc chon trong tab Prefabs.
    private static GameObject _activeWallPrefab;
    // Prefab corner hien dang duoc chon trong tab Prefabs.
    private static GameObject _activeCornerPrefab;
    // Prefab obstacle hien dang duoc chon trong tab Prefabs.
    private static GameObject _activeObstaclePrefab;
    // Prefab tile hien dang duoc chon trong tab Prefabs.
    private static GameObject _activeTilePrefab;

    // Field chon LevelData asset can load/update.
    private ObjectField _levelAsset;
    // Field nhap ID level.
    private IntegerField _levelID;
    // Field nhap kich thuoc grid theo truc X.
    private IntegerField _gridX;
    // Field nhap kich thuoc grid theo truc Y.
    private IntegerField _gridY;
    // Container bao ngoai grid preview.
    private VisualElement _gridHolder;
    // VisualElement chua cac cell preview va nhan event drag/click.
    private VisualElement _gridPreview;
    // Panel hien thong ke so block theo mau.
    private VisualElement _blockSummary;
    // Panel tab Level.
    private VisualElement _levelPanel;
    // Panel tab Materials.
    private VisualElement _materialsPanel;
    // Container chua cac ObjectField material theo BlockColor.
    private VisualElement _materialFields;
    // Panel tab Prefabs.
    private VisualElement _prefabsPanel;
    // Container chua cac ObjectField prefab moi truong.
    private VisualElement _prefabFields;
    // Field chon prefab wall.
    private ObjectField _wallPrefabField;
    // Field chon prefab corner.
    private ObjectField _cornerPrefabField;
    // Field chon prefab obstacle.
    private ObjectField _obstaclePrefabField;
    // Field chon prefab tile.
    private ObjectField _tilePrefabField;

    // Danh sach block/object moi truong dang duoc dat trong level editor.
    private readonly List<LevelBlockData> _placedBlocks = new List<LevelBlockData>();
    // Cac tile duoc tao tu dong khi wall/corner tao thanh khung kin trong phien editor hien tai.
    private readonly HashSet<Vector2Int> _autoCreatedTilePositions = new HashSet<Vector2Int>();
    // Index block dang duoc chon trong _placedBlocks; -1 nghia la khong chon.
    private int _selectedBlockIndex = -1;
    // Index block dang duoc keo trong _placedBlocks; -1 nghia la khong keo.
    private int _draggingBlockIndex = -1;
    // Do lech giua cell click va pivot/vi tri block khi bat dau drag.
    private Vector2Int _dragCellOffset;
    // Vi tri grid gan nhat duoc chon/click.
    private Vector2Int _selectedGridPosition;
    // Danh dau _selectedGridPosition co gia tri hop le hay chua.
    private bool _hasSelectedGridPosition;
    // Tool moi truong dang chon de dat wall/corner/obstacle/tile.
    private EnvironmentTool _selectedEnvironmentTool = EnvironmentTool.None;
    // Root GameObject tam dung de preview level trong Scene view.
    private GameObject _runtimePreviewRoot;

    /// <summary>
    /// Mở cửa sổ LevelEditor từ menu Unity.
    /// </summary>
    [MenuItem("LevelEditor/LevelEditor")]
    public static void ShowWindow()
    {
        GetWindow<LevelEditor>("LevelEditor");
    }

    /// <summary>
    /// Khởi tạo giao diện UI Toolkit, bind field, đăng ký event và dựng preview ban đầu.
    /// </summary>
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
        _prefabsPanel = root.Q<VisualElement>("prefabsPanel");
        _prefabFields = root.Q<VisualElement>("prefabFields");
        _blockSummary = root.Q<VisualElement>("blockSummary");

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

        root.Q<Button>("levelTab").clicked += () => ShowTab(EditorTab.Level);
        root.Q<Button>("materialsTab").clicked += () => ShowTab(EditorTab.Materials);
        root.Q<Button>("prefabsTab").clicked += () => ShowTab(EditorTab.Prefabs);
        root.Q<Button>("addBlock").clicked += AddBlock;
        root.Q<Button>("initLevel").clicked += InitLevel;
        Button wallButton = root.Q<Button>("Wall");
        if (wallButton != null)
            wallButton.clicked += AddWal;

        Button cornerButton = root.Q<Button>("Corner");
        if (cornerButton != null)
            cornerButton.clicked += AddCorner;

        Button obstacleButton = root.Q<Button>("Obstacle");
        if (obstacleButton != null)
            obstacleButton.clicked += AddObstacle;

        Button tileButton = root.Q<Button>("Tile");
        if (tileButton != null)
            tileButton.clicked += AddTile;

        BuildMaterialFields();
        BuildPrefabFields();
        _materialFields.RegisterCallback<DragUpdatedEvent>(OnMaterialsDragUpdated);
        _materialFields.RegisterCallback<DragPerformEvent>(OnMaterialsDragPerform);
        RebuildGridPreview();
    }


    /// <summary>
    /// Dọn runtime preview khi cửa sổ editor bị đóng hoặc disable.
    /// </summary>
    private void OnDisable()
    {
        ClearRuntimePreview(true);
    }

    /// <summary>
    /// Lấy material đang được map với màu block trong editor.
    /// </summary>
    public static Material GetMaterial(TypeBlockColor typeBlockColor)
    {
        return ColorMaterials.TryGetValue(typeBlockColor, out Material material) ? material : null;
    }

    /// <summary>
    /// Hiển thị tab được chọn và ẩn các panel còn lại.
    /// </summary>
    private void ShowTab(EditorTab tab)
    {
        _levelPanel.EnableInClassList("hidden", tab != EditorTab.Level);
        _materialsPanel.EnableInClassList("hidden", tab != EditorTab.Materials);
        _prefabsPanel.EnableInClassList("hidden", tab != EditorTab.Prefabs);
    }

    private enum EditorTab
    {
        // Tab chinh de chinh level/grid/block.
        Level,
        // Tab map BlockColor sang Material.
        Materials,
        // Tab chon prefab moi truong.
        Prefabs
    }

    private enum EnvironmentTool
    {
        // Khong dung tool moi truong, click grid se chon/keo block binh thuong.
        None,
        // Tool dat wall.
        Wall,
        // Tool dat corner.
        Corner,
        // Tool dat vat can.
        Obstacle,
        // Tool dat tile nen.
        Tile
    }

    /// <summary>
    /// Tạo các ObjectField cho từng BlockColor để người dùng map material.
    /// </summary>
    private void BuildMaterialFields()
    {
        _materialFields.Clear();

        foreach (TypeBlockColor blockColor in Enum.GetValues(typeof(TypeBlockColor)))
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

    /// <summary>
    /// Tạo các ObjectField cấu hình prefab môi trường như tường, góc, vật cản và tile.
    /// </summary>
    private void BuildPrefabFields()
    {
        if (_prefabFields == null)
            return;

        _prefabFields.Clear();
        _wallPrefabField = CreatePrefabField("Wall", WallPatch);
        _cornerPrefabField = CreatePrefabField("Corner", CornerPatch);
        _obstaclePrefabField = CreatePrefabField("Obstacle", ObstaclePatch);
        _tilePrefabField = CreatePrefabField("Tile", TilePatch);

        _prefabFields.Add(_wallPrefabField);
        _prefabFields.Add(_cornerPrefabField);
        _prefabFields.Add(_obstaclePrefabField);
        _prefabFields.Add(_tilePrefabField);
        UpdateActiveEnvironmentPrefabs();
    }

    /// <summary>
    /// Tạo một field chọn prefab môi trường với prefab mặc định làm fallback.
    /// </summary>
    private ObjectField CreatePrefabField(string label, string defaultPath)
    {
        ObjectField field = new ObjectField(label)
        {
            objectType = typeof(GameObject),
            allowSceneObjects = false,
            value = AssetDatabase.LoadAssetAtPath<GameObject>(defaultPath)
        };

        field.RegisterValueChangedCallback(_ =>
        {
            UpdateActiveEnvironmentPrefabs();
            RebuildGridPreview();
        });
        return field;
    }

    /// <summary>
    /// Cập nhật cache prefab môi trường đang được chọn trong UI.
    /// </summary>
    private void UpdateActiveEnvironmentPrefabs()
    {
        _activeWallPrefab = GetPrefabFromField(_wallPrefabField, WallPatch);
        _activeCornerPrefab = GetPrefabFromField(_cornerPrefabField, CornerPatch);
        _activeObstaclePrefab = GetPrefabFromField(_obstaclePrefabField, ObstaclePatch);
        _activeTilePrefab = GetPrefabFromField(_tilePrefabField, TilePatch);
    }

    /// <summary>
    /// Tìm material trong project có tên khớp với BlockColor.
    /// </summary>
    private static Material FindMaterialForColor(TypeBlockColor typeBlockColor)
    {
        Material existingMaterial = GetMaterial(typeBlockColor);
        if (existingMaterial != null)
            return existingMaterial;

        string[] guids =
            AssetDatabase.FindAssets($"{typeBlockColor} t:Material", new[] { "Assets/_Project/Resources/Material" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null &&
                string.Equals(material.name, typeBlockColor.ToString(), StringComparison.OrdinalIgnoreCase))
                return material;
        }

        return null;
    }

    /// <summary>
    /// Cập nhật visual mode khi người dùng kéo material vào panel material.
    /// </summary>
    private void OnMaterialsDragUpdated(DragUpdatedEvent evt)
    {
        DragAndDrop.visualMode = HasDraggedMaterials() ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
        evt.StopPropagation();
    }

    /// <summary>
    /// Map các material được thả vào BlockColor có tên tương ứng.
    /// </summary>
    private void OnMaterialsDragPerform(DragPerformEvent evt)
    {
        int mappedCount = 0;

        foreach (Object objectReference in DragAndDrop.objectReferences)
        {
            Material material = objectReference as Material;
            if (material == null)
                continue;

            foreach (TypeBlockColor blockColor in Enum.GetValues(typeof(TypeBlockColor)))
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

    /// <summary>
    /// Kiểm tra DragAndDrop hiện tại có chứa ít nhất một material hay không.
    /// </summary>
    private static bool HasDraggedMaterials()
    {
        foreach (Object objectReference in DragAndDrop.objectReferences)
        {
            if (objectReference is Material)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Tạo hoặc cập nhật asset LevelData và prefab level từ dữ liệu đang đặt trong editor.
    /// </summary>
    private void InitLevel()
    {
        int level = Mathf.Max(1, _levelID.value);
        int gridX = Mathf.Clamp(_gridX.value, MinGridSize, MaxGridSize);
        int gridY = Mathf.Clamp(_gridY.value, MinGridSize, MaxGridSize);

        _levelID.SetValueWithoutNotify(level);
        _gridX.SetValueWithoutNotify(gridX);
        _gridY.SetValueWithoutNotify(gridY);
        EnsureLevelFolderExists();
        AutoUpdateWallsAndCorners();

        LevelData selectedLevel = _levelAsset.value as LevelData;
        if (selectedLevel != null)
        {
            Undo.RecordObject(selectedLevel, "Update Level");
            selectedLevel.Init(level, new Vector2Int(gridX, gridY));
            selectedLevel.SetBlocks(_placedBlocks);
            EditorUtility.SetDirty(selectedLevel);
            string prefabPath = SaveLevelPrefab(level);
            AssetDatabase.SaveAssets();
            Selection.activeObject = selectedLevel;
            Debug.Log(
                $"Updated level: {AssetDatabase.GetAssetPath(selectedLevel)} | Prefab: {prefabPath} | {BuildBlockSummaryLog()}");
            return;
        }

        string assetName = $"level_{level}";
        string assetPath = $"{LevelFolderPath}/{assetName}.asset";

        LevelData levelData = AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
        if (levelData != null)
        {
            Undo.RecordObject(levelData, "Update Level");
            levelData.Init(level, new Vector2Int(gridX, gridY));
            levelData.SetBlocks(_placedBlocks);
            EditorUtility.SetDirty(levelData);

            string prefabPath = SaveLevelPrefab(level);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = levelData;
            Debug.Log($"Updated existing level: {assetPath} | Prefab: {prefabPath} | {BuildBlockSummaryLog()}");
            return;
        }

        levelData = CreateInstance<LevelData>();
        levelData.Init(level, new Vector2Int(gridX, gridY));
        levelData.SetBlocks(_placedBlocks);

        AssetDatabase.CreateAsset(levelData, assetPath);
        string createdPrefabPath = SaveLevelPrefab(level);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = levelData;
        Debug.Log($"Created level: {assetPath} | Prefab: {createdPrefabPath} | {BuildBlockSummaryLog()}");
    }

    /// <summary>
    /// Nạp dữ liệu từ LevelData asset vào editor để tiếp tục chỉnh sửa.
    /// </summary>
    private void LoadLevel(LevelData levelData)
    {
        _placedBlocks.Clear();
        _autoCreatedTilePositions.Clear();
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
                blockData.TypeBlockColor,
                blockData.Material,
                blockData.IsEnvironment));
        }

        AutoUpdateWallsAndCorners();
        RebuildGridPreview();
    }

    /// <summary>
    /// Mở popup chọn block thường và tắt tool môi trường hiện tại.
    /// </summary>
    private void AddBlock()
    {
        _selectedEnvironmentTool = EnvironmentTool.None;
        LevelBlockPickerPopup.Open();
    }

    /// <summary>
    /// Đảm bảo thư mục lưu LevelData và prefab level tồn tại.
    /// </summary>
    private static void EnsureLevelFolderExists()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            AssetDatabase.CreateFolder("Assets", "_Project");

        if (!AssetDatabase.IsValidFolder(LevelFolderPath))
            AssetDatabase.CreateFolder("Assets/_Project", "LevelEditor");
    }

    /// <summary>
    /// Build root prefab tạm và lưu thành prefab level trong thư mục level editor.
    /// </summary>
    private string SaveLevelPrefab(int level)
    {
        string prefabPath = $"{LevelFolderPath}/level_{level}.prefab";
        GameObject prefabRoot = BuildLevelPrefabRoot(level);

        try
        {
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            DestroyImmediate(prefabRoot);
        }

        return prefabPath;
    }

    /// <summary>
    /// Tạo GameObject root chứa toàn bộ object của level để đem đi lưu prefab.
    /// </summary>
    private GameObject BuildLevelPrefabRoot(int level)
    {
        GameObject root = new GameObject($"level_{level}");

        RefreshEnvironmentRuntimeCenter(_placedBlocks);
        HashSet<int> groupedWallIndices = CreateWallSegmentChildren(root.transform, false);
        CreateCornerBridgeWallChildren(root.transform, false);

        for (int i = 0; i < _placedBlocks.Count; i++)
        {
            if (groupedWallIndices.Contains(i))
                continue;

            CreateLevelPrefabChild(root.transform, _placedBlocks[i]);
        }

        _hasEnvironmentRuntimeCenter = false;

        return root;
    }

    /// <summary>
    /// Instantiate một prefab block vào root level với vị trí, rotation và material đúng runtime.
    /// </summary>
    private void CreateLevelPrefabChild(Transform parent, LevelBlockData blockData)
    {
        if (blockData == null || blockData.Prefab == null)
            return;

        GameObject child = PrefabUtility.InstantiatePrefab(blockData.Prefab) as GameObject;
        if (child == null)
            child = Instantiate(blockData.Prefab);

        child.name = blockData.Prefab.name;
        child.transform.SetParent(parent, false);
        child.transform.localPosition = ToRuntimeWorldPosition(blockData);
        child.transform.localRotation = Quaternion.Euler(0f, NormalizeRotation(blockData.Rotation) * 90f, 0f);

        ApplyRuntimePreviewMaterial(child, blockData.Material);
    }

    /// <summary>
    /// Dựng lại lưới preview 2D trong editor theo kích thước grid và dữ liệu block hiện tại.
    /// </summary>
    private void RebuildGridPreview()
    {
        if (_gridPreview == null)
            return;

        _gridPreview.Clear();
        RebuildBlockSummary();

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

    /// <summary>
    /// Dựng lại UI thống kê tổng block và số lượng từng màu.
    /// </summary>
    private void RebuildBlockSummary()
    {
        if (_blockSummary == null)
            return;

        _blockSummary.Clear();

        Label totalLabel = new Label($"Total Blocks: {GetCountedBlockCount()}");
        totalLabel.AddToClassList("block-summary-total");
        _blockSummary.Add(totalLabel);

        Dictionary<TypeBlockColor, int> colorCounts = GetBlockColorCounts();
        bool hasColor = false;
        VisualElement colorGrid = new VisualElement();
        colorGrid.AddToClassList("block-summary-grid");

        foreach (TypeBlockColor blockColor in Enum.GetValues(typeof(TypeBlockColor)))
        {
            colorCounts.TryGetValue(blockColor, out int count);
            if (count <= 0)
                continue;

            hasColor = true;
            VisualElement row = new VisualElement();
            row.AddToClassList("block-summary-row");

            VisualElement swatch = new VisualElement();
            swatch.AddToClassList("block-summary-swatch");
            swatch.style.backgroundColor = GetMaterialColor(GetMaterial(blockColor), GetFallbackColor(blockColor));
            row.Add(swatch);
            row.Add(new Label($"{blockColor}: {count}"));
            colorGrid.Add(row);
        }

        if (!hasColor)
            _blockSummary.Add(new Label("No blocks on grid."));
        else
            _blockSummary.Add(colorGrid);
    }

    /// <summary>
    /// Đếm số block chơi được theo từng màu trong danh sách đang đặt.
    /// </summary>
    private Dictionary<TypeBlockColor, int> GetBlockColorCounts()
    {
        Dictionary<TypeBlockColor, int> colorCounts = new Dictionary<TypeBlockColor, int>();

        foreach (LevelBlockData blockData in _placedBlocks)
        {
            if (!IsCountedBlock(blockData))
                continue;

            TypeBlockColor typeBlockColor = blockData.TypeBlockColor;
            colorCounts.TryGetValue(typeBlockColor, out int currentCount);
            colorCounts[typeBlockColor] = currentCount + 1;
        }

        return colorCounts;
    }

    /// <summary>
    /// Đếm tổng số block chơi được, bỏ qua tile/tường/góc/vật cản môi trường.
    /// </summary>
    private int GetCountedBlockCount()
    {
        int count = 0;
        foreach (LevelBlockData blockData in _placedBlocks)
        {
            if (IsCountedBlock(blockData))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Tạo chuỗi log tóm tắt số lượng block để in khi lưu level.
    /// </summary>
    private string BuildBlockSummaryLog()
    {
        Dictionary<TypeBlockColor, int> colorCounts = GetBlockColorCounts();
        List<string> colorSummary = new List<string>();

        foreach (TypeBlockColor blockColor in Enum.GetValues(typeof(TypeBlockColor)))
        {
            colorCounts.TryGetValue(blockColor, out int count);
            if (count > 0)
                colorSummary.Add($"{blockColor}: {count}");
        }

        string colors = colorSummary.Count > 0 ? string.Join(", ", colorSummary) : "No colors";
        return $"Total Blocks: {GetCountedBlockCount()}, {colors}";
    }

    /// <summary>
    /// Xác định trạng thái drag khi block prefab đang được kéo trên grid.
    /// </summary>
    private void OnGridDragUpdated(DragUpdatedEvent evt)
    {
        LevelBlockDragPayload payload = GetDraggedBlockPayload();
        DragAndDrop.visualMode = payload != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
        evt.StopPropagation();
    }

    /// <summary>
    /// Thả block vào grid và thêm block nếu vị trí hợp lệ.
    /// </summary>
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

    /// <summary>
    /// Xử lý click trên grid để chọn, kéo, đặt môi trường hoặc bỏ chọn.
    /// </summary>
    private void OnGridPointerDown(PointerDownEvent evt)
    {
        _gridPreview.Focus();

        Vector2Int gridPosition = ToGridPosition(_gridPreview.WorldToLocal(evt.position));
        _selectedGridPosition = gridPosition;
        _hasSelectedGridPosition = IsInsideGrid(gridPosition);

        if (_selectedEnvironmentTool != EnvironmentTool.None)
        {
            int existingToolIndex = FindEnvironmentIndexAt(gridPosition,
                GetEnvironmentToolMatch(_selectedEnvironmentTool));
            if (existingToolIndex >= 0)
                SelectBlockForDrag(existingToolIndex, gridPosition, evt.pointerId);
            else
                PlaceOrSelectEnvironmentAt(gridPosition);

            evt.StopPropagation();
            return;
        }

        int blockIndex = FindPlacedBlockIndexAt(gridPosition);
        if (blockIndex < 0)
            blockIndex = FindPlacedBlockIndexAt(gridPosition, -1, true);

        SelectBlockForDrag(blockIndex, gridPosition, evt.pointerId);

        RebuildGridPreview();
        evt.StopPropagation();
    }

    /// <summary>
    /// Chọn block dưới chuột và chuẩn bị offset để kéo block trên grid.
    /// </summary>
    private void SelectBlockForDrag(int blockIndex, Vector2Int gridPosition, int pointerId)
    {
        _selectedBlockIndex = blockIndex;
        _draggingBlockIndex = blockIndex;

        if (blockIndex < 0)
            return;

        _dragCellOffset = gridPosition - _placedBlocks[blockIndex].GridPosition;
        _gridPreview.CapturePointer(pointerId);
    }

    /// <summary>
    /// Cập nhật vị trí block đang kéo khi pointer di chuyển trên grid.
    /// </summary>
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
        RemoveTileIfBlockedByEnvironment(movedBlock, _draggingBlockIndex);
        AutoUpdateWallsAndCorners();
        AutoCreateTilesInsideClosedFrames();
        RebuildGridPreview();
        evt.StopPropagation();
    }

    /// <summary>
    /// Kết thúc thao tác kéo block và nhả pointer capture.
    /// </summary>
    private void OnGridPointerUp(PointerUpEvent evt)
    {
        _draggingBlockIndex = -1;
        if (_gridPreview.HasPointerCapture(evt.pointerId))
            _gridPreview.ReleasePointer(evt.pointerId);

        evt.StopPropagation();
    }

    /// <summary>
    /// Xử lý phím tắt trên grid như xoay block hoặc xóa block đang chọn.
    /// </summary>
    private void OnGridKeyDown(KeyDownEvent evt)
    {
        if (_selectedBlockIndex < 0 || _selectedBlockIndex >= _placedBlocks.Count)
            return;

        if (evt.keyCode == KeyCode.D)
        {
            _placedBlocks.RemoveAt(_selectedBlockIndex);
            _selectedBlockIndex = -1;
            _draggingBlockIndex = -1;
            AutoUpdateWallsAndCorners();
            AutoCreateTilesInsideClosedFrames();
            RebuildGridPreview();
            evt.StopPropagation();
            return;
        }

        if (evt.keyCode == KeyCode.E)
        {
            LevelBlockData currentBlock = _placedBlocks[_selectedBlockIndex];
            LevelBlockData rotatedBlock = CloneBlock(currentBlock, currentBlock.GridPosition,
                currentBlock.Rotation + 1);
            if (!CanPlaceBlock(rotatedBlock, _selectedBlockIndex))
            {
                Debug.LogWarning(
                    $"Cannot rotate '{currentBlock.Prefab.name}' at {currentBlock.GridPosition}. Rotation would go outside the grid or overlap another object.");
                evt.StopPropagation();
                return;
            }

            _placedBlocks[_selectedBlockIndex] = rotatedBlock;
            RebuildGridPreview();
            evt.StopPropagation();
            return;
        }

        return;
    }

    /// <summary>
    /// Tạo LevelBlockData từ payload kéo thả và thêm vào grid nếu có thể đặt.
    /// </summary>
    private bool TryAddBlock(LevelBlockDragPayload payload, Vector2Int gridPosition)
    {
        LevelBlockData newBlock =
            new LevelBlockData(payload.Prefab, gridPosition, 0, payload.TypeBlockColor, payload.Material);
        if (!CanPlaceBlock(newBlock, -1))
        {
            Debug.LogWarning(
                $"Cannot place block '{payload.Prefab.name}' at {gridPosition}. Block is outside the grid or overlaps another block.");
            return false;
        }

        _placedBlocks.Add(newBlock);
        _selectedBlockIndex = _placedBlocks.Count - 1;
        RebuildGridPreview();
        return true;
    }

    /// <summary>
    /// Kiểm tra một block có thể đặt vào grid mà không vượt biên hoặc đè block khác hay không.
    /// </summary>
    private bool CanPlaceBlock(LevelBlockData blockData, int ignoreIndex)
    {
        if (blockData == null || blockData.Prefab == null)
            return false;

        BlockBehavior block = GetBlockBehavior(blockData.Prefab);
        if (block == null)
            return CanPlaceSingleCellBlock(blockData, ignoreIndex);

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
                if (cellPosition.x < 0 || cellPosition.x >= gridWidth || cellPosition.y < 0 ||
                    cellPosition.y >= gridHeight)
                    return false;

                if (!IsTileBlock(blockData) && IsGridCellOccupied(cellPosition.x, cellPosition.y, ignoreIndex))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Kiểm tra điều kiện đặt cho block môi trường một ô.
    /// </summary>
    private bool CanPlaceSingleCellBlock(LevelBlockData blockData, int ignoreIndex)
    {
        if (!IsInsideGrid(blockData.GridPosition))
            return false;

        return IsTileBlock(blockData) || !IsGridCellOccupied(blockData.GridPosition.x, blockData.GridPosition.y, ignoreIndex);
    }

    /// <summary>
    /// Tính màu hiển thị của một ô grid dựa trên block đang chiếm ô đó.
    /// </summary>
    private Color GetGridCellColor(int x, int y)
    {
        Vector2Int gridPosition = new Vector2Int(x, y);
        int blockIndex = FindPlacedBlockIndexAt(gridPosition);
        if (blockIndex < 0)
            blockIndex = FindPlacedBlockIndexAt(gridPosition, -1, true);

        if (blockIndex < 0)
            return new Color(0.18f, 0.2f, 0.22f);

        LevelBlockData blockData = _placedBlocks[blockIndex];
        if (IsWallBlock(blockData))
            return new Color(0.25f, 0.27f, 0.3f);

        if (IsCornerBlock(blockData))
            return new Color(0.35f, 0.37f, 0.4f);

        if (IsObstacleBlock(blockData))
            return new Color(0.46f, 0.34f, 0.2f);

        if (IsTileBlock(blockData))
            return new Color(0.23f, 0.28f, 0.26f);

        return GetMaterialColor(blockData.Material, GetFallbackColor(blockData.TypeBlockColor));
    }

    /// <summary>
    /// Kiểm tra một ô grid có đang bị block chiếm hay không.
    /// </summary>
    private bool IsGridCellOccupied(int gridX, int gridY, int ignoreIndex)
    {
        return FindPlacedBlockIndexAt(new Vector2Int(gridX, gridY), ignoreIndex) >= 0;
    }

    /// <summary>
    /// Tìm index block đang nằm tại ô grid, có thể bỏ qua một block hoặc tile.
    /// </summary>
    private int FindPlacedBlockIndexAt(Vector2Int gridPosition, int ignoreIndex = -1, bool includeTiles = false)
    {
        for (int i = _placedBlocks.Count - 1; i >= 0; i--)
        {
            if (i == ignoreIndex)
                continue;

            LevelBlockData placedBlock = _placedBlocks[i];
            if (!includeTiles && IsTileBlock(placedBlock))
                continue;

            BlockBehavior block = GetBlockBehavior(placedBlock.Prefab);
            if (block == null)
            {
                if (placedBlock.GridPosition == gridPosition)
                    return i;

                continue;
            }

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

    /// <summary>
    /// Tạo bản sao LevelBlockData với vị trí và rotation mới.
    /// </summary>
    private static LevelBlockData CloneBlock(LevelBlockData source, Vector2Int gridPosition, int rotation)
    {
        return new LevelBlockData(source.Prefab, gridPosition, rotation, source.TypeBlockColor, source.Material,
            source.IsEnvironment);
    }

    /// <summary>
    /// Lấy BlockBehavior từ prefab hoặc con của prefab.
    /// </summary>
    private static BlockBehavior GetBlockBehavior(GameObject prefab)
    {
        return prefab != null ? prefab.GetComponentInChildren<BlockBehavior>() : null;
    }

    /// <summary>
    /// Chuyển tọa độ cell local của block sau xoay sang tọa độ cell trong level grid.
    /// </summary>
    private static Vector2Int ToLevelCell(Vector2Int gridPosition, BlockBehavior block, int blockX, int blockY,
        int rotation)
    {
        Vector2Int pivot = GetRotatedPivot(block, rotation);
        return new Vector2Int(gridPosition.x + blockX - pivot.x, gridPosition.y + blockY - pivot.y);
    }

    /// <summary>
    /// Chuyển vị trí chuột trong grid preview thành tọa độ ô grid.
    /// </summary>
    private static Vector2Int ToGridPosition(Vector2 localMousePosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(localMousePosition.x / GridCellSize),
            Mathf.FloorToInt(localMousePosition.y / GridCellSize));
    }

    /// <summary>
    /// Tính kích thước block sau khi áp rotation theo bước 90 độ.
    /// </summary>
    private static Vector2Int GetRotatedSize(BlockBehavior block, int rotation)
    {
        return NormalizeRotation(rotation) % 2 == 0
            ? new Vector2Int(block.Width, block.Height)
            : new Vector2Int(block.Height, block.Width);
    }

    /// <summary>
    /// Tính pivot mới của block sau khi xoay.
    /// </summary>
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

    /// <summary>
    /// Kiểm tra một cell của block sau xoay có occupied hay không.
    /// </summary>
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

    /// <summary>
    /// Chuẩn hóa rotation về khoảng 0..3.
    /// </summary>
    private static int NormalizeRotation(int rotation)
    {
        return ((rotation % 4) + 4) % 4;
    }

    /// <summary>
    /// Lấy màu từ material hoặc trả fallback nếu material không hợp lệ.
    /// </summary>
    private static Color GetMaterialColor(Material material, Color fallback)
    {
        if (material == null)
            return fallback;

        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");

        return material.HasProperty("_Color") ? material.GetColor("_Color") : fallback;
    }

    /// <summary>
    /// Trả về màu preview mặc định tương ứng với BlockColor.
    /// </summary>
    private static Color GetFallbackColor(TypeBlockColor typeBlockColor)
    {
        switch (typeBlockColor)
        {
            case TypeBlockColor.Yellow:
                return new Color(1f, 0.86f, 0.12f);
            case TypeBlockColor.Blue:
                return new Color(0.12f, 0.42f, 1f);
            case TypeBlockColor.Pink:
                return new Color(1f, 0.25f, 0.68f);
            default:
                return new Color(0.9f, 0.22f, 0.18f);
        }
    }

    /// <summary>
    /// Dựng lại preview 3D trong scene từ các block đang đặt trong editor.
    /// </summary>
    private void RebuildRuntimePreview()
    {
        ClearRuntimePreview(true);

        if (_placedBlocks.Count == 0)
            return;

        _runtimePreviewRoot = new GameObject(RuntimePreviewRootName);
        _runtimePreviewRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        RefreshEnvironmentRuntimeCenter(_placedBlocks);
        HashSet<int> groupedWallIndices = CreateWallSegmentChildren(_runtimePreviewRoot.transform, true);
        CreateCornerBridgeWallChildren(_runtimePreviewRoot.transform, true);

        for (int i = 0; i < _placedBlocks.Count; i++)
        {
            if (groupedWallIndices.Contains(i))
                continue;

            CreateRuntimePreviewBlock(_placedBlocks[i]);
        }

        _hasEnvironmentRuntimeCenter = false;
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Tạo object preview 3D cho một block đơn lẻ trong scene.
    /// </summary>
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
        previewObject.transform.rotation = Quaternion.Euler(0f, NormalizeRotation(blockData.Rotation) * 90f, 0f);

        ApplyRuntimePreviewMaterial(previewObject, blockData.Material);
    }

    /// <summary>
    /// Gộp các đoạn wall thẳng thành object con dài hơn và trả về index wall đã được xử lý.
    /// </summary>
    private HashSet<int> CreateWallSegmentChildren(Transform parent, bool preview)
    {
        HashSet<int> groupedWallIndices = new HashSet<int>();
        GameObject wallPrefab = GetWallPrefab();
        if (wallPrefab == null)
            return groupedWallIndices;

        for (int i = 0; i < _placedBlocks.Count; i++)
        {
            if (groupedWallIndices.Contains(i))
                continue;

            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !IsWallBlock(blockData))
                continue;

            int rotation = NormalizeRotation(blockData.Rotation);
            Vector2Int direction = rotation == 1 ? Vector2Int.right : Vector2Int.up;
            if (HasWallEnvironmentAt(blockData.GridPosition - direction))
                continue;

            List<int> segmentIndices = GetWallSegmentIndices(blockData.GridPosition, direction);
            if (segmentIndices.Count < 2 && !HasWallSegmentCornerConnection(segmentIndices, direction))
                continue;

            foreach (int segmentIndex in segmentIndices)
                groupedWallIndices.Add(segmentIndex);

            CreateWallSegmentChild(parent, wallPrefab, segmentIndices, direction, rotation, preview);
        }

        return groupedWallIndices;
    }

    /// <summary>
    /// Kiểm tra đoạn wall có nối với corner ở đầu hoặc cuối theo hướng đang xét hay không.
    /// </summary>
    // Tao wall ngan giua hai corner ke nhau khi it nhat mot corner nam canh tile.
    private void CreateCornerBridgeWallChildren(Transform parent, bool preview)
    {
        GameObject wallPrefab = GetWallPrefab();
        if (wallPrefab == null)
            return;

        for (int i = 0; i < _placedBlocks.Count; i++)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !IsCornerBlock(blockData))
                continue;

            TryCreateCornerBridgeWallChild(parent, wallPrefab, blockData.GridPosition, Vector2Int.right, 1, preview);
            TryCreateCornerBridgeWallChild(parent, wallPrefab, blockData.GridPosition, Vector2Int.up, 0, preview);
        }
    }

    /// <summary>
    /// Tao mot wall bridge neu corner tiep theo cung huong va co tile o mot phia.
    /// </summary>
    private void TryCreateCornerBridgeWallChild(
        Transform parent,
        GameObject wallPrefab,
        Vector2Int start,
        Vector2Int direction,
        int rotation,
        bool preview)
    {
        Vector2Int end = start + direction;
        if (!TryGetCornerBlockAt(start, out LevelBlockData startCorner) ||
            !TryGetCornerBlockAt(end, out LevelBlockData endCorner))
            return;

        if (!TryGetCornerBridgeTileDirection(start, end, direction, out Vector2Int tileDirection))
            return;

        Vector3 worldDirection = ToRuntimeDirection(direction);
        Vector3 startPosition = ToRuntimeWorldPosition(startCorner);
        Vector3 endPosition = ToRuntimeWorldPosition(endCorner);
        Vector3 centerPosition = (startPosition + endPosition) * 0.5f;
        float length = Mathf.Max(0.1f,
            Mathf.Abs(Vector3.Dot(endPosition - startPosition, worldDirection)) -
            CornerRuntimeInset * 2f +
            WallCornerOverlap * 2f);

        GameObject wallObject = PrefabUtility.InstantiatePrefab(wallPrefab) as GameObject;
        if (wallObject == null)
            wallObject = Instantiate(wallPrefab);

        wallObject.name = preview ? $"Preview_{wallPrefab.name}_CornerBridge" : $"{wallPrefab.name}_CornerBridge";
        if (preview)
            SetRuntimePreviewHideFlags(wallObject);

        wallObject.transform.SetParent(parent, false);
        wallObject.transform.position = centerPosition;
        wallObject.transform.rotation = Quaternion.Euler(0f, rotation * 90f, 0f);

        Vector3 scale = wallObject.transform.localScale;
        scale.z *= length;
        wallObject.transform.localScale = scale;
    }

    /// <summary>
    /// Xac dinh phia tile cua cap corner ke nhau.
    /// </summary>
    private bool TryGetCornerBridgeTileDirection(
        Vector2Int start,
        Vector2Int end,
        Vector2Int direction,
        out Vector2Int tileDirection)
    {
        Vector2Int perpendicular = new Vector2Int(-direction.y, direction.x);
        if (TryGetCornerTileDirection(start, perpendicular, -perpendicular, out tileDirection) ||
            TryGetCornerTileDirection(end, perpendicular, -perpendicular, out tileDirection))
            return true;

        tileDirection = GetNearestTileDirection(start, false);
        if (tileDirection != Vector2Int.zero)
            return true;

        tileDirection = GetNearestTileDirection(end, false);
        if (tileDirection != Vector2Int.zero)
            return true;

        tileDirection = Vector2Int.zero;
        return false;
    }

    /// <summary>
    /// Uu tien tile nam dung phia khe ho giua cap corner.
    /// </summary>
    private bool TryGetCornerTileDirection(
        Vector2Int cornerPosition,
        Vector2Int firstDirection,
        Vector2Int secondDirection,
        out Vector2Int tileDirection)
    {
        if (HasTileEnvironmentAt(cornerPosition + firstDirection))
        {
            tileDirection = firstDirection;
            return true;
        }

        if (HasTileEnvironmentAt(cornerPosition + secondDirection))
        {
            tileDirection = secondDirection;
            return true;
        }

        tileDirection = Vector2Int.zero;
        return false;
    }

    private bool HasWallSegmentCornerConnection(List<int> segmentIndices, Vector2Int direction)
    {
        if (segmentIndices.Count == 0)
            return false;

        Vector2Int start = _placedBlocks[segmentIndices[0]].GridPosition;
        Vector2Int end = _placedBlocks[segmentIndices[segmentIndices.Count - 1]].GridPosition;
        return TryGetWallSegmentCorner(start, direction, true, out _, out _) ||
               TryGetWallSegmentCorner(end, direction, false, out _, out _);
    }

    /// <summary>
    /// Thu thập các index wall liên tiếp tạo thành một segment từ vị trí bắt đầu.
    /// </summary>
    private List<int> GetWallSegmentIndices(Vector2Int startPosition, Vector2Int direction)
    {
        List<int> segmentIndices = new List<int>();
        Vector2Int currentPosition = startPosition;

        while (true)
        {
            int wallIndex = FindEnvironmentIndexAt(currentPosition, IsWallBlock);
            if (wallIndex < 0)
                break;

            segmentIndices.Add(wallIndex);
            currentPosition += direction;
        }

        return segmentIndices;
    }

    /// <summary>
    /// Tạo object wall segment đã scale/đặt vị trí phù hợp cho preview hoặc prefab lưu ra.
    /// </summary>
    private void CreateWallSegmentChild(
        Transform parent,
        GameObject wallPrefab,
        List<int> segmentIndices,
        Vector2Int direction,
        int rotation,
        bool preview)
    {
        LevelBlockData firstBlock = _placedBlocks[segmentIndices[0]];
        LevelBlockData lastBlock = _placedBlocks[segmentIndices[segmentIndices.Count - 1]];
        Vector2Int start = firstBlock.GridPosition;
        Vector2Int end = lastBlock.GridPosition;

        Vector3 worldDirection = new Vector3(direction.x, 0f, -direction.y);
        Vector3 startCenterPosition = ToRuntimeWorldPosition(firstBlock);
        Vector3 endCenterPosition = ToRuntimeWorldPosition(lastBlock);
        Vector3 spanStartPosition = startCenterPosition - worldDirection * 0.5f;
        Vector3 spanEndPosition = endCenterPosition + worldDirection * 0.5f;

        bool startCornerNeedsOverlap =
            TryGetWallSegmentCorner(start, direction, true, out LevelBlockData startCorner, out bool startNeedsOverlap);
        if (startCornerNeedsOverlap)
            spanStartPosition =
                ProjectOntoWallSegment(startCenterPosition, ToRuntimeWorldPosition(startCorner), worldDirection);

        bool endCornerNeedsOverlap =
            TryGetWallSegmentCorner(end, direction, false, out LevelBlockData endCorner, out bool endNeedsOverlap);
        if (endCornerNeedsOverlap)
            spanEndPosition =
                ProjectOntoWallSegment(endCenterPosition, ToRuntimeWorldPosition(endCorner), worldDirection);

        if (startNeedsOverlap)
            spanStartPosition -= worldDirection * WallCornerOverlap;
        if (endNeedsOverlap)
            spanEndPosition += worldDirection * WallCornerOverlap;

        Vector3 centerPosition = (spanStartPosition + spanEndPosition) * 0.5f;
        float length = Mathf.Max(0.1f, Vector3.Distance(spanStartPosition, spanEndPosition));

        GameObject wallObject = PrefabUtility.InstantiatePrefab(wallPrefab) as GameObject;
        if (wallObject == null)
            wallObject = Instantiate(wallPrefab);

        wallObject.name = preview ? $"Preview_{wallPrefab.name}_Segment" : $"{wallPrefab.name}_Segment";
        if (preview)
            SetRuntimePreviewHideFlags(wallObject);

        wallObject.transform.SetParent(parent, false);
        wallObject.transform.position = centerPosition;
        wallObject.transform.rotation = Quaternion.Euler(0f, rotation * 90f, 0f);

        Vector3 scale = wallObject.transform.localScale;
        scale.z *= length;
        wallObject.transform.localScale = scale;

        ApplyRuntimePreviewMaterial(wallObject, firstBlock.Material);
    }

    /// <summary>
    /// Chieu vi tri corner len truc wall segment de tinh diem noi va do dai segment.
    /// </summary>
    private static Vector3 ProjectOntoWallSegment(Vector3 wallCenterPosition, Vector3 cornerPosition, Vector3 worldDirection)
    {
        return wallCenterPosition + worldDirection * Vector3.Dot(cornerPosition - wallCenterPosition, worldDirection);
    }

    /// <summary>
    /// Tìm corner nối với một đầu của wall segment và trả về dữ liệu corner nếu có.
    /// </summary>
    private bool TryGetWallSegmentCorner(
        Vector2Int segmentEnd,
        Vector2Int direction,
        bool startSide,
        out LevelBlockData cornerBlock,
        out bool needsOverlap)
    {
        Vector2Int sideDirection = startSide ? -direction : direction;
        Vector2Int perpendicular = new Vector2Int(-direction.y, direction.x);

        if (TryGetCornerBlockAt(segmentEnd + sideDirection, out cornerBlock))
        {
            needsOverlap = false;
            return true;
        }

        if (TryGetCornerBlockAt(segmentEnd + sideDirection + perpendicular, out cornerBlock))
        {
            needsOverlap = true;
            return true;
        }

        if (TryGetCornerBlockAt(segmentEnd + sideDirection - perpendicular, out cornerBlock))
        {
            needsOverlap = true;
            return true;
        }

        needsOverlap = false;
        return false;
    }

    /// <summary>
    /// Cập nhật material của các block đã đặt khi material của một màu thay đổi.
    /// </summary>
    private void RefreshPlacedBlockMaterials(TypeBlockColor typeBlockColor)
    {
        Material material = GetMaterial(typeBlockColor);

        for (int i = 0; i < _placedBlocks.Count; i++)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (!IsCountedBlock(blockData) || blockData.TypeBlockColor != typeBlockColor)
                continue;

            _placedBlocks[i] = new LevelBlockData(blockData.Prefab, blockData.GridPosition, blockData.Rotation,
                blockData.TypeBlockColor, material, blockData.IsEnvironment);
        }
    }

    /// <summary>
    /// Chuyển tọa độ grid của block sang vị trí world dùng cho runtime/prefab.
    /// </summary>
    private Vector3 ToRuntimeWorldPosition(LevelBlockData blockData)
    {
        Vector3 position = new Vector3(blockData.GridPosition.x, 0f, -blockData.GridPosition.y);

        if (IsWallBlock(blockData))
            return position + GetWallRuntimeInset(blockData.GridPosition, blockData.Rotation);

        if (IsCornerBlock(blockData))
            return position + GetCornerRuntimeInset(blockData.GridPosition);

        return position;
    }

    /// <summary>
    /// Tính tâm vùng môi trường để căn preview runtime quanh origin.
    /// </summary>
    private static void RefreshEnvironmentRuntimeCenter(IEnumerable<LevelBlockData> blocks)
    {
        int count = 0;
        Vector2 sum = Vector2.zero;

        foreach (LevelBlockData blockData in blocks)
        {
            if (blockData == null || (!IsWallBlock(blockData) && !IsCornerBlock(blockData)))
                continue;

            sum += new Vector2(blockData.GridPosition.x, blockData.GridPosition.y);
            count++;
        }

        _hasEnvironmentRuntimeCenter = count > 0;
        _environmentRuntimeCenter = _hasEnvironmentRuntimeCenter ? sum / count : Vector2.zero;
    }

    /// <summary>
    /// Tính offset nhỏ cho wall để canh đúng với tile/corner trong runtime.
    /// </summary>
    private Vector3 GetWallRuntimeInset(Vector2Int gridPosition, int rotation)
    {
        bool hasRelevantTile;
        Vector2Int tileDirection = GetWallTileDirection(gridPosition, rotation, out hasRelevantTile);
        if (tileDirection != Vector2Int.zero)
            return ToRuntimeDirection(tileDirection) * WallRuntimeInset;

        if (hasRelevantTile)
            return Vector3.zero;

        if (!_hasEnvironmentRuntimeCenter)
            return Vector3.zero;

        return GetCenterFallbackInset(gridPosition, WallRuntimeInset);
    }

    /// <summary>
    /// Tính offset nhỏ cho corner dựa trên tile gần nhất hoặc hướng về tâm.
    /// </summary>
    private Vector3 GetCornerRuntimeInset(Vector2Int gridPosition)
    {
        Vector2Int tileDirection = GetNearestTileDirection(gridPosition, true);
        if (tileDirection != Vector2Int.zero)
            return ToRuntimeDirection(tileDirection) * CornerRuntimeInset;

        if (!_hasEnvironmentRuntimeCenter)
            return Vector3.zero;

        return GetCenterFallbackInset(gridPosition, CornerRuntimeInset);
    }

    /// <summary>
    /// Xác định hướng tile liên quan tới wall để biết wall nên dịch vào phía nào.
    /// </summary>
    private Vector2Int GetWallTileDirection(Vector2Int gridPosition, int rotation, out bool hasRelevantTile)
    {
        Vector2Int direction = Vector2Int.zero;
        Vector2Int firstOffset = NormalizeRotation(rotation) == 1 ? Vector2Int.up : Vector2Int.right;
        Vector2Int secondOffset = -firstOffset;

        hasRelevantTile = false;
        AddTileDirection(ref direction, ref hasRelevantTile, gridPosition, firstOffset);
        AddTileDirection(ref direction, ref hasRelevantTile, gridPosition, secondOffset);
        return direction;
    }

    /// <summary>
    /// Tìm hướng từ ô môi trường tới tile gần nhất, có thể xét cả đường chéo.
    /// </summary>
    private Vector2Int GetNearestTileDirection(Vector2Int gridPosition, bool includeDiagonal)
    {
        Vector2Int direction = Vector2Int.zero;

        AddTileDirection(ref direction, gridPosition, Vector2Int.right);
        AddTileDirection(ref direction, gridPosition, Vector2Int.left);
        AddTileDirection(ref direction, gridPosition, Vector2Int.up);
        AddTileDirection(ref direction, gridPosition, Vector2Int.down);

        if (direction != Vector2Int.zero || !includeDiagonal)
            return direction;

        AddTileDirection(ref direction, gridPosition, Vector2Int.right + Vector2Int.up);
        AddTileDirection(ref direction, gridPosition, Vector2Int.right + Vector2Int.down);
        AddTileDirection(ref direction, gridPosition, Vector2Int.left + Vector2Int.up);
        AddTileDirection(ref direction, gridPosition, Vector2Int.left + Vector2Int.down);
        return direction;
    }

    /// <summary>
    /// Cộng hướng offset vào vector tổng nếu tại offset đó có tile.
    /// </summary>
    private void AddTileDirection(ref Vector2Int direction, Vector2Int gridPosition, Vector2Int offset)
    {
        if (HasTileEnvironmentAt(gridPosition + offset))
            direction += offset;
    }

    /// <summary>
    /// Cộng hướng offset vào vector tổng theo tùy chọn có xét đường chéo hay không.
    /// </summary>
    private void AddTileDirection(
        ref Vector2Int direction,
        ref bool hasTile,
        Vector2Int gridPosition,
        Vector2Int offset)
    {
        if (!HasTileEnvironmentAt(gridPosition + offset))
            return;

        direction += offset;
        hasTile = true;
    }

    /// <summary>
    /// Kiểm tra vị trí grid có tile môi trường hay không.
    /// </summary>
    private bool HasTileEnvironmentAt(Vector2Int gridPosition)
    {
        for (int i = _placedBlocks.Count - 1; i >= 0; i--)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !blockData.IsEnvironment)
                continue;

            if (blockData.GridPosition == gridPosition && IsTileBlock(blockData))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Chuyển hướng grid 2D thành hướng Vector3 trong world runtime.
    /// </summary>
    private static Vector3 ToRuntimeDirection(Vector2Int direction)
    {
        float x = Mathf.Clamp(direction.x, -1, 1);
        float y = Mathf.Clamp(direction.y, -1, 1);
        return new Vector3(x, 0f, -y);
    }

    /// <summary>
    /// Tính offset hướng về tâm môi trường khi không tìm được tile tham chiếu.
    /// </summary>
    private static Vector3 GetCenterFallbackInset(Vector2Int gridPosition, float inset)
    {
        Vector2 toCenter = _environmentRuntimeCenter - new Vector2(gridPosition.x, gridPosition.y);
        Vector2 normalized = toCenter.normalized;
        return new Vector3(normalized.x * inset, 0f, -normalized.y * inset);
    }

    /// <summary>
    /// Áp material preview cho toàn bộ MeshRenderer trong object và con của nó.
    /// </summary>
    private static void ApplyRuntimePreviewMaterial(GameObject previewObject, Material material)
    {
        if (material == null)
            return;

        Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
            renderer.sharedMaterial = material;
    }

    /// <summary>
    /// Gắn HideFlags cho preview object để không lưu vào scene.
    /// </summary>
    private static void SetRuntimePreviewHideFlags(GameObject previewObject)
    {
        HideFlags hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        previewObject.hideFlags = hideFlags;

        Transform[] children = previewObject.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
            child.gameObject.hideFlags = hideFlags;
    }

    /// <summary>
    /// Xóa root preview hiện tại và tùy chọn xóa cả preview orphan còn sót trong scene.
    /// </summary>
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

    /// <summary>
    /// Đọc payload block đang được kéo từ DragAndDrop hoặc fallback từ object reference.
    /// </summary>
    private static LevelBlockDragPayload GetDraggedBlockPayload()
    {
        if (DragAndDrop.GetGenericData(BlockDragKey) is LevelBlockDragPayload payload &&
            GetBlockBehavior(payload.Prefab) != null)
            return payload;

        foreach (Object objectReference in DragAndDrop.objectReferences)
        {
            if (objectReference is GameObject gameObject && GetBlockBehavior(gameObject) != null)
                return new LevelBlockDragPayload(gameObject, TypeBlockColor.Red, GetMaterial(TypeBlockColor.Red));
        }

        return null;
    }

    /// <summary>
    /// Bắt đầu thao tác kéo block prefab từ popup vào grid editor.
    /// </summary>
    public static void StartBlockDrag(GameObject prefab, TypeBlockColor typeBlockColor, Material material)
    {
        LevelBlockDragPayload payload = new LevelBlockDragPayload(prefab, typeBlockColor, material);
        DragAndDrop.PrepareStartDrag();
        DragAndDrop.objectReferences = new Object[] { prefab };
        DragAndDrop.SetGenericData(BlockDragKey, payload);
        DragAndDrop.StartDrag(prefab.name);
    }

    private sealed class LevelBlockDragPayload
    {
        // Prefab block dang duoc keo tu picker vao grid.
        public readonly GameObject Prefab;
        // Mau logic se gan cho block khi tha vao grid.
        public readonly TypeBlockColor TypeBlockColor;
        // Material tuong ung voi mau dang chon, dung cho preview/runtime object.
        public readonly Material Material;

        /// <summary>
        /// Đóng gói prefab, màu và material để truyền qua DragAndDrop.
        /// </summary>
        public LevelBlockDragPayload(GameObject prefab, TypeBlockColor typeBlockColor, Material material)
        {
            Prefab = prefab;
            TypeBlockColor = typeBlockColor;
            Material = material;
        }
    }

    /// <summary>
    /// Chọn tool đặt tile môi trường.
    /// </summary>
    private void AddTile()
    {
        SelectEnvironmentTool(EnvironmentTool.Tile);
    }

    /// <summary>
    /// Chọn tool đặt vật cản môi trường.
    /// </summary>
    private void AddObstacle()
    {
        SelectEnvironmentTool(EnvironmentTool.Obstacle);
    }

    /// <summary>
    /// Chọn tool đặt corner môi trường.
    /// </summary>
    private void AddCorner()
    {
        SelectEnvironmentTool(EnvironmentTool.Corner);
    }

    /// <summary>
    /// Chọn tool đặt wall môi trường.
    /// </summary>
    private void AddWal()
    {
        SelectEnvironmentTool(EnvironmentTool.Wall);
    }

    /// <summary>
    /// Cập nhật tool môi trường đang chọn và reset trạng thái chọn/kéo block.
    /// </summary>
    private void SelectEnvironmentTool(EnvironmentTool tool)
    {
        _selectedEnvironmentTool = tool;
        _selectedBlockIndex = -1;
        _draggingBlockIndex = -1;
        Debug.Log($"Selected environment tool: {tool}. Click a grid cell to place or select it, press D to delete selected object.");
    }

    /// <summary>
    /// Đặt object môi trường tại ô grid hoặc chọn object cùng loại nếu đã tồn tại.
    /// </summary>
    private void PlaceOrSelectEnvironmentAt(Vector2Int gridPosition)
    {
        if (!IsInsideGrid(gridPosition))
            return;

        GameObject prefab = GetPrefabForEnvironmentTool(_selectedEnvironmentTool);
        if (prefab == null)
            return;

        Predicate<LevelBlockData> selectedToolMatch = GetEnvironmentToolMatch(_selectedEnvironmentTool);
        int existingSelectedIndex = FindEnvironmentIndexAt(gridPosition, selectedToolMatch);
        if (existingSelectedIndex >= 0)
        {
            _selectedBlockIndex = existingSelectedIndex;
            _draggingBlockIndex = -1;
            RebuildGridPreview();
            return;
        }

        if (_selectedEnvironmentTool != EnvironmentTool.Tile)
        {
            RemoveNonTileEnvironmentAt(gridPosition);
            RemoveTileEnvironmentAt(gridPosition);
        }

        LevelBlockData blockData = CreateEnvironmentBlock(prefab, gridPosition, 0);
        if (!CanPlaceBlock(blockData, -1))
        {
            Debug.LogWarning($"Cannot place {_selectedEnvironmentTool} at {gridPosition}. Cell is outside the grid or occupied.");
            return;
        }

        _placedBlocks.Add(blockData);
        _selectedBlockIndex = _placedBlocks.Count - 1;
        _draggingBlockIndex = -1;
        AutoUpdateWallsAndCorners();
        AutoCreateTilesInsideClosedFrames();
        RebuildGridPreview();
    }

    /// <summary>
    /// Xóa tile ở vị trí bị wall/corner/obstacle chiếm để tránh chồng môi trường.
    /// </summary>
    private void RemoveTileIfBlockedByEnvironment(LevelBlockData blockData, int ignoreIndex)
    {
        if (blockData == null || IsTileBlock(blockData))
            return;

        if (!IsWallBlock(blockData) && !IsCornerBlock(blockData) && !IsObstacleBlock(blockData))
            return;

        RemoveTileEnvironmentAt(blockData.GridPosition, ignoreIndex);
    }

    /// <summary>
    /// Lấy prefab tương ứng với tool môi trường đang chọn.
    /// </summary>
    private GameObject GetPrefabForEnvironmentTool(EnvironmentTool tool)
    {
        switch (tool)
        {
            case EnvironmentTool.Wall:
                return GetWallPrefab();
            case EnvironmentTool.Corner:
                return GetCornerPrefab();
            case EnvironmentTool.Obstacle:
                return GetObstaclePrefab();
            case EnvironmentTool.Tile:
                return GetTilePrefab();
            default:
                return null;
        }
    }

    /// <summary>
    /// Trả predicate dùng để nhận diện block môi trường theo tool.
    /// </summary>
    private static Predicate<LevelBlockData> GetEnvironmentToolMatch(EnvironmentTool tool)
    {
        switch (tool)
        {
            case EnvironmentTool.Wall:
                return IsWallBlock;
            case EnvironmentTool.Corner:
                return IsCornerBlock;
            case EnvironmentTool.Obstacle:
                return IsObstacleBlock;
            case EnvironmentTool.Tile:
                return IsTileBlock;
            default:
                return _ => false;
        }
    }

    /// <summary>
    /// Tạo lại nền môi trường mặc định gồm tile, border wall và corner, đồng thời giữ các block hợp lệ.
    /// </summary>
    private void EnsureDefaultMapEnvironment(int gridWidth, int gridHeight)
    {
        GameObject tilePrefab = GetTilePrefab();
        GameObject wallPrefab = GetWallPrefab();
        GameObject cornerPrefab = GetCornerPrefab();
        if (tilePrefab == null || wallPrefab == null || cornerPrefab == null)
            return;

        List<LevelBlockData> preservedBlocks = new List<LevelBlockData>();
        foreach (LevelBlockData blockData in _placedBlocks)
        {
            if (blockData == null || IsTileBlock(blockData) || IsWallBlock(blockData) || IsCornerBlock(blockData))
                continue;

            preservedBlocks.Add(blockData);
        }

        _placedBlocks.Clear();
        AddTiles(tilePrefab, gridWidth, gridHeight);
        AddBorderWalls(wallPrefab, gridWidth, gridHeight);
        AddCorners(cornerPrefab, gridWidth, gridHeight);

        foreach (LevelBlockData blockData in preservedBlocks)
        {
            if (CanPlaceBlock(blockData, -1))
            {
                _placedBlocks.Add(blockData);
                continue;
            }

            Debug.LogWarning(
                $"Skipped '{blockData.Prefab.name}' at {blockData.GridPosition} because it overlaps the generated border or is outside the grid.");
        }

        _selectedBlockIndex = -1;
        _draggingBlockIndex = -1;
        RebuildGridPreview();
    }

    /// <summary>
    /// Thêm tile phủ toàn bộ grid hiện tại.
    /// </summary>
    private void AddTiles(GameObject tilePrefab)
    {
        AddTiles(tilePrefab, Mathf.Clamp(_gridX.value, MinGridSize, MaxGridSize),
            Mathf.Clamp(_gridY.value, MinGridSize, MaxGridSize));
    }

    /// <summary>
    /// Thêm tile phủ toàn bộ vùng grid có kích thước truyền vào.
    /// </summary>
    private void AddTiles(GameObject tilePrefab, int gridWidth, int gridHeight)
    {
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
                _placedBlocks.Add(CreateEnvironmentBlock(tilePrefab, new Vector2Int(x, y), 0));
        }
    }

    /// <summary>
    /// Thêm wall viền quanh grid hiện tại.
    /// </summary>
    private void AddBorderWalls(GameObject wallPrefab)
    {
        AddBorderWalls(wallPrefab, Mathf.Clamp(_gridX.value, MinGridSize, MaxGridSize),
            Mathf.Clamp(_gridY.value, MinGridSize, MaxGridSize));
    }

    /// <summary>
    /// Thêm wall dọc theo bốn cạnh grid với rotation đúng hướng.
    /// </summary>
    private void AddBorderWalls(GameObject wallPrefab, int gridWidth, int gridHeight)
    {
        for (int x = 1; x < gridWidth - 1; x++)
        {
            _placedBlocks.Add(CreateEnvironmentBlock(wallPrefab, new Vector2Int(x, 0), 0));
            _placedBlocks.Add(CreateEnvironmentBlock(wallPrefab, new Vector2Int(x, gridHeight - 1), 2));
        }

        for (int y = 1; y < gridHeight - 1; y++)
        {
            _placedBlocks.Add(CreateEnvironmentBlock(wallPrefab, new Vector2Int(0, y), 3));
            _placedBlocks.Add(CreateEnvironmentBlock(wallPrefab, new Vector2Int(gridWidth - 1, y), 1));
        }
    }

    /// <summary>
    /// Thêm corner ở bốn góc của grid hiện tại.
    /// </summary>
    private void AddCorners(GameObject cornerPrefab)
    {
        AddCorners(cornerPrefab, Mathf.Clamp(_gridX.value, MinGridSize, MaxGridSize),
            Mathf.Clamp(_gridY.value, MinGridSize, MaxGridSize));
    }

    /// <summary>
    /// Thêm corner vào các góc hợp lệ của vùng grid truyền vào.
    /// </summary>
    private void AddCorners(GameObject cornerPrefab, int gridWidth, int gridHeight)
    {
        _placedBlocks.Add(CreateEnvironmentBlock(cornerPrefab, new Vector2Int(0, 0), 0));

        if (gridWidth > 1)
            _placedBlocks.Add(CreateEnvironmentBlock(cornerPrefab, new Vector2Int(gridWidth - 1, 0), 1));

        if (gridHeight > 1)
            _placedBlocks.Add(CreateEnvironmentBlock(cornerPrefab, new Vector2Int(0, gridHeight - 1), 3));

        if (gridWidth > 1 && gridHeight > 1)
            _placedBlocks.Add(CreateEnvironmentBlock(cornerPrefab, new Vector2Int(gridWidth - 1, gridHeight - 1), 2));
    }

    /// <summary>
    /// Thử thêm một block môi trường và rebuild preview nếu đặt thành công.
    /// </summary>
    private bool TryAddEnvironmentBlock(GameObject prefab, Vector2Int gridPosition, int rotation)
    {
        LevelBlockData blockData = CreateEnvironmentBlock(prefab, gridPosition, rotation);
        if (!CanPlaceBlock(blockData, -1))
            return false;

        _placedBlocks.Add(blockData);
        _selectedBlockIndex = _placedBlocks.Count - 1;
        AutoUpdateWallsAndCorners();
        AutoCreateTilesInsideClosedFrames();
        RebuildGridPreview();
        return true;
    }

    /// <summary>
    /// Tự xoay hoặc đổi wall thành corner dựa trên các wall/corner lân cận.
    /// </summary>
    private void AutoUpdateWallsAndCorners()
    {
        GameObject wallPrefab = GetWallPrefab();
        GameObject cornerPrefab = GetCornerPrefab();
        if (wallPrefab == null || cornerPrefab == null)
            return;

        for (int i = 0; i < _placedBlocks.Count; i++)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !blockData.IsEnvironment)
                continue;

            if (IsCornerBlock(blockData))
            {
                if (TryGetAutoCornerRotation(blockData.GridPosition, out int cornerRotation) &&
                    NormalizeRotation(blockData.Rotation) != cornerRotation)
                    _placedBlocks[i] = CreateEnvironmentBlock(cornerPrefab, blockData.GridPosition, cornerRotation);

                continue;
            }

            if (!IsWallBlock(blockData))
                continue;

            if (TryGetAutoCornerRotation(blockData.GridPosition, out int rotation))
            {
                _placedBlocks[i] = CreateEnvironmentBlock(cornerPrefab, blockData.GridPosition, rotation);
                continue;
            }

            if (!TryGetAutoWallRotation(blockData.GridPosition, out rotation))
                continue;

            if (NormalizeRotation(blockData.Rotation) != rotation)
                _placedBlocks[i] = CreateEnvironmentBlock(wallPrefab, blockData.GridPosition, rotation);
        }
    }

    /// <summary>
    /// Tu tao tile cho nhung o nam ben trong vung wall/corner khep kin.
    /// </summary>
    private void AutoCreateTilesInsideClosedFrames()
    {
        GameObject tilePrefab = GetTilePrefab();
        if (tilePrefab == null)
            return;

        int gridWidth = Mathf.Clamp(_gridX.value, MinGridSize, MaxGridSize);
        int gridHeight = Mathf.Clamp(_gridY.value, MinGridSize, MaxGridSize);
        if (gridWidth <= 2 || gridHeight <= 2)
        {
            RemoveAllAutoCreatedTiles();
            return;
        }

        bool[,] blockedByFrame = BuildWallFrameMask(gridWidth, gridHeight);
        bool[,] reachableFromOutside = BuildOutsideReachableMask(blockedByFrame, gridWidth, gridHeight);

        RemoveAutoCreatedTilesOutsideClosedFrames(blockedByFrame, reachableFromOutside, gridWidth, gridHeight);

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                Vector2Int gridPosition = new Vector2Int(x, y);
                if (blockedByFrame[x, y] || reachableFromOutside[x, y] || HasTileEnvironmentAt(gridPosition) ||
                    HasNonTileEnvironmentAt(gridPosition))
                    continue;

                _placedBlocks.Add(CreateEnvironmentBlock(tilePrefab, gridPosition, 0));
                _autoCreatedTilePositions.Add(gridPosition);
            }
        }
    }

    /// <summary>
    /// Xoa cac tile auto-fill khi khung da bi mo lai hoac o do bi object moi truong khac chiem.
    /// </summary>
    private void RemoveAutoCreatedTilesOutsideClosedFrames(
        bool[,] blockedByFrame,
        bool[,] reachableFromOutside,
        int gridWidth,
        int gridHeight)
    {
        for (int i = _placedBlocks.Count - 1; i >= 0; i--)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !IsTileBlock(blockData) ||
                !_autoCreatedTilePositions.Contains(blockData.GridPosition))
                continue;

            Vector2Int gridPosition = blockData.GridPosition;
            bool isInsideGrid = gridPosition.x >= 0 && gridPosition.x < gridWidth && gridPosition.y >= 0 &&
                                gridPosition.y < gridHeight;
            bool shouldKeep = isInsideGrid && !blockedByFrame[gridPosition.x, gridPosition.y] &&
                              !reachableFromOutside[gridPosition.x, gridPosition.y] &&
                              !HasNonTileEnvironmentAt(gridPosition);

            if (shouldKeep)
                continue;

            _placedBlocks.RemoveAt(i);
            _autoCreatedTilePositions.Remove(gridPosition);
            AdjustSelectionAfterRemove(i);
        }
    }

    /// <summary>
    /// Xoa tat ca tile auto-fill dang duoc ghi nho trong phien editor hien tai.
    /// </summary>
    private void RemoveAllAutoCreatedTiles()
    {
        for (int i = _placedBlocks.Count - 1; i >= 0; i--)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !IsTileBlock(blockData) ||
                !_autoCreatedTilePositions.Contains(blockData.GridPosition))
                continue;

            _placedBlocks.RemoveAt(i);
            AdjustSelectionAfterRemove(i);
        }

        _autoCreatedTilePositions.Clear();
    }

    /// <summary>
    /// Tao mask cac o bi chan boi wall/corner.
    /// </summary>
    private bool[,] BuildWallFrameMask(int gridWidth, int gridHeight)
    {
        bool[,] blockedByFrame = new bool[gridWidth, gridHeight];

        for (int i = 0; i < _placedBlocks.Count; i++)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !blockData.IsEnvironment || (!IsWallBlock(blockData) && !IsCornerBlock(blockData)))
                continue;

            Vector2Int gridPosition = blockData.GridPosition;
            if (gridPosition.x < 0 || gridPosition.x >= gridWidth || gridPosition.y < 0 || gridPosition.y >= gridHeight)
                continue;

            blockedByFrame[gridPosition.x, gridPosition.y] = true;
        }

        return blockedByFrame;
    }

    /// <summary>
    /// Flood-fill cac o trong grid co the di tu ngoai vao khi wall/corner la bien chan.
    /// </summary>
    private static bool[,] BuildOutsideReachableMask(bool[,] blockedByFrame, int gridWidth, int gridHeight)
    {
        bool[,] reachable = new bool[gridWidth, gridHeight];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int x = 0; x < gridWidth; x++)
        {
            EnqueueOutsideCell(new Vector2Int(x, 0), blockedByFrame, reachable, queue);
            EnqueueOutsideCell(new Vector2Int(x, gridHeight - 1), blockedByFrame, reachable, queue);
        }

        for (int y = 1; y < gridHeight - 1; y++)
        {
            EnqueueOutsideCell(new Vector2Int(0, y), blockedByFrame, reachable, queue);
            EnqueueOutsideCell(new Vector2Int(gridWidth - 1, y), blockedByFrame, reachable, queue);
        }

        Vector2Int[] directions =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (next.x < 0 || next.x >= gridWidth || next.y < 0 || next.y >= gridHeight)
                    continue;

                EnqueueOutsideCell(next, blockedByFrame, reachable, queue);
            }
        }

        return reachable;
    }

    /// <summary>
    /// Them o vao queue flood-fill neu o do hop le va chua bi wall/corner chan.
    /// </summary>
    private static void EnqueueOutsideCell(
        Vector2Int gridPosition,
        bool[,] blockedByFrame,
        bool[,] reachable,
        Queue<Vector2Int> queue)
    {
        if (blockedByFrame[gridPosition.x, gridPosition.y] || reachable[gridPosition.x, gridPosition.y])
            return;

        reachable[gridPosition.x, gridPosition.y] = true;
        queue.Enqueue(gridPosition);
    }

    /// <summary>
    /// Suy luận rotation của wall từ các wall lân cận.
    /// </summary>
    private bool TryGetAutoWallRotation(Vector2Int gridPosition, out int rotation)
    {
        if (HasWallEnvironmentAt(gridPosition + Vector2Int.up) ||
            HasWallEnvironmentAt(gridPosition + Vector2Int.down))
        {
            rotation = 0;
            return true;
        }

        if (HasWallEnvironmentAt(gridPosition + Vector2Int.right) ||
            HasWallEnvironmentAt(gridPosition + Vector2Int.left))
        {
            rotation = 1;
            return true;
        }

        rotation = 0;
        return false;
    }

    /// <summary>
    /// Uu tien cap wall/corner co tile nam o duong cheo phia trong de tranh xoay nguoc cac cum goc lom.
    /// </summary>
    private bool TryGetTileBackedCornerRotation(Vector2Int gridPosition, out int rotation)
    {
        // Corner lom co the cham 2 cap wall-like khac nhau (vi corner noi corner).
        // Tile nam cheo ben trong la tin hieu chac hon de biet mat corner can quay ve phia nao.
        if (HasCornerSupportWithInnerTile(gridPosition, Vector2Int.right, Vector2Int.up))
        {
            rotation = 0;
            return true;
        }

        if (HasCornerSupportWithInnerTile(gridPosition, Vector2Int.left, Vector2Int.up))
        {
            rotation = 1;
            return true;
        }

        if (HasCornerSupportWithInnerTile(gridPosition, Vector2Int.left, Vector2Int.down))
        {
            rotation = 2;
            return true;
        }

        if (HasCornerSupportWithInnerTile(gridPosition, Vector2Int.right, Vector2Int.down))
        {
            rotation = 3;
            return true;
        }

        rotation = 0;
        return false;
    }

    private bool HasCornerSupportWithInnerTile(Vector2Int gridPosition, Vector2Int firstDirection,
        Vector2Int secondDirection)
    {
        // Hop le khi corner co 2 canh wall-like vuong goc va o cheo giua 2 canh do la tile san choi.
        return HasWallLikeEnvironmentAt(gridPosition + firstDirection) &&
               HasWallLikeEnvironmentAt(gridPosition + secondDirection) &&
               HasTileEnvironmentAt(gridPosition + firstDirection + secondDirection);
    }

    /// <summary>
    /// Suy luận rotation corner khi ô hiện tại nối với hai hướng wall-like vuông góc.
    /// </summary>
    private bool TryGetAutoCornerRotation(Vector2Int gridPosition, out int rotation)
    {
        // Uu tien rule co tile de tranh case corner lom bi map nhu outer corner va xoay nguoc.
        if (TryGetTileBackedCornerRotation(gridPosition, out rotation))
            return true;

        // Fallback cho corner vien ngoai hoac nhung o chua co tile noi that lam tham chieu.
        if (HasWallLikeEnvironmentAt(gridPosition + Vector2Int.right) &&
            HasWallLikeEnvironmentAt(gridPosition + Vector2Int.up))
        {
            rotation = 0;
            return true;
        }

        if (HasWallLikeEnvironmentAt(gridPosition + Vector2Int.left) &&
            HasWallLikeEnvironmentAt(gridPosition + Vector2Int.up))
        {
            rotation = 1;
            return true;
        }

        if (HasWallLikeEnvironmentAt(gridPosition + Vector2Int.left) &&
            HasWallLikeEnvironmentAt(gridPosition + Vector2Int.down))
        {
            rotation = 2;
            return true;
        }

        if (HasWallLikeEnvironmentAt(gridPosition + Vector2Int.right) &&
            HasWallLikeEnvironmentAt(gridPosition + Vector2Int.down))
        {
            rotation = 3;
            return true;
        }

        rotation = 0;
        return false;
    }

    /// <summary>
    /// Kiểm tra vị trí grid có wall môi trường hay không.
    /// </summary>
    private bool HasWallEnvironmentAt(Vector2Int gridPosition)
    {
        for (int i = _placedBlocks.Count - 1; i >= 0; i--)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !blockData.IsEnvironment)
                continue;

            if (blockData.GridPosition == gridPosition && IsWallBlock(blockData))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra vị trí grid có corner môi trường hay không.
    /// </summary>
    private bool HasCornerEnvironmentAt(Vector2Int gridPosition)
    {
        return TryGetCornerBlockAt(gridPosition, out _);
    }

    /// <summary>
    /// Tìm corner môi trường tại vị trí grid và trả dữ liệu block nếu có.
    /// </summary>
    private bool TryGetCornerBlockAt(Vector2Int gridPosition, out LevelBlockData cornerBlock)
    {
        for (int i = _placedBlocks.Count - 1; i >= 0; i--)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !blockData.IsEnvironment)
                continue;

            if (blockData.GridPosition == gridPosition && IsCornerBlock(blockData))
            {
                cornerBlock = blockData;
                return true;
            }
        }

        cornerBlock = null;
        return false;
    }

    /// <summary>
    /// Kiểm tra vị trí có wall hoặc corner, dùng cho logic tự nối môi trường.
    /// </summary>
    private bool HasWallLikeEnvironmentAt(Vector2Int gridPosition)
    {
        for (int i = _placedBlocks.Count - 1; i >= 0; i--)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !blockData.IsEnvironment)
                continue;

            if (blockData.GridPosition == gridPosition && (IsWallBlock(blockData) || IsCornerBlock(blockData)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Kiem tra o grid co object moi truong khong phai tile hay khong.
    /// </summary>
    private bool HasNonTileEnvironmentAt(Vector2Int gridPosition)
    {
        for (int i = _placedBlocks.Count - 1; i >= 0; i--)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !blockData.IsEnvironment || IsTileBlock(blockData))
                continue;

            if (blockData.GridPosition == gridPosition)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Tìm index block môi trường tại vị trí grid theo điều kiện match.
    /// </summary>
    private int FindEnvironmentIndexAt(Vector2Int gridPosition, Predicate<LevelBlockData> match)
    {
        for (int i = _placedBlocks.Count - 1; i >= 0; i--)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !blockData.IsEnvironment || !match(blockData))
                continue;

            if (blockData.GridPosition == gridPosition)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Xóa các object môi trường không phải tile tại một vị trí grid.
    /// </summary>
    private void RemoveNonTileEnvironmentAt(Vector2Int gridPosition)
    {
        for (int i = _placedBlocks.Count - 1; i >= 0; i--)
        {
            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !blockData.IsEnvironment || IsTileBlock(blockData))
                continue;

            if (blockData.GridPosition == gridPosition)
                _placedBlocks.RemoveAt(i);
        }
    }

    /// <summary>
    /// Xóa tile môi trường tại vị trí grid, có thể bỏ qua một index đang thao tác.
    /// </summary>
    private void RemoveTileEnvironmentAt(Vector2Int gridPosition, int ignoreIndex = -1)
    {
        for (int i = _placedBlocks.Count - 1; i >= 0; i--)
        {
            if (i == ignoreIndex)
                continue;

            LevelBlockData blockData = _placedBlocks[i];
            if (blockData == null || !IsTileBlock(blockData))
                continue;

            if (blockData.GridPosition == gridPosition)
            {
                _placedBlocks.RemoveAt(i);
                _autoCreatedTilePositions.Remove(gridPosition);
                AdjustSelectionAfterRemove(i);
            }
        }
    }

    /// <summary>
    /// Điều chỉnh index selected/dragging sau khi một block bị xóa khỏi danh sách.
    /// </summary>
    private void AdjustSelectionAfterRemove(int removedIndex)
    {
        if (_selectedBlockIndex == removedIndex)
            _selectedBlockIndex = -1;
        else if (_selectedBlockIndex > removedIndex)
            _selectedBlockIndex--;

        if (_draggingBlockIndex == removedIndex)
            _draggingBlockIndex = -1;
        else if (_draggingBlockIndex > removedIndex)
            _draggingBlockIndex--;
    }

    /// <summary>
    /// Tạo LevelBlockData cho object môi trường với màu/material mặc định.
    /// </summary>
    private static LevelBlockData CreateEnvironmentBlock(GameObject prefab, Vector2Int gridPosition, int rotation)
    {
        return new LevelBlockData(prefab, gridPosition, rotation, TypeBlockColor.Red, null, true);
    }

    /// <summary>
    /// Xóa hàng loạt block môi trường thỏa điều kiện và reset trạng thái chọn.
    /// </summary>
    private void RemoveEnvironmentBlocks(Predicate<LevelBlockData> match)
    {
        _placedBlocks.RemoveAll(blockData => blockData != null && match(blockData));
        _selectedBlockIndex = -1;
        _draggingBlockIndex = -1;
    }

    /// <summary>
    /// Load prefab môi trường từ asset path và cảnh báo nếu thiếu.
    /// </summary>
    private static GameObject LoadEnvironmentPrefab(string assetPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
            Debug.LogWarning($"Missing environment prefab: {assetPath}");

        return prefab;
    }

    /// <summary>
    /// Lấy prefab wall hiện tại hoặc fallback mặc định.
    /// </summary>
    private GameObject GetWallPrefab()
    {
        _activeWallPrefab = GetPrefabFromField(_wallPrefabField, WallPatch);
        return _activeWallPrefab;
    }

    /// <summary>
    /// Lấy prefab corner hiện tại hoặc fallback mặc định.
    /// </summary>
    private GameObject GetCornerPrefab()
    {
        _activeCornerPrefab = GetPrefabFromField(_cornerPrefabField, CornerPatch);
        return _activeCornerPrefab;
    }

    /// <summary>
    /// Lấy prefab obstacle hiện tại hoặc fallback mặc định.
    /// </summary>
    private GameObject GetObstaclePrefab()
    {
        _activeObstaclePrefab = GetPrefabFromField(_obstaclePrefabField, ObstaclePatch);
        return _activeObstaclePrefab;
    }

    /// <summary>
    /// Lấy prefab tile hiện tại hoặc fallback mặc định.
    /// </summary>
    private GameObject GetTilePrefab()
    {
        _activeTilePrefab = GetPrefabFromField(_tilePrefabField, TilePatch);
        return _activeTilePrefab;
    }

    /// <summary>
    /// Lấy prefab từ ObjectField, nếu trống thì load prefab fallback.
    /// </summary>
    private static GameObject GetPrefabFromField(ObjectField field, string fallbackPath)
    {
        GameObject prefab = field != null ? field.value as GameObject : null;
        return prefab != null ? prefab : LoadEnvironmentPrefab(fallbackPath);
    }

    /// <summary>
    /// Kiểm tra tọa độ grid có nằm trong kích thước level hiện tại hay không.
    /// </summary>
    private bool IsInsideGrid(Vector2Int gridPosition)
    {
        int gridWidth = Mathf.Clamp(_gridX.value, MinGridSize, MaxGridSize);
        int gridHeight = Mathf.Clamp(_gridY.value, MinGridSize, MaxGridSize);
        return gridPosition.x >= 0 && gridPosition.x < gridWidth && gridPosition.y >= 0 && gridPosition.y < gridHeight;
    }

    /// <summary>
    /// Kiểm tra LevelBlockData có phải tile môi trường hay không.
    /// </summary>
    private static bool IsTileBlock(LevelBlockData blockData)
    {
        return IsEnvironmentBlock(blockData, TilePatch, _activeTilePrefab);
    }

    /// <summary>
    /// Kiểm tra LevelBlockData có phải wall môi trường hay không.
    /// </summary>
    private static bool IsWallBlock(LevelBlockData blockData)
    {
        return IsEnvironmentBlock(blockData, WallPatch, _activeWallPrefab);
    }

    /// <summary>
    /// Kiểm tra LevelBlockData có phải corner môi trường hay không.
    /// </summary>
    private static bool IsCornerBlock(LevelBlockData blockData)
    {
        return IsEnvironmentBlock(blockData, CornerPatch, _activeCornerPrefab);
    }

    /// <summary>
    /// Kiểm tra LevelBlockData có phải obstacle môi trường hay không.
    /// </summary>
    private static bool IsObstacleBlock(LevelBlockData blockData)
    {
        return IsEnvironmentBlock(blockData, ObstaclePatch, _activeObstaclePrefab);
    }

    /// <summary>
    /// Kiểm tra block có được tính vào thống kê gameplay hay không.
    /// </summary>
    private static bool IsCountedBlock(LevelBlockData blockData)
    {
        return blockData != null &&
               !blockData.IsEnvironment &&
               !IsTileBlock(blockData) &&
               !IsWallBlock(blockData) &&
               !IsCornerBlock(blockData) &&
               !IsObstacleBlock(blockData);
    }

    /// <summary>
    /// So sánh block với prefab môi trường bằng reference active prefab hoặc asset path.
    /// </summary>
    private static bool IsEnvironmentBlock(LevelBlockData blockData, string assetPath, GameObject activePrefab)
    {
        if (blockData == null || blockData.Prefab == null)
            return false;

        return blockData.Prefab == activePrefab || AssetDatabase.GetAssetPath(blockData.Prefab) == assetPath;
    }

    /*private void CreateRuntimePreviewBlock(LevelBlockData blockData)
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
    }*/
    /// <summary>
    /// Hàm dự phòng để phát triển logic preview wall riêng nếu cần.
    /// </summary>
    private void CreateRuntimePreviewWall()
    {

    }
}
