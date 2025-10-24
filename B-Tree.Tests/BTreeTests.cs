using B_Tree;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace UnitTestProject1
{
    public class BTreeBasicTests
    {
        [Theory]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        public void Insert_Get_Works_For_Small_Set(int t)
        {
            var tree = new BTree<int, string>(t);
            tree.AddOrUpdate(10, "a").Should().BeTrue();
            tree.AddOrUpdate(20, "b").Should().BeTrue();
            tree.AddOrUpdate(5, "c").Should().BeTrue();

            tree.TryGet(10, out var v10).Should().BeTrue(); v10.Should().Be("a");
            tree.TryGet(20, out var v20).Should().BeTrue(); v20.Should().Be("b");
            tree.TryGet(5, out var v5).Should().BeTrue(); v5.Should().Be("c");

            tree.TryGet(7, out var _).Should().BeFalse();
            tree.Count.Should().Be(3);
        }

        [Fact]
        public void Updating_Key_Overwrites_Value_And_Does_Not_Change_Count()
        {
            var t = new BTree<int, string>(t: 8);
            t.AddOrUpdate(1, "one").Should().BeTrue();
            t.AddOrUpdate(1, "uno").Should().BeFalse();

            t.Count.Should().Be(1);
            t.TryGet(1, out var v).Should().BeTrue();
            v.Should().Be("uno");
        }

        [Fact]
        public void Many_Inserts_Cause_Splits_And_Order_Is_Sorted()
        {
            var rnd = new Random(12345);
            var t = new BTree<int, int>(t: 16);

            foreach(var _ in Enumerable.Range(0, 2000))
            {
                int k = rnd.Next(0, 5000);
                t.AddOrUpdate(k, k * 10);
            }

            int prev = int.MinValue;
            foreach (var kv in t)
            {
                kv.Key.Should().BeGreaterThanOrEqualTo(prev);
                kv.Value.Should().Be(kv.Key * 10);
                prev = kv.Key;
            }
        }

        [Fact]
        public void Works_With_Custom_Comparer()
        {
            var ci = StringComparer.OrdinalIgnoreCase;
            var t = new BTree<string, int>(t:8, comparer: ci);

            t.AddOrUpdate("Key", 1).Should().BeTrue();
            t.AddOrUpdate("Key", 2).Should().BeFalse();

            t.Count.Should().Be(1);
            t.TryGet("KEY", out var v).Should().BeTrue();
            v.Should().Be(2);
        }
    }
}
