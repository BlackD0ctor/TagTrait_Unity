using NUnit.Framework;
using TagTraitSystem.Runtime.Core;

namespace TagTraitSystem.Tests.EditMode
{
    public sealed class EnumDefinitionTests
    {
        [Test]
        public void TagID_Values_AreFixed()
        {
            Assert.AreEqual(0, (int)TagID.None);
            Assert.AreEqual(1, (int)TagID.TestAlpha);
            Assert.AreEqual(2, (int)TagID.TestBeta);
            Assert.AreEqual(3, (int)TagID.TestGamma);
        }

        [Test]
        public void TagID_SampleValues_AreFixed()
        {
            Assert.AreEqual(100, (int)TagID.SampleKeyword);
            Assert.AreEqual(101, (int)TagID.SamplePermanent);
            Assert.AreEqual(102, (int)TagID.SampleRefresh);
            Assert.AreEqual(103, (int)TagID.SampleMaxDuration);
            Assert.AreEqual(104, (int)TagID.SampleStackCount);
        }

        [Test]
        public void TagCategory_Values_AreFixed()
        {
            Assert.AreEqual(0, (int)TagCategory.None);
            Assert.AreEqual(1, (int)TagCategory.Keyword);
            Assert.AreEqual(2, (int)TagCategory.Status);
        }

        [Test]
        public void StackPolicy_Values_AreFixed()
        {
            Assert.AreEqual(0, (int)StackPolicy.None);
            Assert.AreEqual(1, (int)StackPolicy.MaxDuration);
            Assert.AreEqual(2, (int)StackPolicy.Refresh);
            Assert.AreEqual(3, (int)StackPolicy.StackCount);
        }

        [Test]
        public void TagChangeReason_Values_AreFixed()
        {
            Assert.AreEqual(0, (int)TagChangeReason.Added);
            Assert.AreEqual(1, (int)TagChangeReason.Removed);
            Assert.AreEqual(2, (int)TagChangeReason.DurationRefreshed);
            Assert.AreEqual(3, (int)TagChangeReason.DurationExtendedToMax);
            Assert.AreEqual(4, (int)TagChangeReason.StackIncreased);
            Assert.AreEqual(5, (int)TagChangeReason.StackDecreased);
            Assert.AreEqual(6, (int)TagChangeReason.ChangedToPermanent);
            Assert.AreEqual(7, (int)TagChangeReason.Expired);
        }

        [Test]
        public void ExclusiveGroup_None_IsZero()
        {
            Assert.AreEqual(0, (int)ExclusiveGroup.None);
        }
    }
}
