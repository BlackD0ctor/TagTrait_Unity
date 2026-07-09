using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TagTraitSystem.Runtime.Components;
using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Definitions;
using TagTraitSystem.Runtime.Traits;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TagTraitSystem.Tests.EditMode
{
    public sealed class TagContainerPerishableStackCountTickTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerPerishableStackCountTickTests");
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
        public void Tick_WhenPerishableStackCountHasOneStack_DecreasesRemainingTime()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 3f, 3);

            container.Tick(1f);

            Assert.AreEqual(1, GetInstance(definition).StackCount);
            Assert.AreEqual(2f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void Tick_WhenDeltaEndsExactlyAtFirstStackBoundary_ActivatesNextStackAtFullDuration()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 5f);
            GetInstance(definition).DecreaseRemainingTime(3f);

            container.Tick(2f);

            Assert.AreEqual(2, GetInstance(definition).StackCount);
            Assert.AreEqual(5f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void Tick_WhenDeltaConsumesOneStackAndPartOfNext_CarriesOverflow()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 5f);
            GetInstance(definition).DecreaseRemainingTime(3f);

            container.Tick(3f);

            Assert.AreEqual(2, GetInstance(definition).StackCount);
            Assert.AreEqual(4f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void Tick_WhenDeltaConsumesMultipleStacks_CarriesOverflow()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 5f);
            GetInstance(definition).DecreaseRemainingTime(3f);

            container.Tick(8f);

            Assert.AreEqual(1, GetInstance(definition).StackCount);
            Assert.AreEqual(4f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void Tick_WhenDeltaIsLessThanRemaining_DoesNotChangeStackCount()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 5f);

            container.Tick(1f);

            Assert.AreEqual(3, GetInstance(definition).StackCount);
            Assert.AreEqual(4f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void Tick_WhenStackCountDoesNotChange_DoesNotInvokeUpdated()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 5f);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            container.Tick(1f);

            Assert.AreEqual(3, GetInstance(definition).StackCount);
            Assert.AreEqual(0, updatedCount);
        }

        [Test]
        public void Tick_WhenOneOrMoreStacksExpireAndTagRemains_InvokesUpdatedOnce()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 5f);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            container.Tick(6f);

            Assert.AreEqual(2, GetInstance(definition).StackCount);
            Assert.AreEqual(1, updatedCount);
        }

        [Test]
        public void Tick_WhenStacksExpireAndTagRemains_UsesStackDecreasedReason()
        {
            AddPerishableStackTagWithStacks(3, 5f);
            TagChangeReason reason = TagChangeReason.Added;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                reason = eventData.Reason;
            };

            container.Tick(6f);

            Assert.AreEqual(TagChangeReason.StackDecreased, reason);
        }

        [Test]
        public void Tick_WhenStacksExpireAndTagRemains_PreservesPreviousTickStartValues()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 5f);
            TagChangeEventData capturedEvent = default(TagChangeEventData);
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                capturedEvent = eventData;
            };

            container.Tick(6f);

            Assert.AreSame(definition, capturedEvent.Definition);
            Assert.AreEqual(5f, capturedEvent.PreviousDuration);
            Assert.AreEqual(5f, capturedEvent.PreviousRemainingTime);
            Assert.AreEqual(3, capturedEvent.PreviousStackCount);
        }

        [Test]
        public void Tick_WhenMultipleStacksExpire_DoesNotInvokePerStackEvents()
        {
            AddPerishableStackTagWithStacks(3, 5f);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            container.Tick(11f);

            Assert.AreEqual(1, updatedCount);
        }

        [Test]
        public void Tick_WhenStacksExpireAndTagRemains_DoesNotInvokeOnRemove()
        {
            RecordingTagTraitSO trait = CreateTrait();
            AddPerishableStackTagWithStacks(TagID.TestAlpha, "Alpha", 5f, 3, 3, trait);

            container.Tick(6f);

            Assert.AreEqual(0, trait.OnRemoveCallCount);
        }

        [Test]
        public void Tick_WhenCallbackQueries_SeesFinalStackAndRemainingTime()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 5f);
            int stackCount = 0;
            float remainingTime = 0f;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                TagInstance instance;
                Assert.IsTrue(container.TryGetTag(definition, out instance));
                stackCount = instance.StackCount;
                remainingTime = instance.RemainingTime;
            };

            container.Tick(6f);

            Assert.AreEqual(2, stackCount);
            Assert.AreEqual(4f, remainingTime);
        }

        [Test]
        public void Tick_WhenDeltaEqualsTotalRemaining_RemovesTag()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 5f);

            container.Tick(15f);

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Tick_WhenDeltaExceedsTotalRemaining_RemovesTag()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 5f);

            container.Tick(16f);

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Tick_WhenAllStacksExpire_SetsRemainingZeroBeforeRemoval()
        {
            AddPerishableStackTagWithStacks(3, 5f);
            float remainingTime = -1f;
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                remainingTime = eventData.Instance.RemainingTime;
            };

            container.Tick(15f);

            Assert.AreEqual(0f, remainingTime);
        }

        [Test]
        public void Tick_WhenAllStacksExpire_KeepsStackCountOneBeforeRemoval()
        {
            AddPerishableStackTagWithStacks(3, 5f);
            int stackCount = 0;
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                stackCount = eventData.Instance.StackCount;
            };

            container.Tick(15f);

            Assert.AreEqual(1, stackCount);
        }

        [Test]
        public void Tick_WhenAllStacksExpire_InvokesOnRemoveOnce()
        {
            RecordingTagTraitSO trait = CreateTrait();
            AddPerishableStackTagWithStacks(TagID.TestAlpha, "Alpha", 5f, 3, 3, trait);

            container.Tick(15f);

            Assert.AreEqual(1, trait.OnRemoveCallCount);
        }

        [Test]
        public void Tick_WhenAllStacksExpire_InvokesRemovedOnceWithExpiredReason()
        {
            AddPerishableStackTagWithStacks(3, 5f);
            int removedCount = 0;
            TagChangeReason reason = TagChangeReason.Added;
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                removedCount++;
                reason = eventData.Reason;
            };

            container.Tick(15f);

            Assert.AreEqual(1, removedCount);
            Assert.AreEqual(TagChangeReason.Expired, reason);
        }

        [Test]
        public void Tick_WhenAllStacksExpire_DoesNotInvokeStackDecreased()
        {
            AddPerishableStackTagWithStacks(3, 5f);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            container.Tick(15f);

            Assert.AreEqual(0, updatedCount);
        }

        [Test]
        public void Tick_WhenAllStacksExpire_UsesTickStartPreviousValues()
        {
            AddPerishableStackTagWithStacks(3, 5f);
            TagChangeEventData capturedEvent = default(TagChangeEventData);
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                capturedEvent = eventData;
            };

            container.Tick(15f);

            Assert.AreEqual(5f, capturedEvent.PreviousDuration);
            Assert.AreEqual(5f, capturedEvent.PreviousRemainingTime);
            Assert.AreEqual(3, capturedEvent.PreviousStackCount);
        }

        [Test]
        public void Tick_WhenDeltaIsWithinEpsilonOfBoundary_UsesBoundaryResult()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 5f);
            GetInstance(definition).DecreaseRemainingTime(3f);

            container.Tick(1.99995f);

            Assert.AreEqual(2, GetInstance(definition).StackCount);
            Assert.AreEqual(5f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void Tick_WhenManyStacksAndLargeDelta_ComputesWithoutPerStackLoop()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 5f, 100000);
            TagInstance instance = GetInstance(definition);
            SetStackCount(instance, 100000);

            container.Tick(499995f);

            Assert.AreEqual(1, GetInstance(definition).StackCount);
            Assert.AreEqual(5f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void Tick_WhenOneLargeTickAndManySmallTicks_AreEquivalent()
        {
            TagDefinition firstDefinition = AddPerishableStackTagWithStacks(3, 5f);
            TagContainer secondContainer = CreateContainer("SecondTickContainer");
            TagDefinition secondDefinition = CreateStackDefinition(TagID.TestBeta, "Beta", 5f, 3, CreateTrait());
            Assert.IsTrue(secondContainer.PerishableTagAdd(secondDefinition, 5f));
            Assert.IsTrue(secondContainer.PerishableTagAdd(secondDefinition, 5f));
            Assert.IsTrue(secondContainer.PerishableTagAdd(secondDefinition, 5f));

            container.Tick(7f);
            for (int i = 0; i < 7; i++)
            {
                secondContainer.Tick(1f);
            }

            Assert.AreEqual(GetInstance(firstDefinition).StackCount, GetInstance(secondContainer, secondDefinition).StackCount);
            Assert.AreEqual(GetInstance(firstDefinition).RemainingTime, GetInstance(secondContainer, secondDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenRemainingTimeIsContinuous_DoesNotRoundAfterTick()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 5f, 3);

            container.Tick(1.234f);

            Assert.AreEqual(3.766f, GetInstance(definition).RemainingTime, 0.0001f);
        }

        [Test]
        public void Tick_WhenBoundaryLeavesOneStack_SetsRemainingToDuration()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 5f);
            GetInstance(definition).DecreaseRemainingTime(3f);

            container.Tick(7f);

            Assert.AreEqual(1, GetInstance(definition).StackCount);
            Assert.AreEqual(5f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void Tick_WhenTargetStackMutatesBeforeProcessing_SkipsCurrentTick()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, alphaTrait);
            TagDefinition betaDefinition = AddPerishableStackTag(TagID.TestBeta, "Beta", 3f, 3);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                container.PerishableTagAdd(betaDefinition, 3f);
            };

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.AreEqual(2, GetInstance(betaDefinition).StackCount);
            Assert.AreEqual(3f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenTargetStackMutatesAfterProcessing_DoesNotRestoreConsumedStacks()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition betaDefinition = AddPerishableStackTagWithStacks(TagID.TestBeta, "Beta", 3f, 3, 2);
            TagDefinition alphaDefinition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, alphaTrait);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                container.PerishableTagAdd(betaDefinition, 3f);
            };

            container.Tick(3f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.That(GetInstance(betaDefinition).StackCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(GetInstance(betaDefinition).StackCount, Is.LessThanOrEqualTo(3));
        }

        [Test]
        public void Tick_WhenTargetIsRemovedBeforeProcessing_SkipsSnapshotEntry()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, alphaTrait);
            TagDefinition betaDefinition = AddPerishableStackTag(TagID.TestBeta, "Beta", 3f, 3);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                container.TagSub(betaDefinition);
            };

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.IsFalse(container.TagCheck(betaDefinition));
        }

        [Test]
        public void Tick_WhenTargetIsConvertedPermanentBeforeProcessing_SkipsSnapshotEntry()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, alphaTrait);
            TagDefinition betaDefinition = AddPerishableStackTag(TagID.TestBeta, "Beta", 3f, 3);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                container.TagAdd(betaDefinition);
            };

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.IsFalse(GetInstance(betaDefinition).IsPerishable);
            Assert.AreEqual(0f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenNestedTickCalled_DoesNothing()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, alphaTrait);
            TagDefinition betaDefinition = AddPerishableStackTag(TagID.TestBeta, "Beta", 3f, 3);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                container.Tick(1f);
            };

            container.Tick(1f);

            Assert.AreEqual(2f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenCallbackChangesDifferentID_AllowsChange()
        {
            TagDefinition betaDefinition = AddPerishableStackTagWithStacks(TagID.TestBeta, "Beta", 3f, 3, 2);
            TagDefinition alphaDefinition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 1f, 1, CreateTrait());
            bool addResult = false;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                addResult = container.PerishableTagAdd(alphaDefinition, 1f);
            };

            container.Tick(3f);

            Assert.IsTrue(addResult);
            Assert.IsTrue(container.TagCheck(alphaDefinition));
            Assert.AreEqual(1, GetInstance(betaDefinition).StackCount);
        }

        [Test]
        public void Tick_WhenCallbackChangesSameID_BlocksChange()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(2, 3f);
            bool tagSubResult = true;
            bool perishableAddResult = true;
            bool activateResult = true;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                tagSubResult = container.TagSub(definition);
                perishableAddResult = container.PerishableTagAdd(definition, 3f);
                activateResult = container.TagActivate(definition);
            };

            container.Tick(3f);

            Assert.IsFalse(tagSubResult);
            Assert.IsFalse(perishableAddResult);
            Assert.IsFalse(activateResult);
            Assert.AreEqual(1, GetInstance(definition).StackCount);
        }

        [Test]
        public void Tick_WhenCompleted_ClearsTickTrackingCollections()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, alphaTrait);
            TagDefinition betaDefinition = AddPerishableStackTag(TagID.TestBeta, "Beta", 3f, 3);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                container.PerishableTagAdd(betaDefinition, 3f);
            };

            container.Tick(1f);
            container.Tick(1f);

            Assert.AreEqual(2f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenDurationIsInvalid_DoesNotMutateAndLogsError()
        {
            TagDefinition definition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 1f, 3, CreateTrait());
            TagInstance instance = new TagInstance(definition, 0f);
            Assert.IsTrue(container.TryAddInstance(instance));
            LogAssert.Expect(LogType.Error, new Regex("invalid duration"));

            container.Tick(1f);

            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(0f, instance.Duration);
            Assert.AreEqual(1, instance.StackCount);
        }

        [Test]
        public void Tick_WhenStackCountIsBelowOne_DoesNotMutateAndLogsError()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 3f, 3);
            TagInstance instance = GetInstance(definition);
            SetStackCount(instance, 0);
            LogAssert.Expect(LogType.Error, new Regex("StackCount less than 1"));

            container.Tick(1f);

            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(0, instance.StackCount);
            Assert.AreEqual(3f, instance.RemainingTime);
        }

        [Test]
        public void Tick_WhenStackCountExceedsMaximum_DoesNotClamp()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3, 3f);
            SetIntProperty(definition, "maxStackCount", 2);
            LogAssert.Expect(LogType.Error, new Regex("StackCount greater than MaxStackCount"));

            container.Tick(1f);

            Assert.AreEqual(3, GetInstance(definition).StackCount);
            Assert.AreEqual(2, definition.MaxStackCount);
        }

        [Test]
        public void Tick_WhenMalformedTraitDataExpires_DoesNotInvokeInvalidOnRemove()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, trait);
            SetStringProperty(definition, "tagName", string.Empty);

            container.Tick(1f);

            Assert.AreEqual(0, trait.OnRemoveCallCount);
            Assert.IsFalse(container.TagCheck(definition));
        }

        private TagDefinition AddPerishableStackTagWithStacks(int stackCount, float duration)
        {
            return AddPerishableStackTagWithStacks(TagID.TestAlpha, "Alpha", duration, stackCount, stackCount, CreateTrait());
        }

        private TagDefinition AddPerishableStackTagWithStacks(
            TagID tagID,
            string tagName,
            float duration,
            int maxStackCount,
            int stackCount)
        {
            return AddPerishableStackTagWithStacks(tagID, tagName, duration, maxStackCount, stackCount, CreateTrait());
        }

        private TagDefinition AddPerishableStackTagWithStacks(
            TagID tagID,
            string tagName,
            float duration,
            int maxStackCount,
            int stackCount,
            RecordingTagTraitSO trait)
        {
            TagDefinition definition = AddPerishableStackTag(tagID, tagName, duration, maxStackCount, trait);
            for (int i = 1; i < stackCount; i++)
            {
                Assert.IsTrue(container.PerishableTagAdd(definition, duration));
            }

            return definition;
        }

        private TagDefinition AddPerishableStackTag(TagID tagID, string tagName, float duration, int maxStackCount)
        {
            return AddPerishableStackTag(tagID, tagName, duration, maxStackCount, CreateTrait());
        }

        private TagDefinition AddPerishableStackTag(
            TagID tagID,
            string tagName,
            float duration,
            int maxStackCount,
            RecordingTagTraitSO trait)
        {
            TagDefinition definition = CreateStackDefinition(tagID, tagName, duration, maxStackCount, trait);
            Assert.IsTrue(container.PerishableTagAdd(definition, duration));
            return definition;
        }

        private TagDefinition CreateStackDefinition(
            TagID tagID,
            string tagName,
            float duration,
            int maxStackCount,
            RecordingTagTraitSO trait)
        {
            TagDefinition definition = ScriptableObject.CreateInstance<TagDefinition>();
            createdObjects.Add(definition);
            SerializedObject serializedObject = new SerializedObject(definition);
            SetEnumProperty(serializedObject, "tagID", (int)tagID);
            SetStringProperty(serializedObject, "tagName", tagName);
            SetEnumProperty(serializedObject, "category", (int)TagCategory.Status);
            SetObjectProperty(serializedObject, "trait", trait);
            SetEnumProperty(serializedObject, "stackPolicy", (int)StackPolicy.StackCount);
            SetIntProperty(serializedObject, "maxStackCount", maxStackCount);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private TagContainer CreateContainer(string name)
        {
            GameObject newGameObject = new GameObject(name);
            createdObjects.Add(newGameObject);
            return newGameObject.AddComponent<TagContainer>();
        }

        private TagInstance GetInstance(TagDefinition definition)
        {
            return GetInstance(container, definition);
        }

        private static TagInstance GetInstance(TagContainer targetContainer, TagDefinition definition)
        {
            TagInstance instance;
            Assert.IsTrue(targetContainer.TryGetTag(definition, out instance));
            return instance;
        }

        private RecordingTagTraitSO CreateTrait()
        {
            RecordingTagTraitSO trait = ScriptableObject.CreateInstance<RecordingTagTraitSO>();
            createdObjects.Add(trait);
            return trait;
        }

        private static void SetStackCount(TagInstance instance, int stackCount)
        {
            SetBackingField(instance, "<StackCount>k__BackingField", stackCount);
        }

        private static void SetBackingField(TagInstance instance, string fieldName, object value)
        {
            FieldInfo fieldInfo = typeof(TagInstance).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(fieldInfo);
            fieldInfo.SetValue(instance, value);
        }

        private static void SetStringProperty(TagDefinition definition, string propertyName, string value)
        {
            SerializedObject serializedObject = new SerializedObject(definition);
            SetStringProperty(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIntProperty(TagDefinition definition, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(definition);
            SetIntProperty(serializedObject, propertyName, value);
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
    }
}
