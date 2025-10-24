using B_Tree;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace UnitTestProject1
{
    public class BTreeRangeTests
    {
        [Fact]
        public void Range_Returns_Closed_Interval()
        {
            var t = new BTree<int, int>(t: 8);
            foreach (var k in Enumerable.Range(1, 20))
            {
                t.AddOrUpdate(k, k);
            }

            var range = t.Range(5, 10).Select(kv => kv.Key).ToArray();
            range.Should().Equal(5,6,7,8,9,10);
        }

        [Fact]
        public void Range_Empty_When_From_Greater_Than_To()
        {
            var t = new BTree<int, int>(t: 8);
            foreach (var k in Enumerable.Range(1, 10))
            {
                t.AddOrUpdate(k, k);
            }

            t.Range(8, 4).Should().BeEmpty();
        }

        [Fact]
        public void Range_Singleton_When_From_Equals_To_And_Key_Exists()
        {
            var t = new BTree<int, string>(t: 8);
            t.AddOrUpdate(42, "x");

            var xs = t.Range(42,42).Select(p  => p.Value).ToArray();
            xs.Should().Equal("x");
        }

        [Fact]
        public void Range_Skips_Nonexistent_Keys()
        {
            var t = new BTree<int, int>(t: 8);
            foreach(var k in new[] { 1, 3, 4, 8, 9 })
            {
                t.AddOrUpdate(k, k);
            }

            var res = t.Range(2, 8).Select(kv => kv.Key).ToArray();
            res.Should().Equal(3, 4, 8);
        }
    }
}