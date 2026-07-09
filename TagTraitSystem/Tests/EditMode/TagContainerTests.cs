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
    public sealed class TagContainerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagContainerTests");
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
        public void TryAddInstance_WhenInstanceIsValid_AddsAndReturnsTrue()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha);
            TagInstance instance = new TagInstance(definition);

            bool result = container.TryAddInstance(instance);

            Assert.IsTrue(result);
            Assert.IsTrue(container.TagCheck(definition));

            TagInstance foundInstance;
            Assert.IsTrue(container.TryGetTag(definition, out foundInstance));
            Assert.AreSame(instance, foundInstance);
        }

        [Test]
        public void TryAddInstance_WhenInstanceIsNull_ReturnsFalse()
        {
            Assert.IsFalse(container.TryAddInstance(null));
        }

        [Test]
        public void TryAddInstance_WhenTagIDIsNone_ReturnsFalse()
        {
            TagDefinition definition = CreateDefinition(TagID.None);
            TagInstance instance = new TagInstance(definition);

            Assert.IsFalse(container.TryAddInstance(instance));
            Assert.AreEqual(0, container.TagScan().Count);
        }

        [Test]
        public void TryAddInstance_WhenSameInstanceIDAlreadyExists_ReturnsFalse()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha);
            TagInstance firstInstance = new TagInstance(definition);
            TagInstance secondInstance = new TagInstance(definition);

            Assert.IsTrue(container.TryAddInstance(firstInstance));
            Assert.IsFalse(container.TryAddInstance(secondInstance));

            IReadOnlyCollection<TagInstance> snapshot = container.TagScan();
            Assert.AreEqual(1, snapshot.Count);
            Assert.IsTrue(ContainsInstance(snapshot, firstInstance));
            Assert.IsFalse(ContainsInstance(snapshot, secondInstance));
        }

        [Test]
        public void TryAddInstance_WhenDifferentDefinitionHasSameID_ReturnsFalseAndKeepsExisting()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestAlpha);
            TagInstance firstInstance = new TagInstance(firstDefinition);
            TagInstance secondInstance = new TagInstance(secondDefinition);

            Assert.IsTrue(container.TryAddInstance(firstInstance));
            LogAssert.Expect(LogType.Error, new Regex("same TagID"));

            Assert.IsFalse(container.TryAddInstance(secondInstance));

            TagInstance foundInstance;
            Assert.IsTrue(container.TryGetTag(secondDefinition, out foundInstance));
            Assert.AreSame(firstInstance, foundInstance);
            Assert.AreEqual(1, container.TagScan().Count);
        }

        [Test]
        public void TagScan_WhenContainerIsEmpty_ReturnsNonNullEmptyCollection()
        {
            IReadOnlyCollection<TagInstance> snapshot = container.TagScan();

            Assert.IsNotNull(snapshot);
            Assert.AreEqual(0, snapshot.Count);
        }

        [Test]
        public void TagScan_WhenTagsExist_ReturnsAllInstances()
        {
            TagInstance alphaInstance = AddInstance(TagID.TestAlpha);
            TagInstance betaInstance = AddInstance(TagID.TestBeta);

            IReadOnlyCollection<TagInstance> snapshot = container.TagScan();

            Assert.AreEqual(2, snapshot.Count);
            Assert.IsTrue(ContainsInstance(snapshot, alphaInstance));
            Assert.IsTrue(ContainsInstance(snapshot, betaInstance));
        }

        [Test]
        public void TagScan_WhenTagIsAddedAfterScan_DoesNotChangePreviousSnapshot()
        {
            TagInstance alphaInstance = AddInstance(TagID.TestAlpha);
            IReadOnlyCollection<TagInstance> firstSnapshot = container.TagScan();

            TagInstance betaInstance = AddInstance(TagID.TestBeta);
            IReadOnlyCollection<TagInstance> secondSnapshot = container.TagScan();

            Assert.AreEqual(1, firstSnapshot.Count);
            Assert.IsTrue(ContainsInstance(firstSnapshot, alphaInstance));
            Assert.IsFalse(ContainsInstance(firstSnapshot, betaInstance));
            Assert.AreEqual(2, secondSnapshot.Count);
            Assert.IsTrue(ContainsInstance(secondSnapshot, alphaInstance));
            Assert.IsTrue(ContainsInstance(secondSnapshot, betaInstance));
        }

        [Test]
        public void TagCheck_WhenDefinitionExists_ReturnsTrue()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha);
            container.TryAddInstance(new TagInstance(definition));

            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void TagCheck_WhenDefinitionDoesNotExist_ReturnsFalse()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha);

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagCheck_WhenDefinitionIsNull_ReturnsFalse()
        {
            Assert.IsFalse(container.TagCheck((TagDefinition)null));
        }

        [Test]
        public void TagCheck_WhenTagIDIsNone_ReturnsFalse()
        {
            TagDefinition definition = CreateDefinition(TagID.None);

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagCheck_WhenSameIDDifferentDefinitionIsPassed_ReturnsTrue()
        {
            TagDefinition storedDefinition = CreateDefinition(TagID.TestAlpha);
            TagDefinition queryDefinition = CreateDefinition(TagID.TestAlpha);
            container.TryAddInstance(new TagInstance(storedDefinition));

            Assert.IsTrue(container.TagCheck(queryDefinition));
        }

        [Test]
        public void TagCheckByID_WhenIDExists_ReturnsTrue()
        {
            AddInstance(TagID.TestAlpha);

            Assert.IsTrue(container.TagCheck(TagID.TestAlpha));
        }

        [Test]
        public void TagCheckByID_WhenIDDoesNotExist_ReturnsFalse()
        {
            Assert.IsFalse(container.TagCheck(TagID.TestAlpha));
        }

        [Test]
        public void TagCheckByID_WhenIDIsNone_ReturnsFalse()
        {
            Assert.IsFalse(container.TagCheck(TagID.None));
        }

        [Test]
        public void TagANDCheck_WhenAllTagsExist_ReturnsTrue()
        {
            TagDefinition alphaDefinition = AddInstanceAndReturnDefinition(TagID.TestAlpha);
            TagDefinition betaDefinition = AddInstanceAndReturnDefinition(TagID.TestBeta);

            Assert.IsTrue(container.TagANDCheck(alphaDefinition, betaDefinition));
        }

        [Test]
        public void TagANDCheck_WhenOneTagIsMissing_ReturnsFalse()
        {
            TagDefinition alphaDefinition = AddInstanceAndReturnDefinition(TagID.TestAlpha);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta);

            Assert.IsFalse(container.TagANDCheck(alphaDefinition, betaDefinition));
        }

        [Test]
        public void TagANDCheck_WhenArrayIsNull_ReturnsFalse()
        {
            Assert.IsFalse(container.TagANDCheck((TagDefinition[])null));
        }

        [Test]
        public void TagANDCheck_WhenArrayIsEmpty_ReturnsFalse()
        {
            Assert.IsFalse(container.TagANDCheck(new TagDefinition[0]));
        }

        [Test]
        public void TagANDCheck_WhenArrayContainsNull_ReturnsFalseAndLogsError()
        {
            TagDefinition alphaDefinition = AddInstanceAndReturnDefinition(TagID.TestAlpha);
            LogAssert.Expect(LogType.Error, new Regex("null definition"));

            Assert.IsFalse(container.TagANDCheck(alphaDefinition, null));
        }

        [Test]
        public void TagANDCheck_WhenArrayContainsNoneID_ReturnsFalseAndLogsError()
        {
            TagDefinition alphaDefinition = AddInstanceAndReturnDefinition(TagID.TestAlpha);
            TagDefinition noneDefinition = CreateDefinition(TagID.None);
            LogAssert.Expect(LogType.Error, new Regex("TagID\\.None"));

            Assert.IsFalse(container.TagANDCheck(alphaDefinition, noneDefinition));
        }

        [Test]
        public void TagANDCheck_WhenSingleTagExists_MatchesTagCheck()
        {
            TagDefinition alphaDefinition = AddInstanceAndReturnDefinition(TagID.TestAlpha);

            Assert.AreEqual(container.TagCheck(alphaDefinition), container.TagANDCheck(alphaDefinition));
        }

        [Test]
        public void TagANDCheck_WhenInputContainsDuplicateTag_ReturnsExpectedResult()
        {
            TagDefinition alphaDefinition = AddInstanceAndReturnDefinition(TagID.TestAlpha);

            Assert.IsTrue(container.TagANDCheck(alphaDefinition, alphaDefinition));
        }

        [Test]
        public void TagORCheck_WhenOneTagExists_ReturnsTrue()
        {
            TagDefinition alphaDefinition = AddInstanceAndReturnDefinition(TagID.TestAlpha);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta);

            Assert.IsTrue(container.TagORCheck(betaDefinition, alphaDefinition));
        }

        [Test]
        public void TagORCheck_WhenAllTagsAreMissing_ReturnsFalse()
        {
            TagDefinition alphaDefinition = CreateDefinition(TagID.TestAlpha);
            TagDefinition betaDefinition = CreateDefinition(TagID.TestBeta);

            Assert.IsFalse(container.TagORCheck(alphaDefinition, betaDefinition));
        }

        [Test]
        public void TagORCheck_WhenArrayIsNull_ReturnsFalse()
        {
            Assert.IsFalse(container.TagORCheck((TagDefinition[])null));
        }

        [Test]
        public void TagORCheck_WhenArrayIsEmpty_ReturnsFalse()
        {
            Assert.IsFalse(container.TagORCheck(new TagDefinition[0]));
        }

        [Test]
        public void TagORCheck_WhenArrayContainsNull_ReturnsFalseAndLogsError()
        {
            TagDefinition alphaDefinition = AddInstanceAndReturnDefinition(TagID.TestAlpha);
            LogAssert.Expect(LogType.Error, new Regex("null definition"));

            Assert.IsFalse(container.TagORCheck(alphaDefinition, null));
        }

        [Test]
        public void TagORCheck_WhenArrayContainsNoneID_ReturnsFalseAndLogsError()
        {
            TagDefinition alphaDefinition = AddInstanceAndReturnDefinition(TagID.TestAlpha);
            TagDefinition noneDefinition = CreateDefinition(TagID.None);
            LogAssert.Expect(LogType.Error, new Regex("TagID\\.None"));

            Assert.IsFalse(container.TagORCheck(alphaDefinition, noneDefinition));
        }

        [Test]
        public void TagORCheck_WhenExistingTagAppearsBeforeNull_ReturnsFalseAndLogsError()
        {
            TagDefinition alphaDefinition = AddInstanceAndReturnDefinition(TagID.TestAlpha);
            LogAssert.Expect(LogType.Error, new Regex("null definition"));

            Assert.IsFalse(container.TagORCheck(alphaDefinition, null));
        }

        [Test]
        public void TagORCheck_WhenSingleTagExists_MatchesTagCheck()
        {
            TagDefinition alphaDefinition = AddInstanceAndReturnDefinition(TagID.TestAlpha);

            Assert.AreEqual(container.TagCheck(alphaDefinition), container.TagORCheck(alphaDefinition));
        }

        [Test]
        public void TagORCheck_WhenInputContainsDuplicateTag_ReturnsExpectedResult()
        {
            TagDefinition alphaDefinition = AddInstanceAndReturnDefinition(TagID.TestAlpha);

            Assert.IsTrue(container.TagORCheck(alphaDefinition, alphaDefinition));
        }

        [Test]
        public void TryGetTag_WhenTagExists_ReturnsTrueAndSameInstance()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha);
            TagInstance instance = new TagInstance(definition);
            container.TryAddInstance(instance);

            TagInstance foundInstance;
            bool result = container.TryGetTag(definition, out foundInstance);

            Assert.IsTrue(result);
            Assert.AreSame(instance, foundInstance);
        }

        [Test]
        public void TryGetTag_WhenTagDoesNotExist_ReturnsFalseAndNull()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha);

            TagInstance foundInstance;
            bool result = container.TryGetTag(definition, out foundInstance);

            Assert.IsFalse(result);
            Assert.IsNull(foundInstance);
        }

        [Test]
        public void TryGetTag_WhenDefinitionIsNull_ReturnsFalseAndNull()
        {
            TagInstance foundInstance;
            bool result = container.TryGetTag(null, out foundInstance);

            Assert.IsFalse(result);
            Assert.IsNull(foundInstance);
        }

        [Test]
        public void TryGetTag_WhenTagIDIsNone_ReturnsFalseAndNull()
        {
            TagDefinition definition = CreateDefinition(TagID.None);

            TagInstance foundInstance;
            bool result = container.TryGetTag(definition, out foundInstance);

            Assert.IsFalse(result);
            Assert.IsNull(foundInstance);
        }

        [Test]
        public void TryGetTag_WhenSameIDDifferentDefinitionIsPassed_ReturnsTrueAndExistingInstance()
        {
            TagDefinition storedDefinition = CreateDefinition(TagID.TestAlpha);
            TagDefinition queryDefinition = CreateDefinition(TagID.TestAlpha);
            TagInstance instance = new TagInstance(storedDefinition);
            container.TryAddInstance(instance);

            TagInstance foundInstance;
            bool result = container.TryGetTag(queryDefinition, out foundInstance);

            Assert.IsTrue(result);
            Assert.AreSame(instance, foundInstance);
        }

        [Test]
        public void TagAdd_WhenValidPermanentTagDoesNotExist_AddsAndReturnsTrue()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.None);

            Assert.IsTrue(container.TagAdd(definition));

            AssertPermanentTagWasAdded(definition);
        }

        [Test]
        public void TagAdd_WhenSourceIsNull_AddsTag()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.None);

            Assert.IsTrue(container.TagAdd(definition, null));

            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void TagAdd_WhenSourceIsSpecified_AddsTag()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.None);
            GameObject source = CreateSourceObject();

            Assert.IsTrue(container.TagAdd(definition, source));

            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void TagAdd_WhenKeywordHasNoTrait_AddsAndReturnsTrue()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, null, StackPolicy.None);

            Assert.IsTrue(container.TagAdd(definition));

            AssertPermanentTagWasAdded(definition);
        }

        [Test]
        public void TagAdd_WhenCategoryIsNoneAndTraitExists_AddsAndReturnsTrue()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "None Category", TagCategory.None, CreateTrait(), StackPolicy.None);

            Assert.IsTrue(container.TagAdd(definition));

            AssertPermanentTagWasAdded(definition);
        }

        [Test]
        public void TagAdd_WhenTagNameContainsOnlyWhitespace_AddsAndReturnsTrue()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "   ", StackPolicy.None);

            Assert.IsTrue(container.TagAdd(definition));

            AssertPermanentTagWasAdded(definition);
        }

        [Test]
        public void TagAdd_WhenTagNameHasLeadingOrTrailingWhitespace_AddsAndReturnsTrue()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, " Alpha ", StackPolicy.None);

            Assert.IsTrue(container.TagAdd(definition));

            AssertPermanentTagWasAdded(definition);
        }

        [Test]
        public void TagAdd_WhenDefinitionIsNull_ReturnsFalseAndLogsError()
        {
            LogAssert.Expect(LogType.Error, new Regex("null tag definition"));

            Assert.IsFalse(container.TagAdd(null));
            Assert.AreEqual(0, container.TagScan().Count);
        }

        [Test]
        public void TagAdd_WhenTagIDIsNone_ReturnsFalseAndLogsError()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.None, "None ID", StackPolicy.None);
            LogAssert.Expect(LogType.Error, new Regex("TagID\\.None"));

            Assert.IsFalse(container.TagAdd(definition));
            Assert.AreEqual(0, container.TagScan().Count);
        }

        [Test]
        public void TagAdd_WhenTagNameIsEmpty_ReturnsFalseAndLogsError()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, string.Empty, StackPolicy.None);
            LogAssert.Expect(LogType.Error, new Regex("empty name"));

            Assert.IsFalse(container.TagAdd(definition));
            Assert.AreEqual(0, container.TagScan().Count);
        }

        [Test]
        public void TagAdd_WhenKeywordHasTrait_ReturnsFalseAndLogsError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, CreateTrait(), StackPolicy.None);
            LogAssert.Expect(LogType.Error, new Regex("Keyword"));

            Assert.IsFalse(container.TagAdd(definition));
            Assert.AreEqual(0, container.TagScan().Count);
        }

        [Test]
        public void TagAdd_WhenNonKeywordTraitIsNull_ReturnsFalseAndLogsError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Status", TagCategory.Status, null, StackPolicy.None);
            LogAssert.Expect(LogType.Error, new Regex("Non-keyword"));

            Assert.IsFalse(container.TagAdd(definition));
            Assert.AreEqual(0, container.TagScan().Count);
        }

        [Test]
        public void TagAdd_WhenSameDefinitionAlreadyExists_ReturnsFalseAndKeepsExisting()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.None);
            Assert.IsTrue(container.TagAdd(definition));

            TagInstance existingInstance;
            Assert.IsTrue(container.TryGetTag(definition, out existingInstance));

            Assert.IsFalse(container.TagAdd(definition));
            LogAssert.NoUnexpectedReceived();

            TagInstance foundInstance;
            Assert.IsTrue(container.TryGetTag(definition, out foundInstance));
            Assert.AreSame(existingInstance, foundInstance);
            Assert.AreEqual(1, foundInstance.StackCount);
            Assert.AreEqual(1, container.TagScan().Count);
        }

        [Test]
        public void TagAdd_WhenStackCountPolicyTagAlreadyExistsAtDefaultMaximum_ReturnsFalse()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.StackCount);
            Assert.IsTrue(container.TagAdd(definition));

            TagInstance existingInstance;
            Assert.IsTrue(container.TryGetTag(definition, out existingInstance));

            Assert.IsFalse(container.TagAdd(definition));
            LogAssert.NoUnexpectedReceived();

            TagInstance foundInstance;
            Assert.IsTrue(container.TryGetTag(definition, out foundInstance));
            Assert.AreSame(existingInstance, foundInstance);
            Assert.AreEqual(1, foundInstance.StackCount);
        }

        [Test]
        public void TagAdd_WhenDifferentDefinitionHasSameID_ReturnsFalseAndKeepsExisting()
        {
            TagDefinition firstDefinition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.None);
            TagDefinition secondDefinition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha Other", StackPolicy.None);
            Assert.IsTrue(container.TagAdd(firstDefinition));

            LogAssert.Expect(LogType.Error, new Regex("same TagID"));

            Assert.IsFalse(container.TagAdd(secondDefinition));

            TagInstance foundInstance;
            Assert.IsTrue(container.TryGetTag(firstDefinition, out foundInstance));
            Assert.AreSame(firstDefinition, foundInstance.Definition);
            Assert.AreEqual(1, container.TagScan().Count);
        }

        [Test]
        public void TagSub_WhenTagExists_RemovesAndReturnsTrue()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.None);
            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));

            TagInstance foundInstance;
            Assert.IsFalse(container.TryGetTag(definition, out foundInstance));
            Assert.IsNull(foundInstance);
        }

        [Test]
        public void TagSub_WhenOneOfMultipleTagsIsRemoved_DoesNotAffectOtherTags()
        {
            TagDefinition alphaDefinition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.None);
            TagDefinition betaDefinition = CreateValidStatusDefinition(TagID.TestBeta, "Beta", StackPolicy.None);
            Assert.IsTrue(container.TagAdd(alphaDefinition));
            Assert.IsTrue(container.TagAdd(betaDefinition));

            Assert.IsTrue(container.TagSub(alphaDefinition));

            Assert.IsFalse(container.TagCheck(alphaDefinition));
            Assert.IsTrue(container.TagCheck(betaDefinition));
            Assert.AreEqual(1, container.TagScan().Count);
        }

        [Test]
        public void TagSub_WhenInvalidKeywordTraitTagWasInserted_RemovesAndReturnsTrue()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, CreateTrait(), StackPolicy.None);
            Assert.IsTrue(container.TryAddInstance(new TagInstance(definition)));

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagSub_WhenNonKeywordNullTraitTagWasInserted_RemovesAndReturnsTrue()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Status", TagCategory.Status, null, StackPolicy.None);
            Assert.IsTrue(container.TryAddInstance(new TagInstance(definition)));

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagSub_WhenEmptyNameTagWasInserted_RemovesAndReturnsTrue()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, string.Empty, TagCategory.Status, CreateTrait(), StackPolicy.None);
            Assert.IsTrue(container.TryAddInstance(new TagInstance(definition)));

            Assert.IsTrue(container.TagSub(definition));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagSub_WhenSourceIsNull_RemovesTag()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.None);
            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagSub(definition, null));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagSub_WhenSourceIsSpecified_RemovesTag()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.None);
            GameObject source = CreateSourceObject();
            Assert.IsTrue(container.TagAdd(definition));

            Assert.IsTrue(container.TagSub(definition, source));

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void TagSub_WhenDefinitionIsNull_ReturnsFalseAndLogsError()
        {
            LogAssert.Expect(LogType.Error, new Regex("null tag definition"));

            Assert.IsFalse(container.TagSub(null));
        }

        [Test]
        public void TagSub_WhenTagIDIsNone_ReturnsFalseAndLogsError()
        {
            TagDefinition definition = CreateDefinition(TagID.None, "None ID", TagCategory.Keyword, null, StackPolicy.None);
            LogAssert.Expect(LogType.Error, new Regex("TagID\\.None"));

            Assert.IsFalse(container.TagSub(definition));
        }

        [Test]
        public void TagSub_WhenTagDoesNotExist_ReturnsFalse()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.None);

            Assert.IsFalse(container.TagSub(definition));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TagSub_WhenDifferentDefinitionHasSameID_ReturnsFalseAndKeepsExisting()
        {
            TagDefinition firstDefinition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.None);
            TagDefinition secondDefinition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha Other", StackPolicy.None);
            Assert.IsTrue(container.TagAdd(firstDefinition));
            LogAssert.Expect(LogType.Error, new Regex("same TagID"));

            Assert.IsFalse(container.TagSub(secondDefinition));

            Assert.IsTrue(container.TagCheck(firstDefinition));
            Assert.AreEqual(1, container.TagScan().Count);
        }

        [Test]
        public void TagScan_WhenTagIsRemovedAfterScan_DoesNotChangePreviousSnapshot()
        {
            TagDefinition definition = CreateValidStatusDefinition(TagID.TestAlpha, "Alpha", StackPolicy.None);
            Assert.IsTrue(container.TagAdd(definition));
            IReadOnlyCollection<TagInstance> firstSnapshot = container.TagScan();
            TagInstance snapshotInstance = GetFirstInstance(firstSnapshot);

            Assert.IsTrue(container.TagSub(definition));
            IReadOnlyCollection<TagInstance> secondSnapshot = container.TagScan();

            Assert.AreEqual(1, firstSnapshot.Count);
            Assert.IsTrue(ContainsInstance(firstSnapshot, snapshotInstance));
            Assert.AreEqual(0, secondSnapshot.Count);
        }

        private TagInstance AddInstance(TagID tagID)
        {
            TagDefinition definition = CreateDefinition(tagID);
            TagInstance instance = new TagInstance(definition);
            Assert.IsTrue(container.TryAddInstance(instance));
            return instance;
        }

        private TagDefinition AddInstanceAndReturnDefinition(TagID tagID)
        {
            TagDefinition definition = CreateDefinition(tagID);
            TagInstance instance = new TagInstance(definition);
            Assert.IsTrue(container.TryAddInstance(instance));
            return definition;
        }

        private TagDefinition CreateDefinition(TagID tagID)
        {
            return CreateDefinition(tagID, string.Empty, TagCategory.None, null, StackPolicy.None);
        }

        private TagDefinition CreateValidStatusDefinition(TagID tagID, string tagName, StackPolicy stackPolicy)
        {
            return CreateDefinition(tagID, tagName, TagCategory.Status, CreateTrait(), stackPolicy);
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
            SerializedProperty tagIDProperty = serializedObject.FindProperty("tagID");
            Assert.IsNotNull(tagIDProperty);
            tagIDProperty.enumValueIndex = (int)tagID;

            SerializedProperty tagNameProperty = serializedObject.FindProperty("tagName");
            Assert.IsNotNull(tagNameProperty);
            tagNameProperty.stringValue = tagName;

            SerializedProperty categoryProperty = serializedObject.FindProperty("category");
            Assert.IsNotNull(categoryProperty);
            categoryProperty.enumValueIndex = (int)category;

            SerializedProperty traitProperty = serializedObject.FindProperty("trait");
            Assert.IsNotNull(traitProperty);
            traitProperty.objectReferenceValue = trait;

            SerializedProperty stackPolicyProperty = serializedObject.FindProperty("stackPolicy");
            Assert.IsNotNull(stackPolicyProperty);
            stackPolicyProperty.enumValueIndex = (int)stackPolicy;

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
            GameObject source = new GameObject("TagOperationSource");
            createdObjects.Add(source);
            return source;
        }

        private void AssertPermanentTagWasAdded(TagDefinition definition)
        {
            TagInstance foundInstance;
            Assert.IsTrue(container.TryGetTag(definition, out foundInstance));
            Assert.IsTrue(container.TagCheck(definition));
            Assert.AreSame(definition, foundInstance.Definition);
            Assert.AreEqual(0f, foundInstance.Duration);
            Assert.AreEqual(0f, foundInstance.RemainingTime);
            Assert.AreEqual(1, foundInstance.StackCount);
            Assert.IsFalse(foundInstance.IsPerishable);
        }

        private static TagInstance GetFirstInstance(IReadOnlyCollection<TagInstance> instances)
        {
            foreach (TagInstance instance in instances)
            {
                return instance;
            }

            return null;
        }

        private static bool ContainsInstance(IReadOnlyCollection<TagInstance> instances, TagInstance expectedInstance)
        {
            foreach (TagInstance instance in instances)
            {
                if (instance == expectedInstance)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
