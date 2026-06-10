using System.Collections.Generic;
using UnityEngine;

namespace Jam
{
    public class BlockBehavior : MonoBehaviour
    {
        [SerializeField] private int width = 1;
        public int Width => width;

        [SerializeField] private int height = 1;
        public int Height => height;

        [SerializeField] private Vector2Int pivot;
        public Vector2Int Pivot => pivot;

        private BlockColorData _colorData;
        public BlockColorData ColorData => _colorData;

        [SerializeField] private List<bool> occupiedCells = new() { true };
        public IReadOnlyList<bool> OccupiedCells => occupiedCells;
        public int ActivePoints => CountActiveCells();

        [SerializeField] private List<bool> horizontalBoundsCells = new() { true };
        public IReadOnlyList<bool> HorizontalBoundsCells => horizontalBoundsCells;

        [SerializeField] private List<bool> verticalBoundsCells = new() { true };
        public IReadOnlyList<bool> VerticalBoundsCells => verticalBoundsCells;
        [SerializeField] private MeshRenderer _meshRenderer;
        
        public bool IsCellOccupied(int x, int y)
        {
            return GetCell(occupiedCells, x, y);
        }
        
        public bool IsCellInHorizontalBounds(int x, int y)
        {
            return GetCell(horizontalBoundsCells, x, y);
        }
        
        public bool IsCellInVerticalBounds(int x, int y)
        {
            return GetCell(verticalBoundsCells, x, y);
        }

        public void SetSize(int newWidth, int newHeight)
        {
            ResizeShape(Mathf.Max(1, newWidth), Mathf.Max(1, newHeight));
        }

        public void SetPivot(Vector2Int newPivot)
        {
            pivot = ClampPivot(newPivot);
        }

        public void SetCellOccupied(int x, int y, bool isOccupied)
        {
            SetCell(occupiedCells, x, y, isOccupied);
        }

        public void SetHorizontalBoundsCell(int x, int y, bool isUsed)
        {
            SetCell(horizontalBoundsCells, x, y, isUsed);
        }

        public void SetVerticalBoundsCell(int x, int y, bool isUsed)
        {
            SetCell(verticalBoundsCells, x, y, isUsed);
        }
        
        public void SetShape(int newWidth, int newHeight, IReadOnlyList<bool> newOccupiedCells)
        {
            SetShape(newWidth, newHeight, newOccupiedCells, pivot, null, null);
        }
        
        public void SetColor(BlockColorData colorData)
        {
            _colorData = colorData;
            _meshRenderer.material = colorData.Material;
            
        }
        
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

  
        void OnValidate()
        {
            NormalizeShape();
        }

  
        bool GetCell(IReadOnlyList<bool> cells, int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return false;

            int index = ToIndex(x, y);
            return cells != null && index < cells.Count && cells[index];
        }

 
        void SetCell(List<bool> cells, int x, int y, bool value)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            NormalizeShape();
            cells[ToIndex(x, y)] = value;
        }

        int ToIndex(int x, int y)
        {
            return y * width + x;
        }

      
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
        
        void NormalizeShape()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            pivot = ClampPivot(pivot);

            NormalizeCells(ref occupiedCells, false);
            NormalizeCells(ref horizontalBoundsCells, true);
            NormalizeCells(ref verticalBoundsCells, true);
        }

      
        void NormalizeCells(ref List<bool> cells, bool defaultValue)
        {
            int cellCount = width * height;
            cells ??= new List<bool>(cellCount);

            while (cells.Count < cellCount)
                cells.Add(defaultValue);

            if (cells.Count > cellCount)
                cells.RemoveRange(cellCount, cells.Count - cellCount);
        }
        
        Vector2Int ClampPivot(Vector2Int value)
        {
            return new Vector2Int(Mathf.Clamp(value.x, 0, width - 1), Mathf.Clamp(value.y, 0, height - 1));
        }
        
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
    }
}
