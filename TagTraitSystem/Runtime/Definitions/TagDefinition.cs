using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Traits;
using UnityEngine;

namespace TagTraitSystem.Runtime.Definitions
{
    /// <summary>
    /// Stores the source configuration for a tag.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTagDefinition", menuName = "TagTraitSystem/Tag Definition", order = 0)]
    public sealed class TagDefinition : ScriptableObject
    {
        [SerializeField] private TagID tagID;
        [SerializeField] private string tagName = string.Empty;
        [SerializeField] private TagCategory category;
        [SerializeField] private TagTraitSO trait;
        [SerializeField] private StackPolicy stackPolicy;
        [SerializeField] private int maxStackCount = 1;
        [SerializeField] private ExclusiveGroup exclusiveGroup;
        [SerializeField] private int priority;
        [SerializeField] private bool isSaveable;

        /// <summary>
        /// Gets the unique tag identifier.
        /// </summary>
        public TagID TagID => tagID;

        /// <summary>
        /// Gets the display name for this tag.
        /// </summary>
        public string TagName => tagName;

        /// <summary>
        /// Gets the tag category.
        /// </summary>
        public TagCategory Category => category;

        /// <summary>
        /// Gets the ScriptableObject trait assigned to this tag.
        /// </summary>
        public TagTraitSO Trait => trait;

        /// <summary>
        /// Gets the duplicate application policy for this tag.
        /// </summary>
        public StackPolicy StackPolicy => stackPolicy;

        /// <summary>
        /// Gets the configured maximum stack count.
        /// </summary>
        public int MaxStackCount => maxStackCount;

        /// <summary>
        /// Gets the exclusive group assigned to this tag.
        /// </summary>
        public ExclusiveGroup ExclusiveGroup => exclusiveGroup;

        /// <summary>
        /// Gets the priority assigned to this tag.
        /// </summary>
        public int Priority => priority;

        /// <summary>
        /// Gets whether this tag can be saved by a persistence layer.
        /// </summary>
        public bool IsSaveable => isSaveable;

        /// <summary>
        /// Gets whether this tag is classified as a keyword tag.
        /// </summary>
        public bool IsKeywordTag => category == TagCategory.Keyword;
    }
}
