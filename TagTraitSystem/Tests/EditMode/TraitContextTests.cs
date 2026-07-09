using NUnit.Framework;
using TagTraitSystem.Runtime.Components;
using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Definitions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TagTraitSystem.Tests.EditMode
{
    public sealed class TraitContextTests
    {
        private GameObject containerObject;
        private GameObject sourceObject;
        private TagDefinition definition;

        [TearDown]
        public void TearDown()
        {
            if (containerObject != null)
            {
                Object.DestroyImmediate(containerObject);
                containerObject = null;
            }

            if (sourceObject != null)
            {
                Object.DestroyImmediate(sourceObject);
                sourceObject = null;
            }

            if (definition != null)
            {
                Object.DestroyImmediate(definition);
                definition = null;
            }
        }

        [Test]
        public void TraitContext_WhenCreated_PreservesAllValues()
        {
            TagContainer container = CreateContainer();
            sourceObject = new GameObject("Source");
            definition = ScriptableObject.CreateInstance<TagDefinition>();
            TagInstance instance = new TagInstance(definition);

            TraitContext context = new TraitContext(container, container.gameObject, sourceObject, definition, instance);

            Assert.AreSame(container, context.Container);
            Assert.AreSame(container.gameObject, context.Target);
            Assert.AreSame(sourceObject, context.Source);
            Assert.AreSame(definition, context.Definition);
            Assert.AreSame(instance, context.Instance);
        }

        [Test]
        public void TraitContext_Target_IsContainerGameObject()
        {
            TagContainer container = CreateContainer();

            TraitContext context = new TraitContext(container, container.gameObject, null, null, null);

            Assert.AreSame(container.gameObject, context.Target);
        }

        [Test]
        public void TraitContext_WhenSourceIsNull_PreservesNull()
        {
            TagContainer container = CreateContainer();

            TraitContext context = new TraitContext(container, container.gameObject, null, null, null);

            Assert.IsNull(context.Source);
        }

        [Test]
        public void TraitContext_WhenSourceIsSpecified_PreservesSource()
        {
            TagContainer container = CreateContainer();
            sourceObject = new GameObject("Source");

            TraitContext context = new TraitContext(container, container.gameObject, sourceObject, null, null);

            Assert.AreSame(sourceObject, context.Source);
        }

        private TagContainer CreateContainer()
        {
            containerObject = new GameObject("TraitContextTests");
            return containerObject.AddComponent<TagContainer>();
        }
    }
}
