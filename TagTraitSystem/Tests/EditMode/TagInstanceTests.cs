using System;
using System.Reflection;
using NUnit.Framework;
using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Definitions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TagTraitSystem.Tests.EditMode
{
    public sealed class TagInstanceTests
    {
        private TagDefinition tagDefinition;

        [TearDown]
        public void TearDown()
        {
            if (tagDefinition != null)
            {
                Object.DestroyImmediate(tagDefinition);
                tagDefinition = null;
            }
        }

        [Test]
        public void TagInstance_WhenPermanentCreated_InitializesExpectedState()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();

            TagInstance instance = new TagInstance(tagDefinition);

            Assert.AreSame(tagDefinition, instance.Definition);
            Assert.AreEqual(0f, instance.Duration);
            Assert.AreEqual(0f, instance.RemainingTime);
            Assert.AreEqual(1, instance.StackCount);
            Assert.IsFalse(instance.IsPerishable);
        }

        [Test]
        public void TagInstance_WhenPerishableCreated_InitializesExpectedState()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();

            TagInstance instance = new TagInstance(tagDefinition, 2.5f);

            Assert.AreSame(tagDefinition, instance.Definition);
            Assert.AreEqual(2.5f, instance.Duration);
            Assert.AreEqual(2.5f, instance.RemainingTime);
            Assert.AreEqual(1, instance.StackCount);
            Assert.IsTrue(instance.IsPerishable);
        }

        [Test]
        public void TagInstance_WhenDurationHasUnnormalizedValue_PreservesInput()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();

            TagInstance instance = new TagInstance(tagDefinition, 1.25f);

            Assert.AreEqual(1.25f, instance.Duration);
            Assert.AreEqual(1.25f, instance.RemainingTime);
        }

        [Test]
        public void DecreaseRemainingTime_WhenCalled_DecreasesRemainingTimeOnly()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            TagInstance instance = new TagInstance(tagDefinition, 2.5f);

            instance.DecreaseRemainingTime(0.75f);

            Assert.AreEqual(2.5f, instance.Duration);
            Assert.AreEqual(1.75f, instance.RemainingTime);
            Assert.AreEqual(1, instance.StackCount);
            Assert.IsTrue(instance.IsPerishable);
        }

        [Test]
        public void DecreaseRemainingTime_WhenResultIsBelowZero_ClampsToZero()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            TagInstance instance = new TagInstance(tagDefinition, 1f);

            instance.DecreaseRemainingTime(2f);

            Assert.AreEqual(1f, instance.Duration);
            Assert.AreEqual(0f, instance.RemainingTime);
        }

        [Test]
        public void TryConvertToPermanent_WhenPerishable_ConvertsAndReturnsTrue()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            TagInstance instance = new TagInstance(tagDefinition, 2.5f);

            Assert.IsTrue(instance.TryConvertToPermanent());

            Assert.AreSame(tagDefinition, instance.Definition);
            Assert.AreEqual(0f, instance.Duration);
            Assert.AreEqual(0f, instance.RemainingTime);
            Assert.AreEqual(1, instance.StackCount);
            Assert.IsFalse(instance.IsPerishable);
        }

        [Test]
        public void TryConvertToPermanent_WhenAlreadyPermanent_ReturnsFalseWithoutChanges()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            TagInstance instance = new TagInstance(tagDefinition);

            Assert.IsFalse(instance.TryConvertToPermanent());

            Assert.AreSame(tagDefinition, instance.Definition);
            Assert.AreEqual(0f, instance.Duration);
            Assert.AreEqual(0f, instance.RemainingTime);
            Assert.AreEqual(1, instance.StackCount);
            Assert.IsFalse(instance.IsPerishable);
        }

        [Test]
        public void TryRefreshDuration_WhenPerishable_RefreshesAndReturnsTrue()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            TagInstance instance = new TagInstance(tagDefinition, 1f);

            Assert.IsTrue(instance.TryRefreshDuration(2.5f));

            Assert.AreSame(tagDefinition, instance.Definition);
            Assert.AreEqual(2.5f, instance.Duration);
            Assert.AreEqual(2.5f, instance.RemainingTime);
            Assert.AreEqual(1, instance.StackCount);
            Assert.IsTrue(instance.IsPerishable);
        }

        [Test]
        public void TryRefreshDuration_WhenPermanent_ReturnsFalseWithoutChanges()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            TagInstance instance = new TagInstance(tagDefinition);

            Assert.IsFalse(instance.TryRefreshDuration(2.5f));

            Assert.AreSame(tagDefinition, instance.Definition);
            Assert.AreEqual(0f, instance.Duration);
            Assert.AreEqual(0f, instance.RemainingTime);
            Assert.AreEqual(1, instance.StackCount);
            Assert.IsFalse(instance.IsPerishable);
        }

        [Test]
        public void TryExtendDurationToMax_WhenDurationExtends_UpdatesDurationAndRemainingTime()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            TagInstance instance = new TagInstance(tagDefinition, 2f);
            instance.DecreaseRemainingTime(1f);

            Assert.IsTrue(instance.TryExtendDurationToMax(3f, instance.RemainingTime, 0.0001f));

            Assert.AreEqual(3f, instance.Duration);
            Assert.AreEqual(3f, instance.RemainingTime);
            Assert.AreEqual(1, instance.StackCount);
            Assert.IsTrue(instance.IsPerishable);
        }

        [Test]
        public void TryExtendDurationToMax_WhenStoredDurationIsLonger_UpdatesRemainingTimeOnly()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            TagInstance instance = new TagInstance(tagDefinition, 9f);
            instance.DecreaseRemainingTime(7f);

            Assert.IsTrue(instance.TryExtendDurationToMax(3f, instance.RemainingTime, 0.0001f));

            Assert.AreEqual(9f, instance.Duration);
            Assert.AreEqual(3f, instance.RemainingTime);
        }

        [Test]
        public void TryExtendDurationToMax_WhenNoActualExtension_ReturnsFalseWithoutChanges()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            TagInstance instance = new TagInstance(tagDefinition, 9f);
            instance.DecreaseRemainingTime(5f);

            Assert.IsFalse(instance.TryExtendDurationToMax(3f, instance.RemainingTime, 0.0001f));

            Assert.AreEqual(9f, instance.Duration);
            Assert.AreEqual(4f, instance.RemainingTime);
        }

        [Test]
        public void TryExtendDurationToMax_WhenPermanent_ReturnsFalseWithoutChanges()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            TagInstance instance = new TagInstance(tagDefinition);

            Assert.IsFalse(instance.TryExtendDurationToMax(3f, 3f, 0.0001f));

            Assert.AreEqual(0f, instance.Duration);
            Assert.AreEqual(0f, instance.RemainingTime);
            Assert.IsFalse(instance.IsPerishable);
        }

        [Test]
        public void TryIncreaseStackCount_WhenBelowMaximum_IncreasesAndReturnsTrue()
        {
            tagDefinition = CreateDefinitionWithMaxStackCount(2);
            TagInstance instance = new TagInstance(tagDefinition);

            Assert.IsTrue(instance.TryIncreaseStackCount());

            Assert.AreEqual(2, instance.StackCount);
        }

        [Test]
        public void TryIncreaseStackCount_WhenAtMaximum_ReturnsFalseWithoutChanges()
        {
            tagDefinition = CreateDefinitionWithMaxStackCount(1);
            TagInstance instance = new TagInstance(tagDefinition);

            Assert.IsFalse(instance.TryIncreaseStackCount());

            Assert.AreEqual(1, instance.StackCount);
        }

        [Test]
        public void TryIncreaseStackCount_WhenMaximumIsInvalid_ReturnsFalseWithoutChanges()
        {
            tagDefinition = CreateDefinitionWithMaxStackCount(0);
            TagInstance instance = new TagInstance(tagDefinition);

            Assert.IsFalse(instance.TryIncreaseStackCount());

            Assert.AreEqual(1, instance.StackCount);
        }

        [Test]
        public void TryIncreasePerishableStackCount_WhenBelowMaximum_IncreasesAndResetsRemainingTime()
        {
            tagDefinition = CreateDefinitionWithMaxStackCount(2);
            TagInstance instance = new TagInstance(tagDefinition, 3f);
            instance.DecreaseRemainingTime(1f);

            Assert.IsTrue(instance.TryIncreasePerishableStackCount());

            Assert.AreEqual(2, instance.StackCount);
            Assert.AreEqual(3f, instance.RemainingTime);
            Assert.IsTrue(instance.IsPerishable);
        }

        [Test]
        public void TryIncreasePerishableStackCount_WhenPermanent_ReturnsFalseWithoutChanges()
        {
            tagDefinition = CreateDefinitionWithMaxStackCount(2);
            TagInstance instance = new TagInstance(tagDefinition);

            Assert.IsFalse(instance.TryIncreasePerishableStackCount());

            Assert.AreEqual(1, instance.StackCount);
            Assert.AreEqual(0f, instance.RemainingTime);
        }

        [Test]
        public void TryDecreaseStackCount_WhenAboveOne_DecreasesAndReturnsTrue()
        {
            tagDefinition = CreateDefinitionWithMaxStackCount(2);
            TagInstance instance = new TagInstance(tagDefinition);
            Assert.IsTrue(instance.TryIncreaseStackCount());

            Assert.IsTrue(instance.TryDecreaseStackCount());

            Assert.AreEqual(1, instance.StackCount);
        }

        [Test]
        public void TryDecreaseStackCount_WhenAtOne_ReturnsFalseWithoutChanges()
        {
            tagDefinition = CreateDefinitionWithMaxStackCount(2);
            TagInstance instance = new TagInstance(tagDefinition);

            Assert.IsFalse(instance.TryDecreaseStackCount());

            Assert.AreEqual(1, instance.StackCount);
        }

        [Test]
        public void TryDecreasePerishableStackCount_WhenAboveOne_DecreasesAndResetsRemainingTime()
        {
            tagDefinition = CreateDefinitionWithMaxStackCount(2);
            TagInstance instance = new TagInstance(tagDefinition, 3f);
            Assert.IsTrue(instance.TryIncreasePerishableStackCount());
            instance.DecreaseRemainingTime(1f);

            Assert.IsTrue(instance.TryDecreasePerishableStackCount());

            Assert.AreEqual(1, instance.StackCount);
            Assert.AreEqual(3f, instance.RemainingTime);
            Assert.IsTrue(instance.IsPerishable);
        }

        [Test]
        public void TryDecreasePerishableStackCount_WhenAtOne_ReturnsFalseWithoutChanges()
        {
            tagDefinition = CreateDefinitionWithMaxStackCount(2);
            TagInstance instance = new TagInstance(tagDefinition, 3f);
            instance.DecreaseRemainingTime(1f);

            Assert.IsFalse(instance.TryDecreasePerishableStackCount());

            Assert.AreEqual(1, instance.StackCount);
            Assert.AreEqual(2f, instance.RemainingTime);
        }

        [Test]
        public void TagInstance_WhenCreated_DoesNotCopyMaxStackCountState()
        {
            tagDefinition = CreateDefinitionWithMaxStackCount(5);
            TagInstance instance = new TagInstance(tagDefinition);

            FieldInfo[] fields = typeof(TagInstance).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                Assert.AreNotEqual("maxStackCount", fields[i].Name);
            }

            Assert.AreEqual(1, instance.StackCount);
        }

        [Test]
        public void TagInstance_WhenPermanentDefinitionIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TagInstance(null));
        }

        [Test]
        public void TagInstance_WhenPerishableDefinitionIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TagInstance(null, 1f));
        }

        private static TagDefinition CreateDefinitionWithMaxStackCount(int maxStackCount)
        {
            TagDefinition definition = ScriptableObject.CreateInstance<TagDefinition>();
            SerializedObject serializedObject = new SerializedObject(definition);
            SerializedProperty maxStackCountProperty = serializedObject.FindProperty("maxStackCount");
            Assert.IsNotNull(maxStackCountProperty);
            maxStackCountProperty.intValue = maxStackCount;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }
    }
}
