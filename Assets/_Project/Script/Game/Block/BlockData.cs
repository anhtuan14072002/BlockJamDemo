using UnityEngine;

namespace Jam
{
    /// <summary>
    /// Mo ta mot loai block co the spawn/chon trong game.
    /// </summary>
    [System.Serializable]
    public class BlockData
    {
        // Kieu hinh dang logic cua block, vi du Single, Square, L, U, T.
        [SerializeField] BlockType type;
        // Property chi doc de code khac lay loai block.
        public BlockType Type => type;

        // Prefab duoc dung de tao block trong scene.
        [SerializeField] GameObject prefab;
        // Property chi doc de code khac lay prefab.
        public GameObject Prefab => prefab;

        // Component BlockBehavior cache tu prefab/instance, dung de truy cap shape va logic block.
        public BlockBehavior BlockBehavior { get; private set; }
    }
}
