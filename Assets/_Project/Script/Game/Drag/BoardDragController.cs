using System.Collections.Generic;
using UnityEngine;

namespace Jam.Game.Drag
{
    public class BoardDragController : MonoBehaviour
    {
        [SerializeField] private LayerMask _draggableMask = ~0;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private float _overlapBoxScale = 0.98f;
        [SerializeField] private float _dragLiftHeight = 0.25f;

        private readonly Collider[] _overlapResults = new Collider[32];
        private readonly List<Vector3> _worldCells = new();
        private readonly HashSet<BlockBehavior> _contactLoggedBlocks = new();

        private Camera _camera;
        private BlockBehavior _draggingBlock;
        private Vector3 _dragOffset;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Vector3 _lastValidPosition;
        private float _dragPlaneY;

        private void Awake()
        {
            _camera = Camera.main;
            _cellSize = Mathf.Max(0.01f, _cellSize);
        }

        private void Update()
        {
            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
                return;

            Vector2 pointerPosition = Input.mousePosition;
            bool pointerDown = Input.GetMouseButtonDown(0);
            bool pointerHeld = Input.GetMouseButton(0);
            bool pointerUp = Input.GetMouseButtonUp(0);

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                pointerPosition = touch.position;
                pointerDown = touch.phase == TouchPhase.Began;
                pointerHeld = touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
                pointerUp = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            }

            if (pointerDown)
                TryBeginDrag(pointerPosition);

            if (_draggingBlock == null)
                return;

            if (pointerHeld)
                UpdateDrag(pointerPosition);

            if (pointerUp)
                EndDrag();
        }

        private void TryBeginDrag(Vector2 screenPosition)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, float.PositiveInfinity, _draggableMask,
                    QueryTriggerInteraction.Collide))
                return;

            BlockBehavior block = hit.collider.GetComponentInParent<BlockBehavior>();
            if (block == null)
                return;

            _draggingBlock = block;
            _contactLoggedBlocks.Clear();
            _startPosition = block.transform.position;
            _startRotation = block.transform.rotation;
            _lastValidPosition = _startPosition;
            _dragPlaneY = _startPosition.y;

            if (!TryGetPointOnDragPlane(screenPosition, out Vector3 pointerPlanePoint))
                pointerPlanePoint = new Vector3(hit.point.x, _dragPlaneY, hit.point.z);

            _dragOffset = _startPosition - pointerPlanePoint;
            _dragOffset.y = 0f;
        }

        private void UpdateDrag(Vector2 screenPosition)
        {
            if (!TryGetPointOnDragPlane(screenPosition, out Vector3 point))
                return;

            Vector3 target = point + _dragOffset;
            target.y = _dragPlaneY;

            if (TryMoveToValidPosition(_draggingBlock, _lastValidPosition, target, out Vector3 validPosition))
                _lastValidPosition = validPosition;

            if (_draggingBlock == null)
                return;

            Vector3 displayPosition = _lastValidPosition;
            displayPosition.y = _dragPlaneY + _dragLiftHeight;
            _draggingBlock.transform.position = displayPosition;
        }

        private void EndDrag()
        {
            Vector3 target = SnapToGrid(_lastValidPosition);
            target.y = _dragPlaneY;

            bool canPlace = CanPlace(_draggingBlock, target);
            if (_draggingBlock == null)
                return;

            if (canPlace)
            {
                _draggingBlock.transform.position = target;
            }
            else
            {
                _draggingBlock.transform.SetPositionAndRotation(_startPosition, _startRotation);
            }

            _draggingBlock = null;
        }

        private bool TryMoveToValidPosition(
            BlockBehavior block,
            Vector3 fromPosition,
            Vector3 toPosition,
            out Vector3 validPosition)
        {
            if (TryMoveAlongPath(block, fromPosition, toPosition, out validPosition, out bool reachedTarget) &&
                reachedTarget)
                return true;

            Vector3 slideStart = validPosition;
            bool moved = !SameHorizontalPosition(fromPosition, slideStart);

            bool movedXThenZ = TryMoveByAxisOrder(block, slideStart, toPosition, true, out Vector3 xThenZPosition);
            bool movedZThenX = TryMoveByAxisOrder(block, slideStart, toPosition, false, out Vector3 zThenXPosition);

            if (movedXThenZ && (!movedZThenX || IsCloserToTarget(xThenZPosition, zThenXPosition, toPosition)))
            {
                validPosition = xThenZPosition;
                return true;
            }

            if (movedZThenX)
            {
                validPosition = zThenXPosition;
                return true;
            }

            return moved;
        }

        private bool TryMoveByAxisOrder(
            BlockBehavior block,
            Vector3 fromPosition,
            Vector3 toPosition,
            bool moveXFirst,
            out Vector3 validPosition)
        {
            validPosition = fromPosition;
            Vector3 firstTarget = moveXFirst
                ? new Vector3(toPosition.x, _dragPlaneY, fromPosition.z)
                : new Vector3(fromPosition.x, _dragPlaneY, toPosition.z);

            bool moved = TryMoveAlongPath(block, validPosition, firstTarget, out validPosition, out _);
            Vector3 secondTarget = moveXFirst
                ? new Vector3(validPosition.x, _dragPlaneY, toPosition.z)
                : new Vector3(toPosition.x, _dragPlaneY, validPosition.z);
            moved |= TryMoveAlongPath(block, validPosition, secondTarget, out validPosition, out _);

            return moved;
        }

        private bool TryMoveAlongPath(
            BlockBehavior block,
            Vector3 fromPosition,
            Vector3 toPosition,
            out Vector3 validPosition,
            out bool reachedTarget)
        {
            validPosition = fromPosition;
            reachedTarget = false;

            float distance = Vector3.Distance(
                new Vector3(fromPosition.x, 0f, fromPosition.z),
                new Vector3(toPosition.x, 0f, toPosition.z));
            int stepCount = Mathf.CeilToInt(distance / (_cellSize * 0.2f));

            if (stepCount == 0)
            {
                if (!CanPlace(block, toPosition)) return false;

                validPosition = toPosition;
                reachedTarget = true;
                return true;
            }

            bool moved = false;

            for (int i = 1; i <= stepCount; i++)
            {
                float t = i / (float)stepCount;
                Vector3 nextPosition = Vector3.Lerp(fromPosition, toPosition, t);
                nextPosition.y = _dragPlaneY;

                if (!CanPlace(block, nextPosition)) break;

                validPosition = nextPosition;
                moved = true;
            }

            reachedTarget = moved && SameHorizontalPosition(validPosition, toPosition);
            return moved;
        }

        private static bool SameHorizontalPosition(Vector3 a, Vector3 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.z, b.z);
        }

        private static bool IsCloserToTarget(Vector3 a, Vector3 b, Vector3 target)
        {
            Vector2 aPosition = new(a.x, a.z);
            Vector2 bPosition = new(b.x, b.z);
            Vector2 targetPosition = new(target.x, target.z);
            return (aPosition - targetPosition).sqrMagnitude < (bPosition - targetPosition).sqrMagnitude;
        }

        private bool TryGetPointOnDragPlane(Vector2 screenPosition, out Vector3 point)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            Plane plane = new(Vector3.up, new Vector3(0f, _dragPlaneY, 0f));

            if (plane.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }

            point = default;
            return false;
        }

        private Vector3 SnapToGrid(Vector3 worldPosition)
        {
            Vector3 local = worldPosition - transform.position;
            int x = Mathf.RoundToInt(local.x / _cellSize);
            int y = Mathf.RoundToInt(-local.z / _cellSize);

            return transform.position +
                   new Vector3(x * _cellSize, worldPosition.y - transform.position.y, -y * _cellSize);
        }

        private bool CanPlace(BlockBehavior block, Vector3 targetPosition)
        {
            GetOccupiedWorldCells(block, targetPosition, _worldCells);

            foreach (Vector3 worldCell in _worldCells)
            {
                if (HasBlockingColliderAt(block, worldCell)) return false;
            }
            return true;
        }

        private void GetOccupiedWorldCells(BlockBehavior block, Vector3 targetPosition, List<Vector3> cells)
        {
            cells.Clear();

            for (int y = 0; y < block.Height; y++)
            {
                for (int x = 0; x < block.Width; x++)
                {
                    if (!block.IsCellOccupied(x, y))
                        continue;

                    Vector3 localOffset = new Vector3(
                        (x - block.Pivot.x) * _cellSize,
                        0f,
                        -(y - block.Pivot.y) * _cellSize);
                    cells.Add(targetPosition + block.transform.rotation * localOffset);
                }
            }
        }

        private bool HasBlockingColliderAt(BlockBehavior draggedBlock, Vector3 cellCenter)
        {
            Vector3 center = cellCenter + Vector3.up * 0.5f;
            Vector3 halfExtents = new(
                _cellSize * _overlapBoxScale * 0.5f,
                0.75f,
                _cellSize * _overlapBoxScale * 0.5f);

            int count = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                _overlapResults,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider other = _overlapResults[i];
                if (other == null || other.transform.IsChildOf(draggedBlock.transform))
                    continue;

                BlockBehavior otherBlock = other.GetComponentInParent<BlockBehavior>();
                if (otherBlock != null)
                {
                    HandleBlockContact(draggedBlock, otherBlock);
                    return true;
                }

                if (!IsIgnoredSurface(other))
                    return true;
            }

            return false;
        }

        private void HandleBlockContact(BlockBehavior draggedBlock, BlockBehavior otherBlock)
        {
            bool isSameColor = HasSameBlockColor(draggedBlock, otherBlock);
            if (!_contactLoggedBlocks.Contains(otherBlock))
            {
                _contactLoggedBlocks.Add(otherBlock);
                Debug.Log(
                    $"Block contact: dragged={DescribeBlockColor(draggedBlock)}, other={DescribeBlockColor(otherBlock)}, same={isSameColor}",
                    otherBlock);
            }

            if (!isSameColor)
                return;

            Debug.Log("Same color blocks destroyed.", otherBlock);
            Destroy(draggedBlock.gameObject);
            Destroy(otherBlock.gameObject);

            if (_draggingBlock == draggedBlock)
                _draggingBlock = null;
        }

        private static string DescribeBlockColor(BlockBehavior block)
        {
            if (block.ColorData != null)
                return block.ColorData.Type.ToString();

            Renderer renderer = block.GetComponentInChildren<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                return $"Material:{renderer.sharedMaterial.name}";

            return "Unknown";
        }

        private static bool HasSameBlockColor(BlockBehavior firstBlock, BlockBehavior secondBlock)
        {
            if (firstBlock.ColorData != null && secondBlock.ColorData != null)
                return firstBlock.ColorData.Type == secondBlock.ColorData.Type;

            Material firstMaterial = GetBlockMaterial(firstBlock);
            Material secondMaterial = GetBlockMaterial(secondBlock);
            if (firstMaterial == null || secondMaterial == null)
                return false;

            return NormalizeMaterialName(firstMaterial.name) == NormalizeMaterialName(secondMaterial.name);
        }

        private static Material GetBlockMaterial(BlockBehavior block)
        {
            Renderer renderer = block.GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.sharedMaterial : null;
        }

        private static string NormalizeMaterialName(string materialName)
        {
            return materialName.Replace(" (Instance)", string.Empty);
        }

        private static bool IsIgnoredSurface(Collider collider)
        {
            Transform current = collider.transform;
            while (current != null)
            {
                string objectName = current.name;
                if (objectName.Contains("Inner Tile") ||
                    objectName.Contains("Plane") ||
                    (objectName.Contains("Ground") &&
                     !objectName.Contains("Border") &&
                     !objectName.Contains("Corner")))
                    return true;

                current = current.parent;
            }

            return false;
        }

    }
}
