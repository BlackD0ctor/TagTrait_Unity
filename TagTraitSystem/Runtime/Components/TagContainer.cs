using System;
using System.Collections.Generic;
using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Definitions;
using TagTraitSystem.Runtime.Diagnostics;
using TagTraitSystem.Runtime.Traits;
using UnityEngine;

namespace TagTraitSystem.Runtime.Components
{
    /// <summary>
    /// Provides a component-owned tag collection and query API.
    /// </summary>
    public class TagContainer : MonoBehaviour
    {
        private const float TimeEpsilon = 0.0001f;

        private readonly Dictionary<TagID, TagInstance> tagInstances = new Dictionary<TagID, TagInstance>();
        private readonly HashSet<TagID> processingTagIDs = new HashSet<TagID>();
        private readonly HashSet<TagID> tickModifiedTagIDs = new HashSet<TagID>();
        private readonly Dictionary<TagID, float> tickStartRemainingTimes = new Dictionary<TagID, float>();
        private bool isTicking;

        // 이후 PerishableTagAdd, TagActivate, 시간 및 스택 상태 변경도 동일 TagID 가드를 사용해야 한다.

        /// <summary>
        /// Occurs after a tag has been added.
        /// </summary>
        public event Action<TagChangeEventData> OnTagAdded;

        /// <summary>
        /// Occurs after a tag has been removed.
        /// </summary>
        public event Action<TagChangeEventData> OnTagRemoved;

        /// <summary>
        /// Occurs after an existing tag has been updated.
        /// </summary>
        public event Action<TagChangeEventData> OnTagUpdated;

        /// <summary>
        /// Adds an existing tag instance to the internal collection.
        /// </summary>
        /// <param name="instance">The tag instance to add.</param>
        /// <returns>True when the instance is added; otherwise false.</returns>
        internal bool TryAddInstance(TagInstance instance)
        {
            if (instance == null)
            {
                return false;
            }

            TagDefinition definition = instance.Definition;
            if (definition == null || definition.TagID == TagID.None)
            {
                return false;
            }

            TagInstance existingInstance;
            if (tagInstances.TryGetValue(definition.TagID, out existingInstance))
            {
                if (existingInstance.Definition != definition)
                {
                    TagDiagnostics.LogError("A different tag definition with the same TagID is already registered.", definition);
                }
                else
                {
                }

                return false;
            }

            tagInstances.Add(definition.TagID, instance);
            return true;
        }

        /// <summary>
        /// Adds a permanent tag.
        /// </summary>
        /// <param name="definition">The tag definition to add.</param>
        /// <param name="source">The object requesting the operation.</param>
        /// <returns>True when the tag is added; otherwise false.</returns>
        public bool TagAdd(TagDefinition definition, GameObject source = null)
        {
            if (!ValidateDefinitionForAdd(definition))
            {
                return false;
            }

            TagID tagID = definition.TagID;
            if (!processingTagIDs.Add(tagID))
            {
                LogReentrantMutationBlocked(tagID);
                return false;
            }

            try
            {
                TagInstance existingInstance;
                if (tagInstances.TryGetValue(tagID, out existingInstance))
                {
                    if (existingInstance.Definition != definition)
                    {
                        TagDiagnostics.LogError("A different tag definition with the same TagID is already registered.", definition);
                        return false;
                    }

                    if (!existingInstance.IsPerishable)
                    {
                        if (definition.StackPolicy != StackPolicy.StackCount)
                        {
                            return false;
                        }

                        float previousDuration = existingInstance.Duration;
                        float previousRemainingTime = existingInstance.RemainingTime;
                        int previousStackCount = existingInstance.StackCount;
                        if (!existingInstance.TryIncreaseStackCount())
                        {
                            return false;
                        }

                        TagChangeEventData stackIncreaseEventData = new TagChangeEventData(
                            this,
                            existingInstance.Definition,
                            existingInstance,
                            source,
                            TagChangeReason.StackIncreased,
                            previousDuration,
                            previousRemainingTime,
                            previousStackCount);
                        SafeInvoke(OnTagUpdated, stackIncreaseEventData);
                        return true;
                    }

                    float conversionPreviousDuration = existingInstance.Duration;
                    float conversionPreviousRemainingTime = existingInstance.RemainingTime;
                    int conversionPreviousStackCount = existingInstance.StackCount;
                    if (!existingInstance.TryConvertToPermanent())
                    {
                        return false;
                    }

                    TagChangeEventData EventData = new TagChangeEventData(
                        this,
                        existingInstance.Definition,
                        existingInstance,
                        source,
                        TagChangeReason.ChangedToPermanent,
                        conversionPreviousDuration,
                        conversionPreviousRemainingTime,
                        conversionPreviousStackCount);
                    SafeInvoke(OnTagUpdated, EventData);
                    return true;
                }

                TagInstance instance = new TagInstance(definition);
                if (!TryAddInstance(instance))
                {
                    return false;
                }

                TraitContext traitContext = CreateTraitContext(instance, source);
                SafeInvokeOnAdd(instance, traitContext);

                TagChangeEventData eventData = new TagChangeEventData(
                    this,
                    definition,
                    instance,
                    source,
                    TagChangeReason.Added,
                    0f,
                    0f,
                    0);
                SafeInvoke(OnTagAdded, eventData);
                return true;
            }
            finally
            {
                processingTagIDs.Remove(tagID);
            }
        }

        /// <summary>
        /// Adds a tag that expires over time.
        /// </summary>
        /// <param name="definition">The tag definition to add.</param>
        /// <param name="duration">The initial duration in seconds.</param>
        /// <param name="source">The object requesting the operation.</param>
        /// <returns>True when the tag is added; otherwise false.</returns>
        public bool PerishableTagAdd(TagDefinition definition, float duration, GameObject source = null)
        {
            if (!ValidateDefinitionForAdd(definition))
            {
                return false;
            }

            float normalizedDuration;
            if (!TryNormalizeDuration(duration, out normalizedDuration))
            {
                TagDiagnostics.LogError("Cannot add a perishable tag with a non-positive, NaN, or infinite duration.", definition);
                return false;
            }

            TagID tagID = definition.TagID;
            if (!processingTagIDs.Add(tagID))
            {
                LogReentrantMutationBlocked(tagID);
                return false;
            }

            try
            {
                TagInstance existingInstance;
                if (tagInstances.TryGetValue(tagID, out existingInstance))
                {
                    if (existingInstance.Definition != definition)
                    {
                        TagDiagnostics.LogError("A different tag definition with the same TagID is already registered.", definition);
                        return false;
                    }

                    if (!existingInstance.IsPerishable)
                    {
                        return false;
                    }

                    if (definition.StackPolicy == StackPolicy.Refresh)
                    {
                        float previousDuration = existingInstance.Duration;
                        float previousRemainingTime = existingInstance.RemainingTime;
                        int previousStackCount = existingInstance.StackCount;
                        if (!existingInstance.TryRefreshDuration(normalizedDuration))
                        {
                            return false;
                        }

                        if (isTicking)
                        {
                            tickModifiedTagIDs.Add(tagID);
                        }

                        TagChangeEventData refreshEventData = new TagChangeEventData(
                            this,
                            existingInstance.Definition,
                            existingInstance,
                            source,
                            TagChangeReason.DurationRefreshed,
                            previousDuration,
                            previousRemainingTime,
                            previousStackCount);
                        SafeInvoke(OnTagUpdated, refreshEventData);
                        return true;
                    }

                    if (definition.StackPolicy == StackPolicy.MaxDuration)
                    {
                        float comparisonRemainingTime = existingInstance.RemainingTime;
                        if (isTicking)
                        {
                            float tickStartRemainingTime;
                            if (tickStartRemainingTimes.TryGetValue(tagID, out tickStartRemainingTime))
                            {
                                comparisonRemainingTime = tickStartRemainingTime;
                            }
                        }

                        float previousDuration = existingInstance.Duration;
                        float previousRemainingTime = existingInstance.RemainingTime;
                        int previousStackCount = existingInstance.StackCount;
                        if (!existingInstance.TryExtendDurationToMax(normalizedDuration, comparisonRemainingTime, TimeEpsilon))
                        {
                            return false;
                        }

                        if (isTicking)
                        {
                            tickModifiedTagIDs.Add(tagID);
                        }

                        TagChangeEventData maxDurationEventData = new TagChangeEventData(
                            this,
                            existingInstance.Definition,
                            existingInstance,
                            source,
                            TagChangeReason.DurationExtendedToMax,
                            previousDuration,
                            previousRemainingTime,
                            previousStackCount);
                        SafeInvoke(OnTagUpdated, maxDurationEventData);
                        return true;
                    }

                    if (definition.StackPolicy == StackPolicy.StackCount)
                    {
                        if (existingInstance.StackCount >= definition.MaxStackCount)
                        {
                            return false;
                        }

                        if (normalizedDuration != existingInstance.Duration)
                        {
                            TagDiagnostics.LogError("Perishable StackCount tags must be stacked with the same normalized duration.", definition);
                            return false;
                        }

                        float previousDuration = existingInstance.Duration;
                        float previousRemainingTime = existingInstance.RemainingTime;
                        int previousStackCount = existingInstance.StackCount;
                        if (!existingInstance.TryIncreasePerishableStackCount())
                        {
                            return false;
                        }

                        if (isTicking)
                        {
                            tickModifiedTagIDs.Add(tagID);
                        }

                        TagChangeEventData stackIncreaseEventData = new TagChangeEventData(
                            this,
                            existingInstance.Definition,
                            existingInstance,
                            source,
                            TagChangeReason.StackIncreased,
                            previousDuration,
                            previousRemainingTime,
                            previousStackCount);
                        SafeInvoke(OnTagUpdated, stackIncreaseEventData);
                        return true;
                    }

                    return false;
                }

                TagInstance instance = new TagInstance(definition, normalizedDuration);
                if (!TryAddInstance(instance))
                {
                    return false;
                }

                TraitContext traitContext = CreateTraitContext(instance, source);
                SafeInvokeOnAdd(instance, traitContext);

                TagChangeEventData eventData = new TagChangeEventData(
                    this,
                    definition,
                    instance,
                    source,
                    TagChangeReason.Added,
                    0f,
                    0f,
                    0);
                SafeInvoke(OnTagAdded, eventData);
                return true;
            }
            finally
            {
                processingTagIDs.Remove(tagID);
            }
        }

        /// <summary>
        /// Removes a held tag completely.
        /// </summary>
        /// <param name="definition">The tag definition to remove.</param>
        /// <param name="source">The object requesting the operation.</param>
        /// <returns>True when the tag is removed; otherwise false.</returns>
        public bool TagSub(TagDefinition definition, GameObject source = null)
        {
            if (definition == null)
            {
                TagDiagnostics.LogError("Cannot remove a null tag definition.");
                return false;
            }

            if (definition.TagID == TagID.None)
            {
                TagDiagnostics.LogError("Cannot remove a tag definition with TagID.None.", definition);
                return false;
            }

            TagID tagID = definition.TagID;
            if (!processingTagIDs.Add(tagID))
            {
                LogReentrantMutationBlocked(tagID);
                return false;
            }

            try
            {
                TagInstance existingInstance;
                if (!tagInstances.TryGetValue(definition.TagID, out existingInstance))
                {
                    return false;
                }

                if (existingInstance.Definition != definition)
                {
                    TagDiagnostics.LogError("A different tag definition with the same TagID is already registered.", definition);
                    return false;
                }

                float previousDuration = existingInstance.Duration;
                float previousRemainingTime = existingInstance.RemainingTime;
                int previousStackCount = existingInstance.StackCount;
                bool skipTraitOnRemove = false;

                if (existingInstance.Definition.StackPolicy == StackPolicy.StackCount)
                {
                    if (existingInstance.Definition.MaxStackCount < 1)
                    {
                        TagDiagnostics.LogError("Cannot reduce a StackCount tag with MaxStackCount less than 1.", existingInstance.Definition);
                    }
                    else if (existingInstance.StackCount > existingInstance.Definition.MaxStackCount)
                    {
                        TagDiagnostics.LogError("A StackCount tag has StackCount greater than MaxStackCount.", existingInstance.Definition);
                    }

                    if (!IsDefinitionValidForStackCountReduction(existingInstance.Definition))
                    {
                        skipTraitOnRemove = true;
                    }
                    else if (!existingInstance.IsPerishable && existingInstance.TryDecreaseStackCount())
                    {
                        TagChangeEventData stackDecreaseEventData = new TagChangeEventData(
                            this,
                            existingInstance.Definition,
                            existingInstance,
                            source,
                            TagChangeReason.StackDecreased,
                            previousDuration,
                            previousRemainingTime,
                            previousStackCount);
                        SafeInvoke(OnTagUpdated, stackDecreaseEventData);
                        return true;
                    }
                    else if (existingInstance.IsPerishable && existingInstance.TryDecreasePerishableStackCount())
                    {
                        if (isTicking)
                        {
                            tickModifiedTagIDs.Add(tagID);
                        }

                        TagChangeEventData stackDecreaseEventData = new TagChangeEventData(
                            this,
                            existingInstance.Definition,
                            existingInstance,
                            source,
                            TagChangeReason.StackDecreased,
                            previousDuration,
                            previousRemainingTime,
                            previousStackCount);
                        SafeInvoke(OnTagUpdated, stackDecreaseEventData);
                        return true;
                    }
                }

                RemoveInstanceInternal(
                    existingInstance,
                    source,
                    TagChangeReason.Removed,
                    previousDuration,
                    previousRemainingTime,
                    previousStackCount,
                    !skipTraitOnRemove);
                return true;
            }
            finally
            {
                processingTagIDs.Remove(tagID);
            }
        }

        /// <summary>
        /// Activates a held tag trait.
        /// </summary>
        /// <param name="definition">The tag definition to activate.</param>
        /// <param name="source">The object requesting the operation.</param>
        /// <returns>The trait activation result when activation succeeds; otherwise false.</returns>
        public bool TagActivate(TagDefinition definition, GameObject source = null)
        {
            if (!ValidateTagActivateDefinition(definition))
            {
                return false;
            }

            TagID tagID = definition.TagID;
            if (!processingTagIDs.Add(tagID))
            {
                LogReentrantMutationBlocked(tagID);
                return false;
            }

            try
            {
                TagInstance existingInstance;
                if (!tagInstances.TryGetValue(definition.TagID, out existingInstance))
                {
                    return false;
                }

                if (existingInstance.Definition != definition)
                {
                    TagDiagnostics.LogError("A different tag definition with the same TagID is already registered.", definition);
                    return false;
                }

                TraitContext traitContext = CreateTraitContext(existingInstance, source);
                return SafeInvokeOnActivate(existingInstance, traitContext);
            }
            finally
            {
                processingTagIDs.Remove(tagID);
            }
        }

        /// <summary>
        /// Advances perishable tag timers and expires depleted tags.
        /// </summary>
        /// <param name="deltaTime">The elapsed time to subtract from perishable tags.</param>
        public void Tick(float deltaTime)
        {
            if (isTicking)
            {
                TagDiagnostics.LogWarning("A nested Tick call was blocked.", this);
                return;
            }

            if (deltaTime == 0f)
            {
                return;
            }

            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                TagDiagnostics.LogError("Cannot tick tags with a negative, NaN, or infinite deltaTime.", this);
                return;
            }

            isTicking = true;
            try
            {
                tickModifiedTagIDs.Clear();

                TagInstance[] snapshot = new TagInstance[tagInstances.Count];
                tagInstances.Values.CopyTo(snapshot, 0);
                RecordTickStartRemainingTimes(snapshot);

                for (int i = 0; i < snapshot.Length; i++)
                {
                    TickInstance(snapshot[i], deltaTime);
                }
            }
            finally
            {
                tickModifiedTagIDs.Clear();
                tickStartRemainingTimes.Clear();
                isTicking = false;
            }
        }

        private void SafeInvoke(Action<TagChangeEventData> eventHandler, TagChangeEventData eventData)
        {
            if (eventHandler == null)
            {
                return;
            }

            Delegate[] invocationList = eventHandler.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                Action<TagChangeEventData> subscriber = (Action<TagChangeEventData>)invocationList[i];
                try
                {
                    subscriber(eventData);
                }
                catch (Exception exception)
                {
                    TagDiagnostics.LogError("Tag change event subscriber threw an exception: " + exception, this);
                }
            }
        }

        private void TickInstance(TagInstance snapshotInstance, float deltaTime)
        {
            if (snapshotInstance == null)
            {
                return;
            }

            if (snapshotInstance.Definition == null)
            {
                TagDiagnostics.LogError("Cannot tick a tag instance with a null definition.", this);
                return;
            }

            TagDefinition definition = snapshotInstance.Definition;
            TagInstance currentInstance;
            if (!tagInstances.TryGetValue(definition.TagID, out currentInstance))
            {
                return;
            }

            if (currentInstance != snapshotInstance || !currentInstance.IsPerishable)
            {
                return;
            }

            if (tickModifiedTagIDs.Contains(definition.TagID))
            {
                return;
            }

            if (!processingTagIDs.Add(definition.TagID))
            {
                return;
            }

            try
            {
                if (!tagInstances.TryGetValue(definition.TagID, out currentInstance))
                {
                    return;
                }

                if (currentInstance != snapshotInstance || !currentInstance.IsPerishable)
                {
                    return;
                }

                if (tickModifiedTagIDs.Contains(definition.TagID))
                {
                    return;
                }

                float previousDuration = currentInstance.Duration;
                float previousRemainingTime = currentInstance.RemainingTime;
                int previousStackCount = currentInstance.StackCount;

                if (!ValidateTickInstanceState(currentInstance))
                {
                    return;
                }

                if (currentInstance.Definition.StackPolicy == StackPolicy.StackCount)
                {
                    TickStackCountInstance(
                        currentInstance,
                        deltaTime,
                        previousDuration,
                        previousRemainingTime,
                        previousStackCount);
                    return;
                }

                currentInstance.DecreaseRemainingTime(deltaTime);
                if (currentInstance.RemainingTime > TimeEpsilon)
                {
                    return;
                }

                currentInstance.DecreaseRemainingTime(currentInstance.RemainingTime);
                RemoveInstanceInternal(
                    currentInstance,
                    null,
                    TagChangeReason.Expired,
                    previousDuration,
                    previousRemainingTime,
                    previousStackCount);
            }
            finally
            {
                processingTagIDs.Remove(definition.TagID);
            }
        }

        private void LogReentrantMutationBlocked(TagID tagID)
        {
            TagDiagnostics.LogWarning("A reentrant mutation for TagID '" + tagID + "' was blocked.", this);
        }

        private void RecordTickStartRemainingTimes(TagInstance[] snapshot)
        {
            tickStartRemainingTimes.Clear();
            for (int i = 0; i < snapshot.Length; i++)
            {
                TagInstance instance = snapshot[i];
                if (instance == null || instance.Definition == null || !instance.IsPerishable)
                {
                    continue;
                }

                TagDefinition definition = instance.Definition;
                TagInstance currentInstance;
                if (tagInstances.TryGetValue(definition.TagID, out currentInstance) && currentInstance == instance)
                {
                    tickStartRemainingTimes[definition.TagID] = instance.RemainingTime;
                }
            }
        }

        private bool ValidateTickInstanceState(TagInstance instance)
        {
            if (instance.Definition == null)
            {
                TagDiagnostics.LogError("Cannot tick a tag instance with a null definition.", this);
                return false;
            }

            if (instance.StackCount < 1)
            {
                TagDiagnostics.LogError("Cannot tick a tag with StackCount less than 1.", instance.Definition);
                return false;
            }

            if (instance.IsPerishable && (instance.Duration <= 0f || float.IsNaN(instance.Duration) || float.IsInfinity(instance.Duration)))
            {
                TagDiagnostics.LogError("Cannot tick a perishable tag with an invalid duration.", instance.Definition);
                return false;
            }

            if (instance.RemainingTime < 0f || float.IsNaN(instance.RemainingTime) || float.IsInfinity(instance.RemainingTime))
            {
                TagDiagnostics.LogError("Cannot tick a tag with an invalid remaining time.", instance.Definition);
                return false;
            }

            if (instance.Definition.StackPolicy == StackPolicy.StackCount && instance.Definition.MaxStackCount < 1)
            {
                TagDiagnostics.LogError("Cannot tick a StackCount tag with MaxStackCount less than 1.", instance.Definition);
                return false;
            }

            if (instance.Definition.StackPolicy == StackPolicy.StackCount && instance.StackCount > instance.Definition.MaxStackCount)
            {
                TagDiagnostics.LogError("A StackCount tag has StackCount greater than MaxStackCount.", instance.Definition);
            }

            return true;
        }

        private void TickStackCountInstance(
            TagInstance instance,
            float deltaTime,
            float previousDuration,
            float previousRemainingTime,
            int previousStackCount)
        {
            if (!ValidateStackCountTickState(instance))
            {
                return;
            }

            double duration = instance.Duration;
            double totalRemaining = (double)instance.RemainingTime + (((double)instance.StackCount - 1d) * duration);
            double remainingAfter = totalRemaining - deltaTime;
            if (remainingAfter <= TimeEpsilon)
            {
                instance.TryApplyPerishableStackTickResult(1, 0f);
                RemoveInstanceInternal(
                    instance,
                    null,
                    TagChangeReason.Expired,
                    previousDuration,
                    previousRemainingTime,
                    previousStackCount,
                    IsDefinitionValidForStackCountReduction(instance.Definition));
                return;
            }

            double finalStackCountValue = Math.Ceiling((remainingAfter - TimeEpsilon) / duration);
            int finalStackCount = previousStackCount;
            if (finalStackCountValue < previousStackCount)
            {
                finalStackCount = (int)finalStackCountValue;
            }

            if (finalStackCount < 1)
            {
                finalStackCount = 1;
            }

            double finalRemaining = remainingAfter - (((double)finalStackCount - 1d) * duration);
            if (finalRemaining > duration - TimeEpsilon)
            {
                finalRemaining = duration;
            }

            if (finalRemaining <= TimeEpsilon)
            {
                finalRemaining = duration;
            }

            float finalRemainingTime = (float)finalRemaining;
            if (!instance.TryApplyPerishableStackTickResult(finalStackCount, finalRemainingTime))
            {
                return;
            }

            if (finalStackCount == previousStackCount)
            {
                return;
            }

            TagChangeEventData stackDecreaseEventData = new TagChangeEventData(
                this,
                instance.Definition,
                instance,
                null,
                TagChangeReason.StackDecreased,
                previousDuration,
                previousRemainingTime,
                previousStackCount);
            SafeInvoke(OnTagUpdated, stackDecreaseEventData);
        }

        private bool ValidateStackCountTickState(TagInstance instance)
        {
            if (instance.StackCount < 1)
            {
                TagDiagnostics.LogError("Cannot tick a StackCount tag with StackCount less than 1.", instance.Definition);
                return false;
            }

            if (instance.Duration <= 0f || float.IsNaN(instance.Duration) || float.IsInfinity(instance.Duration))
            {
                TagDiagnostics.LogError("Cannot tick a StackCount tag with an invalid duration.", instance.Definition);
                return false;
            }

            if (instance.RemainingTime < 0f || float.IsNaN(instance.RemainingTime) || float.IsInfinity(instance.RemainingTime))
            {
                TagDiagnostics.LogError("Cannot tick a StackCount tag with an invalid remaining time.", instance.Definition);
                return false;
            }

            return true;
        }

        private void RemoveInstanceInternal(
            TagInstance instance,
            GameObject source,
            TagChangeReason reason,
            float previousDuration,
            float previousRemainingTime,
            int previousStackCount,
            bool invokeTrait = true)
        {
            if (invokeTrait)
            {
                TraitContext traitContext = CreateTraitContext(instance, source);
                SafeInvokeOnRemove(instance, traitContext);
            }

            tagInstances.Remove(instance.Definition.TagID);

            TagChangeEventData eventData = new TagChangeEventData(
                this,
                instance.Definition,
                instance,
                source,
                reason,
                previousDuration,
                previousRemainingTime,
                previousStackCount);
            SafeInvoke(OnTagRemoved, eventData);
        }

        private TraitContext CreateTraitContext(TagInstance instance, GameObject source)
        {
            return new TraitContext(this, gameObject, source, instance.Definition, instance);
        }

        private bool ShouldInvokeTrait(TagInstance instance)
        {
            TagDefinition definition = instance.Definition;
            return definition.Category != TagCategory.Keyword && definition.Trait != null;
        }

        private void SafeInvokeOnAdd(TagInstance instance, TraitContext context)
        {
            if (!ShouldInvokeTrait(instance))
            {
                return;
            }

            try
            {
                instance.Definition.Trait.OnAdd(context);
            }
            catch (Exception exception)
            {
                TagDiagnostics.LogError("Tag trait OnAdd threw an exception: " + exception, instance.Definition.Trait);
            }
        }

        private void SafeInvokeOnRemove(TagInstance instance, TraitContext context)
        {
            if (!ShouldInvokeTrait(instance))
            {
                return;
            }

            try
            {
                instance.Definition.Trait.OnRemove(context);
            }
            catch (Exception exception)
            {
                TagDiagnostics.LogError("Tag trait OnRemove threw an exception: " + exception, instance.Definition.Trait);
            }
        }

        private bool SafeInvokeOnActivate(TagInstance instance, TraitContext context)
        {
            ITagTrait trait = instance.Definition.Trait;
            try
            {
                return trait.OnActivate(context);
            }
            catch (Exception exception)
            {
                TagDiagnostics.LogError("Tag trait OnActivate threw an exception: " + exception, instance.Definition.Trait);
                return false;
            }
        }

        /// <summary>
        /// Returns a shallow snapshot of current tag instances in unspecified order.
        /// TagInstance references are not cloned.
        /// </summary>
        /// <returns>A snapshot collection of current tag instances.</returns>
        public IReadOnlyCollection<TagInstance> TagScan()
        {
            if (tagInstances.Count == 0)
            {
                return System.Array.Empty<TagInstance>();
            }

            TagInstance[] snapshot = new TagInstance[tagInstances.Count];
            tagInstances.Values.CopyTo(snapshot, 0);
            return snapshot;
        }

        /// <summary>
        /// Returns whether a tag with the same ID as the definition exists.
        /// </summary>
        /// <param name="definition">The definition to query.</param>
        /// <returns>True when a tag with the same ID exists; otherwise false.</returns>
        public bool TagCheck(TagDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            return TagCheck(definition.TagID);
        }

        /// <summary>
        /// Returns whether a tag with the given ID exists.
        /// </summary>
        /// <param name="tagID">The tag ID to query.</param>
        /// <returns>True when the tag ID exists; otherwise false.</returns>
        public bool TagCheck(TagID tagID)
        {
            if (tagID == TagID.None)
            {
                return false;
            }

            return tagInstances.ContainsKey(tagID);
        }

        /// <summary>
        /// Returns whether all given tag definitions exist by ID.
        /// </summary>
        /// <param name="definitions">The definitions to query.</param>
        /// <returns>True when every valid definition exists; otherwise false.</returns>
        public bool TagANDCheck(params TagDefinition[] definitions)
        {
            if (!ValidateDefinitionArray(definitions))
            {
                return false;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                if (!TagCheck(definitions[i].TagID))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns whether any given tag definition exists by ID.
        /// </summary>
        /// <param name="definitions">The definitions to query.</param>
        /// <returns>True when at least one valid definition exists; otherwise false.</returns>
        public bool TagORCheck(params TagDefinition[] definitions)
        {
            if (!ValidateDefinitionArray(definitions))
            {
                return false;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                if (TagCheck(definitions[i].TagID))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to get the stored tag instance with the same ID as the definition.
        /// </summary>
        /// <param name="definition">The definition to query.</param>
        /// <param name="instance">The stored tag instance when found.</param>
        /// <returns>True when a tag with the same ID exists; otherwise false.</returns>
        public bool TryGetTag(TagDefinition definition, out TagInstance instance)
        {
            instance = null;

            if (definition == null || definition.TagID == TagID.None)
            {
                return false;
            }

            return tagInstances.TryGetValue(definition.TagID, out instance);
        }

        private bool ValidateDefinitionArray(TagDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                TagDefinition definition = definitions[i];
                if (definition == null)
                {
                    TagDiagnostics.LogError("Tag query contains a null definition.");
                    return false;
                }

                if (definition.TagID == TagID.None)
                {
                    TagDiagnostics.LogError("Tag query contains a definition with TagID.None.", definition);
                    return false;
                }
            }

            return true;
        }

        private bool ValidateDefinitionForAdd(TagDefinition definition)
        {
            if (definition == null)
            {
                TagDiagnostics.LogError("Cannot add a null tag definition.");
                return false;
            }

            if (definition.TagID == TagID.None)
            {
                TagDiagnostics.LogError("Cannot add a tag definition with TagID.None.", definition);
                return false;
            }

            if (string.IsNullOrEmpty(definition.TagName))
            {
                TagDiagnostics.LogError("Cannot add a tag definition with an empty name.", definition);
                return false;
            }

            if (definition.Category == TagCategory.Keyword && definition.Trait != null)
            {
                TagDiagnostics.LogError("Keyword tag definitions cannot have a trait.", definition);
                return false;
            }

            if (definition.Category != TagCategory.Keyword && definition.Trait == null)
            {
                TagDiagnostics.LogError("Non-keyword tag definitions must have a trait.", definition);
                return false;
            }

            if (definition.StackPolicy == StackPolicy.StackCount && definition.MaxStackCount < 1)
            {
                TagDiagnostics.LogError("StackCount tag definitions must have MaxStackCount greater than or equal to 1.", definition);
                return false;
            }

            return true;
        }

        private bool IsDefinitionValidForStackCountReduction(TagDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.TagName))
            {
                return false;
            }

            if (definition.Category == TagCategory.Keyword && definition.Trait != null)
            {
                return false;
            }

            if (definition.Category != TagCategory.Keyword && definition.Trait == null)
            {
                return false;
            }

            if (definition.StackPolicy == StackPolicy.StackCount && definition.MaxStackCount < 1)
            {
                return false;
            }

            return true;
        }

        private static bool TryNormalizeDuration(float duration, out float normalizedDuration)
        {
            normalizedDuration = 0f;
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                return false;
            }

            double roundedDuration = Math.Round(duration, 1, MidpointRounding.AwayFromZero);
            normalizedDuration = Mathf.Max(0.1f, (float)roundedDuration);
            return true;
        }

        private bool ValidateTagActivateDefinition(TagDefinition definition)
        {
            if (definition == null)
            {
                TagDiagnostics.LogError("Cannot activate a null tag definition.");
                return false;
            }

            if (definition.TagID == TagID.None)
            {
                TagDiagnostics.LogError("Cannot activate a tag definition with TagID.None.", definition);
                return false;
            }

            if (string.IsNullOrEmpty(definition.TagName))
            {
                TagDiagnostics.LogError("Cannot activate a tag definition with an empty name.", definition);
                return false;
            }

            if (definition.Category == TagCategory.Keyword)
            {
                if (definition.Trait != null)
                {
                    TagDiagnostics.LogError("Keyword tag definitions cannot be activated with a trait.", definition);
                }

                return false;
            }

            if (definition.Trait == null)
            {
                TagDiagnostics.LogError("Non-keyword tag definitions must have a trait to activate.", definition);
                return false;
            }

            return true;
        }
    }
}
