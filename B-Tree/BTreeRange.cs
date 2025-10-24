using System.Collections.Generic;

namespace B_Tree
{
    public static class BTreeRangeExtensions
    {
        // Convenience overloads for Range with open/closed options if you want later.
        public static IEnumerable<KeyValuePair<K, V>> RangeClosed<K, V>(this BTree<K, V> tree, K fromKey, K toKey)
            => tree.Range(fromKey, toKey);
    }
}