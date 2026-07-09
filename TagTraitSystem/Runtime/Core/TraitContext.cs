using TagTraitSystem.Runtime.Components;
using TagTraitSystem.Runtime.Definitions;
using UnityEngine;

namespace TagTraitSystem.Runtime.Core
{
    /// <summary>
    /// Provides runtime context for a trait callback.
    /// Object-specific runtime state is carried by this context and the TagInstance, not by the ScriptableObject trait asset.
    /// </summary>
    public readonly struct TraitContext
    {
        /// <summary>
        /// Gets the container executing the trait.
        /// </summary>
        public TagContainer Container { get; }

        /// <summary>
        /// Gets the GameObject that owns the container.
        /// </summary>
        public GameObject Target { get; }

        /// <summary>
        /// Gets the source GameObject passed to the public API.
        /// </summary>
        public GameObject Source { get; }

        /// <summary>
        /// Gets the tag definition being processed.
        /// </summary>
        public TagDefinition Definition { get; }

        /// <summary>
        /// Gets the runtime tag instance being processed.
        /// </summary>
        public TagInstance Instance { get; }

        internal TraitContext(
            TagContainer container,
            GameObject target,
            GameObject source,
            TagDefinition definition,
            TagInstance instance)
        {
            Container = container;
            Target = target;
            Source = source;
            Definition = definition;
            Instance = instance;
        }
    }
}
