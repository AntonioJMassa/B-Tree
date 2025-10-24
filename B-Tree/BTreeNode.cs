using System.Collections.Generic;

namespace B_Tree
{
    internal sealed class BTreeNode<K, V>
    {
        public readonly List<K> Keys;
        public readonly List<V> Values;
        public readonly List<BTreeNode<K, V>> Children;
        public bool IsLeaf;

        public BTreeNode(bool isLeaf, int capacity)
        {
            IsLeaf = isLeaf;
            Keys = new List<K>(capacity);
            Values = new List<V>(capacity);
            Children = new List<BTreeNode<K, V>>(capacity + 1);
        }

        public int KeyCount => Keys.Count;

        public int LowerBound(K target, IComparer<K> comparer)
        {
            int lo = 0, hi = Keys.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if(comparer.Compare(Keys[mid], target) < 0) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }
    }
}