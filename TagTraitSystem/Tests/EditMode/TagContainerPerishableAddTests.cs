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
    public sealed class TagContainerPerishableAddTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerPerishableAddTests");
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
        public void PerishableTagAdd_WhenNonKeywordTraitIsValid_AddsPerishableTag()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));

            TagInstance instance;
            Assert.IsTrue(container.TryGetTag(definition, out instance));
            Assert.IsTrue(instance.IsPerishable);
            Assert.AreEqual(1, instance.StackCount);
            Assert.AreEqual(1.2f, instance.Duration);
            Assert.AreEqual(1.2f, instance.RemainingTime);
            Assert.AreEqual(1, trait.OnAddCallCount);
        }

        [Test]
        public void PerishableTagAdd_WhenKeywordIsValid_AddsWithoutTrait()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, null);
            int addedEventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { addedEventCount++; };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));

            TagInstance instance;
            Assert.IsTrue(container.TryGetTag(definition, out instance));
            Assert.IsTrue(instance.IsPerishable);
            Assert.AreEqual(1, addedEventCount);
        }

        [Test]
        public void PerishableTagAdd_WhenCategoryNoneHasTrait_Adds()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "NoneCategory", TagCategory.None, trait);

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));

            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(1, trait.OnAddCallCount);
        }

        [TestCase(" ")]
        [TestCase(" Alpha ")]
        public void PerishableTagAdd_WhenTagNameHasWhitespace_Adds(string tagName)
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, tagName, TagCategory.Status, trait);

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));

            Assert.IsTrue(container.TagCheck(definition));
        }

        [TestCase(1.24f, 1.2f)]
        [TestCase(1.25f, 1.3f)]
        [TestCase(1.26f, 1.3f)]
        [TestCase(0.001f, 0.1f)]
        [TestCase(0.04f, 0.1f)]
        [TestCase(0.05f, 0.1f)]
        [TestCase(0.15f, 0.2f)]
        public void PerishableTagAdd_WhenDurationIsValid_NormalizesInitialDuration(float inputDuration, float expectedDuration)
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);

            Assert.IsTrue(container.PerishableTagAdd(definition, inputDuration));

            TagInstance instance;
            Assert.IsTrue(container.TryGetTag(definition, out instance));
            Assert.AreEqual(expectedDuration, instance.Duration);
            Assert.AreEqual(expectedDuration, instance.RemainingTime);
        }

        [Test]
        public void PerishableTagAdd_WhenInvoked_InvokesOnAddBeforeOnTagAdded()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            List<string> order = new List<string>();
            trait.OnAddAction = delegate(TraitContext context)
            {
                order.Add("OnAdd");
            };
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                order.Add("OnTagAdded");
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));

            Assert.AreEqual("OnAdd", order[0]);
            Assert.AreEqual("OnTagAdded", order[1]);
        }

        [Test]
        public void PerishableTagAdd_WhenSourceIsSpecified_PreservesSourceInTraitAndEvent()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            GameObject source = CreateSourceObject();
            GameObject eventSource = null;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                eventSource = eventData.Source;
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f, source));

            Assert.AreSame(source, trait.LastOnAddContext.Source);
            Assert.AreSame(source, eventSource);
        }

        [Test]
        public void PerishableTagAdd_WhenAddedEventIsRaised_UsesExpectedData()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            bool captured = false;
            TagChangeEventData capturedEvent = default(TagChangeEventData);
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                captured = true;
                capturedEvent = eventData;
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.24f));

            TagInstance instance;
            Assert.IsTrue(container.TryGetTag(definition, out instance));
            Assert.IsTrue(captured);
            Assert.AreSame(container, capturedEvent.Container);
            Assert.AreSame(definition, capturedEvent.Definition);
            Assert.AreSame(instance, capturedEvent.Instance);
            Assert.AreEqual(TagChangeReason.Added, capturedEvent.Reason);
            Assert.AreEqual(0f, capturedEvent.PreviousDuration);
            Assert.AreEqual(0f, capturedEvent.PreviousRemainingTime);
            Assert.AreEqual(0, capturedEvent.PreviousStackCount);
        }

        [Test]
        public void PerishableTagAdd_WhenOnAddThrows_KeepsStateRaisesEventAndReturnsTrue()
        {
            RecordingTagTraitSO trait = CreateTrait();
            trait.ThrowOnAdd = true;
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            int addedEventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { addedEventCount++; };
            LogAssert.Expect(LogType.Error, new Regex("OnAdd"));

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));

            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(1, addedEventCount);
        }

        [Test]
        public void PerishableTagAdd_WhenDefinitionIsNull_ReturnsFalseAndLogsError()
        {
            LogAssert.Expect(LogType.Error, new Regex("null tag definition"));

            Assert.IsFalse(container.PerishableTagAdd(null, 1.2f));
        }

        [Test]
        public void PerishableTagAdd_WhenTagIDIsNone_ReturnsFalseAndLogsError()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.None, "None", TagCategory.Status, trait);
            LogAssert.Expect(LogType.Error, new Regex("TagID\\.None"));

            Assert.IsFalse(container.PerishableTagAdd(definition, 1.2f));
        }

        [Test]
        public void PerishableTagAdd_WhenTagNameIsEmpty_ReturnsFalseAndLogsError()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, string.Empty, TagCategory.Status, trait);
            LogAssert.Expect(LogType.Error, new Regex("empty name"));

            Assert.IsFalse(container.PerishableTagAdd(definition, 1.2f));
        }

        [Test]
        public void PerishableTagAdd_WhenKeywordHasTrait_ReturnsFalseAndLogsError()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, trait);
            LogAssert.Expect(LogType.Error, new Regex("Keyword"));

            Assert.IsFalse(container.PerishableTagAdd(definition, 1.2f));
        }

        [Test]
        public void PerishableTagAdd_WhenNonKeywordTraitIsNull_ReturnsFalseAndLogsError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, null);
            LogAssert.Expect(LogType.Error, new Regex("Non-keyword"));

            Assert.IsFalse(container.PerishableTagAdd(definition, 1.2f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void PerishableTagAdd_WhenDurationIsInvalid_ReturnsFalseLogsErrorAndDoesNotChangeState(float duration)
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            int changeEventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { changeEventCount++; };
            LogAssert.Expect(LogType.Error, new Regex("duration"));

            Assert.IsFalse(container.PerishableTagAdd(definition, duration));

            Assert.IsFalse(container.TagCheck(definition));
            Assert.AreEqual(0, trait.OnAddCallCount);
            Assert.AreEqual(0, changeEventCount);
        }

        [Test]
        public void PerishableTagAdd_WhenSameDefinitionAlreadyExists_ReturnsFalseWithoutChanges()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));
            Assert.IsTrue(container.TryGetTag(definition, out TagInstance originalInstance));

            Assert.IsFalse(container.PerishableTagAdd(definition, 2.2f));

            Assert.IsTrue(container.TryGetTag(definition, out TagInstance foundInstance));
            Assert.AreSame(originalInstance, foundInstance);
            Assert.AreEqual(1.2f, foundInstance.Duration);
            Assert.AreEqual(1, trait.OnAddCallCount);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PerishableTagAdd_WhenPermanentTagExists_ReturnsFalseWithoutChanges()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            Assert.IsTrue(container.TagAdd(definition));
            Assert.IsTrue(container.TryGetTag(definition, out TagInstance originalInstance));

            Assert.IsFalse(container.PerishableTagAdd(definition, 1.2f));

            Assert.IsTrue(container.TryGetTag(definition, out TagInstance foundInstance));
            Assert.AreSame(originalInstance, foundInstance);
            Assert.IsFalse(foundInstance.IsPerishable);
            Assert.AreEqual(1, trait.OnAddCallCount);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PerishableTagAdd_WhenDifferentDefinitionHasSameID_ReturnsFalseAndLogsError()
        {
            RecordingTagTraitSO firstTrait = CreateTrait();
            RecordingTagTraitSO secondTrait = CreateTrait();
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, firstTrait);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestAlpha, "AlphaOther", TagCategory.Status, secondTrait);
            Assert.IsTrue(container.PerishableTagAdd(firstDefinition, 1.2f));
            LogAssert.Expect(LogType.Error, new Regex("same TagID"));

            Assert.IsFalse(container.PerishableTagAdd(secondDefinition, 1.2f));

            Assert.IsTrue(container.TagCheck(firstDefinition));
            Assert.AreEqual(0, secondTrait.OnAddCallCount);
        }

        [Test]
        public void OnAdd_WhenSameIDPerishableTagAddIsRequested_BlocksReentrancy()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            bool reentrantResult = true;
            trait.OnAddAction = delegate(TraitContext context)
            {
                reentrantResult = container.PerishableTagAdd(definition, 1.2f);
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));

            Assert.IsFalse(reentrantResult);
            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(1, trait.OnAddCallCount);
        }

        [Test]
        public void OnAdd_WhenSameIDTagAddIsRequested_BlocksReentrancy()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            bool reentrantResult = true;
            trait.OnAddAction = delegate(TraitContext context)
            {
                reentrantResult = container.TagAdd(definition);
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));

            Assert.IsFalse(reentrantResult);
            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(1, trait.OnAddCallCount);
        }

        [Test]
        public void OnAdd_WhenSameIDTagSubIsRequested_BlocksReentrancy()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            bool reentrantResult = true;
            trait.OnAddAction = delegate(TraitContext context)
            {
                reentrantResult = container.TagSub(definition);
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));

            Assert.IsFalse(reentrantResult);
            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void OnAdd_WhenSameIDTagActivateIsRequested_BlocksReentrancy()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            bool reentrantResult = true;
            trait.OnAddAction = delegate(TraitContext context)
            {
                reentrantResult = container.TagActivate(definition);
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));

            Assert.IsFalse(reentrantResult);
            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void OnAdd_WhenDifferentIDPerishableTagAddIsRequested_AllowsOperation()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            RecordingTagTraitSO betaTrait = CreateTrait();
            TagDefinition alphaDefinition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, alphaTrait);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta, "Beta", TagCategory.Status, betaTrait);
            bool betaResult = false;
            alphaTrait.OnAddAction = delegate(TraitContext context)
            {
                betaResult = container.PerishableTagAdd(betaDefinition, 1.2f);
            };

            Assert.IsTrue(container.PerishableTagAdd(alphaDefinition, 1.2f));

            Assert.IsTrue(betaResult);
            Assert.IsTrue(container.TagCheck(betaDefinition));
            Assert.AreEqual(1, betaTrait.OnAddCallCount);
        }

        [Test]
        public void PerishableTagAdd_WhenFails_DoesNotRaiseChangeEvents()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.None, "None", TagCategory.Status, trait);
            int changeEventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { changeEventCount++; };
            LogAssert.Expect(LogType.Error, new Regex("TagID\\.None"));

            Assert.IsFalse(container.PerishableTagAdd(definition, 1.2f));

            Assert.AreEqual(0, changeEventCount);
            Assert.AreEqual(0, trait.OnAddCallCount);
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
            GameObject source = new GameObject("PerishableAddSource");
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
