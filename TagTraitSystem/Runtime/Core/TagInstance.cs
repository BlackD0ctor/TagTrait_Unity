using System;
using TagTraitSystem.Runtime.Definitions;

namespace TagTraitSystem.Runtime.Core
{
    /// <summary>
    /// Stores the runtime state for a tag held by an object.
    /// </summary>
    public sealed class TagInstance
    {
        /// <summary>
        /// Gets the source definition for this tag state.
        /// </summary>
        public TagDefinition Definition { get; }

        /// <summary>
        /// Gets the configured duration for this tag state.
        /// </summary>
        public float Duration { get; private set; }

        /// <summary>
        /// Gets the remaining duration for this tag state.
        /// </summary>
        public float RemainingTime { get; private set; }

        /// <summary>
        /// Gets the current stack count for this tag state.
        /// </summary>
        public int StackCount { get; private set; }

        /// <summary>
        /// Gets whether this tag state expires over time.
        /// </summary>
        public bool IsPerishable { get; private set; }

        // 생성 책임은 공개 API가 아니라 Assembly 내부로 제한한다.
        internal TagInstance(TagDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Duration = 0f;
            RemainingTime = 0f;
            StackCount = 1;
            IsPerishable = false;
        }

        // 생성 책임은 공개 API가 아니라 Assembly 내부로 제한한다.
        internal TagInstance(TagDefinition definition, float duration)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Duration = duration;
            RemainingTime = duration;
            StackCount = 1;
            IsPerishable = true;
        }

        internal void DecreaseRemainingTime(float deltaTime)
        {
            RemainingTime -= deltaTime;
            if (RemainingTime < 0f)
            {
                RemainingTime = 0f;
            }
        }

        internal bool TryConvertToPermanent()
        {
            if (!IsPerishable)
            {
                return false;
            }

            Duration = 0f;
            RemainingTime = 0f;
            IsPerishable = false;
            return true;
        }

        internal bool TryRefreshDuration(float duration)
        {
            if (!IsPerishable)
            {
                return false;
            }

            Duration = duration;
            RemainingTime = duration;
            return true;
        }

        internal bool TryExtendDurationToMax(float duration, float comparisonRemainingTime, float epsilon)
        {
            if (!IsPerishable)
            {
                return false;
            }

            float newDuration = Math.Max(Duration, duration);
            float newRemainingTime = Math.Max(comparisonRemainingTime, duration);
            if (newDuration <= Duration + epsilon && newRemainingTime <= comparisonRemainingTime + epsilon)
            {
                return false;
            }

            Duration = newDuration;
            RemainingTime = newRemainingTime;
            return true;
        }

        internal bool TryIncreaseStackCount()
        {
            if (Definition.MaxStackCount < 1)
            {
                return false;
            }

            if (StackCount >= Definition.MaxStackCount)
            {
                return false;
            }

            StackCount++;
            return true;
        }

        internal bool TryIncreasePerishableStackCount()
        {
            if (!IsPerishable)
            {
                return false;
            }

            if (Definition.MaxStackCount < 1)
            {
                return false;
            }

            if (StackCount >= Definition.MaxStackCount)
            {
                return false;
            }

            StackCount++;
            RemainingTime = Duration;
            return true;
        }

        internal bool TryDecreaseStackCount()
        {
            if (StackCount <= 1)
            {
                return false;
            }

            StackCount--;
            return true;
        }

        internal bool TryDecreasePerishableStackCount()
        {
            if (!IsPerishable)
            {
                return false;
            }

            if (StackCount <= 1)
            {
                return false;
            }

            StackCount--;
            RemainingTime = Duration;
            return true;
        }

        internal bool TryApplyPerishableStackTickResult(int stackCount, float remainingTime)
        {
            if (!IsPerishable)
            {
                return false;
            }

            if (stackCount < 1)
            {
                return false;
            }

            if (remainingTime < 0f || float.IsNaN(remainingTime) || float.IsInfinity(remainingTime))
            {
                return false;
            }

            StackCount = stackCount;
            RemainingTime = remainingTime;
            return true;
        }
    }
}
