using UnityEngine;

namespace Jam
{
    /// <summary>
    /// Gom du lieu hien thi cho mot mau block: enum mau, material va mau raw.
    /// </summary>
    [System.Serializable]
    public class BlockColorData
    {
        // Gia tri enum dai dien cho mau logic cua block.
        [SerializeField] BlockColor type;
        // Property chi doc de code khac lay mau logic.
        public BlockColor Type => type;

        // Material duoc gan len MeshRenderer khi block dung mau nay.
        [SerializeField] Material material;
        // Property chi doc de code khac lay material cua mau.
        public Material Material => material;

        // Gia tri Color thuan, dung khi can ve UI/preview khong can material.
        [SerializeField] Color color;
        // Property chi doc de code khac lay mau hien thi.
        public Color Color => color;
    }
}
