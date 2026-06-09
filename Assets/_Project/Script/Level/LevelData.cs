using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Jam
{
    [System.Serializable]
    public class LevelBlockColorCount
    {
        [SerializeField] private TypeBlockColor typeBlockColor;
        public TypeBlockColor TypeBlockColor => typeBlockColor;
        [SerializeField] private int count;
        public int Count => count;

        public LevelBlockColorCount(TypeBlockColor newTypeBlockColor, int newCount)
        {
            typeBlockColor = newTypeBlockColor;
            count = Mathf.Max(0, newCount);
        }
    }

    [System.Serializable]
    public class LevelBlockData
    {
        [SerializeField] private GameObject prefab;
        public GameObject Prefab => prefab;
        [SerializeField] private Vector2Int gridPosition;
        public Vector2Int GridPosition => gridPosition;
        [SerializeField] private int rotation;
        public int Rotation => rotation;

        [SerializeField] private TypeBlockColor typeBlockColor;
        public TypeBlockColor TypeBlockColor => typeBlockColor;
        [SerializeField] private Material material;
        public Material Material => material;

        [SerializeField] private bool isEnvironment;
        public bool IsEnvironment => isEnvironment;

 
        public LevelBlockData(
            GameObject newPrefab,
            Vector2Int newGridPosition,
            int newRotation,
            TypeBlockColor newTypeBlockColor,
            Material newMaterial,
            bool newIsEnvironment = false)
        {
            prefab = newPrefab;
            gridPosition = newGridPosition;
            rotation = ((newRotation % 4) + 4) % 4;
            typeBlockColor = newTypeBlockColor;
            material = newMaterial;
            isEnvironment = newIsEnvironment;
        }
    }

    [CreateAssetMenu(fileName = "level_1", menuName = "Jam/Level Data")]
    public class LevelData : ScriptableObject
    {
        [SerializeField] private int levelId = 1;
        public int LevelId => levelId;

        [SerializeField] private Vector2Int gridSize = Vector2Int.one;
        public Vector2Int GridSize => gridSize;

        [SerializeField] private List<LevelBlockData> blocks = new();
        public IReadOnlyList<LevelBlockData> Blocks => blocks;

        [SerializeField] private int blockCount;
        public int BlockCount => blockCount;

        [SerializeField] private List<LevelBlockColorCount> blockColorCounts = new();
        public IReadOnlyList<LevelBlockColorCount> BlockColorCounts => blockColorCounts;
        public void Init(int newLevelId, Vector2Int newGridSize)
        {
            levelId = Mathf.Max(1, newLevelId);
            gridSize = new Vector2Int(Mathf.Max(1, newGridSize.x), Mathf.Max(1, newGridSize.y));
            RebuildBlockSummary();
        }
        
        public void SetBlocks(IEnumerable<LevelBlockData> newBlocks)
        {
            if (blocks == null)
                blocks = new List<LevelBlockData>();

            blocks.Clear();

            if (newBlocks != null)
                blocks.AddRange(newBlocks);

            RebuildBlockSummary();
        }
        
        private void OnValidate()
        {
            RebuildBlockSummary();
        }

        private void RebuildBlockSummary()
        {
            blockCount = 0;
            if (blockColorCounts == null)
                blockColorCounts = new List<LevelBlockColorCount>();

            blockColorCounts.Clear();

            Dictionary<TypeBlockColor, int> colorCounts = new Dictionary<TypeBlockColor, int>();

            if (blocks == null)
            {
                blocks = new List<LevelBlockData>();
                return;
            }

            foreach (LevelBlockData block in blocks)
            {
                if (block == null || block.IsEnvironment || IsEnvironmentBlock(block)) continue;
                blockCount++;
                TypeBlockColor typeBlockColor = block.TypeBlockColor;
                colorCounts.TryGetValue(typeBlockColor, out int currentCount);
                colorCounts[typeBlockColor] = currentCount + 1;
            }

            foreach (TypeBlockColor blockColor in System.Enum.GetValues(typeof(TypeBlockColor)))
            {
                colorCounts.TryGetValue(blockColor, out int count);
                if (count > 0)
                    blockColorCounts.Add(new LevelBlockColorCount(blockColor, count));
            }
        }

        /// Kiểm tra một block có phải prefab môi trường dựa trên tên prefab hay không.
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

        /// So sánh tên prefab với tên môi trường theo cách không phân biệt hoa thường.
        private static bool IsEnvironmentPrefabName(string prefabName, string environmentName)
        {
            return string.Equals(prefabName, environmentName, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
