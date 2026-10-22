using System;

namespace QuackUp.Utils
{
    public readonly struct TilingCell : IEquatable<TilingCell>, IComparable<TilingCell>
    {
        public int Row { get; }
        public int Column { get; }

        public TilingCell(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public bool Equals(TilingCell other) => Row == other.Row && Column == other.Column;
        public override bool Equals(object obj) => obj is TilingCell other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Row, Column);
        public int CompareTo(TilingCell other) => Row != other.Row
            ? Row.CompareTo(other.Row)
            : Column.CompareTo(other.Column);
        public static bool operator ==(TilingCell left, TilingCell right) => left.Equals(right);
        public static bool operator !=(TilingCell left, TilingCell right) => !left.Equals(right);
    }
}
