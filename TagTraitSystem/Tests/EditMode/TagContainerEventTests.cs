using System;
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
    public sealed class TagContainerEventTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerEventTests");
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
        public void OnTagAdded_WhenTagAddSucceeds_InvokesOnce()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            int addedCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                addedCount++;
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(1, addedCount);
        }

        [Test]
        public void OnTagAdded_WhenInvoked_ContainsExpectedData()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            TagChangeEventData receivedData = default(TagChangeEventData);
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                receivedData = eventData;
            };

            Assert.IsTrue(container.TagAdd(definition));

            TagInstance storedInstance;
            Assert.IsTrue(container.TryGetTag(definition, out storedInstance));
            Assert.AreSame(container, receivedData.Container);
            Assert.AreSame(definition, receivedData.Definition);
            Assert.AreSame(storedInstance, receivedData.Instance);
            Assert.IsNull(receivedData.Source);
            Assert.AreEqual(TagChangeReason.Added, receivedData.Reason);
            Assert.AreEqual(0f, receivedData.PreviousDuration);
            Assert.AreEqual(0f, receivedData.PreviousRemainingTime);
            Assert.AreEqual(0, receivedData.PreviousStackCount);
        }

        [Test]
        public void OnTagAdded_WhenSourceIsNull_PreservesNullSource()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            GameObject receivedSource = gameObject;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                receivedSource = eventData.Source;
            };

            Assert.IsTrue(container.TagAdd(definition, null));

            Assert.IsNull(receivedSource);
        }

        [Test]
        public void OnTagAdded_WhenSourceIsSpecified_PreservesSource()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            GameObject source = CreateSourceObject();
            GameObject receivedSource = null;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                receivedSource = eventData.Source;
            };

            Assert.IsTrue(container.TagAdd(definition, source));

            Assert.AreSame(source, receivedSource);
        }

        [Test]
        public void OnTagAdded_WhenCallbackQueriesContainer_SeesAddedState()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            bool tagCheckResult = false;
            bool tryGetResult = false;
            int scanCount = -1;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                TagInstance foundInstance;
                tagCheckResult = container.TagCheck(definition);
                tryGetResult = container.TryGetTag(definition, out foundInstance);
                scanCount = container.TagScan().Count;
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(tagCheckResult);
            Assert.IsTrue(tryGetResult);
            Assert.AreEqual(1, scanCount);
        }

        [Test]
        public void TagAdd_WhenFails_DoesNotInvokeAnyChangeEvent()
        {
            int eventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { eventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { eventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { eventCount++; };
            LogAssert.Expect(LogType.Error, new Regex("null tag definition"));

            Assert.IsFalse(container.TagAdd(null));

            Assert.AreEqual(0, eventCount);
        }

        [Test]
        public void TagAdd_WhenSucceeds_DoesNotInvokeRemovedOrUpdated()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            int removedCount = 0;
            int updatedCount = 0;
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { removedCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(0, removedCount);
            Assert.AreEqual(0, updatedCount);
        }

        [Test]
        public void OnTagRemoved_WhenTagSubSucceeds_InvokesOnce()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            Assert.IsTrue(container.TagAdd(definition));
            int removedCount = 0;
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                removedCount++;
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, removedCount);
        }

        [Test]
        public void OnTagRemoved_WhenInvoked_ContainsExpectedData()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            Assert.IsTrue(container.TagAdd(definition));
            TagInstance storedInstance;
            Assert.IsTrue(container.TryGetTag(definition, out storedInstance));
            TagChangeEventData receivedData = default(TagChangeEventData);
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                receivedData = eventData;
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreSame(container, receivedData.Container);
            Assert.AreSame(definition, receivedData.Definition);
            Assert.AreSame(storedInstance, receivedData.Instance);
            Assert.IsNull(receivedData.Source);
            Assert.AreEqual(TagChangeReason.Removed, receivedData.Reason);
            Assert.AreEqual(storedInstance.Duration, receivedData.PreviousDuration);
            Assert.AreEqual(storedInstance.RemainingTime, receivedData.PreviousRemainingTime);
            Assert.AreEqual(storedInstance.StackCount, receivedData.PreviousStackCount);
        }

        [Test]
        public void OnTagRemoved_WhenCallbackQueriesContainer_SeesRemovedState()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            Assert.IsTrue(container.TagAdd(definition));
            bool tagCheckResult = true;
            bool tryGetResult = true;
            int scanCount = -1;
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                TagInstance foundInstance;
                tagCheckResult = container.TagCheck(definition);
                tryGetResult = container.TryGetTag(definition, out foundInstance);
                scanCount = container.TagScan().Count;
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(tagCheckResult);
            Assert.IsFalse(tryGetResult);
            Assert.AreEqual(0, scanCount);
        }

        [Test]
        public void OnTagRemoved_WhenSourceIsNull_PreservesNullSource()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            Assert.IsTrue(container.TagAdd(definition));
            GameObject receivedSource = gameObject;
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                receivedSource = eventData.Source;
            };

            Assert.IsTrue(container.TagSub(definition, null));

            Assert.IsNull(receivedSource);
        }

        [Test]
        public void OnTagRemoved_WhenSourceIsSpecified_PreservesSource()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            GameObject source = CreateSourceObject();
            Assert.IsTrue(container.TagAdd(definition));
            GameObject receivedSource = null;
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                receivedSource = eventData.Source;
            };

            Assert.IsTrue(container.TagSub(definition, source));

            Assert.AreSame(source, receivedSource);
        }

        [Test]
        public void TagSub_WhenFails_DoesNotInvokeAnyChangeEvent()
        {
            int eventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { eventCount++; };
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { eventCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { eventCount++; };
            LogAssert.Expect(LogType.Error, new Regex("null tag definition"));

            Assert.IsFalse(container.TagSub(null));

            Assert.AreEqual(0, eventCount);
        }

        [Test]
        public void TagSub_WhenSucceeds_DoesNotInvokeAddedOrUpdated()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            Assert.IsTrue(container.TagAdd(definition));
            int addedCount = 0;
            int updatedCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { addedCount++; };
            container.OnTagUpdated += delegate(TagChangeEventData eventData) { updatedCount++; };

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(0, addedCount);
            Assert.AreEqual(0, updatedCount);
        }

        [Test]
        public void SafeInvoke_WhenFirstSubscriberThrows_InvokesLaterSubscribers()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            bool laterSubscriberInvoked = false;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                throw new InvalidOperationException("first subscriber failed");
            };
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                laterSubscriberInvoked = true;
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(laterSubscriberInvoked);
        }

        [Test]
        public void TagAdd_WhenSubscriberThrows_KeepsStateAndReturnsTrue()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                throw new InvalidOperationException("subscriber failed");
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void TagSub_WhenSubscriberThrows_KeepsStateAndReturnsTrue()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            Assert.IsTrue(container.TagAdd(definition));
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                throw new InvalidOperationException("subscriber failed");
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void SafeInvoke_WhenSubscriberThrows_LogsErrorWithoutRethrow()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                throw new InvalidOperationException("subscriber failed");
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.DoesNotThrow(delegate
            {
                Assert.IsTrue(container.TagAdd(definition));
            });
        }

        [Test]
        public void EventException_WhenHandled_DoesNotLeaveReentrancyGuardLocked()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                throw new InvalidOperationException("subscriber failed");
            };
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));

            Assert.IsTrue(container.TagAdd(definition));
            Assert.IsTrue(container.TagSub(definition));
        }

        private TagDefinition CreateValidStatusDefinition(TagID tagID, string tagName)
        {
            return CreateDefinition(tagID, tagName, TagCategory.Status, CreateTrait(), StackPolicy.None);
        }

        private TagDefinition CreateDefinition(
            TagID tagID,
            string tagName,
            TagCategory category,
            TestTagTraitSO trait,
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

        private TestTagTraitSO CreateTrait()
        {
            TestTagTraitSO trait = ScriptableObject.CreateInstance<TestTagTraitSO>();
            createdObjects.Add(trait);
            return trait;
        }

        private GameObject CreateSourceObject()
        {
            GameObject source = new GameObject("TagEventSource");
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
