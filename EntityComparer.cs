using System.Collections.Generic;
using FrameLog.Contexts;

namespace HistoryExplorerHelper
{
    internal class EntityComparer : IEqualityComparer<object>
    {
        private readonly IHistoryContext context;

        public EntityComparer(IHistoryContext context)
        {
            this.context = context;
        }

        public new bool Equals(object x, object y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            if (x.GetType() != y.GetType()) return false;
            if (context.ObjectHasReference(x) && context.ObjectHasReference(y))
            {
                return context.GetReferenceForObject(x).Equals(context.GetReferenceForObject(y));
            }
            return x.Equals(y);
        }

        public int GetHashCode(object obj)
        {
            return obj.GetHashCode();
        }
    }
}
