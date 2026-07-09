using TagTraitSystem.Runtime.Core;

namespace TagTraitSystem.Runtime.Traits
{
    /// <summary>
    /// Defines the lifecycle contract for tag traits.
    /// </summary>
    public interface ITagTrait
    {
        /// <summary>
        /// Called after a tag has been added to a container.
        /// </summary>
        /// <param name="context">The trait execution context.</param>
        void OnAdd(TraitContext context);

        /// <summary>
        /// Called before a tag is removed from a container.
        /// </summary>
        /// <param name="context">The trait execution context.</param>
        void OnRemove(TraitContext context);

        /// <summary>
        /// Called when a held tag is activated.
        /// </summary>
        /// <param name="context">The trait execution context.</param>
        /// <returns>True when activation succeeds; otherwise false.</returns>
        bool OnActivate(TraitContext context);
    }
}
