using UnityEngine;

namespace Watermelon
{
    public static class ShaderHelper
    {
        // Keyword trong shader dung de bat/tat outline.
        private const string OUTLINE_KEYWORD = "OUTLINE_ON";
        // Ten pass shader can bat/tat khi outline thay doi.
        private const string OUTLINE_PASS_NAME = "SRPDefaultUnlit";

        // ID cache cua property _OutlineWidth de set shader nhanh hon dung string.
        private static readonly int OUTLINE_WIDTH_HASH = Shader.PropertyToID("_OutlineWidth");
        // ID cache cua property _OutlineColor de set shader nhanh hon dung string.
        private static readonly int OUTLINE_COLOR_HASH = Shader.PropertyToID("_OutlineColor");

        /// <summary>
        /// Bật hoặc tắt keyword/pass outline trên material.
        /// </summary>
        public static void SetOutlineState(this Material material, bool state)
        {
            if (state)
            {
                material.EnableKeyword(OUTLINE_KEYWORD);
            }
            else
            {
                material.DisableKeyword(OUTLINE_KEYWORD);
            }

            material.SetShaderPassEnabled(OUTLINE_PASS_NAME, state);
        }

        /// <summary>
        /// Thiết lập độ dày outline trực tiếp trên material.
        /// </summary>
        public static void SetOutlineWidth(this Material material, float value)
        {
            material.SetFloat(OUTLINE_WIDTH_HASH, value);
        }

        /// <summary>
        /// Thiết lập độ dày outline qua MaterialPropertyBlock để không tạo material instance mới.
        /// </summary>
        public static void SetOutlineWidth(this Renderer renderer, MaterialPropertyBlock propertyBlock, float value)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(OUTLINE_WIDTH_HASH, value);
            renderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// Thiết lập màu outline trực tiếp trên material.
        /// </summary>
        public static void SetOutlineColor(this Material material, Color color)
        {
            material.SetColor(OUTLINE_COLOR_HASH, color);
        }

        /// <summary>
        /// Thiết lập màu outline qua MaterialPropertyBlock cho renderer cụ thể.
        /// </summary>
        public static void SetOutlineColor(this Renderer renderer, MaterialPropertyBlock propertyBlock, Color color)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(OUTLINE_COLOR_HASH, color);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
