using TagTraitSystem.Runtime.Components;
using TagTraitSystem.Runtime.Definitions;
using UnityEngine;

namespace TagTraitSystem.Runtime.Core
{
    /// <summary>
    /// Provides a read-only snapshot describing a tag state change event.
    /// The Instance reference itself can still point to mutable runtime state.
    /// </summary>
    public readonly struct TagChangeEventData
    {
        /// <summary>
        /// Gets the container that raised the event.
        /// </summary>
        public TagContainer Container { get; }

        /// <summary>
        /// Gets the tag definition that changed.
        /// </summary>
        public TagDefinition Definition { get; }

        /// <summary>
        /// Gets the tag instance after the change or the removed instance.
        /// </summary>
        public TagInstance Instance { get; }

        /// <summary>
        /// Gets the source object passed to the public API.
        /// </summary>
        public GameObject Source { get; }

        /// <summary>
        /// Gets the reason for the tag state change.
        /// </summary>
        public TagChangeReason Reason { get; }

        /// <summary>
        /// Gets the duration before the public API call changed the state.
        /// </summary>
        public float PreviousDuration { get; }

        /// <summary>
        /// Gets the remaining time before the public API call changed the state.
        /// </summary>
        public float PreviousRemainingTime { get; }

        /// <summary>
        /// Gets the stack count before the public API call changed the state.
        /// </summary>
        public int PreviousStackCount { get; }

        internal TagChangeEventData(
            TagContainer container,
            TagDefinition definition,
            TagInstance instance,
            GameObject source,
            TagChangeReason reason,
            float previousDuration,
            float previousRemainingTime,
            int previousStackCount)
        {
            Container = container;
            Definition = definition;
            Instance = instance;
            Source = source;
            Reason = reason;
            PreviousDuration = previousDuration;
            PreviousRemainingTime = previousRemainingTime;
            PreviousStackCount = previousStackCount;
        }
    }
}
