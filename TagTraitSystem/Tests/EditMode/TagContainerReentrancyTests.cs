using System.Collections.Generic;
using NUnit.Framework;
using TagTraitSystem.Runtime.Components;
using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Definitions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TagTraitSystem.Tests.EditMode
{
    public sealed class TagContainerReentrancyTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerReentrancyTests");
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
        public void OnTagAdded_WhenSameIDTagAddIsRequested_ReturnsFalse()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            bool reentrantResult = true;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                reentrantResult = container.TagAdd(definition);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(reentrantResult);
            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void OnTagAdded_WhenSameIDTagSubIsRequested_ReturnsFalse()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            bool reentrantResult = true;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                reentrantResult = container.TagSub(definition);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(reentrantResult);
            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void OnTagRemoved_WhenSameIDTagAddIsRequested_ReturnsFalse()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            Assert.IsTrue(container.TagAdd(definition));
            bool reentrantResult = true;
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                reentrantResult = container.TagAdd(definition);
            };

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(reentrantResult);
            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void SameIDReentrancy_WhenBlocked_DoesNotInvokeAdditionalEvents()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            int addedCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                addedCount++;
                container.TagAdd(definition);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(1, addedCount);
        }

        [Test]
        public void OnTagAdded_WhenDifferentIDTagAddIsRequested_Succeeds()
        {
            TagDefinition alphaDefinition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            TagDefinition betaDefinition = CreateValidStatusDefinition(TagID.TestBeta, "Beta");
            bool betaAddResult = false;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                if (eventData.Definition == alphaDefinition)
                {
                    betaAddResult = container.TagAdd(betaDefinition);
                }
            };

            Assert.IsTrue(container.TagAdd(alphaDefinition));

            Assert.IsTrue(betaAddResult);
            Assert.IsTrue(container.TagCheck(alphaDefinition));
            Assert.IsTrue(container.TagCheck(betaDefinition));
        }

        [Test]
        public void DifferentIDReentrancy_WhenAllowed_InvokesExpectedEvents()
        {
            TagDefinition alphaDefinition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            TagDefinition betaDefinition = CreateValidStatusDefinition(TagID.TestBeta, "Beta");
            int addedCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                addedCount++;
                if (eventData.Definition == alphaDefinition)
                {
                    container.TagAdd(betaDefinition);
                }
            };

            Assert.IsTrue(container.TagAdd(alphaDefinition));

            Assert.AreEqual(2, addedCount);
        }

        [Test]
        public void CrossReentrancy_WhenOriginalIDIsRequestedAgain_BlocksOriginalID()
        {
            TagDefinition alphaDefinition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            TagDefinition betaDefinition = CreateValidStatusDefinition(TagID.TestBeta, "Beta");
            bool alphaSubDuringBetaResult = true;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                if (eventData.Definition == alphaDefinition)
                {
                    container.TagAdd(betaDefinition);
                }
                else if (eventData.Definition == betaDefinition)
                {
                    alphaSubDuringBetaResult = container.TagSub(alphaDefinition);
                }
            };

            Assert.IsTrue(container.TagAdd(alphaDefinition));

            Assert.IsFalse(alphaSubDuringBetaResult);
            Assert.IsTrue(container.TagCheck(alphaDefinition));
            Assert.IsTrue(container.TagCheck(betaDefinition));
        }

        [Test]
        public void EventCallback_WhenQueryAPIsAreCalled_ReturnsFinalState()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            bool tagCheckResult = false;
            bool andResult = false;
            bool orResult = false;
            bool tryGetResult = false;
            int scanCount = -1;
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                TagInstance foundInstance;
                tagCheckResult = container.TagCheck(definition);
                andResult = container.TagANDCheck(definition);
                orResult = container.TagORCheck(definition);
                tryGetResult = container.TryGetTag(definition, out foundInstance);
                scanCount = container.TagScan().Count;
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(tagCheckResult);
            Assert.IsTrue(andResult);
            Assert.IsTrue(orResult);
            Assert.IsTrue(tryGetResult);
            Assert.AreEqual(1, scanCount);
        }

        [Test]
        public void FailedDuplicateAdd_WhenCompleted_DoesNotLeaveGuardLocked()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            Assert.IsTrue(container.TagAdd(definition));
            Assert.IsFalse(container.TagAdd(definition));
            Assert.IsTrue(container.TagSub(definition));

            Assert.IsTrue(container.TagAdd(definition));
        }

        [Test]
        public void FailedMissingSub_WhenCompleted_DoesNotLeaveGuardLocked()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha");
            Assert.IsFalse(container.TagSub(definition));

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
