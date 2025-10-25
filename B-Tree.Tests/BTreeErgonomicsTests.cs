using B_Tree;
using FluentAssertions;
using System;
using Xunit;

namespace UnitTestProject1
{
    public class BTreeErgonomicsTests
    {
        [Fact]
        public void DefaultCtor_Uses_DefaultMinDegree_And_Computes_MaxKeys()
        {
            var t = new BTree<int, int>();
            t.T.Should().Be(BTree<int,int>.DefaultMinDegree);
            t.MaxKeys.Should().Be(2 * t.T - 1);
            t.IsEmpty.Should().BeTrue();
            t.Height().Should().Be(1);
        }

        [Fact]
        public void Guardrails_Invalid_T_Throws()
        {
            var act = () => new BTree<int, int>(t: 1);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Height_Grows_As_Inserts_Cause_Splits()
        {
            var t = new BTree<int, int>(t: 4); // small T to force splits early
            t.Height().Should().Be(1);

            for(int i = 0; i < 50; i++)
            {
                t.AddOrUpdate(i, i);
            }

            t.IsEmpty.Should().BeFalse();
            t.Height().Should().BeGreaterThan(1); // should have grown beyond single level
        }
    }
}