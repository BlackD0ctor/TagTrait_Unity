using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TagTraitSystem.Editor.Validation;
using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Definitions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TagTraitSystem.Tests.EditMode
{
    public sealed class TagDefinitionValidatorTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

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
        }

        [Test]
        public void Validate_WhenDefinitionIsNull_ReturnsError()
        {
            IReadOnlyList<TagValidationIssue> issues = Validate(new TagDefinitionAssetRecord(null, "Assets/Tags/Missing.asset"));

            AssertHasIssue(issues, TagValidationSeverity.Error, "could not be loaded");
        }

        [Test]
        public void Validate_WhenTagIDIsNone_ReturnsError()
        {
            TagDefinition definition = CreateDefinition(TagID.None, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(Record(definition, "Assets/Tags/A.asset"));

            AssertHasIssue(issues, TagValidationSeverity.Error, "TagID.None");
        }

        [Test]
        public void Validate_WhenTagNameIsNull_ReturnsError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            SetStringField(definition, "tagName", null);

            IReadOnlyList<TagValidationIssue> issues = Validate(Record(definition, "Assets/Tags/A.asset"));

            AssertHasIssue(issues, TagValidationSeverity.Error, "empty TagName");
        }

        [Test]
        public void Validate_WhenTagNameIsEmpty_ReturnsError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, string.Empty, TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(Record(definition, "Assets/Tags/A.asset"));

            AssertHasIssue(issues, TagValidationSeverity.Error, "empty TagName");
        }

        [Test]
        public void Validate_WhenTagNameIsWhitespace_DoesNotReturnNameError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "   ", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(Record(definition, "Assets/Tags/A.asset"));

            AssertNoIssueContains(issues, "empty TagName");
        }

        [Test]
        public void Validate_WhenNameHasLeadingOrTrailingWhitespace_DoesNotReturnNameError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, " Alpha ", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(Record(definition, "Assets/Tags/A.asset"));

            AssertNoIssueContains(issues, "empty TagName");
        }

        [Test]
        public void Validate_WhenKeywordHasTrait_ReturnsError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Keyword", TagCategory.Keyword, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(Record(definition, "Assets/Tags/A.asset"));

            AssertHasIssue(issues, TagValidationSeverity.Error, "Keyword tag with a trait");
        }

        [Test]
        public void Validate_WhenNonKeywordHasNoTrait_ReturnsError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, null, StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(Record(definition, "Assets/Tags/A.asset"));

            AssertHasIssue(issues, TagValidationSeverity.Error, "non-keyword tag without a trait");
        }

        [Test]
        public void Validate_WhenStackMaximumIsBelowOne_ReturnsError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.StackCount, 0);

            IReadOnlyList<TagValidationIssue> issues = Validate(Record(definition, "Assets/Tags/A.asset"));

            AssertHasIssue(issues, TagValidationSeverity.Error, "MaxStackCount less than 1");
        }

        [Test]
        public void Validate_WhenStackMaximumIsOne_DoesNotReturnMaximumError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.StackCount, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(Record(definition, "Assets/Tags/A.asset"));

            AssertNoIssueContains(issues, "MaxStackCount");
        }

        [Test]
        public void Validate_WhenNonStackPolicyHasInvalidMaximum_DoesNotReturnMaximumError()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 0);

            IReadOnlyList<TagValidationIssue> issues = Validate(Record(definition, "Assets/Tags/A.asset"));

            AssertNoIssueContains(issues, "MaxStackCount");
        }

        [Test]
        public void Validate_WhenNonNoneTagIDIsDuplicated_ReturnsErrorForEachAsset()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, "Alpha A", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestAlpha, "Alpha B", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/A.asset"),
                Record(secondDefinition, "Assets/Tags/B.asset"));

            Assert.AreEqual(2, CountIssuesContaining(issues, TagValidationSeverity.Error, "TagID 'TestAlpha'"));
        }

        [Test]
        public void Validate_WhenThreeAssetsShareID_ReturnsThreeIssues()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, "Alpha A", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestAlpha, "Alpha B", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition thirdDefinition = CreateDefinition(TagID.TestAlpha, "Alpha C", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/C.asset"),
                Record(secondDefinition, "Assets/Tags/A.asset"),
                Record(thirdDefinition, "Assets/Tags/B.asset"));

            Assert.AreEqual(3, CountIssuesContaining(issues, TagValidationSeverity.Error, "TagID 'TestAlpha'"));
        }

        [Test]
        public void Validate_WhenTagIDNoneRepeats_DoesNotAddDuplicateIDIssue()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.None, "Alpha A", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.None, "Alpha B", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/A.asset"),
                Record(secondDefinition, "Assets/Tags/B.asset"));

            Assert.AreEqual(0, CountIssuesContaining(issues, TagValidationSeverity.Error, "duplicated by"));
        }

        [Test]
        public void DuplicateIDIssue_IncludesAllConflictingPaths()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, "Alpha A", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestAlpha, "Alpha B", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/B.asset"),
                Record(secondDefinition, "Assets/Tags/A.asset"));

            AssertHasIssue(issues, TagValidationSeverity.Error, "Assets/Tags/A.asset, Assets/Tags/B.asset");
        }

        [Test]
        public void DuplicateIDIssue_UsesEachDefinitionAsContext()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, "Alpha A", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestAlpha, "Alpha B", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/B.asset"),
                Record(secondDefinition, "Assets/Tags/A.asset"));

            AssertHasContext(issues, firstDefinition);
            AssertHasContext(issues, secondDefinition);
        }

        [Test]
        public void DuplicateIDIssues_AreDeterministicallyOrdered()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestBeta, "Beta A", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestAlpha, "Alpha A", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition thirdDefinition = CreateDefinition(TagID.TestBeta, "Beta B", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition fourthDefinition = CreateDefinition(TagID.TestAlpha, "Alpha B", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/D.asset"),
                Record(secondDefinition, "Assets/Tags/C.asset"),
                Record(thirdDefinition, "Assets/Tags/B.asset"),
                Record(fourthDefinition, "Assets/Tags/A.asset"));

            Assert.AreEqual("Assets/Tags/A.asset", issues[0].AssetPath);
            Assert.AreEqual("Assets/Tags/C.asset", issues[1].AssetPath);
            Assert.AreEqual("Assets/Tags/B.asset", issues[2].AssetPath);
            Assert.AreEqual("Assets/Tags/D.asset", issues[3].AssetPath);
        }

        [Test]
        public void Validate_WhenNameIsDuplicated_ReturnsWarningForEachAsset()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, "Poison", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestBeta, "Poison", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/A.asset"),
                Record(secondDefinition, "Assets/Tags/B.asset"));

            Assert.AreEqual(2, CountIssuesContaining(issues, TagValidationSeverity.Warning, "TagName 'Poison'"));
        }

        [Test]
        public void Validate_WhenNamesDifferByCase_DoesNotReturnDuplicateWarning()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, "Poison", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestBeta, "poison", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/A.asset"),
                Record(secondDefinition, "Assets/Tags/B.asset"));

            Assert.AreEqual(0, CountSeverity(issues, TagValidationSeverity.Warning));
        }

        [Test]
        public void Validate_WhenNamesDifferByWhitespace_DoesNotReturnDuplicateWarning()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, "Poison", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestBeta, " Poison ", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/A.asset"),
                Record(secondDefinition, "Assets/Tags/B.asset"));

            Assert.AreEqual(0, CountSeverity(issues, TagValidationSeverity.Warning));
        }

        [Test]
        public void Validate_WhenWhitespaceOnlyNamesMatch_ReturnsWarning()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, "   ", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestBeta, "   ", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/A.asset"),
                Record(secondDefinition, "Assets/Tags/B.asset"));

            Assert.AreEqual(2, CountSeverity(issues, TagValidationSeverity.Warning));
        }

        [Test]
        public void Validate_WhenEmptyNamesRepeat_DoesNotAddDuplicateNameWarning()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, string.Empty, TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestBeta, string.Empty, TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/A.asset"),
                Record(secondDefinition, "Assets/Tags/B.asset"));

            Assert.AreEqual(0, CountSeverity(issues, TagValidationSeverity.Warning));
        }

        [Test]
        public void DuplicateNameIssue_IncludesAllConflictingPaths()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, "Poison", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestBeta, "Poison", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/B.asset"),
                Record(secondDefinition, "Assets/Tags/A.asset"));

            AssertHasIssue(issues, TagValidationSeverity.Warning, "Assets/Tags/A.asset, Assets/Tags/B.asset");
        }

        [Test]
        public void DuplicateNameIssues_AreDeterministicallyOrdered()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, "Poison", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestBeta, "Poison", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/B.asset"),
                Record(secondDefinition, "Assets/Tags/A.asset"));

            Assert.AreEqual("Assets/Tags/A.asset", issues[0].AssetPath);
            Assert.AreEqual("Assets/Tags/B.asset", issues[1].AssetPath);
        }

        [Test]
        public void Validate_DoesNotModifyDefinitions()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, " Alpha ", TagCategory.Status, CreateTrait(), StackPolicy.None, 0);

            Validate(Record(definition, "Assets/Tags/A.asset"));

            Assert.AreEqual(" Alpha ", definition.TagName);
            Assert.AreEqual(0, definition.MaxStackCount);
        }

        [Test]
        public void Validate_DoesNotModifyInputRecords()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinitionAssetRecord[] records = new TagDefinitionAssetRecord[]
            {
                Record(definition, "Assets/Tags/A.asset")
            };

            Validate(records);

            Assert.AreSame(definition, records[0].Definition);
            Assert.AreEqual("Assets/Tags/A.asset", records[0].AssetPath);
        }

        [Test]
        public void Validate_WhenNoIssues_ReturnsEmptyList()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(Record(definition, "Assets/Tags/A.asset"));

            Assert.AreEqual(0, issues.Count);
        }

        [Test]
        public void ValidationMenu_WhenNoIssues_LogsZeroSummary()
        {
            LogAssert.Expect(LogType.Log, new Regex("0 errors, 0 warnings"));

            TagDefinitionValidationMenu.LogValidationResults(new List<TagValidationIssue>());
        }

        [Test]
        public void ValidationMenu_WhenIssuesExist_LogsEachIssueAndSummary()
        {
            TagDefinition definition = CreateDefinition(TagID.TestAlpha, "Alpha", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            List<TagValidationIssue> issues = new List<TagValidationIssue>();
            issues.Add(new TagValidationIssue(TagValidationSeverity.Error, definition, "Assets/Tags/A.asset", "Error issue at Assets/Tags/A.asset."));
            issues.Add(new TagValidationIssue(TagValidationSeverity.Warning, definition, "Assets/Tags/B.asset", "Warning issue at Assets/Tags/B.asset."));
            LogAssert.Expect(LogType.Error, new Regex("Error issue"));
            LogAssert.Expect(LogType.Warning, new Regex("Warning issue"));
            LogAssert.Expect(LogType.Log, new Regex("1 errors, 1 warnings"));

            TagDefinitionValidationMenu.LogValidationResults(issues);
        }

        [Test]
        public void Summary_CountsPerAssetIssues()
        {
            List<TagValidationIssue> issues = new List<TagValidationIssue>();
            issues.Add(new TagValidationIssue(TagValidationSeverity.Error, null, "Assets/Tags/A.asset", "Error A."));
            issues.Add(new TagValidationIssue(TagValidationSeverity.Error, null, "Assets/Tags/B.asset", "Error B."));
            issues.Add(new TagValidationIssue(TagValidationSeverity.Warning, null, "Assets/Tags/C.asset", "Warning C."));
            LogAssert.Expect(LogType.Error, new Regex("Error A"));
            LogAssert.Expect(LogType.Error, new Regex("Error B"));
            LogAssert.Expect(LogType.Warning, new Regex("Warning C"));
            LogAssert.Expect(LogType.Log, new Regex("2 errors, 1 warnings"));

            TagDefinitionValidationMenu.LogValidationResults(issues);
        }

        [Test]
        public void ValidationResult_OrderIsStableForUnsortedInput()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.TestAlpha, "Alpha A", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.TestAlpha, "Alpha B", TagCategory.Status, CreateTrait(), StackPolicy.None, 1);

            IReadOnlyList<TagValidationIssue> issues = Validate(
                Record(firstDefinition, "Assets/Tags/B.asset"),
                Record(secondDefinition, "Assets/Tags/A.asset"));

            Assert.AreEqual("Assets/Tags/A.asset", issues[0].AssetPath);
            Assert.AreEqual("Assets/Tags/B.asset", issues[1].AssetPath);
        }

        [Test]
        public void ValidationMenu_MenuPath_IsExpected()
        {
            Assert.AreEqual("Tools/TagTraitSystem/Validate Tag Definitions", TagDefinitionValidationMenu.MenuPath);
        }

        private IReadOnlyList<TagValidationIssue> Validate(params TagDefinitionAssetRecord[] records)
        {
            return TagDefinitionValidator.Validate(records);
        }

        private TagDefinitionAssetRecord Record(TagDefinition definition, string assetPath)
        {
            return new TagDefinitionAssetRecord(definition, assetPath);
        }

        private void AssertHasIssue(IReadOnlyList<TagValidationIssue> issues, TagValidationSeverity severity, string text)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == severity && issues[i].Message.Contains(text))
                {
                    return;
                }
            }

            Assert.Fail("Expected issue containing: " + text);
        }

        private void AssertNoIssueContains(IReadOnlyList<TagValidationIssue> issues, string text)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                Assert.IsFalse(issues[i].Message.Contains(text));
            }
        }

        private void AssertHasContext(IReadOnlyList<TagValidationIssue> issues, TagDefinition definition)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Definition == definition)
                {
                    return;
                }
            }

            Assert.Fail("Expected issue context was not found.");
        }

        private int CountIssuesContaining(IReadOnlyList<TagValidationIssue> issues, TagValidationSeverity severity, string text)
        {
            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == severity && issues[i].Message.Contains(text))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountSeverity(IReadOnlyList<TagValidationIssue> issues, TagValidationSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == severity)
                {
                    count++;
                }
            }

            return count;
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

        private static void SetStringField(TagDefinition definition, string fieldName, string value)
        {
            FieldInfo fieldInfo = typeof(TagDefinition).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(fieldInfo);
            fieldInfo.SetValue(definition, value);
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
