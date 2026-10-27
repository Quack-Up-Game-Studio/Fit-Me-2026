using System.Collections.Generic;
using UnityEngine;

namespace FitMe.Grid
{
    /// <summary>Initial presentation only; never called when inventory is consumed or rearranged.</summary>
    public static class ShapeTrayLayout
    {
        public static ShapeTrayPiece[] Create(IReadOnlyList<ShapeBlockSupplyQueue.Item> items, Rect bounds,
            float requestedScale, float tiltMin, float tiltMax, float atomEdgeRadius)
        {
            var pieces = new ShapeTrayPiece[items.Count];
            if (items.Count == 0) return pieces;
            // Runtime entries return a defensive matrix clone. Read each once,
            // not once per candidate/sample in the dense coverage search.
            var schemas = new int[items.Count][,];
            for (var i = 0; i < items.Count; i++)
            {
                schemas[i] = items[i].PlayerBlock.Schema;
                pieces[i] = new ShapeTrayPiece(i, schemas[i], bounds, requestedScale,
                    Random.Range(Mathf.Min(tiltMin, tiltMax), Mathf.Max(tiltMin, tiltMax)), items[i].Rotation, bounds.center);
            }

            // Keep standalone constructor safety, but apply its strictest cap to
            // every shape. All further reductions are supply-wide too.
            var commonScale = pieces[0].Scale;
            foreach (var piece in pieces) commonScale = Mathf.Min(commonScale, piece.Scale);
            RebuildAtScale(pieces, schemas, bounds, commonScale);
            var columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(items.Count * bounds.width / bounds.height)));
            if (items.Count <= 6)
            {
                // Choose the grid that preserves the greatest fraction of requested size.
                var bestRatio = -1f;
                for (var candidate = 1; candidate <= items.Count; candidate++)
                {
                    var height = bounds.height / Mathf.CeilToInt((float)items.Count / candidate);
                    var ratio = 1f;
                    foreach (var piece in pieces)
                    {
                        var size = piece.PresentationBounds.size;
                        ratio = Mathf.Min(ratio, bounds.width / candidate / size.x, height / size.y);
                    }
                    if (ratio <= bestRatio) continue;
                    bestRatio = ratio;
                    columns = candidate;
                }
                if (bestRatio < 1f)
                {
                    commonScale *= bestRatio * .999f;
                    RebuildAtScale(pieces, schemas, bounds, commonScale);
                }
            }
            var rows = Mathf.CeilToInt((float)items.Count / columns);
            var cellSize = new Vector2(bounds.width / columns, bounds.height / rows);
            var cells = new List<int>();
            for (var i = 0; i < columns * rows; i++) cells.Add(i);
            for (var i = cells.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
            for (var i = 0; i < items.Count; i++)
            {
                var piece = pieces[i];
                var center = bounds.min + new Vector2((cells[i] % columns + .5f) * cellSize.x, (cells[i] / columns + .5f) * cellSize.y);
                if (items.Count <= 6)
                {
                    var slack = Vector2.Max(Vector2.zero, cellSize - piece.PresentationBounds.size);
                    center += new Vector2(Random.Range(-.4f, .4f) * slack.x, Random.Range(-.4f, .4f) * slack.y);
                }
                piece.DropInTray(center);
                pieces[i] = piece;
            }
            if (items.Count >= 7)
            {
                var exposed = false;
                // Bounded initial-layout retries, never rescaling live inventory.
                for (var reduction = 0; reduction < 48; reduction++)
                {
                    if (TryExposeDensePieces(pieces, schemas, bounds, atomEdgeRadius))
                    {
                        exposed = true;
                        break;
                    }
                    if (reduction == 47) break;
                    commonScale *= .85f;
                    RebuildAtScale(pieces, schemas, bounds, commonScale);
                }
                if (!exposed)
                {
                    PackSeparated(pieces, schemas, bounds);
                    ValidatePickablePatches(pieces, schemas, bounds, atomEdgeRadius);
                }
            }
            return pieces;
        }

        private static void RebuildAtScale(ShapeTrayPiece[] pieces, IReadOnlyList<int[,]> schemas,
            Rect bounds, float scale)
        {
            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                pieces[i] = new ShapeTrayPiece(i, schemas[i], bounds, scale,
                    piece.Tilt, piece.GameplayRotation, piece.Position);
            }
        }

        private static bool TryExposeDensePieces(ShapeTrayPiece[] pieces,
            IReadOnlyList<int[,]> schemas, Rect bounds, float atomEdgeRadius)
        {
            var visiblePoints = new List<List<Vector2>>();
            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                // Retain a patch of a real atom on every lower-sorted piece, not
                // merely a distinct pivot. Failure retries the entire supply.
                var accepted = false;
                var bestPosition = piece.Position;
                var bestScore = -1f;
                for (var attempt = 0; attempt < 256; attempt++)
                {
                    if (attempt > 0)
                        piece.DropInTray(new Vector2(Random.Range(bounds.xMin, bounds.xMax), Random.Range(bounds.yMin, bounds.yMax)));
                    var preservesTargets = true;
                    var score = 0f;
                    foreach (var points in visiblePoints)
                    {
                        var remaining = 0;
                        foreach (var point in points)
                            if (!CoversAtom(piece, schemas[i], point, atomEdgeRadius)) remaining++;
                        if (remaining == 0)
                        {
                            preservesTargets = false;
                            break;
                        }
                        score += (float)remaining / points.Count;
                    }
                    if (!preservesTargets || score <= bestScore) continue;
                    bestScore = score;
                    bestPosition = piece.Position;
                    accepted = true;
                }
                if (!accepted) return false;
                piece.DropInTray(bestPosition);
                foreach (var points in visiblePoints)
                    points.RemoveAll(p => CoversAtom(piece, schemas[i], p, atomEdgeRadius));
                visiblePoints.Add(AtomPoints(piece, schemas[i]));
                pieces[i] = piece;
            }
            return true;
        }

        private static void ValidatePickablePatches(ShapeTrayPiece[] pieces,
            IReadOnlyList<int[,]> schemas, Rect bounds, float atomEdgeRadius)
        {
            for (var lower = 0; lower < pieces.Length; lower++)
            {
                var targets = AtomPoints(pieces[lower], schemas[lower]);
                for (var higher = lower + 1; higher < pieces.Length; higher++)
                {
                    var occluder = higher;
                    targets.RemoveAll(p => CoversAtom(pieces[occluder], schemas[occluder], p, atomEdgeRadius));
                }
                if (targets.Count == 0)
                    throw new System.InvalidOperationException(
                        $"Shape Tray usable bounds {bounds.size} cannot preserve a pickable atom patch for all {pieces.Length} pieces with the current collider radius; enlarge the tray usable bounds or reduce the authored supply.");
            }
        }

        private static List<Vector2> AtomPoints(ShapeTrayPiece piece, int[,] schema)
        {
            var points = new List<Vector2>();
            for (var row = 0; row < schema.GetLength(0); row++)
            for (var column = 0; column < schema.GetLength(1); column++)
            {
                if (schema[row, column] == 0) continue;
                for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                {
                    var local = new Vector3(column - schema.GetLength(1) * .5f + .5f + x * .25f,
                        schema.GetLength(0) * .5f - .5f - row + y * .25f);
                    points.Add(piece.Position + (Vector2)(piece.TrayRotation * local) * piece.Scale);
                }
            }
            return points;
        }

        private static bool CoversAtom(ShapeTrayPiece piece, int[,] schema, Vector2 point, float atomEdgeRadius)
        {
            var local = (Vector2)(Quaternion.Inverse(piece.TrayRotation) * (point - piece.Position)) / piece.Scale;
            // The existing BlockView also has a unit hit box at the pivot.
            if (Mathf.Abs(local.x) < .55f && Mathf.Abs(local.y) < .55f) return true;
            // The collider edge radius is configured in block-local units and
            // scales with the block while it rests in the tray.
            var hitHalfSize = .55f + atomEdgeRadius;
            for (var row = 0; row < schema.GetLength(0); row++)
            for (var column = 0; column < schema.GetLength(1); column++)
                if (schema[row, column] != 0 &&
                    Mathf.Abs(local.x - (column - schema.GetLength(1) * .5f + .5f)) < hitHalfSize &&
                    Mathf.Abs(local.y - (schema.GetLength(0) * .5f - .5f - row)) < hitHalfSize)
                    return true;
            return false;
        }

        private static void PackSeparated(ShapeTrayPiece[] pieces, IReadOnlyList<int[,]> schemas, Rect bounds)
        {
            var columns = Mathf.CeilToInt(Mathf.Sqrt(pieces.Length * bounds.width / bounds.height));
            var cell = new Vector2(bounds.width / columns, bounds.height / Mathf.CeilToInt((float)pieces.Length / columns));
            var ratio = 1f;
            foreach (var piece in pieces)
                ratio = Mathf.Min(ratio, cell.x / piece.PresentationBounds.width, cell.y / piece.PresentationBounds.height);
            RebuildAtScale(pieces, schemas, bounds, pieces[0].Scale * ratio * .6f);
            for (var i = 0; i < pieces.Length; i++)
            {
                pieces[i].DropInTray(bounds.min + new Vector2((i % columns + .5f) * cell.x, (i / columns + .5f) * cell.y));
            }
        }
    }
}
