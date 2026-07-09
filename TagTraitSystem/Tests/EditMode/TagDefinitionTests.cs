using NUnit.Framework;
using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Definitions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TagTraitSystem.Tests.EditMode
{
    public sealed class TagDefinitionTests
    {
        private TagDefinition tagDefinition;
        private TestTagTraitSO trait;

        [TearDown]
        public void TearDown()
        {
            if (tagDefinition != null)
            {
                Object.DestroyImmediate(tagDefinition);
                tagDefinition = null;
            }

            if (trait != null)
            {
                Object.DestroyImmediate(trait);
                trait = null;
            }
        }

        [Test]
        public void TagDefinition_DefaultValues_AreExpected()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();

            Assert.AreEqual(TagID.None, tagDefinition.TagID);
            Assert.AreEqual(string.Empty, tagDefinition.TagName);
            Assert.AreEqual(TagCategory.None, tagDefinition.Category);
            Assert.IsNull(tagDefinition.Trait);
            Assert.AreEqual(StackPolicy.None, tagDefinition.StackPolicy);
            Assert.AreEqual(1, tagDefinition.MaxStackCount);
            Assert.AreEqual(ExclusiveGroup.None, tagDefinition.ExclusiveGroup);
            Assert.AreEqual(0, tagDefinition.Priority);
            Assert.IsFalse(tagDefinition.IsSaveable);
            Assert.IsFalse(tagDefinition.IsKeywordTag);
        }

        [Test]
        public void TagDefinition_WhenSerializedFieldsAreSet_PreservesValues()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            trait = ScriptableObject.CreateInstance<TestTagTraitSO>();

            SetSerializedFields(
                tagDefinition,
                TagID.TestAlpha,
                "Alpha",
                TagCategory.Status,
                trait,
                StackPolicy.StackCount,
                3,
                ExclusiveGroup.None,
                7,
                true);

            Assert.AreEqual(TagID.TestAlpha, tagDefinition.TagID);
            Assert.AreEqual("Alpha", tagDefinition.TagName);
            Assert.AreEqual(TagCategory.Status, tagDefinition.Category);
            Assert.AreSame(trait, tagDefinition.Trait);
            Assert.AreEqual(StackPolicy.StackCount, tagDefinition.StackPolicy);
            Assert.AreEqual(3, tagDefinition.MaxStackCount);
            Assert.AreEqual(ExclusiveGroup.None, tagDefinition.ExclusiveGroup);
            Assert.AreEqual(7, tagDefinition.Priority);
            Assert.IsTrue(tagDefinition.IsSaveable);
        }

        [Test]
        public void TagDefinition_WhenCreated_HasDefaultMaxStackCountOfOne()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();

            Assert.AreEqual(1, tagDefinition.MaxStackCount);
        }

        [Test]
        public void TagDefinition_WhenMaxStackCountIsSerialized_ReturnsConfiguredValue()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            SerializedObject serializedObject = new SerializedObject(tagDefinition);
            SerializedProperty maxStackCountProperty = serializedObject.FindProperty("maxStackCount");
            Assert.IsNotNull(maxStackCountProperty);

            maxStackCountProperty.intValue = 5;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(5, tagDefinition.MaxStackCount);
        }

        [Test]
        public void TagDefinition_WhenOtherStackPolicyHasConfiguredMax_PreservesValue()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();

            SetSerializedFields(
                tagDefinition,
                TagID.TestAlpha,
                "Alpha",
                TagCategory.Status,
                null,
                StackPolicy.Refresh,
                4,
                ExclusiveGroup.None,
                0,
                false);

            Assert.AreEqual(StackPolicy.Refresh, tagDefinition.StackPolicy);
            Assert.AreEqual(4, tagDefinition.MaxStackCount);
        }

        [Test]
        public void IsKeywordTag_WhenCategoryIsKeyword_ReturnsTrue()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            SetCategory(tagDefinition, TagCategory.Keyword);

            Assert.IsTrue(tagDefinition.IsKeywordTag);
        }

        [Test]
        public void IsKeywordTag_WhenCategoryIsNotKeywordAndTraitIsNull_ReturnsFalse()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            SetCategory(tagDefinition, TagCategory.Status);

            Assert.IsFalse(tagDefinition.IsKeywordTag);
        }

        [Test]
        public void IsKeywordTag_WhenKeywordHasTrait_ReturnsTrue()
        {
            tagDefinition = ScriptableObject.CreateInstance<TagDefinition>();
            trait = ScriptableObject.CreateInstance<TestTagTraitSO>();

            SetSerializedFields(
                tagDefinition,
                TagID.TestBeta,
                "Keyword",
                TagCategory.Keyword,
                trait,
                StackPolicy.None,
                1,
                ExclusiveGroup.None,
                0,
                false);

            Assert.IsTrue(tagDefinition.IsKeywordTag);
        }

        private static void SetCategory(TagDefinition target, TagCategory category)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty categoryProperty = serializedObject.FindProperty("category");
            Assert.IsNotNull(categoryProperty);

            categoryProperty.enumValueIndex = (int)category;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedFields(
            TagDefinition target,
            TagID tagID,
            string tagName,
            TagCategory category,
            TestTagTraitSO trait,
            StackPolicy stackPolicy,
            int maxStackCount,
            ExclusiveGroup exclusiveGroup,
            int priority,
            bool isSaveable)
        {
            SerializedObject serializedObject = new SerializedObject(target);

            SetEnumProperty(serializedObject, "tagID", (int)tagID);
            SetStringProperty(serializedObject, "tagName", tagName);
            SetEnumProperty(serializedObject, "category", (int)category);
            SetObjectProperty(serializedObject, "trait", trait);
            SetEnumProperty(serializedObject, "stackPolicy", (int)stackPolicy);
            SetIntProperty(serializedObject, "maxStackCount", maxStackCount);
            SetEnumProperty(serializedObject, "exclusiveGroup", (int)exclusiveGroup);
            SetIntProperty(serializedObject, "priority", priority);
            SetBoolProperty(serializedObject, "isSaveable", isSaveable);

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnumProperty(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.IsNotNull(property);
            property.enumValueIndex = value;
        }

        private static void SetStringProperty(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.IsNotNull(property);
            property.stringValue = value;
        }

        private static void SetObjectProperty(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.IsNotNull(property);
            property.objectReferenceValue = value;
        }

        private static void SetIntProperty(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.IsNotNull(property);
            property.intValue = value;
        }

        private static void SetBoolProperty(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.IsNotNull(property);
            property.boolValue = value;
        }
    }
}
