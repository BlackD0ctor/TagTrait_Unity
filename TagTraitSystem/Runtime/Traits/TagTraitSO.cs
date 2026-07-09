using TagTraitSystem.Runtime.Core;
using UnityEngine;

namespace TagTraitSystem.Runtime.Traits
{
    /// <summary>
    /// Base type for ScriptableObject traits that can be shared by multiple objects.
    /// </summary>
    public abstract class TagTraitSO : ScriptableObject, ITagTrait
    {
        /// <summary>
        /// Called after a tag has been added to a container.
        /// </summary>
        /// <param name="context">The trait execution context.</param>
        public virtual void OnAdd(TraitContext context)
        {
        }

        /// <summary>
        /// Called before a tag is removed from a container.
        /// </summary>
        /// <param name="context">The trait execution context.</param>
        public virtual void OnRemove(TraitContext context)
        {
        }

        /// <summary>
        /// Called when a held tag is activated.
        /// </summary>
        /// <param name="context">The trait execution context.</param>
        /// <returns>True when activation succeeds; otherwise false.</returns>
        public abstract bool OnActivate(TraitContext context);
    }
}
