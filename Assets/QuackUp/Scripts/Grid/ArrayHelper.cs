using System;
using System.Collections.Generic;
using System.Linq;
using QuackUp.Utils;
using UnityEngine;

namespace FitMe.Grid
{
    public static class ArrayHelper
    {
        public static void PrintSchema(int[,] schema)
        {
            string schemaString = "\n";
            for (int i = 0; i < schema.GetLength(0); i++)
            {
                for (int j = 0; j < schema.GetLength(1); j++)
                {
                    schemaString += schema[i, j] + " ";
                }
                schemaString += "\n";
            }
            DebugUtils.Log(schemaString);
        }
        
        public static Vector2Int GetArraySize<T>(this T[,] array)
        {
            return new Vector2Int(array.GetLength(1), array.GetLength(0));
        }
        
        public static List<(T, Vector2Int)> FlattenWithArrayIndex<T>(this T[,] array)
        {
            var result = new List<(T, Vector2Int)>();
            for (var row = 0; row < array.GetLength(0); row++)
            for (var column = 0; column < array.GetLength(1); column++)
            {
                var item = array[row, column];
                result.Add((item, new Vector2Int(row, column)));
            }
            return result;
        }
        
        public static bool IsTheSameShape(List<Vector2Int> shapeA, List<Vector2Int> shapeB)
        {
            var aCount = shapeA.Count;
            var bCount = shapeB.Count;
            if (aCount != bCount)
            {
                DebugUtils.Log($"Shape counts differ: A: {aCount}, B: {bCount}");
                return false;
            }
            if (aCount == 1 && bCount == 1)
            {
                return true;
            }
            var sortedA = shapeA.OrderBy(x => x.x).ThenBy(x => x.y).ToList();
            var sortedB = shapeB.OrderBy(x => x.x).ThenBy(x => x.y).ToList();
            var firstA = sortedA[0];
            var firstB = sortedB[0];
            for (var i = 1; i < sortedA.Count; i++)
            {
                var offsetA = sortedA[i] - firstA;
                var offsetB = sortedB[i] - firstB;
                if (offsetA != offsetB)
                {
                    return false;
                }
            }
            return true;
        }
        
        public static Vector2Int RotateIndexBySchema(Vector2Int index, int schemaIndex, Vector2Int arraySize)
        {
            return schemaIndex switch
            {
                0 => index,
                1 => RotateIndex270(index, arraySize),
                2 => RotateIndex180(index, arraySize),
                3 => RotateIndex90(index, arraySize),
                _ => throw new ArgumentOutOfRangeException(nameof(schemaIndex), "Schema index must be between 0 and 3.")
            };
        }
    
        public static int[,] Rotate90(int[,] array)
        {
            int rows = array.GetLength(0);
            int cols = array.GetLength(1);
            int[,] rotatedArray = new int[cols, rows];

            // Rotate the array 90 degrees clockwise
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    rotatedArray[j, rows - 1 - i] = array[i, j];
                }
            }

            return rotatedArray;
        }
        
        public static Vector2Int RotateIndex90(Vector2Int index, Vector2Int arraySize)
        {
            // Rotating (x, y) 90 degrees clockwise in an array of size (rows, cols)
            int newX = index.y;
            int newY = arraySize.x - 1 - index.x;
            return new Vector2Int(newX, newY);
        }

        public static int[,] Rotate180(int[,] array)
        {
            int rows = array.GetLength(0);
            int cols = array.GetLength(1);
            int[,] rotatedArray = new int[rows, cols];

            // Rotate 180 degrees
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    // Map (i, j) to the rotated position
                    rotatedArray[rows - 1 - i, cols - 1 - j] = array[i, j];
                }
            }

            return rotatedArray;
        }
        
        public static Vector2Int RotateIndex180(Vector2Int index, Vector2Int arraySize)
        {
            // Rotating (x, y) 180 degrees in an array of size (rows, cols)
            int newX = arraySize.x - 1 - index.x;
            int newY = arraySize.y - 1 - index.y;
            return new Vector2Int(newX, newY);
        }
    
        public static int[,] Rotate270(int[,] array)
        {
            int rows = array.GetLength(0);
            int cols = array.GetLength(1);
            int[,] rotatedArray = new int[cols, rows];

            // Rotate the array 90 degrees counterclockwise
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    rotatedArray[cols - 1 - j, i] = array[i, j];
                }
            }

            return rotatedArray;
        }
        
        public static Vector2Int RotateIndex270(Vector2Int index, Vector2Int arraySize)
        {
            // Rotating (x, y) 270 degrees clockwise (or 90 degrees counterclockwise) in an array of size (rows, cols)
            int newX = arraySize.y - 1 - index.y;
            int newY = index.x;
            return new Vector2Int(newX, newY);
        }

        public static bool CanBFitInA(int[,] a, int[,] b, out int[,] placedArray, bool simulatePlacement = false)
        {
            int rowsA = a.GetLength(0);
            int colsA = a.GetLength(1);
            int rowsB = b.GetLength(0);
            int colsB = b.GetLength(1);
            placedArray = a;

            // Check if B can even fit into A
            if (rowsB > rowsA || colsB > colsA) 
                return false;

            // Slide a window over A and compare
            for (int i = 0; i <= rowsA - rowsB; i++)
            {
                for (int j = 0; j <= colsA - colsB; j++)
                {
                    if (!CompareMemberBToA(a, b, i, j, out var tempPlacedArray, simulatePlacement)) continue;
                    placedArray = simulatePlacement ? tempPlacedArray : a; // If not simulating, return original array
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Perform element-by-element comparison of member B to member A at position (ax, ay).
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="ax"></param>
        /// <param name="ay"></param>
        /// <param name="placedArray"></param>
        /// <param name="simulatePlacement"></param>
        /// <returns></returns>
        private static bool CompareMemberBToA(int[,] a, int[,] b, int ax, int ay, out int[,] placedArray, bool simulatePlacement = false)
        {
            int rowsA = a.GetLength(0);
            int colsA = a.GetLength(1);
            int rowsB = b.GetLength(0);
            int colsB = b.GetLength(1);
            placedArray = a;
            var tempPlacedArray = a.Clone() as int[,];

            if (rowsB > rowsA || colsB > colsA)
            {
                return false; // B cannot fit in A
            }
            
            // Compare each element
            for (int i = 0; i < rowsB; i++)
            {
                for (int j = 0; j < colsB; j++)
                {
                    if (b[i, j] != 0 && a[i + ax, j + ay] != b[i, j])
                    {
                        return false;
                    }
                    if (!simulatePlacement) continue;
                    // If simulating placement, mark the position in placedArray
                    if (tempPlacedArray != null) tempPlacedArray[i + ax, j + ay] = 0;
                }
            }
            placedArray = simulatePlacement ? tempPlacedArray : a; // If not simulating, return original array
            return true;
        }

        public static int CountMember(this int[,] array, Func<int, bool> predicate)
        {
            int count = 0;
            for (int i = 0; i < array.GetLength(0); i++)
            {
                for (int j = 0; j < array.GetLength(1); j++)
                {
                    if (predicate(array[i, j]))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        public static void ResizeArrayKeepMembers<T>(ref T[,] array, Vector2Int newSize, T defaultValue = default)
        {
            var newArray = new T[newSize.y, newSize.x];
            var oldRow = array.GetLength(0);
            var oldColumn = array.GetLength(1);
            var newRow = newArray.GetLength(0);
            var newColumn = newArray.GetLength(1);
            for (int x = 0; x < newRow; x++)
            {
                for (int y = 0; y < newColumn; y++)
                {
                    if (x >= newRow || y >= newColumn)
                    {
                        continue;
                    }
                    if (x >= oldRow || y >= oldColumn)
                    {
                        newArray[x, y] = defaultValue;
                        continue;
                    }
                    if (x < newRow && y < newSize.x)
                    {
                        newArray[x, y] = array[x, y];
                    }
                    else
                    {
                        newArray[x, y] = defaultValue;
                    }
                }
            }
            array = newArray;
        }
    }
}
