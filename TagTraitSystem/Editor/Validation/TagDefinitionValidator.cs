using System;
using System.Collections.Generic;
using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Definitions;

namespace TagTraitSystem.Editor.Validation
{
    internal enum TagValidationSeverity
    {
        Warning,
        Error
    }

    internal readonly struct TagDefinitionAssetRecord
    {
        public TagDefinitionAssetRecord(TagDefinition definition, string assetPath)
        {
            Definition = definition;
            AssetPath = assetPath;
        }

        public TagDefinition Definition { get; }
        public string AssetPath { get; }
    }

    internal readonly struct TagValidationIssue
    {
        public TagValidationIssue(
            TagValidationSeverity severity,
            TagDefinition definition,
            string assetPath,
            string message)
        {
            Severity = severity;
            Definition = definition;
            AssetPath = assetPath;
            Message = message;
        }

        public TagValidationSeverity Severity { get; }
        public TagDefinition Definition { get; }
        public string AssetPath { get; }
        public string Message { get; }
    }

    internal static class TagDefinitionValidator
    {
        internal static IReadOnlyList<TagValidationIssue> Validate(IReadOnlyList<TagDefinitionAssetRecord> records)
        {
            List<TagValidationIssue> issues = new List<TagValidationIssue>();
            if (records == null || records.Count == 0)
            {
                return issues;
            }

            TagDefinitionAssetRecord[] sortedRecords = CopyRecords(records);
            Array.Sort(sortedRecords, CompareRecordsByPath);

            AddIndividualIssues(sortedRecords, issues);
            AddDuplicateIDIssues(sortedRecords, issues);
            AddDuplicateNameIssues(sortedRecords, issues);

            return issues;
        }

        private static TagDefinitionAssetRecord[] CopyRecords(IReadOnlyList<TagDefinitionAssetRecord> records)
        {
            TagDefinitionAssetRecord[] copiedRecords = new TagDefinitionAssetRecord[records.Count];
            for (int i = 0; i < records.Count; i++)
            {
                copiedRecords[i] = records[i];
            }

            return copiedRecords;
        }

        private static int CompareRecordsByPath(TagDefinitionAssetRecord left, TagDefinitionAssetRecord right)
        {
            return StringComparer.Ordinal.Compare(left.AssetPath, right.AssetPath);
        }

        private static void AddIndividualIssues(TagDefinitionAssetRecord[] records, List<TagValidationIssue> issues)
        {
            for (int i = 0; i < records.Length; i++)
            {
                TagDefinitionAssetRecord record = records[i];
                TagDefinition definition = record.Definition;
                string assetPath = record.AssetPath;

                if (definition == null)
                {
                    issues.Add(CreateIssue(
                        TagValidationSeverity.Error,
                        null,
                        assetPath,
                        "TagDefinition at '" + assetPath + "' could not be loaded."));
                    continue;
                }

                if (definition.TagID == TagID.None)
                {
                    issues.Add(CreateIssue(
                        TagValidationSeverity.Error,
                        definition,
                        assetPath,
                        "TagDefinition at '" + assetPath + "' uses TagID.None."));
                }

                if (definition.TagName == null || definition.TagName == string.Empty)
                {
                    issues.Add(CreateIssue(
                        TagValidationSeverity.Error,
                        definition,
                        assetPath,
                        "TagDefinition at '" + assetPath + "' has an empty TagName."));
                }

                if (definition.Category == TagCategory.Keyword && definition.Trait != null)
                {
                    issues.Add(CreateIssue(
                        TagValidationSeverity.Error,
                        definition,
                        assetPath,
                        "TagDefinition at '" + assetPath + "' is a Keyword tag with a trait."));
                }

                if (definition.Category != TagCategory.Keyword && definition.Trait == null)
                {
                    issues.Add(CreateIssue(
                        TagValidationSeverity.Error,
                        definition,
                        assetPath,
                        "TagDefinition at '" + assetPath + "' is a non-keyword tag without a trait."));
                }

                if (definition.StackPolicy == StackPolicy.StackCount && definition.MaxStackCount < 1)
                {
                    issues.Add(CreateIssue(
                        TagValidationSeverity.Error,
                        definition,
                        assetPath,
                        "TagDefinition at '" + assetPath + "' uses StackCount with MaxStackCount less than 1."));
                }
            }
        }

        private static void AddDuplicateIDIssues(TagDefinitionAssetRecord[] records, List<TagValidationIssue> issues)
        {
            TagID[] duplicateIDs = GetDuplicateIDs(records);
            Array.Sort(duplicateIDs);

            for (int i = 0; i < duplicateIDs.Length; i++)
            {
                TagID tagID = duplicateIDs[i];
                TagDefinitionAssetRecord[] duplicateRecords = GetRecordsForID(records, tagID);
                string paths = JoinPaths(duplicateRecords);

                for (int j = 0; j < duplicateRecords.Length; j++)
                {
                    TagDefinitionAssetRecord record = duplicateRecords[j];
                    issues.Add(CreateIssue(
                        TagValidationSeverity.Error,
                        record.Definition,
                        record.AssetPath,
                        "TagID '" + tagID + "' is duplicated by: " + paths + "."));
                }
            }
        }

        private static void AddDuplicateNameIssues(TagDefinitionAssetRecord[] records, List<TagValidationIssue> issues)
        {
            string[] duplicateNames = GetDuplicateNames(records);
            Array.Sort(duplicateNames, StringComparer.Ordinal);

            for (int i = 0; i < duplicateNames.Length; i++)
            {
                string tagName = duplicateNames[i];
                TagDefinitionAssetRecord[] duplicateRecords = GetRecordsForName(records, tagName);
                string paths = JoinPaths(duplicateRecords);

                for (int j = 0; j < duplicateRecords.Length; j++)
                {
                    TagDefinitionAssetRecord record = duplicateRecords[j];
                    issues.Add(CreateIssue(
                        TagValidationSeverity.Warning,
                        record.Definition,
                        record.AssetPath,
                        "TagName '" + tagName + "' is duplicated by: " + paths + "."));
                }
            }
        }

        private static TagID[] GetDuplicateIDs(TagDefinitionAssetRecord[] records)
        {
            List<TagID> duplicateIDs = new List<TagID>();
            for (int i = 0; i < records.Length; i++)
            {
                TagDefinition definition = records[i].Definition;
                if (definition == null || definition.TagID == TagID.None)
                {
                    continue;
                }

                TagID tagID = definition.TagID;
                if (ContainsTagID(duplicateIDs, tagID))
                {
                    continue;
                }

                if (CountID(records, tagID) > 1)
                {
                    duplicateIDs.Add(tagID);
                }
            }

            return duplicateIDs.ToArray();
        }

        private static string[] GetDuplicateNames(TagDefinitionAssetRecord[] records)
        {
            List<string> duplicateNames = new List<string>();
            for (int i = 0; i < records.Length; i++)
            {
                TagDefinition definition = records[i].Definition;
                if (definition == null || definition.TagName == null || definition.TagName == string.Empty)
                {
                    continue;
                }

                string tagName = definition.TagName;
                if (ContainsString(duplicateNames, tagName))
                {
                    continue;
                }

                if (CountName(records, tagName) > 1)
                {
                    duplicateNames.Add(tagName);
                }
            }

            return duplicateNames.ToArray();
        }

        private static int CountID(TagDefinitionAssetRecord[] records, TagID tagID)
        {
            int count = 0;
            for (int i = 0; i < records.Length; i++)
            {
                TagDefinition definition = records[i].Definition;
                if (definition != null && definition.TagID == tagID)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountName(TagDefinitionAssetRecord[] records, string tagName)
        {
            int count = 0;
            for (int i = 0; i < records.Length; i++)
            {
                TagDefinition definition = records[i].Definition;
                if (definition != null && string.Equals(definition.TagName, tagName, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static TagDefinitionAssetRecord[] GetRecordsForID(TagDefinitionAssetRecord[] records, TagID tagID)
        {
            List<TagDefinitionAssetRecord> matchingRecords = new List<TagDefinitionAssetRecord>();
            for (int i = 0; i < records.Length; i++)
            {
                TagDefinition definition = records[i].Definition;
                if (definition != null && definition.TagID == tagID)
                {
                    matchingRecords.Add(records[i]);
                }
            }

            TagDefinitionAssetRecord[] result = matchingRecords.ToArray();
            Array.Sort(result, CompareRecordsByPath);
            return result;
        }

        private static TagDefinitionAssetRecord[] GetRecordsForName(TagDefinitionAssetRecord[] records, string tagName)
        {
            List<TagDefinitionAssetRecord> matchingRecords = new List<TagDefinitionAssetRecord>();
            for (int i = 0; i < records.Length; i++)
            {
                TagDefinition definition = records[i].Definition;
                if (definition != null && string.Equals(definition.TagName, tagName, StringComparison.Ordinal))
                {
                    matchingRecords.Add(records[i]);
                }
            }

            TagDefinitionAssetRecord[] result = matchingRecords.ToArray();
            Array.Sort(result, CompareRecordsByPath);
            return result;
        }

        private static string JoinPaths(TagDefinitionAssetRecord[] records)
        {
            string paths = string.Empty;
            for (int i = 0; i < records.Length; i++)
            {
                if (i > 0)
                {
                    paths += ", ";
                }

                paths += records[i].AssetPath;
            }

            return paths;
        }

        private static bool ContainsTagID(List<TagID> tagIDs, TagID tagID)
        {
            for (int i = 0; i < tagIDs.Count; i++)
            {
                if (tagIDs[i] == tagID)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsString(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static TagValidationIssue CreateIssue(
            TagValidationSeverity severity,
            TagDefinition definition,
            string assetPath,
            string message)
        {
            return new TagValidationIssue(severity, definition, assetPath, message);
        }
    }
}
