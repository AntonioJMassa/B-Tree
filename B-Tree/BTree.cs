#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;

namespace B_Tree
{
    /// <summary>
    /// Generic B-Tree (not B+). Stores keys in internal nodes and leaves.
    /// Supports: TryGet, AddOrUpdate, ContainsKey, Count, in-order iteration, range enumeration.
    /// Insert is fully implemented with node splits. Delete is scaffolded (TODO).
    /// </summary>
    /// <typeparam name="K"></typeparam> Key
    /// <typeparam name="V"></typeparam> Value
    public class BTree<K, V> : IEnumerable<KeyValuePair<K, V>>
    {
        /// <summary>
        /// Default minimum degree (T). Max keys per node = 2*T - 1.
        /// Tuned for in-RAM usage (fan-out ~ 64).
        /// </summary>
        public const int DefaultMinDegree = 32;

        /// <summary>
        /// Minimum degree (T). Max keys per node = 2 * T - 1, min (non-root) = T - 1.
        /// </summary>
        public int T { get; }

        /// <summary> Total number of distinct keys stored. </summary>
        public int Count { get; private set; }

        /// <summary>
        /// The comparer used to order keys.
        /// </summary>
        public IComparer<K> Comparer { get; }

        /// <summary>
        /// Maximum number of keys a node can hold (derived from T).
        /// </summary>
        public int MaxKeys => 2 * T - 1;

        /// <summary>
        /// True if the tree is emplty
        /// </summary>
        public bool IsEmpty => Count == 0;

        private BTreeNode<K, V> _root;

        /// <summary>
        /// Create a B-Tree with the given minimum degree (T).
        /// </summary>
        /// <param name="t"> Minimum degree (T). Must be &gt;=2. </param>
        /// <param name="comparer"> Optional comparer for key ordering. </param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public BTree(int t = DefaultMinDegree, IComparer<K>? comparer = null)
        {
            if (t < 2) throw new ArgumentOutOfRangeException(nameof(t), "Minimum degree T must be >= 2.");
            T = t;
            Comparer = comparer ?? Comparer<K>.Default;
            _root = new BTreeNode<K, V>(isLeaf: true, capacity: 2 * T - 1);
            Count = 0;
        }

        public bool ContainsKey(K key) => TryGet(key, out _);

        public bool TryGet(K key, out V value)
        {
            var x = _root;
            while (true)
            {
                int i = x.LowerBound(key, Comparer);
                if (i < x.KeyCount && Comparer.Compare(x.Keys[i], key) == 0)
                {
                    value = x.Values[i];
                    return true;
                }

                if (x.IsLeaf)
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
            if (_root.KeyCount == 2 * T - 1)
            {
                var s = new BTreeNode<K, V>(isLeaf: false, capacity: 2 * T - 1);
                s.Children.Add(_root);
                SplitChild(s, 0); // splits child 0 into two and promotes median
                _root = s;
            }

            bool added = InsertNonFull(_root, key, value);
            if (added) Count++;
            return added;
        }

        /// <summary>
        /// Returns the current height (levels) of the tree, counting the root level as 1.
        /// </summary>
        public int Height()
        {
            int h = 1;
            var x = _root;
            while (!x.IsLeaf)
            {
                h++;
                x = x.Children[0];
            }
            return h;
        }

        /// <summary>
        /// Remove a key if present. Returns true if a key was removed.
        /// </summary>
        public bool Delete(K key)
        {
            if (!_Delete(_root, key)) return false;

            // If the root lost all keys and is an internal node, shrink height.
            if (!_root.IsLeaf && _root.KeyCount == 0)
            {
                _root = _root.Children[0];
            }

            Count--;
            return true;
        }

        /// <summary>
        /// Remove a key and return its previous value if present.
        /// </summary>
        public bool Remove(K key, out V oldValue)
        {
            if (!_Delete(_root, key, out oldValue))
            {
                oldValue = default!;
                return false;
            }

            if (!_root.IsLeaf && _root.KeyCount == 0)
            {
                _root = _root.Children[0];
            }

            Count--;
            return true;
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
                    // left subtree
                    foreach (var kv in InOrder(x.Children[i]))
                    {
                        yield return kv;
                    }

                    // separator key in this internal node
                    yield return new KeyValuePair<K, V>(x.Keys[i], x.Values[i]);
                }

                // rightmost subtree
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
            if (Comparer.Compare(fromKey, toKey) > 0) yield break;
            foreach (var kv in RangeInternal(_root, fromKey, toKey))
            {
                yield return kv;
            }
        }

        private IEnumerable<KeyValuePair<K, V>> RangeInternal(BTreeNode<K, V> x, K fromKey, K toKey)
        {
            if (x.IsLeaf)
            {
                for (int i = 0; i < x.KeyCount; i++)
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
                    if (Comparer.Compare(k, toKey) > 0)
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

            if (x.IsLeaf)
            {
                // Find position to insert or update
                int pos = x.LowerBound(key, Comparer);
                if (pos < x.KeyCount && Comparer.Compare(x.Keys[pos], key) == 0)
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
                int idx = x.LowerBound(key, Comparer);

                if(idx < x.KeyCount && Comparer.Compare(x.Keys[idx], key) == 0)
                {
                    x.Values[idx] = value;
                    return false;
                }

                int childIndex = idx;
                var child = x.Children[childIndex];

                // If child is full, split and decide which side to go
                if (child.KeyCount == 2 * T - 1)
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

        // ----- Delete internals (CLRS-shaped, adapted to node layout) ------

        private bool _Delete(BTreeNode<K, V> x, K key) => _Delete(x, key, out _);

        private bool _Delete(BTreeNode<K, V> x, K key, out V removed)
        {
            int i = x.LowerBound(key, Comparer);

            // Case A: key is in this node
            if (i < x.KeyCount && Comparer.Compare(x.Keys[i], key) == 0)
            {
                if (x.IsLeaf)
                {
                    //A1: Leaf = remove key/value directly
                    removed = x.Values[i];
                    x.Keys.RemoveAt(i);
                    x.Values.RemoveAt(i);
                    return true;
                }

                // A2: internal = need predecessor/successor or merg
                var left = x.Children[i];
                var right = x.Children[i + 1];

                if (left.KeyCount >= T)
                {
                    // Replace with predecessor (max from left subtree), the delete there
                    var (pk, pv) = _MaxInSubtree(left);
                    removed = x.Values[i];
                    x.Keys[i] = pk;
                    x.Values[i] = pv;
                    return _DeleteEnsureChildThenRecurse(x, i, pk, out _);
                }

                if (right.KeyCount >= T)
                {
                    // Replace with successor (min from right subtree), then delete there
                    var (sk, sv) = _MinInSubtree(right);
                    removed = x.Values[i];
                    x.Keys[i] = sk;
                    x.Values[i] = sv;
                    return _DeleteEnsureChildThenRecurse(x, i + 1, sk, out _);
                }

                // Both children have T-1 - merge: Left + key + right into left, then delete in merged
                _MergeChildren(x, i);
                return _Delete(left, key, out removed);
            }
            else
            {
                // Case B: key is NOT in this node
                if (x.IsLeaf)
                {
                    removed = default!;
                    return false; //not found
                }
            }

            // child index to descend
            int ci = i;
            ci = _EnsureChildHasAtLeastT(x, ci);

            // After fix-up, if we merged with the right sibling, the current key might now live in child at ci
            // But if fix-up merged child with the left sibling (when ci > 0 and Left had T-1), we need to adjust ci-1
            //if (ci > x.KeyCount)
            //{
            //    ci = x.KeyCount; // after merging last two children
            //}

            return _Delete(x.Children[ci], key, out removed);
        }

        private int _EnsureChildHasAtLeastT(BTreeNode<K, V> x, int i)
        {
            var c = x.Children[i];
            if (c.KeyCount >= T) return i; // already ok

            // Try borrow from left sibling
            if (i > 0 && x.Children[i - 1].KeyCount >= T)
            {
                _BorrowFromPrev(x, i);
                return i;
            }

            // Try borrow from right sibling
            if (i < x.KeyCount && x.Children[i + 1].KeyCount >= T)
            {
                _BorrowFromNext(x, i);
                return i;
            }

            // Otherwise merge with a sibling
            if (i < x.KeyCount)
            {
                _MergeChildren(x, i); // merge child i with i+1 into i
                return i;
            }
            else
            {
                _MergeChildren(x, i - 1); // merge child i-1 with i into i-1, shift descend index later
                return i - 1;
            }
        }

        // Borrow one key from left sibling into child i
        private void _BorrowFromPrev(BTreeNode<K, V> x, int i)
        {
            var child = x.Children[i];
            var left = x.Children[i - 1];

            //Bring down separator key from x into child's front
            child.Keys.Insert(0, x.Keys[i - 1]);
            child.Values.Insert(0, x.Values[i - 1]);

            // If child is not leaf, move the left's last child pointer as first
            if (!child.IsLeaf)
            {
                child.Children.Insert(0, left.Children[left.Children.Count - 1]);
                left.Children.RemoveAt(left.Children.Count - 1);
            }

            // Move left's last key ujp to x as new separator
            x.Keys[i - 1] = left.Keys[left.KeyCount - 1];
            x.Values[i - 1] = left.Values[left.Values.Count - 1];

            // Remove from left
            left.Keys.RemoveAt(left.KeyCount - 1);
            left.Values.RemoveAt(left.Values.Count - 1);
        }

        // Borrow one key from right sibling into child i
        private void _BorrowFromNext(BTreeNode<K, V> x, int i)
        {
            var child = x.Children[i];
            var right = x.Children[i + 1];

            // Bring down separator key from x into child's end
            child.Keys.Add(x.Keys[i]);
            child.Values.Add(x.Values[i]);

            // If child is not leat, move right's first child as last
            if (!child.IsLeaf)
            {
                child.Children.Add(right.Children[0]);
                right.Children.RemoveAt(0);
            }

            // Move right's first key up to x as new separator
            x.Keys[i] = right.Keys[0];
            x.Values[i] = right.Values[0];

            // Remove from right
            right.Keys.RemoveAt(0);
            right.Values.RemoveAt(0);
        }

        // Merge child i and i+1 into child i, pulling down x.keys[i]/Values[i] in between
        private void _MergeChildren(BTreeNode<K, V> x, int i)
        {
            var left = x.Children[i];
            var right = x.Children[i + 1];

            // Pull down separator
            left.Keys.Add(x.Keys[i]);
            left.Values.Add(x.Values[i]);

            // Append right's keys/values
            if (right.KeyCount > 0)
            {
                left.Keys.AddRange(right.Keys);
                left.Values.AddRange(right.Values);
            }

            // Append children if internal
            if (!left.IsLeaf)
            {
                left.Children.AddRange(right.Children);
            }

            // Remove separator and right child from x
            x.Keys.RemoveAt(i);
            x.Values.RemoveAt(i);
            x.Children.RemoveAt(i + 1);
        }

        //Helper: after replacing with predecessor/successor, ensure child ready and recurse
        private bool _DeleteEnsureChildThenRecurse(BTreeNode<K, V> parent, int childIndex, K key, out V removed)
        {
            childIndex = _EnsureChildHasAtLeastT(parent, childIndex);
            
            //if(childIndex> parent.KeyCount)
            //{
            //    childIndex = parent.KeyCount;
            //}
            return _Delete(parent.Children[childIndex], key, out removed);
        }

        // Find max (predecessor) pair in subtree
        private (K key, V val) _MaxInSubtree(BTreeNode<K, V> x)
        {
            while (!x.IsLeaf)
            {
                x = x.Children[x.KeyCount]; // right most child
            }

            int idx = x.KeyCount - 1;
            return (x.Keys[idx], x.Values[idx]);
        }

        // Find min (successor) pair in subtree
        private (K key, V val) _MinInSubtree(BTreeNode<K, V> x)
        {
            while(!x.IsLeaf)
            {
                x = x.Children[0];
            }

            return (x.Keys[0], x.Values[0]);
        }

        /// <summary>
        /// Verifies B-Tree invariants. Throws InvalidOperationException on the first violation.
        /// - For non-root nodes: T-1 <= keys <= 2T-1
        /// - If internal: children == keys + 1
        /// - Keys in each node are sorted
        /// - Cross-node ordering holds: max(left) <= key[i] <= min(right)
        /// </summary>
        public void Validate()
        {
            void Check(BTreeNode<K, V> n, bool isRoot)
            {
                // 1) Key-count bounds
                if (!isRoot)
                {
                    if (n.KeyCount < T - 1 || n.KeyCount > 2 * T - 1)
                        throw new InvalidOperationException("Key count bounds broken.");
                }
                else
                {
                    // Root may be leaf with 0..(2T-1) keys, or internal with >=1 key
                    if (!n.IsLeaf && n.KeyCount == 0)
                        throw new InvalidOperationException("Internal root has zero keys.");
                    if (n.KeyCount > 2 * T - 1)
                        throw new InvalidOperationException("Root exceeds maximum key count.");
                }

                // 2) If internal: child count must be keys+1
                if (!n.IsLeaf)
                {
                    if (n.Children.Count != n.KeyCount + 1)
                        throw new InvalidOperationException("Child count != keys + 1.");
                }

                // 3) Keys in this node must be sorted
                for (int i = 1; i < n.KeyCount; i++)
                {
                    if (Comparer.Compare(n.Keys[i - 1], n.Keys[i]) > 0)
                        throw new InvalidOperationException("Keys not sorted in node.");
                }

                // 4) Cross-node ordering around each separator
                if (!n.IsLeaf)
                {
                    for (int i = 0; i < n.KeyCount; i++)
                    {
                        var left = n.Children[i];
                        var right = n.Children[i + 1];
                        var sep = n.Keys[i];

                        // leftMax <= sep
                        K leftMax = left.IsLeaf
                            ? left.Keys[left.KeyCount - 1]
                            : _MaxInSubtree(left).key;

                        if (Comparer.Compare(leftMax, sep) > 0)
                            throw new InvalidOperationException("Left child range exceeds separator.");

                        // sep <= rightMin
                        K rightMin = right.IsLeaf
                            ? right.Keys[0]
                            : _MinInSubtree(right).key;

                        if (Comparer.Compare(sep, rightMin) > 0)
                            throw new InvalidOperationException("Right child range below separator.");
                    }
                }

                // 5) Recurse
                if (!n.IsLeaf)
                {
                    for (int i = 0; i < n.Children.Count; i++)
                        Check(n.Children[i], isRoot: false);
                }
            }

            Check(_root, isRoot: true);
        }
    }
}