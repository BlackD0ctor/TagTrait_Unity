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
    public sealed class TagContainerTickTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerTickTests");
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
        public void Tick_WhenPerishableTagExists_DecreasesRemainingTime()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 2f);

            container.Tick(0.5f);

            TagInstance instance = GetInstance(definition);
            Assert.AreEqual(2f, instance.Duration);
            Assert.AreEqual(1.5f, instance.RemainingTime);
            Assert.IsTrue(instance.IsPerishable);
        }

        [Test]
        public void Tick_WhenPermanentTagExists_DoesNotChangeState()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            Assert.IsTrue(container.TagAdd(definition));

            container.Tick(1f);

            TagInstance instance = GetInstance(definition);
            Assert.IsFalse(instance.IsPerishable);
            Assert.AreEqual(0f, instance.Duration);
            Assert.AreEqual(0f, instance.RemainingTime);
            Assert.AreEqual(0, trait.OnRemoveCallCount);
        }

        [Test]
        public void Tick_WhenTimeRemains_DoesNotInvokeTraitOrEvents()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 2f, trait);
            int changeEventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { changeEventCount++; };

            container.Tick(0.5f);

            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(0, trait.OnRemoveCallCount);
            Assert.AreEqual(0, changeEventCount);
        }

        [Test]
        public void Tick_WhenFractionalDeltaIsRepeated_DoesNotRoundEachTick()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f);

            container.Tick(0.333f);
            container.Tick(0.333f);

            TagInstance instance = GetInstance(definition);
            Assert.AreEqual(0.334f, instance.RemainingTime, 0.00001f);
        }

        [Test]
        public void Tick_WhenTagScanSnapshotExists_SameInstanceShowsChangedRemainingTime()
        {
            AddPerishableTag(TagID.TestAlpha, "Alpha", 2f);
            IReadOnlyCollection<TagInstance> snapshot = container.TagScan();
            TagInstance scannedInstance = null;
            foreach (TagInstance instance in snapshot)
            {
                scannedInstance = instance;
            }

            container.Tick(0.5f);

            Assert.IsNotNull(scannedInstance);
            Assert.AreEqual(1.5f, scannedInstance.RemainingTime);
        }

        [Test]
        public void Tick_WhenDeltaTimeIsZero_DoesNothingWithoutLog()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f);

            container.Tick(0f);

            Assert.AreEqual(1f, GetInstance(definition).RemainingTime);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Tick_WhenDeltaTimeIsInvalid_LogsErrorAndDoesNotChangeState(float deltaTime)
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f);
            LogAssert.Expect(LogType.Error, new Regex("deltaTime"));

            container.Tick(deltaTime);

            Assert.AreEqual(1f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void Tick_WhenRemainingTimeBecomesZero_ExpiresTag()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f);

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Tick_WhenDeltaTimeExceedsRemainingTime_ExpiresTagAndClampsToZero()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f);
            TagInstance instance = GetInstance(definition);

            container.Tick(2f);

            Assert.IsFalse(container.TagCheck(definition));
            Assert.AreEqual(0f, instance.RemainingTime);
        }

        [Test]
        public void Tick_WhenRemainingTimeIsInsideEpsilon_ExpiresTagAndClampsToZero()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f);
            TagInstance instance = GetInstance(definition);

            container.Tick(0.99995f);

            Assert.IsFalse(container.TagCheck(definition));
            Assert.AreEqual(0f, instance.RemainingTime);
        }

        [Test]
        public void Tick_WhenTagExpires_InvokesOnRemoveBeforeOnTagRemoved()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, trait);
            List<string> order = new List<string>();
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                order.Add(container.TagCheck(definition) ? "OnRemoveStored" : "OnRemoveMissing");
            };
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                order.Add(container.TagCheck(definition) ? "EventStored" : "EventRemoved");
            };

            container.Tick(1f);

            Assert.AreEqual("OnRemoveStored", order[0]);
            Assert.AreEqual("EventRemoved", order[1]);
        }

        [Test]
        public void Tick_WhenTagExpires_RaisesExpiredEventWithExpectedData()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            TagInstance instance = GetInstance(definition);
            bool captured = false;
            TagChangeEventData capturedEvent = default(TagChangeEventData);
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                captured = true;
                capturedEvent = eventData;
            };

            container.Tick(1.2f);

            Assert.IsTrue(captured);
            Assert.AreSame(container, capturedEvent.Container);
            Assert.AreSame(definition, capturedEvent.Definition);
            Assert.AreSame(instance, capturedEvent.Instance);
            Assert.IsNull(capturedEvent.Source);
            Assert.AreEqual(TagChangeReason.Expired, capturedEvent.Reason);
            Assert.AreEqual(1.2f, capturedEvent.PreviousDuration);
            Assert.AreEqual(1.2f, capturedEvent.PreviousRemainingTime);
            Assert.AreEqual(1, capturedEvent.PreviousStackCount);
            Assert.AreEqual(0f, capturedEvent.Instance.RemainingTime);
        }

        [Test]
        public void Tick_WhenKeywordTagExpires_RemovesWithoutTrait()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, null);
            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Tick_WhenOnRemoveThrows_StillRemovesAndRaisesEvent()
        {
            RecordingTagTraitSO trait = CreateTrait();
            trait.ThrowOnRemove = true;
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, trait);
            int removedEventCount = 0;
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { removedEventCount++; };
            LogAssert.Expect(LogType.Error, new Regex("OnRemove"));

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(definition));
            Assert.AreEqual(1, removedEventCount);
        }

        [Test]
        public void Tick_WhenInvalidKeywordTraitTagExpires_DoesNotInvokeOnRemove()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, trait);
            Assert.IsTrue(container.TryAddInstance(new TagInstance(definition, 1f)));

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(definition));
            Assert.AreEqual(0, trait.OnRemoveCallCount);
        }

        [Test]
        public void Tick_WhenNonKeywordNullTraitTagExpires_DoesNotInvokeOnRemove()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, null);
            Assert.IsTrue(container.TryAddInstance(new TagInstance(definition, 1f)));

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Tick_WhenCallbackAddsTag_NewTagIsProcessedFromNextTick()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            RecordingTagTraitSO betaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, alphaTrait);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta, "Beta", TagCategory.Status, betaTrait);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                Assert.IsTrue(container.PerishableTagAdd(betaDefinition, 2f));
            };

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.AreEqual(2f, GetInstance(betaDefinition).RemainingTime);

            container.Tick(0.5f);

            Assert.AreEqual(1.5f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenCallbackRemovesSnapshotTag_SkipsRemovedSnapshotItem()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            RecordingTagTraitSO betaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, alphaTrait);
            TagDefinition betaDefinition = AddPerishableTag(TagID.TestBeta, "Beta", 2f, betaTrait);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                Assert.IsTrue(container.TagSub(betaDefinition));
            };

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.IsFalse(container.TagCheck(betaDefinition));
            Assert.AreEqual(1, betaTrait.OnRemoveCallCount);
        }

        [Test]
        public void Tick_WhenCallbackReplacesSnapshotInstance_SkipsOldSnapshotItem()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            RecordingTagTraitSO betaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, alphaTrait);
            TagDefinition betaDefinition = AddPerishableTag(TagID.TestBeta, "Beta", 2f, betaTrait);
            TagInstance oldBetaInstance = GetInstance(betaDefinition);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                Assert.IsTrue(container.TagSub(betaDefinition));
                Assert.IsTrue(container.PerishableTagAdd(betaDefinition, 2f));
            };

            container.Tick(1f);

            TagInstance newBetaInstance = GetInstance(betaDefinition);
            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.AreNotSame(oldBetaInstance, newBetaInstance);
            Assert.AreEqual(2f, newBetaInstance.RemainingTime);
        }

        [Test]
        public void Tick_WhenNestedTickIsRequested_DoesNotDoubleDecreaseOtherTags()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 0.5f, alphaTrait);
            TagDefinition betaDefinition = AddPerishableTag(TagID.TestBeta, "Beta", 2f);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                container.Tick(1f);
            };

            container.Tick(0.5f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.AreEqual(1.5f, GetInstance(betaDefinition).RemainingTime);

            container.Tick(0.5f);

            Assert.AreEqual(1f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenCallbackChangesSameID_BlocksReentrancy()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, trait);
            bool reentrantAddResult = true;
            bool reentrantSubResult = true;
            bool reentrantActivateResult = true;
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                reentrantAddResult = container.PerishableTagAdd(definition, 1f);
                reentrantSubResult = container.TagSub(definition);
                reentrantActivateResult = container.TagActivate(definition);
            };

            container.Tick(1f);

            Assert.IsFalse(reentrantAddResult);
            Assert.IsFalse(reentrantSubResult);
            Assert.IsFalse(reentrantActivateResult);
            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Tick_WhenCallbackChangesDifferentID_AllowsOperation()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, alphaTrait);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta, "Beta", TagCategory.Status, CreateTrait());
            bool betaAddResult = false;
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                betaAddResult = container.TagAdd(betaDefinition);
            };

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.IsTrue(betaAddResult);
            Assert.IsTrue(container.TagCheck(betaDefinition));
        }

        [Test]
        public void Tick_WhenCallbackQueriesContainer_ReturnsCurrentState()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, trait);
            bool tagCheckDuringRemove = false;
            bool tryGetDuringRemove = false;
            bool tagCheckDuringEvent = true;
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                TagInstance foundInstance;
                tagCheckDuringRemove = container.TagCheck(definition);
                tryGetDuringRemove = container.TryGetTag(definition, out foundInstance);
            };
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                tagCheckDuringEvent = container.TagCheck(definition);
            };

            container.Tick(1f);

            Assert.IsTrue(tagCheckDuringRemove);
            Assert.IsTrue(tryGetDuringRemove);
            Assert.IsFalse(tagCheckDuringEvent);
        }

        private TagDefinition AddPerishableTag(TagID tagID, string tagName, float duration)
        {
            return AddPerishableTag(tagID, tagName, duration, CreateTrait());
        }

        private TagDefinition AddPerishableTag(TagID tagID, string tagName, float duration, RecordingTagTraitSO trait)
        {
            TagDefinition definition = CreateDefinition(tagID, tagName, TagCategory.Status, trait);
            Assert.IsTrue(container.PerishableTagAdd(definition, duration));
            return definition;
        }

        private TagInstance GetInstance(TagDefinition definition)
        {
            TagInstance instance;
            Assert.IsTrue(container.TryGetTag(definition, out instance));
            return instance;
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
