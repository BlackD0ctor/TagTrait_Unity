using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TagTraitSystem.Runtime.Components;
using TagTraitSystem.Runtime.Core;
using TagTraitSystem.Runtime.Definitions;
using TagTraitSystem.Runtime.Traits;
using TagTraitSystem.Samples;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TagTraitSystem.Tests.EditMode
{
    public sealed class TagTraitSampleControllerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject gameObject;
        private TagTraitSampleController controller;
        private TagContainer container;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TagTraitSampleControllerTests");
            createdObjects.Add(gameObject);
            controller = gameObject.AddComponent<TagTraitSampleController>();
            container = gameObject.GetComponent<TagContainer>();
            InvokeControllerLifecycle(controller, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (controller != null)
            {
                InvokeControllerLifecycle(controller, "OnDisable");
            }

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            gameObject = null;
            controller = null;
            container = null;
        }

        [Test]
        public void SampleController_WhenAdded_HasTagContainer()
        {
            Assert.IsNotNull(controller);
            Assert.IsNotNull(container);
        }

        [Test]
        public void SampleController_WhenDefinitionIsMissing_LogsWarning()
        {
            SetControllerFields(null, 5f, false);

            LogAssert.Expect(LogType.Warning, new Regex("selectedDefinition"));
            controller.AddPermanent();

            Assert.AreEqual(0, container.TagScan().Count);
        }

        [Test]
        public void SampleController_AddPermanent_ForwardsToContainer()
        {
            SampleLogTraitSO trait = CreateSampleTrait();
            TagDefinition definition = CreateDefinition(TagID.SamplePermanent, "SamplePermanent", TagCategory.Status, trait, StackPolicy.None, 1);
            SetControllerFields(definition, 5f, false);

            ExpectSampleLog("SampleLogTrait OnAdd.*SamplePermanent");
            ExpectSampleLog("Sample tag event Added.*SamplePermanent");
            controller.AddPermanent();

            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void SampleController_AddPerishable_ForwardsDurationAndSource()
        {
            SampleLogTraitSO trait = CreateSampleTrait();
            TagDefinition definition = CreateDefinition(TagID.SampleRefresh, "SampleRefresh", TagCategory.Status, trait, StackPolicy.Refresh, 1);
            TagChangeEventData capturedEvent = default(TagChangeEventData);
            SetControllerFields(definition, 2.5f, false);
            container.OnTagAdded += delegate(TagChangeEventData eventData) { capturedEvent = eventData; };

            ExpectSampleLog("SampleLogTrait OnAdd.*SampleRefresh");
            ExpectSampleLog("Sample tag event Added.*SampleRefresh");
            controller.AddPerishable();

            TagInstance instance = GetInstance(definition);
            Assert.AreEqual(2.5f, instance.Duration);
            Assert.AreEqual(2.5f, instance.RemainingTime);
            Assert.AreSame(gameObject, capturedEvent.Source);
        }

        [Test]
        public void SampleController_Remove_ForwardsToContainer()
        {
            SampleLogTraitSO trait = CreateSampleTrait();
            TagDefinition definition = CreateDefinition(TagID.SamplePermanent, "SamplePermanent", TagCategory.Status, trait, StackPolicy.None, 1);
            SetControllerFields(definition, 5f, false);
            ExpectSampleLog("SampleLogTrait OnAdd.*SamplePermanent");
            ExpectSampleLog("Sample tag event Added.*SamplePermanent");
            Assert.IsTrue(container.TagAdd(definition, gameObject));

            ExpectSampleLog("SampleLogTrait OnRemove.*SamplePermanent");
            ExpectSampleLog("Sample tag event Removed.*SamplePermanent");
            controller.Remove();

            Assert.IsFalse(container.TagCheck(definition));
        }

        [Test]
        public void SampleController_Activate_ForwardsToContainer()
        {
            SampleLogTraitSO trait = CreateSampleTrait();
            TagDefinition definition = CreateDefinition(TagID.SamplePermanent, "SamplePermanent", TagCategory.Status, trait, StackPolicy.None, 1);
            SetControllerFields(definition, 5f, false);
            ExpectSampleLog("SampleLogTrait OnAdd.*SamplePermanent");
            ExpectSampleLog("Sample tag event Added.*SamplePermanent");
            Assert.IsTrue(container.TagAdd(definition, gameObject));

            ExpectSampleLog("SampleLogTrait OnActivate.*SamplePermanent");
            controller.Activate();

            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void SampleController_TickOneSecond_AdvancesContainer()
        {
            SampleLogTraitSO trait = CreateSampleTrait();
            TagDefinition definition = CreateDefinition(TagID.SampleRefresh, "SampleRefresh", TagCategory.Status, trait, StackPolicy.Refresh, 1);
            SetControllerFields(definition, 3f, false);
            ExpectSampleLog("SampleLogTrait OnAdd.*SampleRefresh");
            ExpectSampleLog("Sample tag event Added.*SampleRefresh");
            controller.AddPerishable();

            controller.TickOneSecond();

            Assert.AreEqual(2f, GetInstance(definition).RemainingTime);
        }

        [Test]
        public void SampleController_PrintCurrentTags_DoesNotModifyContainer()
        {
            TagDefinition definition = CreateDefinition(TagID.SampleKeyword, "SampleKeyword", TagCategory.Keyword, null, StackPolicy.None, 1);
            SetControllerFields(definition, 5f, false);
            ExpectSampleLog("Sample tag event Added.*SampleKeyword");
            controller.AddPermanent();
            int beforeCount = container.TagScan().Count;

            ExpectSampleLog("Sample current tags count: 1");
            ExpectSampleLog("Sample current tag:.*SampleKeyword");
            controller.PrintCurrentTags();

            Assert.AreEqual(beforeCount, container.TagScan().Count);
            Assert.IsTrue(container.TagCheck(definition));
        }

        [Test]
        public void SampleController_OnEnableAndDisable_ManagesEventSubscriptions()
        {
            TagDefinition firstDefinition = CreateDefinition(TagID.SampleKeyword, "SampleKeyword", TagCategory.Keyword, null, StackPolicy.None, 1);
            TagDefinition secondDefinition = CreateDefinition(TagID.SamplePermanent, "SamplePermanentKeyword", TagCategory.Keyword, null, StackPolicy.None, 1);

            InvokeControllerLifecycle(controller, "OnEnable");
            InvokeControllerLifecycle(controller, "OnEnable");

            ExpectSampleLog("Sample tag event Added.*SampleKeyword");
            Assert.IsTrue(container.TagAdd(firstDefinition, gameObject));

            InvokeControllerLifecycle(controller, "OnDisable");
            Assert.IsTrue(container.TagAdd(secondDefinition, gameObject));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SampleLogTrait_OnActivate_ReturnsConfiguredValue()
        {
            GameObject traitGameObject = new GameObject("SampleTraitActivation");
            createdObjects.Add(traitGameObject);
            TagContainer traitContainer = traitGameObject.AddComponent<TagContainer>();
            SampleLogTraitSO trait = CreateSampleTrait();
            SetBoolProperty(trait, "activationResult", false);
            TagDefinition definition = CreateDefinition(TagID.SamplePermanent, "SamplePermanent", TagCategory.Status, trait, StackPolicy.None, 1);

            ExpectSampleLog("SampleLogTrait OnAdd.*SamplePermanent");
            Assert.IsTrue(traitContainer.TagAdd(definition, traitGameObject));
            ExpectSampleLog("SampleLogTrait OnActivate.*SamplePermanent");
            Assert.IsFalse(traitContainer.TagActivate(definition, traitGameObject));
        }

        [Test]
        public void SampleLogTrait_DoesNotStoreRuntimeTargetState()
        {
            FieldInfo[] fields = typeof(SampleLogTraitSO).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            Assert.AreEqual(1, fields.Length);
            Assert.AreEqual("activationResult", fields[0].Name);
        }

        private SampleLogTraitSO CreateSampleTrait()
        {
            SampleLogTraitSO trait = ScriptableObject.CreateInstance<SampleLogTraitSO>();
            createdObjects.Add(trait);
            return trait;
        }

        private TagDefinition CreateDefinition(
            TagID tagID,
            string tagName,
            TagCategory category,
            TagTraitSO trait,
            StackPolicy stackPolicy,
            int maxStackCount)
        {
            TagDefinition definition = ScriptableObject.CreateInstance<TagDefinition>();
            createdObjects.Add(definition);

            SerializedObject serializedObject = new SerializedObject(definition);
            SetEnumProperty(serializedObject, "tagID", (int)tagID);
            SetStringProperty(serializedObject, "tagName", tagName);
            SetEnumProperty(serializedObject, "category", (int)category);
            SetObjectProperty(serializedObject, "trait", trait);
            SetEnumProperty(serializedObject, "stackPolicy", (int)stackPolicy);
            SetIntProperty(serializedObject, "maxStackCount", maxStackCount);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return definition;
        }

        private void SetControllerFields(TagDefinition definition, float tagDuration, bool shouldAutoTick)
        {
            SerializedObject serializedObject = new SerializedObject(controller);
            SetObjectProperty(serializedObject, "selectedDefinition", definition);
            SetFloatProperty(serializedObject, "duration", tagDuration);
            SetBoolProperty(serializedObject, "autoTick", shouldAutoTick);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private TagInstance GetInstance(TagDefinition definition)
        {
            TagInstance instance;
            Assert.IsTrue(container.TryGetTag(definition, out instance));
            return instance;
        }

        private static void ExpectSampleLog(string pattern)
        {
            LogAssert.Expect(LogType.Log, new Regex(pattern));
        }

        private static void InvokeControllerLifecycle(TagTraitSampleController target, string methodName)
        {
            if (target == null)
            {
                return;
            }

            MethodInfo methodInfo = typeof(TagTraitSampleController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(methodInfo);
            methodInfo.Invoke(target, null);
        }

        private static void SetEnumProperty(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.intValue = value;
        }

        private static void SetStringProperty(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.stringValue = value;
        }

        private static void SetObjectProperty(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.objectReferenceValue = value;
        }

        private static void SetIntProperty(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.intValue = value;
        }

        private static void SetFloatProperty(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.floatValue = value;
        }

        private static void SetBoolProperty(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.boolValue = value;
        }

        private static void SetBoolProperty(Object target, string propertyName, bool value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SetBoolProperty(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
