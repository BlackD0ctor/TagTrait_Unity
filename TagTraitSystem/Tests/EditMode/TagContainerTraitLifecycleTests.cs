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
    public sealed class TagContainerTraitLifecycleTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerTraitLifecycleTests");
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
        public void OnAdd_WhenNonKeywordTagIsAdded_InvokesOnce()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(1, trait.OnAddCallCount);
        }

        [Test]
        public void OnAdd_WhenInvoked_ReceivesExpectedContext()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            GameObject source = CreateSourceObject();

            Assert.IsTrue(container.TagAdd(definition, source));

            Assert.AreSame(container, trait.LastOnAddContext.Container);
            Assert.AreSame(gameObject, trait.LastOnAddContext.Target);
            Assert.AreSame(source, trait.LastOnAddContext.Source);
            Assert.AreSame(definition, trait.LastOnAddContext.Definition);
            Assert.IsTrue(container.TryGetTag(definition, out TagInstance instance));
            Assert.AreSame(instance, trait.LastOnAddContext.Instance);
        }

        [Test]
        public void OnAdd_WhenInvoked_TagIsAlreadyStored()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            bool tagWasStored = false;
            trait.OnAddAction = delegate(TraitContext context)
            {
                tagWasStored = container.TagCheck(definition);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(tagWasStored);
        }

        [Test]
        public void TagAdd_WhenTraitExists_InvokesOnAddBeforeOnTagAdded()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            List<string> order = new List<string>();
            trait.OnAddAction = delegate(TraitContext context)
            {
                order.Add("OnAdd");
            };
            container.OnTagAdded += delegate(TagChangeEventData eventData)
            {
                order.Add("OnTagAdded");
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual("OnAdd", order[0]);
            Assert.AreEqual("OnTagAdded", order[1]);
        }

        [Test]
        public void TagAdd_WhenKeywordTagIsAdded_DoesNotInvokeOnAdd()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, null);

            Assert.IsTrue(container.TagAdd(definition));

            Assert.AreEqual(0, trait.OnAddCallCount);
        }

        [Test]
        public void TagAdd_WhenFails_DoesNotInvokeOnAdd()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.None, "Alpha", TagCategory.Status, trait);
            LogAssert.Expect(LogType.Error, new Regex("TagID\\.None"));

            Assert.IsFalse(container.TagAdd(definition));

            Assert.AreEqual(0, trait.OnAddCallCount);
        }

        [Test]
        public void OnAdd_WhenSameIDMutationIsRequested_BlocksReentrancy()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            bool reentrantResult = true;
            trait.OnAddAction = delegate(TraitContext context)
            {
                reentrantResult = container.TagSub(definition);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(reentrantResult);
            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void OnAdd_WhenSameIDTagAddIsRequested_BlocksReentrancy()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            bool reentrantResult = true;
            trait.OnAddAction = delegate(TraitContext context)
            {
                reentrantResult = container.TagAdd(definition);
            };

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsFalse(reentrantResult);
            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(1, trait.OnAddCallCount);
        }

        [Test]
        public void OnAdd_WhenDifferentIDMutationIsRequested_AllowsOperation()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            RecordingTagTraitSO betaTrait = CreateTrait();
            TagDefinition alphaDefinition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, alphaTrait);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta, "Beta", TagCategory.Status, betaTrait);
            bool betaResult = false;
            alphaTrait.OnAddAction = delegate(TraitContext context)
            {
                betaResult = container.TagAdd(betaDefinition);
            };

            Assert.IsTrue(container.TagAdd(alphaDefinition));

            Assert.IsTrue(betaResult);
            Assert.IsTrue(container.TagCheck(betaDefinition));
        }

        [Test]
        public void TagAdd_WhenOnAddThrows_KeepsTagInvokesEventAndReturnsTrue()
        {
            RecordingTagTraitSO trait = CreateTrait();
            trait.ThrowOnAdd = true;
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            int addedEventCount = 0;
            container.OnTagAdded += delegate(TagChangeEventData eventData) { addedEventCount++; };
            LogAssert.Expect(LogType.Error, new Regex("OnAdd"));

            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreEqual(1, addedEventCount);
        }

        [Test]
        public void OnRemove_WhenNonKeywordTagIsRemoved_InvokesOnce()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(1, trait.OnRemoveCallCount);
        }

        [Test]
        public void OnRemove_WhenInvoked_ReceivesExpectedContext()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            GameObject source = CreateSourceObject();
            Assert.IsTrue(container.TagAdd(definition));
            Assert.IsTrue(container.TryGetTag(definition, out TagInstance instance));

            Assert.IsTrue(container.TagSub(definition, source));

            Assert.AreSame(container, trait.LastOnRemoveContext.Container);
            Assert.AreSame(gameObject, trait.LastOnRemoveContext.Target);
            Assert.AreSame(source, trait.LastOnRemoveContext.Source);
            Assert.AreSame(definition, trait.LastOnRemoveContext.Definition);
            Assert.AreSame(instance, trait.LastOnRemoveContext.Instance);
        }

        [Test]
        public void OnRemove_WhenInvoked_TagIsStillStored()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            bool tagWasStored = false;
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                tagWasStored = container.TagCheck(definition);
            };
            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsTrue(tagWasStored);
        }

        [Test]
        public void TagSub_InvokesOnRemoveBeforeOnTagRemoved()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            List<string> order = new List<string>();
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                order.Add("OnRemove");
            };
            container.OnTagRemoved += delegate(TagChangeEventData eventData)
            {
                order.Add(container.TagCheck(definition) ? "StillStored" : "OnTagRemoved");
            };
            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual("OnRemove", order[0]);
            Assert.AreEqual("OnTagRemoved", order[1]);
        }

        [Test]
        public void TagSub_WhenInvalidKeywordTraitTagExists_DoesNotInvokeOnRemove()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, trait);
            Assert.IsTrue(container.TryAddInstance(new TagInstance(definition)));

            Assert.IsTrue(container.TagSub(definition));

            Assert.AreEqual(0, trait.OnRemoveCallCount);
        }

        [Test]
        public void TagSub_WhenNonKeywordNullTraitTagExists_DoesNotInvokeOnRemove()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, null);
            Assert.IsTrue(container.TryAddInstance(new TagInstance(definition)));

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagSub_WhenFails_DoesNotInvokeOnRemove()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);

            Assert.IsFalse(container.TagSub(definition));

            Assert.AreEqual(0, trait.OnRemoveCallCount);
        }

        [Test]
        public void OnRemove_WhenSameIDMutationIsRequested_BlocksReentrancy()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            bool reentrantResult = true;
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                reentrantResult = container.TagAdd(definition);
            };
            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(reentrantResult);
            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void OnRemove_WhenSameIDTagSubIsRequested_BlocksReentrancy()
        {
            RecordingTagTraitSO trait = CreateTrait();
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            bool reentrantResult = true;
            trait.OnRemoveAction = delegate(TraitContext context)
            {
                reentrantResult = container.TagSub(definition);
            };
            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(reentrantResult);
            Assert.IsFalse(container.TagCheck(definition));
            Assert.AreEqual(1, trait.OnRemoveCallCount);
        }

        [Test]
        public void OnRemove_WhenDifferentIDMutationIsRequested_AllowsOperation()
        {
            RecordingTagTraitSO alphaTrait = CreateTrait();
            RecordingTagTraitSO betaTrait = CreateTrait();
            TagDefinition alphaDefinition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, alphaTrait);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta, "Beta", TagCategory.Status, betaTrait);
            bool betaResult = false;
            alphaTrait.OnRemoveAction = delegate(TraitContext context)
            {
                betaResult = container.TagAdd(betaDefinition);
            };
            Assert.IsTrue(container.TagAdd(alphaDefinition));

            Assert.IsTrue(container.TagSub(alphaDefinition));

            Assert.IsTrue(betaResult);
            Assert.IsTrue(container.TagCheck(betaDefinition));
        }

        [Test]
        public void TagSub_WhenOnRemoveThrows_RemovesTagInvokesEventAndReturnsTrue()
        {
            RecordingTagTraitSO trait = CreateTrait();
            trait.ThrowOnRemove = true;
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, trait);
            int removedEventCount = 0;
            container.OnTagRemoved += delegate(TagChangeEventData eventData) { removedEventCount++; };
            Assert.IsTrue(container.TagAdd(definition));
            LogAssert.Expect(LogType.Error, new Regex("OnRemove"));

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
            Assert.AreEqual(1, removedEventCount);
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

        private GameObject CreateSourceObject()
        {
            GameObject source = new GameObject("TraitLifecycleSource");
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
