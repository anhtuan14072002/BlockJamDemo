using UnityEngine;

namespace Jam
{
    [System.Serializable]
    public class BlockData
    {
        [SerializeField] BlockType type;
        public BlockType Type => type;

        [SerializeField] GameObject prefab;
        public GameObject Prefab => prefab;

        public BlockBehavior BlockBehavior { get; private set; }
    }
}