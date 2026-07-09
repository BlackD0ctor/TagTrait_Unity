using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Diagnostics;
using TagTraitSystem.Runtime.Traits;
using UnityEngine;

namespace TagTraitSystem.Samples
{
    /// <summary>
    /// Logs trait lifecycle callbacks for sample TagDefinition assets.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSampleLogTrait", menuName = "TagTraitSystem/Samples/Logging Trait", order = 100)]
    public sealed class SampleLogTraitSO : TagTraitSO
    {
        [SerializeField] private bool activationResult = true;

        /// <summary>
        /// Logs the add callback context.
        /// </summary>
        /// <param name="context">The trait execution context.</param>
        public override void OnAdd(TraitContext context)
        {
            TagDiagnostics.Log("SampleLogTrait OnAdd: " + FormatContext(context), this);
        }

        /// <summary>
        /// Logs the remove callback context.
        /// </summary>
        /// <param name="context">The trait execution context.</param>
        public override void OnRemove(TraitContext context)
        {
            TagDiagnostics.Log("SampleLogTrait OnRemove: " + FormatContext(context), this);
        }

        /// <summary>
        /// Logs the activation callback context and returns the configured sample result.
        /// </summary>
        /// <param name="context">The trait execution context.</param>
        /// <returns>The configured activation result.</returns>
        public override bool OnActivate(TraitContext context)
        {
            TagDiagnostics.Log("SampleLogTrait OnActivate: " + FormatContext(context), this);
            return activationResult;
        }

        private static string FormatContext(TraitContext context)
        {
            return "Target=" + GetGameObjectName(context.Target)
                + ", Source=" + GetGameObjectName(context.Source)
                + ", TagName=" + GetTagName(context)
                + ", TagID=" + GetTagIDText(context)
                + ", StackCount=" + GetStackCountText(context)
                + ", RemainingTime=" + GetRemainingTimeText(context);
        }

        private static string GetGameObjectName(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "null";
            }

            return gameObject.name;
        }

        private static string GetTagName(TraitContext context)
        {
            if (context.Definition == null)
            {
                return "null";
            }

            return context.Definition.TagName;
        }

        private static string GetTagIDText(TraitContext context)
        {
            if (context.Definition == null)
            {
                return "null";
            }

            return context.Definition.TagID.ToString();
        }

        private static string GetStackCountText(TraitContext context)
        {
            if (context.Instance == null)
            {
                return "null";
            }

            return context.Instance.StackCount.ToString();
        }

        private static string GetRemainingTimeText(TraitContext context)
        {
            if (context.Instance == null)
            {
                return "null";
            }

            return context.Instance.RemainingTime.ToString();
        }
    }
}
