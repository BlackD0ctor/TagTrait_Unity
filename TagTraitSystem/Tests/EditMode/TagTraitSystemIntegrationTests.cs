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
    public sealed class TagTraitSystemIntegrationTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private GameObject sourceObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagTraitSystemIntegrationTests");
            sourceObject = new GameObject("IntegrationSource");
            createdObjects.Add(gameObject);
            createdObjects.Add(sourceObject);
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
            sourceObject = null;
            container = null;
        }

        [Test]
        public void Integration_PermanentTrait_CompletesFullLifecycle()
        {
            List<string> order = new List<string>();
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait, StackPolicy.None, 1);
            TagChangeEventData addedEventData = default(TagChangeEventData);
            TagChangeEventData removedEventData = default(TagChangeEventData);

            trait.OnAddAction = delegate { order.Add("OnAdd"); };
            trait.OnActivateAction = delegate { order.Add("OnActivate"); };
            trait.OnRemoveAction = delegate { order.Add("OnRemove"); };
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                order.Add("Added");
                addedEventData = eventData;
            };
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                order.Add("Removed");
                removedEventData = eventData;
            };

            Assert.IsTrue(container.TagAdd(definition, sourceObject));
            TagInstance instance = GetInstance(definition);
            Assert.IsTrue(container.TagActivate(definition, sourceObject));
            Assert.IsTrue(container.TagSub(definition, sourceObject));

            AssertOrder(order, "OnAdd", "Added", "OnActivate", "OnRemove", "Removed");
            Assert.AreEqual(1, trait.OnAddCallCount);
            Assert.AreEqual(1, trait.OnActivateCallCount);
            Assert.AreEqual(1, trait.OnRemoveCallCount);
            AssertContext(trait.LastOnAddContext, definition, instance);
            AssertContext(trait.LastOnActivateContext, definition, instance);
            AssertContext(trait.LastOnRemoveContext, definition, instance);
            AssertEvent(addedEventData, definition, instance, TagChangeReason.Added);
            AssertEvent(removedEventData, definition, instance, TagChangeReason.Removed);
            Assert.IsFalse(container.TagCheck(definition));
            Assert.AreEqual(0, container.TagScan().Count);
        }

        [Test]
        public void Integration_Keyword_AddActivateRemove_IsTraitFree()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "KeywordAlpha", TagCategory.Keyword, null, StackPolicy.None, 1);
            TagChangeEventData addedEventData = default(TagChangeEventData);
            TagChangeEventData removedEventData = default(TagChangeEventData);

            container.OnTagAdded += delegate(TagChangeEventData eventData) { addedEventData = eventData; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { removedEventData = eventData; };

            Assert.IsTrue(container.TagAdd(definition, sourceObject));
            Assert.IsTrue(container.TagCheck(definition));
            TagInstance instance = GetInstance(definition);
            Assert.AreSame(definition, instance.Definition);
            Assert.AreEqual(TagCategory.Keyword, definition.Category);
            Assert.IsNull(definition.Trait);
            Assert.IsFalse(container.TagActivate(definition, sourceObject));
            Assert.IsTrue(container.TagSub(definition, sourceObject));

            AssertEvent(addedEventData, definition, instance, TagChangeReason.Added);
            AssertEvent(removedEventData, definition, instance, TagChangeReason.Removed);
            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Integration_Refresh_ResetsThenExpires()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "RefreshAlpha", TagCategory.Status, trait, StackPolicy.Refresh, 1);
            int refreshedCount = 0;
            int expiredCount = 0;

            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                if (eventData.Reason == TagChangeReason.DurationRefreshed)
                {
                    refreshedCount++;
                }
            };
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                if (eventData.Reason == TagChangeReason.Expired)
                {
                    expiredCount++;
                }
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 5f, sourceObject));
            TagInstance instance = GetInstance(definition);
            container.Tick(2f);
            Assert.IsTrue(container.PerishableTagAdd(definition, 3f, sourceObject));

            Assert.AreSame(instance, GetInstance(definition));
            Assert.AreEqual(3f, instance.Duration);
            Assert.AreEqual(3f, instance.RemainingTime);
            Assert.AreEqual(1, refreshedCount);
            Assert.AreEqual(1, trait.OnAddCallCount);
            container.Tick(3f);

            Assert.AreEqual(1, expiredCount);
            Assert.AreEqual(1, trait.OnRemoveCallCount);
            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Integration_MaxDuration_ExtendsOnlyWhenNeededThenExpires()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "MaxAlpha", TagCategory.Status, trait, StackPolicy.MaxDuration, 1);
            int extendedCount = 0;
            int removedCount = 0;

            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                if (eventData.Reason == TagChangeReason.DurationExtendedToMax)
                {
                    extendedCount++;
                }
            };
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                if (eventData.Reason == TagChangeReason.Expired)
                {
                    removedCount++;
                }
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 5f, sourceObject));
            TagInstance instance = GetInstance(definition);
            container.Tick(3f);
            Assert.IsTrue(container.PerishableTagAdd(definition, 4f, sourceObject));
            Assert.AreEqual(5f, instance.Duration);
            Assert.AreEqual(4f, instance.RemainingTime);
            Assert.AreEqual(1, extendedCount);

            Assert.IsFalse(container.PerishableTagAdd(definition, 3f, sourceObject));
            LogAssert.NoUnexpectedReceived();
            Assert.AreEqual(1, extendedCount);
            Assert.AreEqual(4f, instance.RemainingTime);

            container.Tick(4f);
            Assert.AreEqual(1, removedCount);
            Assert.AreEqual(1, trait.OnRemoveCallCount);
            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Integration_PerishableStackCount_AddTickAndRemove_CompletesLifecycle()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "StackAlpha", TagCategory.Status, trait, StackPolicy.StackCount, 3);
            int stackIncreasedCount = 0;
            int stackDecreasedCount = 0;
            int expiredCount = 0;

            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                if (eventData.Reason == TagChangeReason.StackIncreased)
                {
                    stackIncreasedCount++;
                }

                if (eventData.Reason == TagChangeReason.StackDecreased)
                {
                    stackDecreasedCount++;
                    Assert.Greater(eventData.Instance.StackCount, 0);
                }
            };
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                if (eventData.Reason == TagChangeReason.Expired)
                {
                    expiredCount++;
                    Assert.Greater(eventData.Instance.StackCount, 0);
                }
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 5f, sourceObject));
            TagInstance instance = GetInstance(definition);
            container.Tick(2f);
            Assert.IsTrue(container.PerishableTagAdd(definition, 5f, sourceObject));

            Assert.AreEqual(2, instance.StackCount);
            Assert.AreEqual(5f, instance.Duration);
            Assert.AreEqual(5f, instance.RemainingTime);
            Assert.AreEqual(1, stackIncreasedCount);
            Assert.AreEqual(1, trait.OnAddCallCount);
            Assert.AreEqual(0, trait.OnRemoveCallCount);

            container.Tick(6f);
            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(1, instance.StackCount);
            Assert.AreEqual(4f, instance.RemainingTime);
            Assert.AreEqual(1, stackDecreasedCount);
            Assert.AreEqual(0, trait.OnRemoveCallCount);

            container.Tick(4f);
            Assert.AreEqual(1, expiredCount);
            Assert.AreEqual(1, trait.OnRemoveCallCount);
            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Integration_PerishableToPermanent_StopsTickAndKeepsInstance()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "ConvertAlpha", TagCategory.Status, trait, StackPolicy.StackCount, 3);
            TagChangeEventData convertedEventData = default(TagChangeEventData);

            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                if (eventData.Reason == TagChangeReason.ChangedToPermanent)
                {
                    convertedEventData = eventData;
                }
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 5f, sourceObject));
            TagInstance instance = GetInstance(definition);
            container.Tick(1f);
            Assert.IsTrue(container.TagAdd(definition, sourceObject));

            Assert.AreSame(instance, GetInstance(definition));
            Assert.IsFalse(instance.IsPerishable);
            Assert.AreEqual(0f, instance.Duration);
            Assert.AreEqual(0f, instance.RemainingTime);
            Assert.AreEqual(1, instance.StackCount);
            AssertEvent(convertedEventData, definition, instance, TagChangeReason.ChangedToPermanent);

            container.Tick(10f);
            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(0f, instance.RemainingTime);
            Assert.IsTrue(container.TagSub(definition, sourceObject));
            Assert.AreEqual(1, trait.OnRemoveCallCount);
            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Integration_MultipleTagIDs_RemainIndependent()
        {
            TagDefinition keywordDefinition = CreateDefinition(TagID.TestAlpha, "KeywordAlpha", TagCategory.Keyword, null, StackPolicy.None, 1);
            Assert.IsTrue(container.TagAdd(keywordDefinition, sourceObject));
            Assert.IsFalse(container.TagActivate(keywordDefinition, sourceObject));
            Assert.IsTrue(container.TagSub(keywordDefinition, sourceObject));

            TagDefinition refreshDefinition = CreateDefinition(TagID.TestAlpha, "RefreshAlpha", TagCategory.Status, CreateTrait(), StackPolicy.Refresh, 1);
            TagDefinition maxDefinition = CreateDefinition(TagID.TestBeta, "MaxBeta", TagCategory.Status, CreateTrait(), StackPolicy.MaxDuration, 1);
            TagDefinition stackDefinition = CreateDefinition(TagID.TestGamma, "StackGamma", TagCategory.Status, CreateTrait(), StackPolicy.StackCount, 3);

            Assert.IsTrue(container.PerishableTagAdd(refreshDefinition, 4f, sourceObject));
            Assert.IsTrue(container.PerishableTagAdd(maxDefinition, 5f, sourceObject));
            Assert.IsTrue(container.PerishableTagAdd(stackDefinition, 3f, sourceObject));
            Assert.IsTrue(container.PerishableTagAdd(stackDefinition, 3f, sourceObject));

            container.Tick(4f);

            Assert.IsFalse(container.TagCheck(refreshDefinition));
            Assert.IsTrue(container.TagCheck(maxDefinition));
            Assert.IsTrue(container.TagCheck(stackDefinition));
            TagInstance maxInstance = GetInstance(maxDefinition);
            TagInstance stackInstance = GetInstance(stackDefinition);
            Assert.AreEqual(1f, maxInstance.RemainingTime);
            Assert.AreEqual(1, stackInstance.StackCount);
            Assert.AreEqual(2f, stackInstance.RemainingTime);
            Assert.AreEqual(2, container.TagScan().Count);
            Assert.IsTrue(container.TryGetTag(maxDefinition, out maxInstance));
            Assert.IsTrue(container.TryGetTag(stackDefinition, out stackInstance));
        }

        [Test]
        public void Integration_TagScanSnapshot_TracksCompositionButSharesInstances()
        {
            TagDefinition alphaDefinition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.Refresh, 1);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta, "Beta", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            Assert.IsTrue(container.PerishableTagAdd(alphaDefinition, 5f, sourceObject));
            IReadOnlyCollection<TagInstance> snapshot = container.TagScan();
            List<TagInstance> snapshotList = new List<TagInstance>(snapshot);
            TagInstance snapshotInstance = snapshotList[0];

            container.Tick(1f);
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual(4f, snapshotInstance.RemainingTime);

            Assert.IsTrue(container.TagAdd(betaDefinition, sourceObject));
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual(2, container.TagScan().Count);
            Assert.AreSame(snapshotInstance, GetInstance(alphaDefinition));
        }

        [Test]
        public void Integration_Diagnostics_DistinguishesErrorWarningAndSilentNoOp()
        {
            TagDefinition stackDefinition = CreateDefinition(TagID.TestAlpha, "StackAlpha", TagCategory.Status, CreateTrait(), StackPolicy.StackCount, 1);
            Assert.IsTrue(container.TagAdd(stackDefinition, sourceObject));
            Assert.IsFalse(container.TagAdd(stackDefinition, sourceObject));
            LogAssert.NoUnexpectedReceived();

            LogAssert.Expect(LogType.Error, new Regex("Cannot add a null tag definition"));
            Assert.IsFalse(container.TagAdd(null));

            TagDefinition reentrantDefinition = CreateDefinition(TagID.TestBeta, "ReentrantBeta", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            RecordingTagTraitSO reentrantTrait = (RecordingTagTraitSO)reentrantDefinition.Trait;
            bool reentrantResult = true;
            reentrantTrait.OnAddAction = delegate { reentrantResult = container.TagAdd(reentrantDefinition, sourceObject); };

            LogAssert.Expect(LogType.Warning, new Regex("reentrant mutation.*TestBeta"));
            Assert.IsTrue(container.TagAdd(reentrantDefinition, sourceObject));
            Assert.IsFalse(reentrantResult);
        }

        [Test]
        public void Integration_EventOrder_MatchesLifecycleContract()
        {
            List<string> order = new List<string>();
            RecordingTagTraitSO permanentTrait = CreateTrait();
            TagDefinition permanentDefinition = CreateDefinition(TagID.TestAlpha, "PermanentAlpha", TagCategory.Status, permanentTrait, StackPolicy.None, 1);
            permanentTrait.OnAddAction = delegate { order.Add("OnAdd"); };
            permanentTrait.OnRemoveAction = delegate { order.Add("OnRemove"); };
            container.OnTagAdded += delegate { order.Add("Added"); };
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                if (eventData.Definition == permanentDefinition)
                {
                    order.Add("Removed");
                }

                if (eventData.Reason == TagChangeReason.Expired)
                {
                    order.Add("Expired");
                }
            };
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                if (eventData.Reason == TagChangeReason.StackDecreased)
                {
                    order.Add("Updated");
                }
            };

            Assert.IsTrue(container.TagAdd(permanentDefinition, sourceObject));
            Assert.IsTrue(container.TagSub(permanentDefinition, sourceObject));
            AssertOrder(order, "OnAdd", "Added", "OnRemove", "Removed");

            order.Clear();
            RecordingTagTraitSO stackTrait = CreateTrait();
            stackTrait.OnRemoveAction = delegate { order.Add("OnRemove"); };
            TagDefinition stackDefinition = CreateDefinition(TagID.TestBeta, "StackBeta", TagCategory.Status, stackTrait, StackPolicy.StackCount, 3);
            Assert.IsTrue(container.PerishableTagAdd(stackDefinition, 3f, sourceObject));
            Assert.IsTrue(container.PerishableTagAdd(stackDefinition, 3f, sourceObject));
            order.Clear();

            container.Tick(3f);
            AssertOrder(order, "Updated");
            Assert.AreEqual(0, stackTrait.OnRemoveCallCount);

            order.Clear();
            container.Tick(3f);
            AssertOrder(order, "OnRemove", "Expired");
            Assert.AreEqual(1, stackTrait.OnRemoveCallCount);
        }

        private RecordingTagTraitSO CreateTrait()
        {
            RecordingTagTraitSO trait = ScriptableObject.CreateInstance<RecordingTagTraitSO>();
            createdObjects.Add(trait);
            return trait;
        }

        private TagDefinition CreateDefinition(
            TagID tagID,
            string tagName,
            TagCategory category,
            RecordingTagTraitSO trait,
            StackPolicy stackPolicy,
            int maxStackCount)
        {
            TagDefinition definition = ScriptableObject.CreateInstance<TagDefinition>();
            createdObjects.Add(definition);

            SerializedObject serializedObject = new SerializedObject(definition);
            SetEnumProperty(serializedObject, "tagID", (int)tagID);
            SetStringProperty(serializedObject, "tagName", tagName);
            SetEnumProperty(serializedObject, "category", (int)category);
            SetObjectProperty(serializedObject, "trait", trait);
            SetEnumProperty(serializedObject, "stackPolicy", (int)stackPolicy);
            SetIntProperty(serializedObject, "maxStackCount", maxStackCount);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return definition;
        }

        private TagInstance GetInstance(TagDefinition definition)
        {
            TagInstance instance;
            Assert.IsTrue(container.TryGetTag(definition, out instance));
            return instance;
        }

        private void AssertContext(TraitContext context, TagDefinition definition, TagInstance instance)
        {
            Assert.AreSame(container, context.Container);
            Assert.AreSame(gameObject, context.Target);
            Assert.AreSame(sourceObject, context.Source);
            Assert.AreSame(definition, context.Definition);
            Assert.AreSame(instance, context.Instance);
        }

        private void AssertEvent(
            TagChangeEventData eventData,
            TagDefinition definition,
            TagInstance instance,
            TagChangeReason reason)
        {
            Assert.AreSame(container, eventData.Container);
            Assert.AreSame(definition, eventData.Definition);
            Assert.AreSame(instance, eventData.Instance);
            Assert.AreSame(sourceObject, eventData.Source);
            Assert.AreEqual(reason, eventData.Reason);
        }

        private static void AssertOrder(List<string> actualOrder, params string[] expectedOrder)
        {
            Assert.AreEqual(expectedOrder.Length, actualOrder.Count);
            for (int i = 0; i < expectedOrder.Length; i++)
            {
                Assert.AreEqual(expectedOrder[i], actualOrder[i]);
            }
        }

        private static void SetEnumProperty(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.enumValueIndex = value;
        }

        private static void SetStringProperty(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.stringValue = value;
        }

        private static void SetObjectProperty(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.objectReferenceValue = value;
        }

        private static void SetIntProperty(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.intValue = value;
        }
    }
}
