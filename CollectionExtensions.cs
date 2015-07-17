using System.Collections.Generic;

namespace HistoryExplorerHelper
{
    public static class CollectionExtensions
    {
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> toAdd)
        {
            foreach (var item in toAdd)
                collection.Add(item);
        }
    }
}
