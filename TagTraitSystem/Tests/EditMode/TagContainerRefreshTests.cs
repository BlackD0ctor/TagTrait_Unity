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
    public sealed class TagContainerRefreshTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerRefreshTests");
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
        public void PerishableTagAdd_WhenRefreshPolicyTagExists_RefreshesAndReturnsTrue()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f, StackPolicy.Refresh);

            Assert.IsTrue(container.PerishableTagAdd(definition, 2.2f));

            Assert.AreEqual(2.2f, GetInstance(definition).Duration);
        }

        [Test]
        public void Refresh_WhenApplied_ReplacesDurationAndRemainingTime()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 3f, StackPolicy.Refresh);
            container.Tick(1f);

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f));

            TagInstance instance = GetInstance(definition);
            Assert.AreEqual(2f, instance.Duration);
            Assert.AreEqual(2f, instance.RemainingTime);
        }

        [Test]
        public void Refresh_WhenNewDurationIsLonger_AppliesNewDuration()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);

            Assert.IsTrue(container.PerishableTagAdd(definition, 3f));

            Assert.AreEqual(3f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void Refresh_WhenNewDurationIsShorter_AppliesNewDuration()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 3f, StackPolicy.Refresh);

            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));

            Assert.AreEqual(1f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void Refresh_WhenDurationIsVerySmallPositive_AppliesPointOne()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);

            Assert.IsTrue(container.PerishableTagAdd(definition, 0.001f));

            Assert.AreEqual(0.1f, GetInstance(definition).Duration);
            Assert.AreEqual(0.1f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void Refresh_WhenDurationIsMidpoint_RoundsAwayFromZero()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.25f));

            Assert.AreEqual(1.3f, GetInstance(definition).Duration);
        }

        [Test]
        public void Refresh_WhenApplied_KeepsSameInstanceReference()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            TagInstance originalInstance = GetInstance(definition);

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f));

            Assert.AreSame(originalInstance, GetInstance(definition));
        }

        [Test]
        public void Refresh_WhenApplied_KeepsPerishableState()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f));

            Assert.IsTrue(GetInstance(definition).IsPerishable);
        }

        [Test]
        public void Refresh_WhenApplied_KeepsStackCount()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            int previousStackCount = GetInstance(definition).StackCount;

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f));

            Assert.AreEqual(previousStackCount, GetInstance(definition).StackCount);
        }

        [Test]
        public void Refresh_WhenValuesAreSame_ReturnsTrueAndInvokesUpdatedOnce()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f, StackPolicy.Refresh);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));

            Assert.AreEqual(1, updatedCount);
        }

        [Test]
        public void Refresh_WhenApplied_UsesDurationRefreshedReason()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            TagChangeReason reason = TagChangeReason.Added;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                reason = eventData.Reason;
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f));

            Assert.AreEqual(TagChangeReason.DurationRefreshed, reason);
        }

        [Test]
        public void Refresh_WhenApplied_PreservesPreviousValues()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 3f, StackPolicy.Refresh);
            container.Tick(1f);
            TagInstance instance = GetInstance(definition);
            TagChangeEventData capturedEvent = default(TagChangeEventData);
            bool captured = false;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                captured = true;
                capturedEvent = eventData;
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));

            Assert.IsTrue(captured);
            Assert.AreSame(container, capturedEvent.Container);
            Assert.AreSame(definition, capturedEvent.Definition);
            Assert.AreSame(instance, capturedEvent.Instance);
            Assert.AreEqual(3f, capturedEvent.PreviousDuration);
            Assert.AreEqual(2f, capturedEvent.PreviousRemainingTime);
            Assert.AreEqual(1, capturedEvent.PreviousStackCount);
        }

        [Test]
        public void Refresh_WhenSourceIsSpecified_PreservesSource()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            GameObject source = CreateSourceObject();
            GameObject capturedSource = null;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                capturedSource = eventData.Source;
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f, source));

            Assert.AreSame(source, capturedSource);
        }

        [Test]
        public void Refresh_WhenCallbackQueriesInstance_SeesUpdatedState()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            bool tagCheckResult = false;
            bool tryGetResult = false;
            float duration = 0f;
            float remainingTime = 0f;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                TagInstance foundInstance;
                tagCheckResult = container.TagCheck(definition);
                tryGetResult = container.TryGetTag(definition, out foundInstance);
                duration = foundInstance.Duration;
                remainingTime = foundInstance.RemainingTime;
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f));

            Assert.IsTrue(tagCheckResult);
            Assert.IsTrue(tryGetResult);
            Assert.AreEqual(2f, duration);
            Assert.AreEqual(2f, remainingTime);
        }

        [Test]
        public void Refresh_WhenApplied_DoesNotInvokeAddedOrRemoved()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            int addedCount = 0;
            int removedCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { addedCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { removedCount++; };

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f));

            Assert.AreEqual(0, addedCount);
            Assert.AreEqual(0, removedCount);
        }

        [Test]
        public void Refresh_WhenApplied_DoesNotInvokeTraitLifecycle()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh, trait);

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f));

            Assert.AreEqual(1, trait.OnAddCallCount);
            Assert.AreEqual(0, trait.OnRemoveCallCount);
            Assert.AreEqual(0, trait.OnActivateCallCount);
        }

        [Test]
        public void Refresh_WhenSubscriberThrows_KeepsStateInvokesLaterSubscribersAndReturnsTrue()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            int laterSubscriberCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                throw new System.InvalidOperationException("Refresh failed");
            };
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                laterSubscriberCount++;
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f));

            Assert.AreEqual(2f, GetInstance(definition).Duration);
            Assert.AreEqual(1, laterSubscriberCount);
        }

        [Test]
        public void PerishableTagAdd_WhenPermanentTagExists_ReturnsFalseWithoutChanges()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.Refresh);
            Assert.IsTrue(container.TagAdd(definition));
            TagInstance instance = GetInstance(definition);

            Assert.IsFalse(container.PerishableTagAdd(definition, 2f));

            Assert.AreSame(instance, GetInstance(definition));
            Assert.IsFalse(instance.IsPerishable);
        }

        [Test]
        public void PerishableTagAdd_WhenNonePolicyTagExists_ReturnsFalseWithoutChanges()
        {
            AssertPolicyFailureKeepsState(StackPolicy.None);
        }

        [Test]
        public void PerishableTagAdd_WhenMaxDurationPolicyTagExists_ExtendsWithStage9Policy()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.MaxDuration);

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f));

            Assert.AreEqual(2f, GetInstance(definition).Duration);
            Assert.AreEqual(2f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void PerishableTagAdd_WhenStackCountPolicyTagExistsAtDefaultMaximum_ReturnsFalse()
        {
            AssertPolicyFailureKeepsState(StackPolicy.StackCount);
        }

        [Test]
        public void PerishableTagAdd_WhenDifferentDefinitionHasSameID_ReturnsFalseAndLogsError()
        {
            TagDefinition firstDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestAlpha, "Other", TagCategory.Status, CreateTrait(), StackPolicy.Refresh);
            LogAssert.Expect(LogType.Error, new Regex("same TagID"));

            Assert.IsFalse(container.PerishableTagAdd(secondDefinition, 2f));

            Assert.AreEqual(1f, GetInstance(firstDefinition).Duration);
        }

        [Test]
        public void PerishableTagAdd_WhenDurationIsInvalid_KeepsExistingState()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            LogAssert.Expect(LogType.Error, new Regex("duration"));

            Assert.IsFalse(container.PerishableTagAdd(definition, float.NaN));

            Assert.AreEqual(1f, GetInstance(definition).Duration);
            Assert.AreEqual(1f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void PerishableTagAdd_WhenRefreshFails_DoesNotInvokeChangeEvents()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.None);
            int changeEventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { changeEventCount++; };

            Assert.IsFalse(container.PerishableTagAdd(definition, 2f));

            Assert.AreEqual(0, changeEventCount);
        }

        [Test]
        public void Refresh_WhenUpdatedCallbackChangesSameID_BlocksChange()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            bool tagAddResult = true;
            bool tagSubResult = true;
            bool perishableAddResult = true;
            bool activateResult = true;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                tagAddResult = container.TagAdd(definition);
                tagSubResult = container.TagSub(definition);
                perishableAddResult = container.PerishableTagAdd(definition, 3f);
                activateResult = container.TagActivate(definition);
            };

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f));

            Assert.IsFalse(tagAddResult);
            Assert.IsFalse(tagSubResult);
            Assert.IsFalse(perishableAddResult);
            Assert.IsFalse(activateResult);
            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(2f, GetInstance(definition).Duration);
        }

        [Test]
        public void Refresh_WhenUpdatedCallbackChangesDifferentID_AllowsChange()
        {
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta, "Beta", TagCategory.Status, CreateTrait(), StackPolicy.None);
            bool betaResult = false;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                betaResult = container.TagAdd(betaDefinition);
            };

            Assert.IsTrue(container.PerishableTagAdd(alphaDefinition, 2f));

            Assert.IsTrue(betaResult);
            Assert.IsTrue(container.TagCheck(betaDefinition));
        }

        [Test]
        public void Refresh_WhenSubscriberThrows_ReleasesReentrancyGuard()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                throw new System.InvalidOperationException("Refresh failed");
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.PerishableTagAdd(definition, 2f));

            Assert.IsTrue(container.TagSub(definition));
        }

        [Test]
        public void Tick_WhenLaterSnapshotTagIsRefreshed_SkipsItsCurrentTickProcessing()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.None, alphaTrait);
            TagDefinition betaDefinition = AddPerishableTag(TagID.TestBeta, "Beta", 2f, StackPolicy.Refresh);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                Assert.IsTrue(container.PerishableTagAdd(betaDefinition, 5f));
            };

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.AreEqual(5f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenTagWasAlreadyProcessedThenRefreshed_EndsWithFullRefreshedDuration()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition betaDefinition = AddPerishableTag(TagID.TestBeta, "Beta", 10f, StackPolicy.Refresh);
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.None, alphaTrait);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                Assert.IsTrue(container.PerishableTagAdd(betaDefinition, 5f));
            };

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.AreEqual(5f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenTagExpiredThenWasReadded_NewInstanceStartsNextTick()
        {
            RecordingTagTraitSO betaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            TagDefinition betaDefinition = AddPerishableTag(TagID.TestBeta, "Beta", 1f, StackPolicy.None, betaTrait);
            betaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                Assert.IsTrue(container.PerishableTagAdd(alphaDefinition, 5f));
            };

            container.Tick(1f);

            TagInstance alphaInstance = GetInstance(alphaDefinition);
            Assert.AreEqual(5f, alphaInstance.RemainingTime);

            container.Tick(1f);

            Assert.AreEqual(4f, alphaInstance.RemainingTime);
        }

        [Test]
        public void Tick_WhenTagRefreshesItselfDuringExpiration_BlocksRefreshAndExpires()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh, trait);
            bool refreshResult = true;
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                refreshResult = container.PerishableTagAdd(definition, 5f);
            };

            container.Tick(1f);

            Assert.IsFalse(refreshResult);
            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Tick_WhenRefreshOccurs_ResultDoesNotDependOnSnapshotOrder()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.None, alphaTrait);
            TagDefinition betaDefinition = AddPerishableTag(TagID.TestBeta, "Beta", 10f, StackPolicy.Refresh);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                Assert.IsTrue(container.PerishableTagAdd(betaDefinition, 5f));
            };

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.AreEqual(5f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenCompleted_ClearsTickModifiedTagIDsForNextTick()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.None, alphaTrait);
            TagDefinition betaDefinition = AddPerishableTag(TagID.TestBeta, "Beta", 2f, StackPolicy.Refresh);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                Assert.IsTrue(container.PerishableTagAdd(betaDefinition, 5f));
            };

            container.Tick(1f);
            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.AreEqual(4f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenLaterIndependentTickRuns_DecreasesRefreshedTime()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.Refresh);
            Assert.IsTrue(container.PerishableTagAdd(definition, 5f));

            container.Tick(1f);

            Assert.AreEqual(4f, GetInstance(definition).RemainingTime);
        }

        private void AssertPolicyFailureKeepsState(StackPolicy stackPolicy)
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, stackPolicy);
            TagInstance instance = GetInstance(definition);

            Assert.IsFalse(container.PerishableTagAdd(definition, 2f));

            Assert.AreSame(instance, GetInstance(definition));
            Assert.AreEqual(1f, instance.Duration);
            Assert.AreEqual(1f, instance.RemainingTime);
        }

        private TagDefinition AddPerishableTag(TagID tagID, string tagName, float duration, StackPolicy stackPolicy)
        {
            return AddPerishableTag(tagID, tagName, duration, stackPolicy, CreateTrait());
        }

        private TagDefinition AddPerishableTag(
            TagID tagID,
            string tagName,
            float duration,
            StackPolicy stackPolicy,
            RecordingTagTraitSO trait)
        {
            TagDefinition definition = CreateDefinition(tagID, tagName, TagCategory.Status, trait, stackPolicy);
            Assert.IsTrue(container.PerishableTagAdd(definition, duration));
            return definition;
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
            StackPolicy stackPolicy)
        {
            TagDefinition definition = ScriptableObject.CreateInstance<TagDefinition>();
            createdObjects.Add(definition);
            SerializedObject serializedObject = new SerializedObject(definition);
            SetEnumProperty(serializedObject, "tagID", (int)tagID);
            SetStringProperty(serializedObject, "tagName", tagName);
            SetEnumProperty(serializedObject, "category", (int)category);
            SetObjectProperty(serializedObject, "trait", trait);
            SetEnumProperty(serializedObject, "stackPolicy", (int)stackPolicy);
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
            GameObject source = new GameObject("RefreshSource");
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
