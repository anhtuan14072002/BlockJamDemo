using UnityEngine;

namespace Jam
{
    [System.Serializable]
    public class BlockColorData
    {
        [SerializeField] TypeBlockColor type;
        public TypeBlockColor Type => type;

        [SerializeField] Material material;
        public Material Material => material;

        [SerializeField] Color color;
        public Color Color => color;
    }
}
