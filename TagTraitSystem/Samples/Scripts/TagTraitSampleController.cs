using System.Collections.Generic;
using TagTraitSystem.Runtime.Components;
using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Definitions;
using TagTraitSystem.Runtime.Diagnostics;
using UnityEngine;

namespace TagTraitSystem.Samples
{
    /// <summary>
    /// Demonstrates the public TagContainer API without duplicating runtime tag state.
    /// </summary>
    [RequireComponent(typeof(TagContainer))]
    public sealed class TagTraitSampleController : MonoBehaviour
    {
        [SerializeField] private TagDefinition selectedDefinition;
        [SerializeField] private float duration = 5f;
        [SerializeField] private bool autoTick = true;

        private TagContainer container;
        private bool isSubscribed;

        private void Awake()
        {
            container = GetComponent<TagContainer>();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            if (!autoTick)
            {
                return;
            }

            if (!EnsureContainer("auto tick"))
            {
                return;
            }

            container.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Adds the selected definition as a permanent tag.
        /// </summary>
        [ContextMenu("Add Permanent Tag")]
        public void AddPermanent()
        {
            TagDefinition definition;
            if (!TryGetSelectedDefinition("add a permanent tag", out definition))
            {
                return;
            }

            container.TagAdd(definition, gameObject);
        }

        /// <summary>
        /// Adds the selected definition as a perishable tag.
        /// </summary>
        [ContextMenu("Add Perishable Tag")]
        public void AddPerishable()
        {
            TagDefinition definition;
            if (!TryGetSelectedDefinition("add a perishable tag", out definition))
            {
                return;
            }

            container.PerishableTagAdd(definition, duration, gameObject);
        }

        /// <summary>
        /// Removes the selected definition from the container.
        /// </summary>
        [ContextMenu("Remove Selected Tag")]
        public void Remove()
        {
            TagDefinition definition;
            if (!TryGetSelectedDefinition("remove a tag", out definition))
            {
                return;
            }

            container.TagSub(definition, gameObject);
        }

        /// <summary>
        /// Activates the selected definition if it is currently held.
        /// </summary>
        [ContextMenu("Activate Selected Tag")]
        public void Activate()
        {
            TagDefinition definition;
            if (!TryGetSelectedDefinition("activate a tag", out definition))
            {
                return;
            }

            container.TagActivate(definition, gameObject);
        }

        /// <summary>
        /// Advances the container by one second.
        /// </summary>
        [ContextMenu("Tick One Second")]
        public void TickOneSecond()
        {
            if (!EnsureContainer("tick one second"))
            {
                return;
            }

            container.Tick(1f);
        }

        /// <summary>
        /// Logs a snapshot of the current tags without mutating the container.
        /// </summary>
        [ContextMenu("Print Current Tags")]
        public void PrintCurrentTags()
        {
            if (!EnsureContainer("print current tags"))
            {
                return;
            }

            IReadOnlyCollection<TagInstance> snapshot = container.TagScan();
            TagDiagnostics.Log("Sample current tags count: " + snapshot.Count, this);

            List<TagInstance> instances = new List<TagInstance>(snapshot);
            for (int i = 0; i < instances.Count; i++)
            {
                TagInstance instance = instances[i];
                TagDefinition definition = instance.Definition;
                TagDiagnostics.Log(
                    "Sample current tag: TagName=" + GetTagName(definition)
                    + ", TagID=" + GetTagIDText(definition)
                    + ", IsPerishable=" + instance.IsPerishable
                    + ", Duration=" + instance.Duration
                    + ", RemainingTime=" + instance.RemainingTime
                    + ", StackCount=" + instance.StackCount,
                    this);
            }
        }

        private bool TryGetSelectedDefinition(string operationName, out TagDefinition definition)
        {
            definition = null;
            if (!EnsureContainer(operationName))
            {
                return false;
            }

            if (selectedDefinition == null)
            {
                TagDiagnostics.LogWarning(
                    "Sample controller cannot " + operationName + " because selectedDefinition is missing.",
                    this);
                return false;
            }

            definition = selectedDefinition;
            return true;
        }

        private bool EnsureContainer(string operationName)
        {
            if (container == null)
            {
                container = GetComponent<TagContainer>();
            }

            if (container != null)
            {
                return true;
            }

            TagDiagnostics.LogWarning(
                "Sample controller cannot " + operationName + " because TagContainer is missing.",
                this);
            return false;
        }

        private void SubscribeEvents()
        {
            if (isSubscribed)
            {
                return;
            }

            if (!EnsureContainer("subscribe to tag events"))
            {
                return;
            }

            container.OnTagAdded += HandleTagAdded;
            container.OnTagUpdated += HandleTagUpdated;
            container.OnTagRemoved += HandleTagRemoved;
            isSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!isSubscribed)
            {
                return;
            }

            if (container == null)
            {
                isSubscribed = false;
                return;
            }

            container.OnTagAdded -= HandleTagAdded;
            container.OnTagUpdated -= HandleTagUpdated;
            container.OnTagRemoved -= HandleTagRemoved;
            isSubscribed = false;
        }

        private void HandleTagAdded(TagChangeEventData eventData)
        {
            LogEvent("Added", eventData);
        }

        private void HandleTagUpdated(TagChangeEventData eventData)
        {
            LogEvent("Updated", eventData);
        }

        private void HandleTagRemoved(TagChangeEventData eventData)
        {
            LogEvent("Removed", eventData);
        }

        private void LogEvent(string eventName, TagChangeEventData eventData)
        {
            TagDiagnostics.Log(
                "Sample tag event " + eventName
                + ": TagName=" + GetTagName(eventData.Definition)
                + ", TagID=" + GetTagIDText(eventData.Definition)
                + ", Reason=" + eventData.Reason
                + ", StackCount=" + GetStackCountText(eventData.Instance)
                + ", RemainingTime=" + GetRemainingTimeText(eventData.Instance)
                + ", Source=" + GetSourceName(eventData.Source),
                this);
        }

        private static string GetTagName(TagDefinition definition)
        {
            if (definition == null)
            {
                return "null";
            }

            return definition.TagName;
        }

        private static string GetTagIDText(TagDefinition definition)
        {
            if (definition == null)
            {
                return "null";
            }

            return definition.TagID.ToString();
        }

        private static string GetStackCountText(TagInstance instance)
        {
            if (instance == null)
            {
                return "null";
            }

            return instance.StackCount.ToString();
        }

        private static string GetRemainingTimeText(TagInstance instance)
        {
            if (instance == null)
            {
                return "null";
            }

            return instance.RemainingTime.ToString();
        }

        private static string GetSourceName(GameObject source)
        {
            if (source == null)
            {
                return "null";
            }

            return source.name;
        }
    }
}
