using System.Collections.Generic;
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
    public sealed class TagContainerPermanentStackCountTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerPermanentStackCountTests");
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
        public void TagAdd_WhenStackCountTagIsMissing_AddsWithOneStack()
        {
            TagDefinition definition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 3, CreateTrait());

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(1, GetInstance(definition).StackCount);
            Assert.IsFalse(GetInstance(definition).IsPerishable);
        }

        [Test]
        public void TagAdd_WhenStackCountMaxIsOne_AddsInitialStack()
        {
            TagDefinition definition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 1, CreateTrait());

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(1, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagAdd_WhenStackCountTagIsFirstAdded_InvokesOnAddThenAdded()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 3, trait);
            int addedCount = 0;
            int onAddCountDuringAdded = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                addedCount++;
                onAddCountDuringAdded = trait.OnAddCallCount;
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(1, addedCount);
            Assert.AreEqual(1, onAddCountDuringAdded);
        }

        [Test]
        public void TagAdd_WhenStackCountTagIsFirstAdded_UsesAddedReason()
        {
            TagDefinition definition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 3, CreateTrait());
            TagChangeReason reason = TagChangeReason.Removed;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                reason = eventData.Reason;
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(TagChangeReason.Added, reason);
        }

        [Test]
        public void TagAdd_WhenStackCountTagIsFirstAdded_UsesPreviousStackCountZero()
        {
            TagDefinition definition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 3, CreateTrait());
            int previousStackCount = -1;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                previousStackCount = eventData.PreviousStackCount;
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(0, previousStackCount);
        }

        [Test]
        public void TagAdd_WhenPermanentStackCountTagExists_IncreasesOneStack()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(2, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagAdd_WhenCalledRepeatedly_IncreasesOnePerSuccessfulCall()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);

            Assert.IsTrue(container.TagAdd(definition));
            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(3, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagAdd_WhenStackIncreases_KeepsSameInstanceReference()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            TagInstance instance = GetInstance(definition);

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreSame(instance, GetInstance(definition));
        }

        [Test]
        public void TagAdd_WhenStackIncreases_KeepsPermanentTimeState()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(0f, GetInstance(definition).Duration);
            Assert.AreEqual(0f, GetInstance(definition).RemainingTime);
            Assert.IsFalse(GetInstance(definition).IsPerishable);
        }

        [Test]
        public void TagAdd_WhenStackIncreases_DoesNotInvokeTraitLifecycle()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3, trait);

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(1, trait.OnAddCallCount);
            Assert.AreEqual(0, trait.OnRemoveCallCount);
            Assert.AreEqual(0, trait.OnActivateCallCount);
        }

        [Test]
        public void TagAdd_WhenStackIncreases_InvokesUpdatedOnce()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(1, updatedCount);
        }

        [Test]
        public void TagAdd_WhenStackIncreases_UsesStackIncreasedReason()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            TagChangeReason reason = TagChangeReason.Removed;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                reason = eventData.Reason;
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(TagChangeReason.StackIncreased, reason);
        }

        [Test]
        public void TagAdd_WhenStackIncreases_PreservesPreviousValues()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            TagInstance instance = GetInstance(definition);
            TagChangeEventData capturedEvent = default(TagChangeEventData);
            bool captured = false;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                captured = true;
                capturedEvent = eventData;
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(captured);
            Assert.AreSame(container, capturedEvent.Container);
            Assert.AreSame(definition, capturedEvent.Definition);
            Assert.AreSame(instance, capturedEvent.Instance);
            Assert.AreEqual(0f, capturedEvent.PreviousDuration);
            Assert.AreEqual(0f, capturedEvent.PreviousRemainingTime);
            Assert.AreEqual(1, capturedEvent.PreviousStackCount);
        }

        [Test]
        public void TagAdd_WhenStackIncreases_PreservesSource()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            GameObject source = CreateSourceObject();
            GameObject capturedSource = null;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                capturedSource = eventData.Source;
            };

            Assert.IsTrue(container.TagAdd(definition, source));

            Assert.AreSame(source, capturedSource);
        }

        [Test]
        public void TagAdd_WhenUpdatedCallbackQueries_SeesIncreasedStack()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            int stackCount = 0;
            bool found = false;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                TagInstance instance;
                found = container.TryGetTag(definition, out instance);
                stackCount = instance.StackCount;
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(found);
            Assert.AreEqual(2, stackCount);
        }

        [Test]
        public void TagAdd_WhenStackIncreases_DoesNotInvokeAddedOrRemoved()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            int addedCount = 0;
            int removedCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { addedCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { removedCount++; };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(0, addedCount);
            Assert.AreEqual(0, removedCount);
        }

        [Test]
        public void TagAdd_WhenUpdatedSubscriberThrows_KeepsIncreaseInvokesLaterSubscribersAndReturnsTrue()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            int laterSubscriberCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                throw new System.InvalidOperationException("Stack increase failed");
            };
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                laterSubscriberCount++;
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(2, GetInstance(definition).StackCount);
            Assert.AreEqual(1, laterSubscriberCount);
        }

        [Test]
        public void TagAdd_WhenStackCountReachesMaximum_ReturnsFalse()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 2);
            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(container.TagAdd(definition));
        }

        [Test]
        public void TagAdd_WhenAtMaximum_DoesNotChangeState()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 2);
            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(container.TagAdd(definition));

            Assert.AreEqual(2, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagAdd_WhenAtMaximum_DoesNotInvokeTraitOrEvents()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 2, trait);
            Assert.IsTrue(container.TagAdd(definition));
            int changeEventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { changeEventCount++; };

            Assert.IsFalse(container.TagAdd(definition));

            Assert.AreEqual(1, trait.OnAddCallCount);
            Assert.AreEqual(0, trait.OnRemoveCallCount);
            Assert.AreEqual(0, trait.OnActivateCallCount);
            Assert.AreEqual(0, changeEventCount);
        }

        [Test]
        public void TagAdd_WhenAtMaximum_DoesNotLogError()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 1);

            Assert.IsFalse(container.TagAdd(definition));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TagAdd_WhenStackCountIsAboveMaximum_DoesNotClampAndReturnsFalse()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            Assert.IsTrue(container.TagAdd(definition));
            Assert.IsTrue(container.TagAdd(definition));
            SetIntProperty(definition, "maxStackCount", 2);

            Assert.IsFalse(container.TagAdd(definition));

            Assert.AreEqual(3, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagAdd_WhenMaximumIsOne_DuplicateAddReturnsFalse()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 1);

            Assert.IsFalse(container.TagAdd(definition));

            Assert.AreEqual(1, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagAdd_WhenStackCountMaximumIsBelowOne_ReturnsFalseAndLogsError()
        {
            TagDefinition definition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 0, CreateTrait());
            LogAssert.Expect(LogType.Error, new Regex("MaxStackCount"));

            Assert.IsFalse(container.TagAdd(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void PerishableTagAdd_WhenStackCountMaximumIsBelowOne_ReturnsFalseAndLogsError()
        {
            TagDefinition definition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 0, CreateTrait());
            LogAssert.Expect(LogType.Error, new Regex("MaxStackCount"));

            Assert.IsFalse(container.PerishableTagAdd(definition, 1f));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void InvalidMaximumFailure_DoesNotChangeStateTraitOrEvents()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 0, trait);
            int changeEventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { changeEventCount++; };
            LogAssert.Expect(LogType.Error, new Regex("MaxStackCount"));

            Assert.IsFalse(container.TagAdd(definition));

            Assert.AreEqual(0, trait.OnAddCallCount);
            Assert.AreEqual(0, trait.OnRemoveCallCount);
            Assert.AreEqual(0, trait.OnActivateCallCount);
            Assert.AreEqual(0, changeEventCount);
            Assert.AreEqual(0, container.TagScan().Count);
        }

        [Test]
        public void TagSub_WhenPermanentStackCountIsThree_DecreasesToTwo()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(3);

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(2, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagSub_WhenPermanentStackCountIsTwo_DecreasesToOne()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(2);

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagSub_WhenStackDecreases_KeepsTagAndSameInstance()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(2);
            TagInstance instance = GetInstance(definition);

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreSame(instance, GetInstance(definition));
        }

        [Test]
        public void TagSub_WhenStackDecreases_DoesNotInvokeTraitLifecycle()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3, trait);
            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, trait.OnAddCallCount);
            Assert.AreEqual(0, trait.OnRemoveCallCount);
            Assert.AreEqual(0, trait.OnActivateCallCount);
        }

        [Test]
        public void TagSub_WhenStackDecreases_InvokesUpdatedOnce()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(2);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, updatedCount);
        }

        [Test]
        public void TagSub_WhenStackDecreases_UsesStackDecreasedReason()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(2);
            TagChangeReason reason = TagChangeReason.Added;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                reason = eventData.Reason;
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(TagChangeReason.StackDecreased, reason);
        }

        [Test]
        public void TagSub_WhenStackDecreases_PreservesPreviousValues()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(2);
            TagInstance instance = GetInstance(definition);
            TagChangeEventData capturedEvent = default(TagChangeEventData);
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                capturedEvent = eventData;
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreSame(container, capturedEvent.Container);
            Assert.AreSame(definition, capturedEvent.Definition);
            Assert.AreSame(instance, capturedEvent.Instance);
            Assert.AreEqual(0f, capturedEvent.PreviousDuration);
            Assert.AreEqual(0f, capturedEvent.PreviousRemainingTime);
            Assert.AreEqual(2, capturedEvent.PreviousStackCount);
        }

        [Test]
        public void TagSub_WhenStackDecreases_PreservesSource()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(2);
            GameObject source = CreateSourceObject();
            GameObject capturedSource = null;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                capturedSource = eventData.Source;
            };

            Assert.IsTrue(container.TagSub(definition, source));

            Assert.AreSame(source, capturedSource);
        }

        [Test]
        public void TagSub_WhenUpdatedCallbackQueries_SeesDecreasedStack()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(2);
            int stackCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                TagInstance instance;
                Assert.IsTrue(container.TryGetTag(definition, out instance));
                stackCount = instance.StackCount;
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, stackCount);
        }

        [Test]
        public void TagSub_WhenStackDecreases_DoesNotInvokeAddedOrRemoved()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(2);
            int addedCount = 0;
            int removedCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { addedCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { removedCount++; };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(0, addedCount);
            Assert.AreEqual(0, removedCount);
        }

        [Test]
        public void TagSub_WhenUpdatedSubscriberThrows_KeepsDecreaseInvokesLaterSubscribersAndReturnsTrue()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(2);
            int laterSubscriberCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                throw new System.InvalidOperationException("Stack decrease failed");
            };
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                laterSubscriberCount++;
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, GetInstance(definition).StackCount);
            Assert.AreEqual(1, laterSubscriberCount);
        }

        [Test]
        public void TagSub_WhenStackCountIsAboveMaximum_DecreasesOnlyOneWithoutClamping()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(3);
            SetIntProperty(definition, "maxStackCount", 2);
            LogAssert.Expect(LogType.Error, new Regex("StackCount greater than MaxStackCount"));

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(2, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagSub_WhenPermanentStackCountIsOne_RemovesTag()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagSub_WhenLastStackRemoved_InvokesOnRemoveBeforeRemoval()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3, trait);
            bool existedDuringOnRemove = false;
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                existedDuringOnRemove = container.TagCheck(definition);
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsTrue(existedDuringOnRemove);
        }

        [Test]
        public void TagSub_WhenLastStackRemoved_InvokesRemovedOnce()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            int removedCount = 0;
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { removedCount++; };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, removedCount);
        }

        [Test]
        public void TagSub_WhenLastStackRemoved_UsesRemovedReason()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            TagChangeReason reason = TagChangeReason.Added;
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                reason = eventData.Reason;
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(TagChangeReason.Removed, reason);
        }

        [Test]
        public void TagSub_WhenLastStackRemoved_UsesPreviousStackCountOne()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            int previousStackCount = 0;
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                previousStackCount = eventData.PreviousStackCount;
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, previousStackCount);
        }

        [Test]
        public void TagSub_WhenLastStackRemoved_DoesNotInvokeStackDecreased()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(0, updatedCount);
        }

        [Test]
        public void TagSub_WhenLastStackRemoved_DoesNotSetStackCountToZero()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            TagInstance instance = GetInstance(definition);

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, instance.StackCount);
        }

        [Test]
        public void TagSub_WhenKeywordTraitStackCountDataIsMalformed_RemovesAllStacks()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(3);
            SetEnumProperty(definition, "category", (int)TagCategory.Keyword);

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagSub_WhenNonKeywordNullTraitStackCountDataIsMalformed_RemovesAllStacks()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(3);
            SetObjectProperty(definition, "trait", null);

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagSub_WhenNameIsEmptyAndHasMultipleStacks_RemovesAllStacks()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(3);
            SetStringProperty(definition, "tagName", string.Empty);

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagSub_WhenMaximumIsBelowOneAndTagExists_RemovesAllStacks()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(3);
            SetIntProperty(definition, "maxStackCount", 0);
            LogAssert.Expect(LogType.Error, new Regex("MaxStackCount less than 1"));

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void MalformedStackRemoval_DoesNotInvokeOnRemove()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3, trait);
            Assert.IsTrue(container.TagAdd(definition));
            SetStringProperty(definition, "tagName", string.Empty);

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(0, trait.OnRemoveCallCount);
        }

        [Test]
        public void MalformedStackRemoval_InvokesRemovedOnce()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(3);
            SetStringProperty(definition, "tagName", string.Empty);
            int removedCount = 0;
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { removedCount++; };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, removedCount);
        }

        [Test]
        public void MalformedStackRemoval_DoesNotInvokeStackDecreased()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(3);
            SetStringProperty(definition, "tagName", string.Empty);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(0, updatedCount);
        }

        [Test]
        public void MalformedStackRemoval_DoesNotNormalizeStackCountBeforeEvent()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(3);
            TagInstance instance = GetInstance(definition);
            SetStringProperty(definition, "tagName", string.Empty);
            int previousStackCount = 0;
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                previousStackCount = eventData.PreviousStackCount;
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(3, previousStackCount);
            Assert.AreEqual(3, instance.StackCount);
        }

        [Test]
        public void TagAdd_WhenPermanentNoneTagExists_ReturnsFalse()
        {
            AssertPermanentDuplicatePolicyFails(StackPolicy.None);
        }

        [Test]
        public void TagAdd_WhenPermanentRefreshTagExists_ReturnsFalse()
        {
            AssertPermanentDuplicatePolicyFails(StackPolicy.Refresh);
        }

        [Test]
        public void TagAdd_WhenPermanentMaxDurationTagExists_ReturnsFalse()
        {
            AssertPermanentDuplicatePolicyFails(StackPolicy.MaxDuration);
        }

        [Test]
        public void PerishableTagAdd_WhenPerishableStackCountTagExists_IncreasesStack()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);

            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));

            Assert.AreEqual(2, GetInstance(definition).StackCount);
            Assert.IsTrue(GetInstance(definition).IsPerishable);
        }

        [Test]
        public void PerishableTagAdd_WhenPermanentStackCountTagExists_ReturnsFalse()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);

            Assert.IsFalse(container.PerishableTagAdd(definition, 1f));

            Assert.IsFalse(GetInstance(definition).IsPerishable);
            Assert.AreEqual(1, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagSub_WhenPerishableStackCountHasOneStack_RemovesEntireTag()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagAdd_WhenPerishableStackCountTagExists_ConvertsOnlyToPermanent()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(GetInstance(definition).IsPerishable);
            Assert.AreEqual(1, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagAdd_WhenConversionOccurs_DoesNotIncreaseStackInSameCall()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(1, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagAdd_WhenConvertedThenAddedAgain_IncreasesSubjectToMaximum()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 2);

            Assert.IsTrue(container.TagAdd(definition));
            Assert.IsTrue(container.TagAdd(definition));
            Assert.IsFalse(container.TagAdd(definition));

            Assert.AreEqual(2, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagAdd_WhenInvalidMaximumPerishableTagExists_DoesNotConvert()
        {
            TagDefinition definition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 0, CreateTrait());
            TagInstance instance = new TagInstance(definition, 1f);
            Assert.IsTrue(container.TryAddInstance(instance));
            LogAssert.Expect(LogType.Error, new Regex("MaxStackCount"));

            Assert.IsFalse(container.TagAdd(definition));

            Assert.IsTrue(GetInstance(definition).IsPerishable);
        }

        [Test]
        public void TagAdd_WhenStackIncreasedCallbackChangesSameID_BlocksChange()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            bool tagAddResult = true;
            bool tagSubResult = true;
            bool perishableAddResult = true;
            bool activateResult = true;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                tagAddResult = container.TagAdd(definition);
                tagSubResult = container.TagSub(definition);
                perishableAddResult = container.PerishableTagAdd(definition, 1f);
                activateResult = container.TagActivate(definition);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(tagAddResult);
            Assert.IsFalse(tagSubResult);
            Assert.IsFalse(perishableAddResult);
            Assert.IsFalse(activateResult);
            Assert.AreEqual(2, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagSub_WhenStackDecreasedCallbackChangesSameID_BlocksChange()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(2);
            bool tagAddResult = true;
            bool tagSubResult = true;
            bool perishableAddResult = true;
            bool activateResult = true;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                tagAddResult = container.TagAdd(definition);
                tagSubResult = container.TagSub(definition);
                perishableAddResult = container.PerishableTagAdd(definition, 1f);
                activateResult = container.TagActivate(definition);
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(tagAddResult);
            Assert.IsFalse(tagSubResult);
            Assert.IsFalse(perishableAddResult);
            Assert.IsFalse(activateResult);
            Assert.AreEqual(1, GetInstance(definition).StackCount);
        }

        [Test]
        public void StackUpdatedCallback_WhenChangingDifferentID_AllowsChange()
        {
            TagDefinition alphaDefinition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta, "Beta", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            bool betaResult = false;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                betaResult = container.TagAdd(betaDefinition);
            };

            Assert.IsTrue(container.TagAdd(alphaDefinition));

            Assert.IsTrue(betaResult);
            Assert.IsTrue(container.TagCheck(betaDefinition));
        }

        [Test]
        public void StackUpdatedCallback_WhenQueryingSameID_AllowsQuery()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            bool checkResult = false;
            bool getResult = false;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                TagInstance instance;
                checkResult = container.TagCheck(definition);
                getResult = container.TryGetTag(definition, out instance);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(checkResult);
            Assert.IsTrue(getResult);
        }

        [Test]
        public void StackUpdatedSubscriber_WhenThrowing_ReleasesGuard()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                throw new System.InvalidOperationException("Stack update failed");
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.TagAdd(definition));

            LogAssert.Expect(LogType.Error, new Regex("subscriber"));
            Assert.IsTrue(container.TagSub(definition));
        }

        [Test]
        public void TagAdd_WhenAtMaximum_ReleasesGuard()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 1);

            Assert.IsFalse(container.TagAdd(definition));
            Assert.IsTrue(container.TagSub(definition));
        }

        [Test]
        public void TagAdd_WhenInvalidMaximumFails_ReleasesGuard()
        {
            TagDefinition definition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 0, CreateTrait());
            LogAssert.Expect(LogType.Error, new Regex("MaxStackCount"));

            Assert.IsFalse(container.TagAdd(definition));

            SetIntProperty(definition, "maxStackCount", 1);
            Assert.IsTrue(container.TagAdd(definition));
        }

        private void AssertPermanentDuplicatePolicyFails(StackPolicy stackPolicy)
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), stackPolicy, 3);
            Assert.IsTrue(container.TagAdd(definition));
            TagInstance instance = GetInstance(definition);

            Assert.IsFalse(container.TagAdd(definition));

            Assert.AreSame(instance, GetInstance(definition));
            Assert.AreEqual(1, instance.StackCount);
        }

        private TagDefinition AddPermanentStackTagWithStacks(int stackCount)
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 3);
            for (int i = 1; i < stackCount; i++)
            {
                Assert.IsTrue(container.TagAdd(definition));
            }

            return definition;
        }

        private TagDefinition AddPermanentStackTag(TagID tagID, string tagName, int maxStackCount)
        {
            return AddPermanentStackTag(tagID, tagName, maxStackCount, CreateTrait());
        }

        private TagDefinition AddPermanentStackTag(
            TagID tagID,
            string tagName,
            int maxStackCount,
            RecordingTagTraitSO trait)
        {
            TagDefinition definition = CreateStackDefinition(tagID, tagName, maxStackCount, trait);
            Assert.IsTrue(container.TagAdd(definition));
            return definition;
        }

        private TagDefinition AddPerishableStackTag(TagID tagID, string tagName, float duration, int maxStackCount)
        {
            TagDefinition definition = CreateStackDefinition(tagID, tagName, maxStackCount, CreateTrait());
            Assert.IsTrue(container.PerishableTagAdd(definition, duration));
            return definition;
        }

        private TagDefinition CreateStackDefinition(
            TagID tagID,
            string tagName,
            int maxStackCount,
            RecordingTagTraitSO trait)
        {
            return CreateDefinition(tagID, tagName, TagCategory.Status, trait, StackPolicy.StackCount, maxStackCount);
        }

        private TagInstance GetInstance(TagDefinition definition)
        {
            TagInstance instance;
            Assert.IsTrue(container.TryGetTag(definition, out instance));
            return instance;
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

        private RecordingTagTraitSO CreateTrait()
        {
            RecordingTagTraitSO trait = ScriptableObject.CreateInstance<RecordingTagTraitSO>();
            createdObjects.Add(trait);
            return trait;
        }

        private GameObject CreateSourceObject()
        {
            GameObject source = new GameObject("PermanentStackSource");
            createdObjects.Add(source);
            return source;
        }

        private static void SetEnumProperty(TagDefinition definition, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(definition);
            SetEnumProperty(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStringProperty(TagDefinition definition, string propertyName, string value)
        {
            SerializedObject serializedObject = new SerializedObject(definition);
            SetStringProperty(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectProperty(TagDefinition definition, string propertyName, Object value)
        {
            SerializedObject serializedObject = new SerializedObject(definition);
            SetObjectProperty(serializedObject, propertyName, value);
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
