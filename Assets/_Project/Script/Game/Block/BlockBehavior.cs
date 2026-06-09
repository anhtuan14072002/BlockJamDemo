using System.Collections.Generic;
using UnityEngine;

namespace Jam
{
    public class BlockBehavior : MonoBehaviour, IClickableObject
    {
        // So o theo chieu ngang cua shape block.
        [SerializeField] private int width = 1;
        // Property chi doc de code khac lay chieu ngang block.
        public int Width => width;

        // So o theo chieu doc cua shape block.
        [SerializeField] private int height = 1;
        // Property chi doc de code khac lay chieu doc block.
        public int Height => height;

        // O goc neo cua block, dung de quy doi vi tri local sang vi tri tren grid.
        [SerializeField] private Vector2Int pivot;
        // Property chi doc de code khac lay pivot hien tai.
        public Vector2Int Pivot => pivot;

        // Du lieu mau dang duoc gan cho block o runtime.
        private BlockColorData _colorData;
        // Property chi doc de code khac xem mau/material cua block.
        public BlockColorData ColorData => _colorData;

        // Danh sach cac o local dang bi block chiem; index = y * width + x.
        [SerializeField] private List<bool> occupiedCells = new() { true };
        // Property chi doc de editor/logic khac doc cac o dang bat.
        public IReadOnlyList<bool> OccupiedCells => occupiedCells;
        // Tong so o dang bat, dung de thong ke kich thuoc thuc cua block.
        public int ActivePoints => CountActiveCells();

        // Cac o dung de tinh bounds ngang rieng voi hinh dang thuc.
        [SerializeField] private List<bool> horizontalBoundsCells = new() { true };
        // Property chi doc cho danh sach bounds ngang.
        public IReadOnlyList<bool> HorizontalBoundsCells => horizontalBoundsCells;

        // Cac o dung de tinh bounds doc rieng voi hinh dang thuc.
        [SerializeField] private List<bool> verticalBoundsCells = new() { true };
        // Property chi doc cho danh sach bounds doc.
        public IReadOnlyList<bool> VerticalBoundsCells => verticalBoundsCells;

        // Renderer nhan material khi block duoc doi mau.
        [SerializeField] private MeshRenderer _meshRenderer;

        /// <summary>
        /// Kiểm tra ô local trong shape có đang được block chiếm hay không.
        /// </summary>
        public bool IsCellOccupied(int x, int y)
        {
            return GetCell(occupiedCells, x, y);
        }

        /// <summary>
        /// Kiểm tra ô local có thuộc vùng giới hạn ngang của block hay không.
        /// </summary>
        public bool IsCellInHorizontalBounds(int x, int y)
        {
            return GetCell(horizontalBoundsCells, x, y);
        }

        /// <summary>
        /// Kiểm tra ô local có thuộc vùng giới hạn dọc của block hay không.
        /// </summary>
        public bool IsCellInVerticalBounds(int x, int y)
        {
            return GetCell(verticalBoundsCells, x, y);
        }

        /// <summary>
        /// Đổi kích thước shape và giữ lại dữ liệu ô cũ trong phần còn nằm trong kích thước mới.
        /// </summary>
        public void SetSize(int newWidth, int newHeight)
        {
            ResizeShape(Mathf.Max(1, newWidth), Mathf.Max(1, newHeight));
        }

        /// <summary>
        /// Cập nhật pivot của block và ép giá trị nằm trong kích thước shape.
        /// </summary>
        public void SetPivot(Vector2Int newPivot)
        {
            pivot = ClampPivot(newPivot);
        }

        /// <summary>
        /// Bật hoặc tắt trạng thái chiếm ô của một cell trong shape.
        /// </summary>
        public void SetCellOccupied(int x, int y, bool isOccupied)
        {
            SetCell(occupiedCells, x, y, isOccupied);
        }

        /// <summary>
        /// Bật hoặc tắt ô được dùng để tính giới hạn ngang.
        /// </summary>
        public void SetHorizontalBoundsCell(int x, int y, bool isUsed)
        {
            SetCell(horizontalBoundsCells, x, y, isUsed);
        }

        /// <summary>
        /// Bật hoặc tắt ô được dùng để tính giới hạn dọc.
        /// </summary>
        public void SetVerticalBoundsCell(int x, int y, bool isUsed)
        {
            SetCell(verticalBoundsCells, x, y, isUsed);
        }

        /// <summary>
        /// Gán shape mới bằng kích thước và danh sách ô chiếm, giữ pivot hiện tại.
        /// </summary>
        public void SetShape(int newWidth, int newHeight, IReadOnlyList<bool> newOccupiedCells)
        {
            SetShape(newWidth, newHeight, newOccupiedCells, pivot, null, null);
        }

        /// <summary>
        /// Gán dữ liệu màu cho block và áp material lên MeshRenderer.
        /// </summary>
        public void SetColor(BlockColorData colorData)
        {
            _colorData = colorData;
            _meshRenderer.material = colorData.Material;
            
        }

        /// <summary>
        /// Gán đầy đủ shape, pivot và hai danh sách bounds cho block.
        /// </summary>
        public void SetShape(
            int newWidth,
            int newHeight,
            IReadOnlyList<bool> newOccupiedCells,
            Vector2Int newPivot,
            IReadOnlyList<bool> newHorizontalBoundsCells,
            IReadOnlyList<bool> newVerticalBoundsCells)
        {
            width = Mathf.Max(1, newWidth);
            height = Mathf.Max(1, newHeight);
            pivot = ClampPivot(newPivot);

            CopyCells(occupiedCells, newOccupiedCells, false);
            CopyCells(horizontalBoundsCells, newHorizontalBoundsCells, true);
            CopyCells(verticalBoundsCells, newVerticalBoundsCells, true);
        }

        /// <summary>
        /// Chuẩn hóa dữ liệu shape mỗi khi Unity validate object trong Inspector.
        /// </summary>
        void OnValidate()
        {
            NormalizeShape();
        }

        /// <summary>
        /// Đọc an toàn trạng thái của một ô, trả false nếu tọa độ nằm ngoài shape.
        /// </summary>
        bool GetCell(IReadOnlyList<bool> cells, int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return false;

            int index = ToIndex(x, y);
            return cells != null && index < cells.Count && cells[index];
        }

        /// <summary>
        /// Ghi trạng thái cho một ô sau khi bảo đảm danh sách cell đã đúng kích thước.
        /// </summary>
        void SetCell(List<bool> cells, int x, int y, bool value)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            NormalizeShape();
            cells[ToIndex(x, y)] = value;
        }

        /// <summary>
        /// Chuyển tọa độ 2D của cell thành index 1D trong danh sách lưu trữ.
        /// </summary>
        int ToIndex(int x, int y)
        {
            return y * width + x;
        }

        /// <summary>
        /// Resize toàn bộ shape, occupied cells và bounds cells cùng lúc.
        /// </summary>
        void ResizeShape(int newWidth, int newHeight)
        {
            List<bool> resizedOccupied = ResizeCells(occupiedCells, width, height, newWidth, newHeight, false);
            List<bool> resizedHorizontalBounds = ResizeCells(horizontalBoundsCells, width, height, newWidth, newHeight, true);
            List<bool> resizedVerticalBounds = ResizeCells(verticalBoundsCells, width, height, newWidth, newHeight, true);

            width = newWidth;
            height = newHeight;
            occupiedCells = resizedOccupied;
            horizontalBoundsCells = resizedHorizontalBounds;
            verticalBoundsCells = resizedVerticalBounds;
            pivot = ClampPivot(pivot);
        }

        /// <summary>
        /// Tạo danh sách cell mới theo kích thước mới và copy dữ liệu cũ còn hợp lệ.
        /// </summary>
        List<bool> ResizeCells(List<bool> source, int oldWidth, int oldHeight, int newWidth, int newHeight, bool defaultValue)
        {
            List<bool> resizedCells = new(newWidth * newHeight);

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    int oldIndex = y * oldWidth + x;
                    bool oldValue = source != null && x < oldWidth && y < oldHeight && oldIndex < source.Count
                        ? source[oldIndex]
                        : defaultValue;
                    resizedCells.Add(oldValue);
                }
            }

            return resizedCells;
        }

        /// <summary>
        /// Copy dữ liệu cell vào danh sách đích, tự điền giá trị mặc định khi thiếu dữ liệu.
        /// </summary>
        void CopyCells(List<bool> target, IReadOnlyList<bool> source, bool defaultValue)
        {
            int cellCount = width * height;
            target ??= new List<bool>(cellCount);
            target.Clear();

            for (int i = 0; i < cellCount; i++)
            {
                bool value = source != null && i < source.Count ? source[i] : defaultValue;
                target.Add(value);
            }
        }

        /// <summary>
        /// Ép shape về trạng thái hợp lệ: kích thước tối thiểu, pivot hợp lệ và đủ số cell.
        /// </summary>
        void NormalizeShape()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            pivot = ClampPivot(pivot);

            NormalizeCells(ref occupiedCells, false);
            NormalizeCells(ref horizontalBoundsCells, true);
            NormalizeCells(ref verticalBoundsCells, true);
        }

        /// <summary>
        /// Đảm bảo danh sách cell không null và có đúng số phần tử theo kích thước shape.
        /// </summary>
        void NormalizeCells(ref List<bool> cells, bool defaultValue)
        {
            int cellCount = width * height;
            cells ??= new List<bool>(cellCount);

            while (cells.Count < cellCount)
                cells.Add(defaultValue);

            if (cells.Count > cellCount)
                cells.RemoveRange(cellCount, cells.Count - cellCount);
        }

        /// <summary>
        /// Giới hạn pivot để luôn nằm trong vùng shape hiện tại.
        /// </summary>
        Vector2Int ClampPivot(Vector2Int value)
        {
            return new Vector2Int(Mathf.Clamp(value.x, 0, width - 1), Mathf.Clamp(value.y, 0, height - 1));
        }

        /// <summary>
        /// Đếm số ô đang bật trong occupied cells để biết block chiếm bao nhiêu điểm.
        /// </summary>
        int CountActiveCells()
        {
            if (occupiedCells == null)
                return 0;

            int count = 0;
            int cellCount = Mathf.Min(width * height, occupiedCells.Count);

            for (int i = 0; i < cellCount; i++)
            {
                if (occupiedCells[i])
                    count++;
            }

            return count;
        }
        public void OnObjectClicked()
        {
            Debug.Log("A");
        }

        public bool CanBeClicked()
        {
            throw new System.NotImplementedException();
        }

        public void OnClickBlocked()
        {
            throw new System.NotImplementedException();
        }
    }
}
