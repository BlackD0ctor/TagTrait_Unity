using System;
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
    public sealed class TagContainerDiagnosticsTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;
        private ILogHandler originalLogHandler;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerDiagnosticsTests");
            createdObjects.Add(gameObject);
            container = gameObject.AddComponent<TagContainer>();
        }

        [TearDown]
        public void TearDown()
        {
            if (originalLogHandler != null)
            {
                Debug.unityLogger.logHandler = originalLogHandler;
                originalLogHandler = null;
            }

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
        public void TagAdd_WhenDefinitionIsNull_UsesTagDiagnosticsError()
        {
            LogAssert.Expect(LogType.Error, new Regex("^\\[TagTraitSystem\\].*null tag definition"));

            Assert.IsFalse(container.TagAdd(null));
        }

        [Test]
        public void TagAdd_WhenTagIDIsNone_UsesTagDiagnosticsError()
        {
            TagDefinition definition = CreateDefinition(TagID.None, "None", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            LogAssert.Expect(LogType.Error, new Regex("^\\[TagTraitSystem\\].*TagID\\.None"));

            Assert.IsFalse(container.TagAdd(definition));
        }

        [Test]
        public void TagAdd_WhenDefinitionReferenceConflicts_UsesTagDiagnosticsError()
        {
            TagDefinition firstDefinition = AddPermanentTag(TagID.TestAlpha, "Alpha");
            TagDefinition secondDefinition = CreateDefinition(TagID.TestAlpha, "Other", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            LogAssert.Expect(LogType.Error, new Regex("^\\[TagTraitSystem\\].*same TagID"));

            Assert.IsFalse(container.TagAdd(secondDefinition));
            Assert.IsTrue(container.TagCheck(firstDefinition));
        }

        [Test]
        public void PerishableTagAdd_WhenDurationIsInvalid_UsesTagDiagnosticsError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            LogAssert.Expect(LogType.Error, new Regex("^\\[TagTraitSystem\\].*duration"));

            Assert.IsFalse(container.PerishableTagAdd(definition, float.NaN));
        }

        [Test]
        public void Tick_WhenDeltaTimeIsInvalid_UsesTagDiagnosticsError()
        {
            LogAssert.Expect(LogType.Error, new Regex("^\\[TagTraitSystem\\].*deltaTime"));

            container.Tick(float.PositiveInfinity);
        }

        [Test]
        public void PerishableTagAdd_WhenStackDurationDiffers_UsesTagDiagnosticsError()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 3);
            LogAssert.Expect(LogType.Error, new Regex("^\\[TagTraitSystem\\].*same normalized duration"));

            Assert.IsFalse(container.PerishableTagAdd(definition, 2f));
        }

        [Test]
        public void StackMutation_WhenMaximumIsInvalid_UsesTagDiagnosticsError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.StackCount, 0);
            LogAssert.Expect(LogType.Error, new Regex("^\\[TagTraitSystem\\].*MaxStackCount"));

            Assert.IsFalse(container.TagAdd(definition));
        }

        [Test]
        public void TagAdd_WhenPermanentStackIsAtMaximum_DoesNotLog()
        {
            TagDefinition definition = AddPermanentStackTag(TagID.TestAlpha, "Alpha", 1);

            Assert.IsFalse(container.TagAdd(definition));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PerishableTagAdd_WhenPerishableStackIsAtMaximum_DoesNotLog()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 1f, 1);

            Assert.IsFalse(container.PerishableTagAdd(definition, 1f));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PerishableTagAdd_WhenMaxDurationDoesNotExtend_DoesNotLog()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 3f, StackPolicy.MaxDuration);

            Assert.IsFalse(container.PerishableTagAdd(definition, 2f));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TagAdd_WhenNonePolicyDuplicateOccurs_DoesNotLog()
        {
            TagDefinition definition = AddPermanentTag(TagID.TestAlpha, "Alpha");

            Assert.IsFalse(container.TagAdd(definition));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TagSub_WhenTagIsMissing_DoesNotLog()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            Assert.IsFalse(container.TagSub(definition));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TagActivate_WhenValidKeywordTag_DoesNotLog()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, null, StackPolicy.None, 1);

            Assert.IsFalse(container.TagActivate(definition));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TagAdd_WhenSameIDReenters_LogsWarningOnce()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                container.TagAdd(definition);
            };
            ExpectReentrantWarning();

            Assert.IsTrue(container.TagAdd(definition));
        }

        [Test]
        public void TagSub_WhenSameIDReenters_LogsWarningOnce()
        {
            TagDefinition definition = AddPermanentTag(TagID.TestAlpha, "Alpha");
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                container.TagSub(definition);
            };
            ExpectReentrantWarning();

            Assert.IsTrue(container.TagSub(definition));
        }

        [Test]
        public void PerishableTagAdd_WhenSameIDReenters_LogsWarningOnce()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait, StackPolicy.None, 1);
            trait.OnAddAction = delegate(TraitContext context)
            {
                container.PerishableTagAdd(definition, 1f);
            };
            ExpectReentrantWarning();

            Assert.IsTrue(container.PerishableTagAdd(definition, 1f));
        }

        [Test]
        public void TagActivate_WhenSameIDReenters_LogsWarningOnce()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPermanentTag(TagID.TestAlpha, "Alpha", trait);
            trait.OnActivateAction = delegate(TraitContext context)
            {
                container.TagActivate(definition);
            };
            ExpectReentrantWarning();

            Assert.IsTrue(container.TagActivate(definition));
        }

        [Test]
        public void ReentrantMutation_WhenBlocked_DoesNotChangeStateTraitOrEvents()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            int removedCount = 0;
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { removedCount++; };
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                container.TagSub(definition);
            };
            ExpectReentrantWarning();

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(0, removedCount);
        }

        [Test]
        public void ReentrantMutation_WhenDifferentID_AllowsWithoutWarning()
        {
            TagDefinition alphaDefinition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta, "Beta", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                if (eventData.Definition == alphaDefinition)
                {
                    container.TagAdd(betaDefinition);
                }
            };

            Assert.IsTrue(container.TagAdd(alphaDefinition));

            Assert.IsTrue(container.TagCheck(betaDefinition));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ReentrantWarning_IncludesTagID()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                container.TagAdd(definition);
            };
            LogAssert.Expect(LogType.Warning, new Regex("TestAlpha"));

            Assert.IsTrue(container.TagAdd(definition));
        }

        [Test]
        public void ReentrantWarning_UsesContainerContext()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            CapturingLogHandler logHandler = BeginCapture();
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                container.TagAdd(definition);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(LogType.Warning, logHandler.LastLogType);
            Assert.AreSame(container, logHandler.LastContext);
        }

        [Test]
        public void SubscriberException_AfterReentrancy_ReleasesGuard()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                container.TagAdd(definition);
                throw new InvalidOperationException("Subscriber failed");
            };
            ExpectReentrantWarning();
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagSub(definition));
        }

        [Test]
        public void Tick_WhenNestedTickCalled_LogsWarningOnce()
        {
            RecordingTagTraitSO trait = CreateTrait();
            AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.None, trait);
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                container.Tick(1f);
            };
            LogAssert.Expect(LogType.Warning, new Regex("nested Tick"));

            container.Tick(1f);
        }

        [Test]
        public void Tick_WhenNestedTickCalled_DoesNotApplyDeltaTwice()
        {
            RecordingTagTraitSO trait = CreateTrait();
            AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.None, trait);
            TagDefinition betaDefinition = AddPerishableTag(TagID.TestBeta, "Beta", 3f, StackPolicy.None);
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                container.Tick(1f);
            };
            LogAssert.Expect(LogType.Warning, new Regex("nested Tick"));

            container.Tick(1f);

            Assert.AreEqual(2f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenNestedTickBlocked_KeepsOuterTickValid()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.None, trait);
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                container.Tick(1f);
            };
            LogAssert.Expect(LogType.Warning, new Regex("nested Tick"));

            container.Tick(1f);

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void Tick_WhenNestedTickBlocked_ReleasesTickStateAfterOuterCompletion()
        {
            RecordingTagTraitSO trait = CreateTrait();
            AddPerishableTag(TagID.TestAlpha, "Alpha", 1f, StackPolicy.None, trait);
            TagDefinition betaDefinition = AddPerishableTag(TagID.TestBeta, "Beta", 3f, StackPolicy.None);
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                container.Tick(1f);
            };
            LogAssert.Expect(LogType.Warning, new Regex("nested Tick"));

            container.Tick(1f);
            container.Tick(1f);

            Assert.AreEqual(1f, GetInstance(betaDefinition).RemainingTime);
        }

        [Test]
        public void Tick_WhenDefinitionIsNull_LogsErrorAndSkipsEntry()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 3f, StackPolicy.None);
            TagInstance instance = GetInstance(definition);
            SetBackingField(instance, "<Definition>k__BackingField", null);
            LogAssert.Expect(LogType.Error, new Regex("null definition"));

            container.Tick(1f);

            Assert.IsNull(instance.Definition);
        }

        [Test]
        public void Tick_WhenStackCountIsBelowOne_LogsErrorAndDoesNotMutate()
        {
            TagDefinition definition = AddPerishableStackTag(TagID.TestAlpha, "Alpha", 3f, 3);
            TagInstance instance = GetInstance(definition);
            SetBackingField(instance, "<StackCount>k__BackingField", 0);
            LogAssert.Expect(LogType.Error, new Regex("StackCount less than 1"));

            container.Tick(1f);

            Assert.AreEqual(0, instance.StackCount);
            Assert.AreEqual(3f, instance.RemainingTime);
        }

        [Test]
        public void Tick_WhenPerishableDurationIsInvalid_LogsErrorAndDoesNotMutate()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagInstance instance = new TagInstance(definition, 0f);
            Assert.IsTrue(container.TryAddInstance(instance));
            LogAssert.Expect(LogType.Error, new Regex("invalid duration"));

            container.Tick(1f);

            Assert.AreEqual(0f, instance.Duration);
            Assert.AreEqual(0f, instance.RemainingTime);
        }

        [Test]
        public void Tick_WhenRemainingTimeIsNegative_LogsErrorAndDoesNotMutate()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 3f, StackPolicy.None);
            TagInstance instance = GetInstance(definition);
            SetBackingField(instance, "<RemainingTime>k__BackingField", -1f);
            LogAssert.Expect(LogType.Error, new Regex("remaining time"));

            container.Tick(1f);

            Assert.AreEqual(-1f, instance.RemainingTime);
        }

        [Test]
        public void Tick_WhenRemainingTimeIsNotFinite_LogsErrorAndDoesNotMutate()
        {
            TagDefinition definition = AddPerishableTag(TagID.TestAlpha, "Alpha", 3f, StackPolicy.None);
            TagInstance instance = GetInstance(definition);
            SetBackingField(instance, "<RemainingTime>k__BackingField", float.PositiveInfinity);
            LogAssert.Expect(LogType.Error, new Regex("remaining time"));

            container.Tick(1f);

            Assert.IsTrue(float.IsInfinity(instance.RemainingTime));
        }

        [Test]
        public void Tick_WhenStackCountExceedsMaximum_LogsErrorWithoutClamping()
        {
            TagDefinition definition = AddPerishableStackTagWithStacks(TagID.TestAlpha, "Alpha", 3f, 3, 3);
            SetIntProperty(definition, "maxStackCount", 2);
            LogAssert.Expect(LogType.Error, new Regex("StackCount greater than MaxStackCount"));

            container.Tick(1f);

            Assert.AreEqual(3, GetInstance(definition).StackCount);
            Assert.AreEqual(2, definition.MaxStackCount);
        }

        [Test]
        public void TagSub_WhenStackCountExceedsMaximum_DecreasesWithoutClampingToMaximum()
        {
            TagDefinition definition = AddPermanentStackTagWithStacks(TagID.TestAlpha, "Alpha", 5, 4);
            SetIntProperty(definition, "maxStackCount", 2);
            LogAssert.Expect(LogType.Error, new Regex("StackCount greater than MaxStackCount"));

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(3, GetInstance(definition).StackCount);
            Assert.AreEqual(2, definition.MaxStackCount);
        }

        [Test]
        public void InvalidEntry_DoesNotPreventOtherTagsFromTicking()
        {
            TagDefinition alphaDefinition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagInstance invalidInstance = new TagInstance(alphaDefinition, 0f);
            Assert.IsTrue(container.TryAddInstance(invalidInstance));
            TagDefinition betaDefinition = AddPerishableTag(TagID.TestBeta, "Beta", 3f, StackPolicy.None);
            LogAssert.Expect(LogType.Error, new Regex("invalid duration"));

            container.Tick(1f);

            Assert.AreEqual(2f, GetInstance(betaDefinition).RemainingTime);
        }

        private void ExpectReentrantWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("reentrant mutation.*TestAlpha"));
        }

        private CapturingLogHandler BeginCapture()
        {
            originalLogHandler = Debug.unityLogger.logHandler;
            CapturingLogHandler logHandler = new CapturingLogHandler();
            Debug.unityLogger.logHandler = logHandler;
            return logHandler;
        }

        private TagDefinition AddPermanentTag(TagID tagID, string tagName)
        {
            return AddPermanentTag(tagID, tagName, CreateTrait());
        }

        private TagDefinition AddPermanentTag(TagID tagID, string tagName, RecordingTagTraitSO trait)
        {
            TagDefinition definition = CreateDefinition(tagID, tagName, TagCategory.Status, trait, StackPolicy.None, 1);
            Assert.IsTrue(container.TagAdd(definition));
            return definition;
        }

        private TagDefinition AddPermanentStackTag(TagID tagID, string tagName, int maxStackCount)
        {
            TagDefinition definition = CreateDefinition(tagID, tagName, TagCategory.Status, CreateTrait(), StackPolicy.StackCount, maxStackCount);
            Assert.IsTrue(container.TagAdd(definition));
            return definition;
        }

        private TagDefinition AddPermanentStackTagWithStacks(TagID tagID, string tagName, int maxStackCount, int stackCount)
        {
            TagDefinition definition = AddPermanentStackTag(tagID, tagName, maxStackCount);
            for (int i = 1; i < stackCount; i++)
            {
                Assert.IsTrue(container.TagAdd(definition));
            }

            return definition;
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
            TagDefinition definition = CreateDefinition(tagID, tagName, TagCategory.Status, trait, stackPolicy, 1);
            Assert.IsTrue(container.PerishableTagAdd(definition, duration));
            return definition;
        }

        private TagDefinition AddPerishableStackTag(TagID tagID, string tagName, float duration, int maxStackCount)
        {
            TagDefinition definition = CreateDefinition(tagID, tagName, TagCategory.Status, CreateTrait(), StackPolicy.StackCount, maxStackCount);
            Assert.IsTrue(container.PerishableTagAdd(definition, duration));
            return definition;
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

        private static void SetBackingField(TagInstance instance, string fieldName, object value)
        {
            FieldInfo fieldInfo = typeof(TagInstance).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(fieldInfo);
            fieldInfo.SetValue(instance, value);
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

        private sealed class CapturingLogHandler : ILogHandler
        {
            public LogType LastLogType { get; private set; }
            public Object LastContext { get; private set; }

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                LastLogType = logType;
                LastContext = context;
            }

            public void LogException(Exception exception, Object context)
            {
                LastLogType = LogType.Exception;
                LastContext = context;
            }
        }
    }
}
