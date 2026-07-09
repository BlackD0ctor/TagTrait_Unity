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
    public sealed class TagContainerPermanentConversionTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerPermanentConversionTests");
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
        public void TagAdd_WhenPerishableTagExists_ConvertsToPermanentAndReturnsTrue()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);

            Assert.IsTrue(container.TagAdd(definition));

            TagInstance instance = GetInstance(definition);
            Assert.IsFalse(instance.IsPerishable);
        }

        [Test]
        public void TagAdd_WhenConvertingToPermanent_KeepsSameInstanceReference()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            TagInstance originalInstance = GetInstance(definition);

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreSame(originalInstance, GetInstance(definition));
        }

        [Test]
        public void TagAdd_WhenConvertingToPermanent_SetsDurationAndRemainingTimeToZero()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);

            Assert.IsTrue(container.TagAdd(definition));

            TagInstance instance = GetInstance(definition);
            Assert.AreEqual(0f, instance.Duration);
            Assert.AreEqual(0f, instance.RemainingTime);
        }

        [Test]
        public void TagAdd_WhenConvertingToPermanent_PreservesStackCount()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            TagInstance instance = GetInstance(definition);
            int previousStackCount = instance.StackCount;

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(previousStackCount, instance.StackCount);
        }

        [Test]
        public void ConvertedPermanentTag_WhenTickRuns_DoesNotDecreaseOrExpire()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            Assert.IsTrue(container.TagAdd(definition));

            container.Tick(5f);

            TagInstance instance = GetInstance(definition);
            Assert.IsFalse(instance.IsPerishable);
            Assert.AreEqual(0f, instance.RemainingTime);
            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void TagAdd_WhenPerishableRemainingTimeIsZeroButStillHeld_ConvertsToPermanent()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None);
            TagInstance instance = new TagInstance(definition, 0f);
            Assert.IsTrue(container.TryAddInstance(instance));

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreSame(instance, GetInstance(definition));
            Assert.IsFalse(instance.IsPerishable);
            Assert.AreEqual(0f, instance.RemainingTime);
        }

        [Test]
        public void ExistingTagScanSnapshot_WhenTagConverts_ReflectsSameInstanceState()
        {
            AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            IReadOnlyCollection<TagInstance> snapshot = container.TagScan();
            TagInstance scannedInstance = GetFirstInstance(snapshot);

            Assert.IsTrue(container.TagAdd(scannedInstance.Definition));

            Assert.IsFalse(scannedInstance.IsPerishable);
            Assert.AreEqual(0f, scannedInstance.Duration);
            Assert.AreEqual(0f, scannedInstance.RemainingTime);
        }

        [Test]
        public void TagAdd_WhenConvertingToPermanent_DoesNotInvokeTraitLifecycle()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f, trait);
            Assert.AreEqual(1, trait.OnAddCallCount);

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(1, trait.OnAddCallCount);
            Assert.AreEqual(0, trait.OnRemoveCallCount);
            Assert.AreEqual(0, trait.OnActivateCallCount);
        }

        [Test]
        public void TagAdd_WhenKeywordConverts_DoesNotInvokeTrait()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, null, StackPolicy.None);
            Assert.IsTrue(container.PerishableTagAdd(definition, 1.2f));

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(GetInstance(definition).IsPerishable);
        }

        [Test]
        public void OnTagUpdated_WhenTagConvertsToPermanent_InvokesOnce()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            int updatedCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(1, updatedCount);
        }

        [Test]
        public void OnTagUpdated_WhenTagConverts_UsesChangedToPermanentReason()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            TagChangeReason reason = TagChangeReason.Added;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                reason = eventData.Reason;
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(TagChangeReason.ChangedToPermanent, reason);
        }

        [Test]
        public void OnTagUpdated_WhenTagConverts_ContainsPreviousValues()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
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
            Assert.AreEqual(1.2f, capturedEvent.PreviousDuration);
            Assert.AreEqual(1.2f, capturedEvent.PreviousRemainingTime);
            Assert.AreEqual(1, capturedEvent.PreviousStackCount);
        }

        [Test]
        public void OnTagUpdated_WhenTagConverts_PreservesSource()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
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
        public void OnTagUpdated_WhenCallbackQueriesContainer_SeesPermanentState()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            bool tagCheckResult = false;
            bool tryGetResult = false;
            bool isPerishable = true;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                TagInstance foundInstance;
                tagCheckResult = container.TagCheck(definition);
                tryGetResult = container.TryGetTag(definition, out foundInstance);
                isPerishable = foundInstance.IsPerishable;
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(tagCheckResult);
            Assert.IsTrue(tryGetResult);
            Assert.IsFalse(isPerishable);
        }

        [Test]
        public void TagAdd_WhenTagConverts_DoesNotInvokeAddedOrRemoved()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            int addedCount = 0;
            int removedCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { addedCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { removedCount++; };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(0, addedCount);
            Assert.AreEqual(0, removedCount);
        }

        [Test]
        public void TagAdd_WhenUpdatedSubscriberThrows_KeepsConversionAndReturnsTrue()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                throw new System.InvalidOperationException("Updated failed");
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(GetInstance(definition).IsPerishable);
        }

        [Test]
        public void SafeInvoke_WhenUpdatedSubscriberThrows_InvokesLaterSubscribers()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            int laterSubscriberCount = 0;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                throw new System.InvalidOperationException("Updated failed");
            };
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                laterSubscriberCount++;
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(1, laterSubscriberCount);
        }

        [Test]
        public void TagAdd_WhenPermanentTagAlreadyExists_ReturnsFalseWithoutEvent()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None);
            int changeEventCount = 0;
            Assert.IsTrue(container.TagAdd(definition));
            container.OnTagAdded += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { changeEventCount++; };

            Assert.IsFalse(container.TagAdd(definition));

            Assert.AreEqual(0, changeEventCount);
        }

        [Test]
        public void TagAdd_WhenPermanentStackCountPolicyTagExistsAtDefaultMaximum_ReturnsFalse()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.StackCount);
            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(container.TagAdd(definition));

            Assert.AreEqual(1, GetInstance(definition).StackCount);
        }

        [Test]
        public void PerishableTagAdd_WhenPermanentTagExists_ReturnsFalseWithoutChanges()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None);
            Assert.IsTrue(container.TagAdd(definition));
            TagInstance instance = GetInstance(definition);

            Assert.IsFalse(container.PerishableTagAdd(definition, 1.2f));

            Assert.AreSame(instance, GetInstance(definition));
            Assert.IsFalse(instance.IsPerishable);
        }

        [Test]
        public void PerishableTagAdd_WhenPerishableTagExists_ReturnsFalseWithoutChanges()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            TagInstance instance = GetInstance(definition);

            Assert.IsFalse(container.PerishableTagAdd(definition, 2.2f));

            Assert.AreSame(instance, GetInstance(definition));
            Assert.AreEqual(1.2f, instance.Duration);
        }

        [Test]
        public void TagAdd_WhenDifferentDefinitionHasSameID_ReturnsFalseAndKeepsExisting()
        {
            TagDefinition firstDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestAlpha, "Other", TagCategory.Status, CreateTrait(), StackPolicy.None);
            LogAssert.Expect(LogType.Error, new Regex("same TagID"));

            Assert.IsFalse(container.TagAdd(secondDefinition));

            Assert.IsTrue(container.TagCheck(firstDefinition));
            Assert.IsTrue(GetInstance(firstDefinition).IsPerishable);
        }

        [Test]
        public void FailedConversionPath_WhenCalled_DoesNotInvokeAnyChangeEvent()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None);
            int changeEventCount = 0;
            Assert.IsTrue(container.TagAdd(definition));
            container.OnTagAdded += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { changeEventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { changeEventCount++; };

            Assert.IsFalse(container.TagAdd(definition));

            Assert.AreEqual(0, changeEventCount);
        }

        [Test]
        public void OnTagUpdated_WhenSameIDTagAddIsRequested_ReturnsFalse()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            bool reentrantResult = true;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                reentrantResult = container.TagAdd(definition);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(reentrantResult);
        }

        [Test]
        public void OnTagUpdated_WhenSameIDTagSubIsRequested_ReturnsFalse()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            bool reentrantResult = true;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                reentrantResult = container.TagSub(definition);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(reentrantResult);
            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void OnTagUpdated_WhenSameIDPerishableAddIsRequested_ReturnsFalse()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            bool reentrantResult = true;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                reentrantResult = container.PerishableTagAdd(definition, 1.2f);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(reentrantResult);
        }

        [Test]
        public void OnTagUpdated_WhenSameIDActivateIsRequested_ReturnsFalse()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            bool reentrantResult = true;
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                reentrantResult = container.TagActivate(definition);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(reentrantResult);
        }

        [Test]
        public void OnTagUpdated_WhenDifferentIDIsChanged_AllowsChange()
        {
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta, "Beta", TagCategory.Status, CreateTrait(), StackPolicy.None);
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
        public void UpdatedSubscriberException_WhenHandled_ReleasesGuard()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1.2f);
            container.OnTagUpdated += delegate(TagChangeEventData eventData)
            {
                throw new System.InvalidOperationException("Updated failed");
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagSub(definition));
        }

        [Test]
        public void Tick_WhenSnapshotTagConvertsBeforeItsTurn_SkipsConvertedInstance()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            TagDefinition alphaDefinition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, alphaTrait);
            TagDefinition betaDefinition = AddPerishableTag(TagID.TestBeta, "Beta", 2f);
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                Assert.IsTrue(container.TagAdd(betaDefinition));
            };

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            TagInstance betaInstance = GetInstance(betaDefinition);
            Assert.IsFalse(betaInstance.IsPerishable);
            Assert.AreEqual(0f, betaInstance.RemainingTime);
        }

        private TagDefinition AddPerishableTag(TagID tagID, string tagName, float duration)
        {
            return AddPerishableTag(tagID, tagName, duration, CreateTrait());
        }

        private TagDefinition AddPerishableTag(TagID tagID, string tagName, float duration, RecordingTagTraitSO trait)
        {
            TagDefinition definition = CreateDefinition(tagID, tagName, TagCategory.Status, trait, StackPolicy.None);
            Assert.IsTrue(container.PerishableTagAdd(definition, duration));
            return definition;
        }

        private TagInstance GetInstance(TagDefinition definition)
        {
            TagInstance instance;
            Assert.IsTrue(container.TryGetTag(definition, out instance));
            return instance;
        }

        private TagInstance GetFirstInstance(IReadOnlyCollection<TagInstance> instances)
        {
            foreach (TagInstance instance in instances)
            {
                return instance;
            }

            Assert.Fail("Expected at least one tag instance.");
            return null;
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
            GameObject source = new GameObject("PermanentConversionSource");
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
