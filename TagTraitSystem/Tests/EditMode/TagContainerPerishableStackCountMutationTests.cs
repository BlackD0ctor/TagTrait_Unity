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
    public sealed class TagContainerPerishableStackCountMutationTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerPerishableStackCountMutationTests");
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
        public void PerishableStackCount_WhenCreated_UsesSequentialSharedDurationModel()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 3f, 3);
            Assert.IsTrue(container.PerishableTagAdd(definition, 3f));

            TagInstance instance = GetInstance(definition);
            float totalRemainingTime = instance.RemainingTime + ((instance.StackCount - 1) * instance.Duration);
            Assert.AreEqual(2, instance.StackCount);
            Assert.AreEqual(3f, instance.Duration);
            Assert.AreEqual(3f, instance.RemainingTime);
            Assert.AreEqual(6f, totalRemainingTime);
        }

        [Test]
        public void PerishableTagAdd_WhenNormalizedDurationMatches_IncreasesStack()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1.24f, 3);

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.21f));

            Assert.AreEqual(2, GetInstance(definition).StackCount);
        }

        [Test]
        public void PerishableTagAdd_WhenNormalizedDurationDiffers_ReturnsFalseAndLogsError()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);
            LogAssert.Expect(LogType.Error, new Regex("same normalized duration"));

            Assert.IsFalse(container.PerishableTagAdd(definition, 2f));

            Assert.AreEqual(1, GetInstance(definition).StackCount);
        }

        [Test]
        public void PerishableTagAdd_WhenRawValuesNormalizeEqual_AllowsStack()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1.24f, 3);

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.21f));

            Assert.AreEqual(1.2f, GetInstance(definition).Duration);
            Assert.AreEqual(2, GetInstance(definition).StackCount);
        }

        [Test]
        public void PerishableTagAdd_WhenAtMaximum_SkipsDurationMismatchValidation()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1);

            Assert.IsFalse(container.PerishableTagAdd(definition, 2f));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PerishableTagAdd_WhenDurationMismatch_FailsWithoutStateTraitOrEvents()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3, trait);
            int changeEventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { changeEventCount++; };
            LogAssert.Expect(LogType.Error, new Regex("same normalized duration"));

            Assert.IsFalse(container.PerishableTagAdd(definition, 2f));

            Assert.AreEqual(1, GetInstance(definition).StackCount);
            Assert.AreEqual(1f, GetInstance(definition).RemainingTime);
            Assert.AreEqual(1, trait.OnAddCallCount);
            Assert.AreEqual(0, trait.OnRemoveCallCount);
            Assert.AreEqual(0, trait.OnActivateCallCount);
            Assert.AreEqual(0, changeEventCount);
        }

        [Test]
        public void PerishableTagAdd_WhenStackCountTagExists_IncreasesOneStack()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);

            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));

            Assert.AreEqual(2, GetInstance(definition).StackCount);
        }

        [Test]
        public void PerishableTagAdd_WhenStackIncreases_ResetsRemainingTimeToDuration()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 3f, 3);
            container.Tick(1f);

            Assert.IsTrue(container.PerishableTagAdd(definition, 3f));

            Assert.AreEqual(3f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void PerishableTagAdd_WhenStackIncreases_KeepsDuration()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 3f, 3);

            Assert.IsTrue(container.PerishableTagAdd(definition, 3f));

            Assert.AreEqual(3f, GetInstance(definition).Duration);
        }

        [Test]
        public void PerishableTagAdd_WhenStackIncreases_KeepsSameInstance()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 3f, 3);
            TagInstance instance = GetInstance(definition);

            Assert.IsTrue(container.PerishableTagAdd(definition, 3f));

            Assert.AreSame(instance, GetInstance(definition));
        }

        [Test]
        public void PerishableTagAdd_WhenStackIncreases_DoesNotInvokeTraitLifecycle()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3, trait);

            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));

            Assert.AreEqual(1, trait.OnAddCallCount);
            Assert.AreEqual(0, trait.OnRemoveCallCount);
            Assert.AreEqual(0, trait.OnActivateCallCount);
        }

        [Test]
        public void PerishableTagAdd_WhenStackIncreases_InvokesUpdatedOnce()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));

            Assert.AreEqual(1, updatedCount);
        }

        [Test]
        public void PerishableTagAdd_WhenStackIncreases_UsesStackIncreasedReason()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);
            TagChangeReason reason = TagChangeReason.Removed;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                reason = eventData.Reason;
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));

            Assert.AreEqual(TagChangeReason.StackIncreased, reason);
        }

        [Test]
        public void PerishableTagAdd_WhenStackIncreases_PreservesPreviousValues()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 3f, 3);
            container.Tick(1f);
            TagInstance instance = GetInstance(definition);
            TagChangeEventData capturedEvent = default(TagChangeEventData);
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                capturedEvent = eventData;
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 3f));

            Assert.AreSame(container, capturedEvent.Container);
            Assert.AreSame(definition, capturedEvent.Definition);
            Assert.AreSame(instance, capturedEvent.Instance);
            Assert.AreEqual(3f, capturedEvent.PreviousDuration);
            Assert.AreEqual(2f, capturedEvent.PreviousRemainingTime);
            Assert.AreEqual(1, capturedEvent.PreviousStackCount);
        }

        [Test]
        public void PerishableTagAdd_WhenStackIncreases_PreservesSource()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);
            GameObject source = CreateSourceObject();
            GameObject capturedSource = null;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                capturedSource = eventData.Source;
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1f, source));

            Assert.AreSame(source, capturedSource);
        }

        [Test]
        public void PerishableTagAdd_WhenCallbackQueries_SeesResetTimeAndIncreasedStack()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 3f, 3);
            container.Tick(1f);
            int stackCount = 0;
            float remainingTime = 0f;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                TagInstance instance;
                Assert.IsTrue(container.TryGetTag(definition, out instance));
                stackCount = instance.StackCount;
                remainingTime = instance.RemainingTime;
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 3f));

            Assert.AreEqual(2, stackCount);
            Assert.AreEqual(3f, remainingTime);
        }

        [Test]
        public void PerishableTagAdd_WhenAtMaximum_ReturnsFalse()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1);

            Assert.IsFalse(container.PerishableTagAdd(definition, 1f));
        }

        [Test]
        public void PerishableTagAdd_WhenAtMaximum_DoesNotResetRemainingTime()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 3f, 1);
            container.Tick(1f);

            Assert.IsFalse(container.PerishableTagAdd(definition, 3f));

            Assert.AreEqual(2f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void PerishableTagAdd_WhenAtMaximum_DoesNotInvokeTraitEventsOrLogs()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, trait);
            int changeEventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { changeEventCount++; };

            Assert.IsFalse(container.PerishableTagAdd(definition, 1f));

            Assert.AreEqual(1, trait.OnAddCallCount);
            Assert.AreEqual(0, trait.OnRemoveCallCount);
            Assert.AreEqual(0, trait.OnActivateCallCount);
            Assert.AreEqual(0, changeEventCount);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PerishableTagAdd_WhenAboveMaximum_DoesNotClamp()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);
            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));
            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));
            SetIntProperty(definition, "maxStackCount", 2);

            Assert.IsFalse(container.PerishableTagAdd(definition, 1f));

            Assert.AreEqual(3, GetInstance(definition).StackCount);
        }

        [Test]
        public void PerishableTagAdd_WhenMaximumIsOne_DuplicateReturnsFalse()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1);

            Assert.IsFalse(container.PerishableTagAdd(definition, 1f));

            Assert.AreEqual(1, GetInstance(definition).StackCount);
        }

        [Test]
        public void PerishableTagAdd_WhenMaximumIsBelowOne_ReturnsFalseAndLogsError()
        {
            TagDefinition definition = CreateStackDefinition(TagID.TestAlpha, "Alpha", 1f, 0, CreateTrait());
            LogAssert.Expect(LogType.Error, new Regex("MaxStackCount"));

            Assert.IsFalse(container.PerishableTagAdd(definition, 1f));
        }

        [Test]
        public void TagSub_WhenPerishableStackCountIsThree_DecreasesToTwo()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(3);

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(2, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagSub_WhenPerishableStackCountIsTwo_DecreasesToOne()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(2);

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, GetInstance(definition).StackCount);
        }

        [Test]
        public void TagSub_WhenPerishableStackDecreases_ResetsRemainingTimeToDuration()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(2);
            container.Tick(1f);

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(GetInstance(definition).Duration, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void TagSub_WhenPerishableStackDecreases_KeepsSameInstance()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(2);
            TagInstance instance = GetInstance(definition);

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreSame(instance, GetInstance(definition));
        }

        [Test]
        public void TagSub_WhenPerishableStackDecreases_DoesNotInvokeTraitLifecycle()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3, trait);
            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, trait.OnAddCallCount);
            Assert.AreEqual(0, trait.OnRemoveCallCount);
            Assert.AreEqual(0, trait.OnActivateCallCount);
        }

        [Test]
        public void TagSub_WhenPerishableStackDecreases_InvokesUpdatedOnce()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(2);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, updatedCount);
        }

        [Test]
        public void TagSub_WhenPerishableStackDecreases_UsesStackDecreasedReason()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(2);
            TagChangeReason reason = TagChangeReason.Added;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                reason = eventData.Reason;
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(TagChangeReason.StackDecreased, reason);
        }

        [Test]
        public void TagSub_WhenPerishableStackDecreases_PreservesPreviousValues()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(2);
            container.Tick(1f);
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
            Assert.AreEqual(3f, capturedEvent.PreviousDuration);
            Assert.AreEqual(2f, capturedEvent.PreviousRemainingTime);
            Assert.AreEqual(2, capturedEvent.PreviousStackCount);
        }

        [Test]
        public void TagSub_WhenPerishableStackDecreases_PreservesSource()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(2);
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
        public void TagSub_WhenCallbackQueries_SeesResetTimeAndDecreasedStack()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(2);
            container.Tick(1f);
            int stackCount = 0;
            float remainingTime = 0f;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                TagInstance instance;
                Assert.IsTrue(container.TryGetTag(definition, out instance));
                stackCount = instance.StackCount;
                remainingTime = instance.RemainingTime;
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, stackCount);
            Assert.AreEqual(3f, remainingTime);
        }

        [Test]
        public void TagSub_WhenPerishableStackCountIsOne_RemovesEntireTag()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagSub_WhenLastPerishableStackRemoved_DoesNotResetTime()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 3f, 3);
            container.Tick(1f);
            TagInstance instance = GetInstance(definition);

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(2f, instance.RemainingTime);
        }

        [Test]
        public void TagSub_WhenLastPerishableStackRemoved_DoesNotSetStackCountZero()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);
            TagInstance instance = GetInstance(definition);

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, instance.StackCount);
        }

        [Test]
        public void TagSub_WhenLastPerishableStackRemoved_UsesRemovedReason()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);
            TagChangeReason reason = TagChangeReason.Added;
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                reason = eventData.Reason;
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(TagChangeReason.Removed, reason);
        }

        [Test]
        public void TagSub_WhenMalformedPerishableStackDataExists_RemovesAllStacks()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(2);
            SetStringProperty(definition, "tagName", string.Empty);

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void MalformedPerishableStackRemoval_DoesNotInvokeOnRemove()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3, trait);
            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));
            SetStringProperty(definition, "tagName", string.Empty);

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(0, trait.OnRemoveCallCount);
        }

        [Test]
        public void MalformedPerishableStackRemoval_DoesNotInvokeStackDecreased()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(2);
            SetStringProperty(definition, "tagName", string.Empty);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(0, updatedCount);
        }

        [Test]
        public void Tick_WhenLaterPerishableStackTagIsIncreased_SkipsItsCurrentTick()
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
        public void Tick_WhenLaterPerishableStackTagIsDecreased_SkipsItsCurrentTick()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, alphaTrait);
            TagDefinition betaDefinition = AddPerishableStackTagWithStacks(TagID.TestBeta, "Beta", 3f, 3, 2);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                container.TagSub(betaDefinition);
            };

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.AreEqual(1, GetInstance(betaDefinition).StackCount);
            Assert.AreEqual(3f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenStackMutationOccursAfterTargetProcessed_DoesNotRestoreConsumedStacks()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition betaDefinition = AddPerishableStackTag(TagID.TestBeta, "Beta", 3f, 3);
            TagDefinition alphaDefinition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, alphaTrait);
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
        public void Tick_WhenStackMutationSucceeds_EndsWithRemainingTimeEqualDuration()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, alphaTrait);
            TagDefinition betaDefinition = AddPerishableStackTag(TagID.TestBeta, "Beta", 3f, 3);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                container.PerishableTagAdd(betaDefinition, 3f);
            };

            container.Tick(1f);

            Assert.AreEqual(GetInstance(betaDefinition).Duration, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenStackMutationFails_DoesNotMarkTickModified()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, alphaTrait);
            TagDefinition betaDefinition = AddPerishableStackTag(TagID.TestBeta, "Beta", 3f, 1);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                container.PerishableTagAdd(betaDefinition, 3f);
            };

            container.Tick(1f);

            Assert.AreEqual(2f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenMaximumNoOpOccurs_DoesNotSkipTargetTick()
        {
            Tick_WhenStackMutationFails_DoesNotMarkTickModified();
        }

        [Test]
        public void Tick_WhenDurationMismatchOccurs_DoesNotSkipTargetTick()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, alphaTrait);
            TagDefinition betaDefinition = AddPerishableStackTag(TagID.TestBeta, "Beta", 3f, 3);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                LogAssert.Expect(LogType.Error, new Regex("same normalized duration"));
                container.PerishableTagAdd(betaDefinition, 4f);
            };

            container.Tick(1f);

            Assert.AreEqual(2f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenProcessingOrderDiffers_MayProduceDifferentStackCountButRemainsValid()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1, alphaTrait);
            TagDefinition betaDefinition = AddPerishableStackTag(TagID.TestBeta, "Beta", 3f, 3);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                container.PerishableTagAdd(betaDefinition, 3f);
            };

            container.Tick(1f);

            TagInstance betaInstance = GetInstance(betaDefinition);
            Assert.That(betaInstance.StackCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(betaInstance.StackCount, Is.LessThanOrEqualTo(2));
            Assert.Greater(betaInstance.RemainingTime, 0f);
        }

        private TagDefinition AddPerishableStackTagWithStacks(int stackCount)
        {
            return AddPerishableStackTagWithStacks(TagID.TestAlpha, "Alpha", 3f, 3, stackCount);
        }

        private TagDefinition AddPerishableStackTagWithStacks(
            TagID tagID,
            string tagName,
            float duration,
            int maxStackCount,
            int stackCount)
        {
            TagDefinition definition = AddPerishableStackTag(tagID, tagName, duration, maxStackCount);
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

        private TagInstance GetInstance(TagDefinition definition)
        {
            TagInstance instance;
            Assert.IsTrue(container.TryGetTag(definition, out instance));
            return instance;
        }

        private RecordingTagTraitSO CreateTrait()
        {
            RecordingTagTraitSO trait = ScriptableObject.CreateInstance<RecordingTagTraitSO>();
            createdObjects.Add(trait);
            return trait;
        }

        private GameObject CreateSourceObject()
        {
            GameObject source = new GameObject("PerishableStackSource");
            createdObjects.Add(source);
            return source;
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
