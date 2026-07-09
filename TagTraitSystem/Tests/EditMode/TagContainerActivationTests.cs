using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TagTraitSystem.Runtime.Components;
using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Definitions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TagTraitSystem.Tests.EditMode
{
    public sealed class TagContainerActivationTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerActivationTests");
            createdObjects.Add(gameObject);
            container = gameObject.AddComponent<TagContainer>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            gameObject = null;
            container = null;
        }

        [Test]
        public void TagActivate_WhenHeldNonKeywordTraitReturnsTrue_ReturnsTrue()
        {
            RecordingTagTraitSO trait = CreateTrait();
            trait.ActivateResult = true;
            TagDefinition definition = AddHeldTag(TagID.TestAlpha, "Alpha", trait);

            Assert.IsTrue(container.TagActivate(definition));
            Assert.AreEqual(1, trait.OnActivateCallCount);
        }

        [Test]
        public void TagActivate_WhenHeldNonKeywordTraitReturnsFalse_ReturnsFalse()
        {
            RecordingTagTraitSO trait = CreateTrait();
            trait.ActivateResult = false;
            TagDefinition definition = AddHeldTag(TagID.TestAlpha, "Alpha", trait);

            Assert.IsFalse(container.TagActivate(definition));
            Assert.AreEqual(1, trait.OnActivateCallCount);
        }

        [Test]
        public void TagActivate_WhenInvoked_ReceivesExpectedContext()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddHeldTag(TagID.TestAlpha, "Alpha", trait);
            GameObject source = CreateSourceObject();
            Assert.IsTrue(container.TryGetTag(definition, out TagInstance instance));

            Assert.IsTrue(container.TagActivate(definition, source));

            Assert.AreSame(container, trait.LastOnActivateContext.Container);
            Assert.AreSame(gameObject, trait.LastOnActivateContext.Target);
            Assert.AreSame(source, trait.LastOnActivateContext.Source);
            Assert.AreSame(definition, trait.LastOnActivateContext.Definition);
            Assert.AreSame(instance, trait.LastOnActivateContext.Instance);
        }

        [Test]
        public void TagActivate_WhenSourceIsNull_PreservesNullSource()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddHeldTag(TagID.TestAlpha, "Alpha", trait);

            Assert.IsTrue(container.TagActivate(definition, null));

            Assert.IsNull(trait.LastOnActivateContext.Source);
        }

        [Test]
        public void TagActivate_WhenSourceIsSpecified_PreservesSource()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddHeldTag(TagID.TestAlpha, "Alpha", trait);
            GameObject source = CreateSourceObject();

            Assert.IsTrue(container.TagActivate(definition, source));

            Assert.AreSame(source, trait.LastOnActivateContext.Source);
        }

        [Test]
        public void TagActivate_WhenInvoked_DoesNotRaiseChangeEvents()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddHeldTag(TagID.TestAlpha, "Alpha", trait);
            int changeEventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { changeEventCount++; };

            Assert.IsTrue(container.TagActivate(definition));

            Assert.AreEqual(0, changeEventCount);
        }

        [Test]
        public void TagActivate_WhenDefinitionIsNull_ReturnsFalseAndLogsError()
        {
            LogAssert.Expect(LogType.Error, new Regex("null tag definition"));

            Assert.IsFalse(container.TagActivate(null));
        }

        [Test]
        public void TagActivate_WhenTagIDIsNone_ReturnsFalseAndLogsError()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.None, "None", TagCategory.Status, trait);
            LogAssert.Expect(LogType.Error, new Regex("TagID\\.None"));

            Assert.IsFalse(container.TagActivate(definition));
        }

        [Test]
        public void TagActivate_WhenTagNameIsEmpty_ReturnsFalseAndLogsError()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, string.Empty, TagCategory.Status, trait);
            LogAssert.Expect(LogType.Error, new Regex("empty name"));

            Assert.IsFalse(container.TagActivate(definition));
        }

        [Test]
        public void TagActivate_WhenTagIsNotHeld_ReturnsFalseWithoutLog()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);

            Assert.IsFalse(container.TagActivate(definition));
            LogAssert.NoUnexpectedReceived();
            Assert.AreEqual(0, trait.OnActivateCallCount);
        }

        [Test]
        public void TagActivate_WhenKeywordHasNoTrait_ReturnsFalseWithoutLog()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, null);

            Assert.IsFalse(container.TagActivate(definition));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TagActivate_WhenKeywordHasTrait_ReturnsFalseLogsErrorAndDoesNotInvokeTrait()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, trait);
            LogAssert.Expect(LogType.Error, new Regex("Keyword"));

            Assert.IsFalse(container.TagActivate(definition));

            Assert.AreEqual(0, trait.OnActivateCallCount);
        }

        [Test]
        public void TagActivate_WhenNonKeywordTraitIsNull_ReturnsFalseAndLogsError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, null);
            LogAssert.Expect(LogType.Error, new Regex("Non-keyword"));

            Assert.IsFalse(container.TagActivate(definition));
        }

        [Test]
        public void TagActivate_WhenDifferentDefinitionHasSameID_ReturnsFalseAndLogsError()
        {
            RecordingTagTraitSO firstTrait = CreateTrait();
            RecordingTagTraitSO secondTrait = CreateTrait();
            TagDefinition storedDefinition = AddHeldTag(TagID.TestAlpha, "Alpha", firstTrait);
            TagDefinition queryDefinition = CreateDefinition(TagID.TestAlpha, "Alpha Other", TagCategory.Status, secondTrait);
            LogAssert.Expect(LogType.Error, new Regex("same TagID"));

            Assert.IsFalse(container.TagActivate(queryDefinition));

            Assert.IsTrue(container.TagCheck(storedDefinition));
            Assert.AreEqual(0, secondTrait.OnActivateCallCount);
        }

        [Test]
        public void TagActivate_WhenTraitThrows_ReturnsFalseLogsErrorAndKeepsState()
        {
            RecordingTagTraitSO trait = CreateTrait();
            trait.ThrowOnActivate = true;
            TagDefinition definition = AddHeldTag(TagID.TestAlpha, "Alpha", trait);
            LogAssert.Expect(LogType.Error, new Regex("OnActivate"));

            Assert.IsFalse(container.TagActivate(definition));

            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void OnActivate_WhenSameIDTagAddIsRequested_Blocks()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddHeldTag(TagID.TestAlpha, "Alpha", trait);
            bool reentrantResult = true;
            trait.OnActivateAction = delegate(TraitContext context)
            {
                reentrantResult = container.TagAdd(definition);
            };

            Assert.IsTrue(container.TagActivate(definition));

            Assert.IsFalse(reentrantResult);
        }

        [Test]
        public void OnActivate_WhenSameIDTagSubIsRequested_Blocks()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddHeldTag(TagID.TestAlpha, "Alpha", trait);
            bool reentrantResult = true;
            trait.OnActivateAction = delegate(TraitContext context)
            {
                reentrantResult = container.TagSub(definition);
            };

            Assert.IsTrue(container.TagActivate(definition));

            Assert.IsFalse(reentrantResult);
            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void OnActivate_WhenSameIDTagActivateIsRequested_Blocks()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddHeldTag(TagID.TestAlpha, "Alpha", trait);
            bool reentrantResult = true;
            trait.OnActivateAction = delegate(TraitContext context)
            {
                reentrantResult = container.TagActivate(definition);
            };

            Assert.IsTrue(container.TagActivate(definition));

            Assert.IsFalse(reentrantResult);
        }

        [Test]
        public void OnActivate_WhenDifferentIDMutationIsRequested_AllowsOperation()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            RecordingTagTraitSO betaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddHeldTag(TagID.TestAlpha, "Alpha", alphaTrait);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta, "Beta", TagCategory.Status, betaTrait);
            bool betaAddResult = false;
            alphaTrait.OnActivateAction = delegate(TraitContext context)
            {
                betaAddResult = container.TagAdd(betaDefinition);
            };

            Assert.IsTrue(container.TagActivate(alphaDefinition));

            Assert.IsTrue(betaAddResult);
            Assert.IsTrue(container.TagCheck(betaDefinition));
        }

        [Test]
        public void OnActivate_WhenDifferentIDActivationIsRequested_AllowsOperation()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            RecordingTagTraitSO betaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddHeldTag(TagID.TestAlpha, "Alpha", alphaTrait);
            TagDefinition betaDefinition = AddHeldTag(TagID.TestBeta, "Beta", betaTrait);
            bool betaActivateResult = false;
            alphaTrait.OnActivateAction = delegate(TraitContext context)
            {
                betaActivateResult = container.TagActivate(betaDefinition);
            };

            Assert.IsTrue(container.TagActivate(alphaDefinition));

            Assert.IsTrue(betaActivateResult);
            Assert.AreEqual(1, betaTrait.OnActivateCallCount);
        }

        [Test]
        public void OnActivate_WhenCallbackQueriesContainer_ReturnsCurrentState()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddHeldTag(TagID.TestAlpha, "Alpha", trait);
            bool tagCheckResult = false;
            bool tryGetResult = false;
            trait.OnActivateAction = delegate(TraitContext context)
            {
                TagInstance foundInstance;
                tagCheckResult = container.TagCheck(definition);
                tryGetResult = container.TryGetTag(definition, out foundInstance);
            };

            Assert.IsTrue(container.TagActivate(definition));

            Assert.IsTrue(tagCheckResult);
            Assert.IsTrue(tryGetResult);
        }

        [Test]
        public void TagActivate_WhenTraitThrows_DoesNotLeaveGuardLocked()
        {
            RecordingTagTraitSO trait = CreateTrait();
            trait.ThrowOnActivate = true;
            TagDefinition definition = AddHeldTag(TagID.TestAlpha, "Alpha", trait);
            LogAssert.Expect(LogType.Error, new Regex("OnActivate"));

            Assert.IsFalse(container.TagActivate(definition));

            trait.ThrowOnActivate = false;
            Assert.IsTrue(container.TagSub(definition));
        }

        private TagDefinition AddHeldTag(TagID tagID, string tagName, RecordingTagTraitSO trait)
        {
            TagDefinition definition = CreateDefinition(tagID, tagName, TagCategory.Status, trait);
            Assert.IsTrue(container.TagAdd(definition));
            return definition;
        }

        private TagDefinition CreateDefinition(TagID tagID, string tagName, TagCategory category, RecordingTagTraitSO trait)
        {
            TagDefinition definition = ScriptableObject.CreateInstance<TagDefinition>();
            createdObjects.Add(definition);
            SerializedObject serializedObject = new SerializedObject(definition);
            SetEnumProperty(serializedObject, "tagID", (int)tagID);
            SetStringProperty(serializedObject, "tagName", tagName);
            SetEnumProperty(serializedObject, "category", (int)category);
            SetObjectProperty(serializedObject, "trait", trait);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private RecordingTagTraitSO CreateTrait()
        {
            RecordingTagTraitSO trait = ScriptableObject.CreateInstance<RecordingTagTraitSO>();
            createdObjects.Add(trait);
            return trait;
        }

        private GameObject CreateSourceObject()
        {
            GameObject source = new GameObject("ActivationSource");
            createdObjects.Add(source);
            return source;
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
    }
}
