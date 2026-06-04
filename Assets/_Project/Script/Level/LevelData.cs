using System.Collections.Generic;
using UnityEngine;

namespace Jam
{
    [System.Serializable]
    public class LevelBlockData
    {
        [SerializeField] private GameObject prefab;
        public GameObject Prefab => prefab;

        [SerializeField] private Vector2Int gridPosition;
        public Vector2Int GridPosition => gridPosition;

        [SerializeField] private int rotation;
        public int Rotation => rotation;

        [SerializeField] private BlockColor blockColor;
        public BlockColor BlockColor => blockColor;

        [SerializeField] private Material material;
        public Material Material => material;

        public LevelBlockData(GameObject newPrefab, Vector2Int newGridPosition, int newRotation, BlockColor newBlockColor, Material newMaterial)
        {
            prefab = newPrefab;
            gridPosition = newGridPosition;
            rotation = ((newRotation % 4) + 4) % 4;
            blockColor = newBlockColor;
            material = newMaterial;
        }
    }

    [CreateAssetMenu(fileName = "level_1", menuName = "Jam/Level Data")]
    public class LevelData : ScriptableObject
    {
        [SerializeField] private int levelId = 1;
        public int LevelId => levelId;

        [SerializeField] private Vector2Int gridSize = Vector2Int.one;
        public Vector2Int GridSize => gridSize;

        [SerializeField] private List<LevelBlockData> blocks = new List<LevelBlockData>();
        public IReadOnlyList<LevelBlockData> Blocks => blocks;

        public void Init(int newLevelId, Vector2Int newGridSize)
        {
            levelId = Mathf.Max(1, newLevelId);
            gridSize = new Vector2Int(Mathf.Max(1, newGridSize.x), Mathf.Max(1, newGridSize.y));
        }

        public void SetBlocks(IEnumerable<LevelBlockData> newBlocks)
        {
            blocks.Clear();

            if (newBlocks == null)
                return;

            blocks.AddRange(newBlocks);
        }
    }
}
