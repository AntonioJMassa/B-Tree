#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace B_Tree
{
    public class BTree<K,V> : IEnumerable<KeyValuePair<K, V>>
    {
        public int T { get; }
        public int Count { get; private set; }
        public IComparer<K> Comparer { get; }

        private BTreeNode<K,V> _root;

        public BTree(int t = 32, IComparer<K>? comparer = null)
        {
            if(t< 2) throw new ArgumentOutOfRangeException(nameof(t), "Minimum degree T must be >= 2." );
            T = t;
            Comparer = comparer ?? Comparer<K>.Default;
            _root = new BTreeNode<K, V>(isLeaf: true, capacity: 2 * T - 1);
            Count = 0;
        }

        public bool ContainsKey(K key) => TryGet(key, out _);

        public bool TryGet(K key, out V value)
        {
            var x = _root;
            while(true)
            {
                int i = x.LowerBound(key, Comparer);
                if(i < x.KeyCount && Comparer.Compare(x.Keys[i], key) == 0)
                {
                    value = x.Values[i];
                    return true;
                }

                if(x.IsLeaf)
                {
                    value = default!;
                    return false;
                }

                x = x.Children[i];
            }
        }

        /// <summary>
        /// Insert or update a key/value
        /// Returns true if a new key was added; false if an existing key was updated;
        /// </summary>
        public bool AddOrUpdate(K key, V value)
        {
            // If root is full, grow tree height by splitting root.
            if(_root.KeyCount == 2 * T - 1)
            {
                var s = new BTreeNode<K, V>(isLeaf: false, capacity: 2 * T - 1);
                s.Children.Add(_root);
                SplitChild(s, 0); // splits child 0 into two and promotes median
                _root = s;
            }

            bool added = InsertNonFull(_root, key, value);
            if(added) Count++;
            return added;
        }

        /// <summary>
        /// TODO: Full delete (borrow/merge) - scaffold only.
        /// </summary>
        public bool Delete(K key)
        {
            throw new NotImplementedException("Delete not implemented yet.");
        }

        /// <summary>
        /// Enumerate all key/value pairs in sorted key order (in-order traversal).
        /// </summary>
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return InOrder(_root).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private IEnumerable<KeyValuePair<K, V>> InOrder(BTreeNode<K, V> x)
        {
            if (x.IsLeaf)
            {
                for (int i = 0; i < x.KeyCount; i++)
                {
                    yield return new KeyValuePair<K, V>(x.Keys[i], x.Values[i]);
                }
            }
            else
            {
                int n = x.KeyCount;
                for (int i = 0; i < n; i++)
                {
                    foreach (var kv in InOrder(x.Children[i]))
                    {
                        yield return kv;
                    }
                }
                foreach (var kv in InOrder(x.Children[n]))
                {
                    yield return kv;
                }
            }
        }

        /// <summary>
        /// Enumerate key/value pairs where fromKey &lt;= key &lt;= toKey (inclusive bounds).
        /// </summary>
        public IEnumerable<KeyValuePair<K, V>> Range(K fromKey, K toKey)
        {
            if(Comparer.Compare(fromKey, toKey) > 0) yield break;
            foreach (var kv in RangeInternal(_root, fromKey, toKey))
            {
                yield return kv;
            }
        }

        private IEnumerable<KeyValuePair<K, V>> RangeInternal(BTreeNode<K, V> x, K fromKey, K toKey)
        {
            if(x.IsLeaf)
            {
                for(int i = 0; i < x.KeyCount;i++)
                {
                    var k = x.Keys[i];
                    if (Comparer.Compare(k, fromKey) < 0) continue;
                    if (Comparer.Compare(k, toKey) > 0) break;
                    yield return new KeyValuePair<K, V>(k, x.Values[i]);
                }
            }
            else
            {
                int i = x.LowerBound(fromKey, Comparer);
                // Visit child i (keys < Keys[i]), then climb while keys <= to
                for (int j = Math.Max(0, i - 1); j < x.KeyCount; j++)
                {
                    // Left Child
                    foreach (var kv in RangeInternal(x.Children[j], fromKey, toKey))
                    {
                        yield return kv;
                    }

                    var k = x.Keys[j];
                    if (Comparer.Compare(k, fromKey) >= 0 && Comparer.Compare(k, toKey) <= 0)
                    {
                        yield return new KeyValuePair<K, V>(k, x.Values[j]);
                    }
                    if(Comparer.Compare(k, toKey) > 0)
                    {
                        yield break;
                    }
                }
                // Rightmost Child
                foreach (var kv in RangeInternal(x.Children[x.KeyCount], fromKey, toKey))
                {
                    yield return kv;
                }
            }
        }

        // ----- Insert Helpers -----

        // Insert into node x that is known to be non-full.
        // Returns true if a new key was inserted, false if updated.
        private bool InsertNonFull(BTreeNode<K, V> x, K key, V value)
        {
            int i = x.KeyCount - 1;

            if(x.IsLeaf)
            {
                // Find position to insert or update
                int pos = x.LowerBound(key, Comparer);
                if(pos < x.KeyCount && Comparer.Compare(x.Keys[pos], key) == 0)
                {
                    // Update existing
                    x.Values[pos] = value;
                    return false;
                }

                // Insert new key/value
                x.Keys.Insert(pos, key);
                x.Values.Insert(pos, value);
                return true;
            }
            else
            {
                // Find child to descend
                int childIndex = x.LowerBound(key, Comparer);
                var child = x.Children[childIndex];

                // If child is full, split and decide which side to go
                if(child.KeyCount == 2 * T - 1)
                {
                    SplitChild(x, childIndex);
                    // After split, the median moved up to x at childIndex
                    int cmp = Comparer.Compare(key, x.Keys[childIndex]);
                    if (cmp > 0) childIndex++;
                    else if (cmp == 0)
                    {
                        // Equal to promoted median; update at x
                        x.Values[childIndex] = value;
                        return false;
                    }
                }

                return InsertNonFull(x.Children[childIndex], key, value);
            }
        }

        // Split child y = x.Children[i] into y(left) and z(right), promote median into x.
        private void SplitChild(BTreeNode<K, V> x, int i)
        {
            var y = x.Children[i];
            var z = new BTreeNode<K, V>(isLeaf: y.IsLeaf, capacity: 2 * T - 1);

            // Median index in y
            int m = T - 1;

            // Cache median (must be done before we mutate y)
            var medianKey = y.Keys[m];
            var medianValue = y.Values[m];

            // ---- move right half keys/values to z ----
            // right side begins after median
            int rightKeyStart = m + 1;
            int rightKeyCount = y.Keys.Count - rightKeyStart;

            if (rightKeyCount > 0)
            {
                z.Keys.AddRange(y.Keys.GetRange(rightKeyStart, rightKeyCount));
                z.Values.AddRange(y.Values.GetRange(rightKeyStart, rightKeyCount));

                y.Keys.RemoveRange(rightKeyStart, rightKeyCount);
                y.Values.RemoveRange(rightKeyStart, rightKeyCount);
            }

            if (!y.IsLeaf)
            {
                int rightChildStart = m + 1;
                int rightChildCount = y.Children.Count - rightChildStart;

                if (rightChildCount > 0)
                {
                    z.Children.AddRange(y.Children.GetRange(rightChildStart, rightChildCount));
                    y.Children.RemoveRange(rightChildStart, rightChildCount);
                }
            }

            // Promote median to parent x
            x.Keys.Insert(i, medianKey);
            x.Values.Insert(i, medianValue);

            // Insert new right child z
            x.Children.Insert(i + 1, z);

            // Remove median from y (now left node)
            y.Keys.RemoveAt(m);
            y.Values.RemoveAt(m);
        }
    }
}