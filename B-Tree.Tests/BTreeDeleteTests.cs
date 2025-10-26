using B_Tree;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace UnitTestProject1
{
    public class BTreeDeleteTests
    {
        [Fact]
        public void Delete_From_Leaf_Works_And_Count_Drops()
        {
            var t = new BTree<int, string>(t: 4);
            t.AddOrUpdate(1, "a");
            t.AddOrUpdate(2, "b");
            t.AddOrUpdate(3, "c");

            t.Delete(2).Should().BeTrue();
            t.Count.Should().Be(2);
            t.ContainsKey(2).Should().BeFalse();

            t.Delete(999).Should().BeFalse();
            t.Count.Should().Be(2);
        }

        [Fact]
        public void Delete_Internal_Key_Replaces_With_Pred_Or_Succ()
        {
            var t = new BTree<int, int>(t: 4);
            // build a few levels
            foreach(var k in Enumerable.Range(1, 50))
            {
                t.AddOrUpdate(k, k);
            }

            t.Delete(25).Should().BeTrue();
            t.ContainsKey(25).Should().BeFalse();

            // sanity check ordering still void
            int prev = int.MinValue;
            foreach(var kv in t)
            {
                kv.Key.Should().BeGreaterThanOrEqualTo(prev);
                kv.Value.Should().Be(kv.Key);
                prev = kv.Key;
            }
        }

        [Fact]
        public void Borrow_And_Merge_Are_Triggered_As_Needed()
        {
            var t = new BTree<int, int>(t: 4);
            foreach(var k in Enumerable.Range(1, 200))
            {
                t.AddOrUpdate(k, k);
            }

            // Delete a swath that will force underflows and merges
            foreach(var k in Enumerable.Range(1, 150))
            {
                t.Delete(k).Should().BeTrue();
                t.Validate();
            }

            // Remaining keys 151 .. 200 should be intact and ordered
            var keys = t.Select(kv => kv.Key).ToArray();
            keys.Should().Equal(Enumerable.Range(151, 50));
        }

        [Fact]
        public void Randomized_Delete_Matches_SortedDictionary()
        {
            var rnd = new Random(2025);
            var t = new BTree<int, int>(t: 8);
            var dict = new SortedDictionary<int, int>();

            // Insert with dublicates (updates)
            foreach (var _ in Enumerable.Range(0, 2000))
            {
                int k = rnd.Next(0, 4000);
                int v = k * 7;
                t.AddOrUpdate(k, v);
                dict[k] = v;
            }

            // Compare iteration before deletes
            t.Select(p => p.Key).Should().Equal(dict.Keys);

            // Random deletes
            var keys = dict.Keys.ToList();
            keys = keys.OrderBy(_ => rnd.Next()).ToList();

            int deleted = 0;
            foreach (var k in keys.Take(keys.Count/2))
            {
                var okT = t.Delete(k);
                t.Validate();
                var okD = dict.Remove(k);
                okT.Should().Be(okD);
                if (okT) deleted++;
            }
            
                // Compare iteration after deletes
                t.Select(p => p.Key).Should().Equal(dict.Keys);
            t.Count.Should().Be(dict.Count);
        }

        [Fact]
        public void Remove_Returns_Old_Value()
        {
            var t = new BTree<int, string>(t: 4);
            t.AddOrUpdate(10, "ten");
            t.Remove(10, out var old).Should().BeTrue();
            old.Should().Be("ten");
            t.ContainsKey(10).Should().BeFalse();
        }
    }
}