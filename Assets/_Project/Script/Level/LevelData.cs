using System.Collections.Generic;
using UnityEngine;

namespace Jam
{
    /// <summary>
    /// Luu so luong block theo mot mau cu the trong level.
    /// </summary>
    [System.Serializable]
    public class LevelBlockColorCount
    {
        // Mau block dang duoc thong ke.
        [SerializeField] private BlockColor blockColor;
        // Property chi doc de code khac lay mau block.
        public BlockColor BlockColor => blockColor;

        // So block co mau nay trong level.
        [SerializeField] private int count;
        // Property chi doc de code khac lay so luong block.
        public int Count => count;

        /// <summary>
        /// Tạo dữ liệu thống kê số lượng block theo một màu.
        /// </summary>
        public LevelBlockColorCount(BlockColor newBlockColor, int newCount)
        {
            blockColor = newBlockColor;
            count = Mathf.Max(0, newCount);
        }
    }

    [System.Serializable]
    public class LevelBlockData
    {
        // Prefab cua block/moi truong duoc dat trong level.
        [SerializeField] private GameObject prefab;
        // Property chi doc de code khac lay prefab.
        public GameObject Prefab => prefab;

        // Vi tri cua block tren grid level.
        [SerializeField] private Vector2Int gridPosition;
        // Property chi doc de code khac lay vi tri grid.
        public Vector2Int GridPosition => gridPosition;

        // Huong xoay cua block, luu theo buoc 90 do va normalize ve 0..3.
        [SerializeField] private int rotation;
        // Property chi doc de code khac lay huong xoay.
        public int Rotation => rotation;

        // Mau logic cua block gameplay.
        [SerializeField] private BlockColor blockColor;
        // Property chi doc de code khac lay mau logic.
        public BlockColor BlockColor => blockColor;

        // Material gan rieng cho block neu co.
        [SerializeField] private Material material;
        // Property chi doc de code khac lay material.
        public Material Material => material;

        // Danh dau day la object moi truong nhu tile/wall/corner, khong phai block gameplay.
        [SerializeField] private bool isEnvironment;
        // Property chi doc de code khac biet block co phai moi truong hay khong.
        public bool IsEnvironment => isEnvironment;

        /// <summary>
        /// Tạo dữ liệu một block được đặt trong level, gồm prefab, vị trí, hướng, màu và loại môi trường.
        /// </summary>
        public LevelBlockData(
            GameObject newPrefab,
            Vector2Int newGridPosition,
            int newRotation,
            BlockColor newBlockColor,
            Material newMaterial,
            bool newIsEnvironment = false)
        {
            prefab = newPrefab;
            gridPosition = newGridPosition;
            rotation = ((newRotation % 4) + 4) % 4;
            blockColor = newBlockColor;
            material = newMaterial;
            isEnvironment = newIsEnvironment;
        }
    }

    [CreateAssetMenu(fileName = "level_1", menuName = "Jam/Level Data")]
    public class LevelData : ScriptableObject
    {
        // ID cua level, dung de dat ten/lua chon level.
        [SerializeField] private int levelId = 1;
        // Property chi doc de code khac lay ID level.
        public int LevelId => levelId;

        // Kich thuoc grid level theo truc X/Y.
        [SerializeField] private Vector2Int gridSize = Vector2Int.one;
        // Property chi doc de code khac lay kich thuoc grid.
        public Vector2Int GridSize => gridSize;

        // Danh sach tat ca block/object da duoc dat trong level.
        [SerializeField] private List<LevelBlockData> blocks = new List<LevelBlockData>();
        // Property chi doc de code khac doc danh sach block ma khong sua truc tiep.
        public IReadOnlyList<LevelBlockData> Blocks => blocks;

        // Tong so block gameplay, khong tinh tile/wall/corner/obstacle moi truong.
        [SerializeField] private int blockCount;
        // Property chi doc de code khac lay tong so block gameplay.
        public int BlockCount => blockCount;

        // Bang thong ke so block gameplay theo tung mau.
        [SerializeField] private List<LevelBlockColorCount> blockColorCounts = new List<LevelBlockColorCount>();
        // Property chi doc de code khac lay thong ke mau.
        public IReadOnlyList<LevelBlockColorCount> BlockColorCounts => blockColorCounts;

        /// <summary>
        /// Khởi tạo hoặc cập nhật id level và kích thước grid.
        /// </summary>
        public void Init(int newLevelId, Vector2Int newGridSize)
        {
            levelId = Mathf.Max(1, newLevelId);
            gridSize = new Vector2Int(Mathf.Max(1, newGridSize.x), Mathf.Max(1, newGridSize.y));
            RebuildBlockSummary();
        }

        /// <summary>
        /// Thay danh sách block của level và cập nhật lại thống kê.
        /// </summary>
        public void SetBlocks(IEnumerable<LevelBlockData> newBlocks)
        {
            if (blocks == null)
                blocks = new List<LevelBlockData>();

            blocks.Clear();

            if (newBlocks != null)
                blocks.AddRange(newBlocks);

            RebuildBlockSummary();
        }

        /// <summary>
        /// Cập nhật thống kê mỗi khi asset LevelData được Unity validate.
        /// </summary>
        private void OnValidate()
        {
            RebuildBlockSummary();
        }

        /// <summary>
        /// Tính lại tổng số block chơi được và số lượng theo từng màu.
        /// </summary>
        private void RebuildBlockSummary()
        {
            blockCount = 0;
            if (blockColorCounts == null)
                blockColorCounts = new List<LevelBlockColorCount>();

            blockColorCounts.Clear();

            Dictionary<BlockColor, int> colorCounts = new Dictionary<BlockColor, int>();

            if (blocks == null)
            {
                blocks = new List<LevelBlockData>();
                return;
            }

            foreach (LevelBlockData block in blocks)
            {
                if (block == null || block.IsEnvironment || IsEnvironmentBlock(block))
                    continue;

                blockCount++;
                BlockColor blockColor = block.BlockColor;
                colorCounts.TryGetValue(blockColor, out int currentCount);
                colorCounts[blockColor] = currentCount + 1;
            }

            foreach (BlockColor blockColor in System.Enum.GetValues(typeof(BlockColor)))
            {
                colorCounts.TryGetValue(blockColor, out int count);
                if (count > 0)
                    blockColorCounts.Add(new LevelBlockColorCount(blockColor, count));
            }
        }

        /// <summary>
        /// Kiểm tra một block có phải prefab môi trường dựa trên tên prefab hay không.
        /// </summary>
        private static bool IsEnvironmentBlock(LevelBlockData block)
        {
            if (block.Prefab == null)
                return false;

            string prefabName = block.Prefab.name;
            return IsEnvironmentPrefabName(prefabName, "Border") ||
                   IsEnvironmentPrefabName(prefabName, "Corner") ||
                   IsEnvironmentPrefabName(prefabName, "Inner Obstacle") ||
                   IsEnvironmentPrefabName(prefabName, "Inner Tile");
        }

        /// <summary>
        /// So sánh tên prefab với tên môi trường theo cách không phân biệt hoa thường.
        /// </summary>
        private static bool IsEnvironmentPrefabName(string prefabName, string environmentName)
        {
            return string.Equals(prefabName, environmentName, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
