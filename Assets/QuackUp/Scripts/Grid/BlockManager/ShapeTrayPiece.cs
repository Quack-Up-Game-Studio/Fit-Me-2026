using UnityEngine;

namespace FitMe.Grid
{
    /// <summary>Per-authored-piece identity and presentation, independent of legacy spawn slots.</summary>
    public sealed class ShapeTrayPiece
    {
        public int PieceId { get; }
        // Session-owned resting rank, not the authored identity or a drag counter.
        public int SortingOrder { get; internal set; }
        public Rect TrayBounds { get; }
        public Rect DropBounds { get; set; }
        public float Scale { get; }
        public float Tilt { get; }
        public Quaternion GameplayRotation { get; private set; }
        public Quaternion TrayRotation => GameplayRotation * Quaternion.Euler(0, 0, Tilt);
        public Vector2 Position { get; private set; }
        public bool IsPlaced { get; private set; }
        private readonly Vector2 _size;

        public ShapeTrayPiece(int pieceId, int[,] schema, Rect trayBounds, float scale, float tilt,
            Quaternion gameplayRotation, Vector2 position)
        {
            PieceId = pieceId;
            TrayBounds = trayBounds;
            DropBounds = trayBounds;
            _size = new Vector2(schema.GetLength(1), schema.GetLength(0));
            Tilt = tilt;
            GameplayRotation = gameplayRotation;
            // Fit full rotated bounds after any quarter turn, without imposing a
            // diagonal-based cap on a requested scale that already fits.
            var radians = tilt * Mathf.Deg2Rad;
            var c = Mathf.Abs(Mathf.Cos(radians));
            var s = Mathf.Abs(Mathf.Sin(radians));
            var longestSide = Mathf.Max(c * _size.x + s * _size.y, s * _size.x + c * _size.y);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) scale = .5f;
            Scale = Mathf.Min(scale, Mathf.Min(trayBounds.width, trayBounds.height) / longestSide);
            DropInTray(position);
        }

        public Rect PresentationBounds
        {
            get
            {
                var radians = TrayRotation.eulerAngles.z * Mathf.Deg2Rad;
                var c = Mathf.Abs(Mathf.Cos(radians));
                var s = Mathf.Abs(Mathf.Sin(radians));
                var extent = new Vector2(c * _size.x + s * _size.y, s * _size.x + c * _size.y) * Scale;
                return new Rect(Position - extent * .5f, extent);
            }
        }

        public void DropInTray(Vector2 position)
        {
            if (IsPlaced) return;
            var half = PresentationBounds.size * .5f;
            Position = new Vector2(Mathf.Clamp(position.x, TrayBounds.xMin + half.x, TrayBounds.xMax - half.x),
                Mathf.Clamp(position.y, TrayBounds.yMin + half.y, TrayBounds.yMax - half.y));
        }

        public void Rotate(float degrees)
        {
            if (!IsPlaced) GameplayRotation *= Quaternion.Euler(0, 0, degrees);
        }

        public void MarkPlaced() => IsPlaced = true;
    }
}
