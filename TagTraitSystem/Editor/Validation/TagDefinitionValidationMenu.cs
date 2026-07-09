using System.Collections.Generic;
using TagTraitSystem.Runtime.Definitions;
using TagTraitSystem.Runtime.Diagnostics;
using UnityEditor;

namespace TagTraitSystem.Editor.Validation
{
    internal static class TagDefinitionValidationMenu
    {
        internal const string MenuPath = "Tools/TagTraitSystem/Validate Tag Definitions";

        [MenuItem(MenuPath)]
        internal static void ValidateTagDefinitions()
        {
            TagDefinitionAssetRecord[] records = FindTagDefinitionAssetRecords();
            IReadOnlyList<TagValidationIssue> issues = TagDefinitionValidator.Validate(records);
            LogValidationResults(issues);
        }

        internal static void LogValidationResults(IReadOnlyList<TagValidationIssue> issues)
        {
            int errorCount = 0;
            int warningCount = 0;

            if (issues != null)
            {
                for (int i = 0; i < issues.Count; i++)
                {
                    TagValidationIssue issue = issues[i];
                    if (issue.Severity == TagValidationSeverity.Error)
                    {
                        errorCount++;
                        TagDiagnostics.LogError(issue.Message, issue.Definition);
                    }
                    else
                    {
                        warningCount++;
                        TagDiagnostics.LogWarning(issue.Message, issue.Definition);
                    }
                }
            }

            TagDiagnostics.Log("TagDefinition validation completed: " + errorCount + " errors, " + warningCount + " warnings.");
        }

        internal static TagDefinitionAssetRecord[] FindTagDefinitionAssetRecords()
        {
            string[] guids = AssetDatabase.FindAssets("t:TagDefinition");
            string[] paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            }

            System.Array.Sort(paths, System.StringComparer.Ordinal);

            TagDefinitionAssetRecord[] records = new TagDefinitionAssetRecord[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                TagDefinition definition = AssetDatabase.LoadAssetAtPath<TagDefinition>(paths[i]);
                records[i] = new TagDefinitionAssetRecord(definition, paths[i]);
            }

            return records;
        }
    }
}
