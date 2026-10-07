using System;
using System.Collections.Generic;

namespace FitMe.Grid
{
    internal static class ContactChain
    {
        internal static void Collect<T>(
            T start,
            Func<T, IEnumerable<T>> getAdjacent,
            Predicate<T> canConnect,
            List<T> contactedItems)
        {
            contactedItems.Add(start);
            CollectAdjacent(start, getAdjacent, canConnect, contactedItems);
        }

        private static void CollectAdjacent<T>(
            T current,
            Func<T, IEnumerable<T>> getAdjacent,
            Predicate<T> canConnect,
            List<T> contactedItems)
        {
            foreach (var adjacent in getAdjacent(current))
            {
                if (!canConnect(adjacent) || contactedItems.Contains(adjacent))
                    continue;

                contactedItems.Add(adjacent);
                CollectAdjacent(adjacent, getAdjacent, canConnect, contactedItems);
            }
        }
    }
}
