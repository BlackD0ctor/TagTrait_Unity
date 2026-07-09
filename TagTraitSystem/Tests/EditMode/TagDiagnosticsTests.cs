using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TagTraitSystem.Runtime.Diagnostics;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TagTraitSystem.Tests.EditMode
{
    public sealed class TagDiagnosticsTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private ILogHandler originalLogHandler;

        [TearDown]
        public void TearDown()
        {
            if (originalLogHandler != null)
            {
                Debug.unityLogger.logHandler = originalLogHandler;
                originalLogHandler = null;
            }

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void TagDiagnostics_Log_AddsPrefix()
        {
            LogAssert.Expect(LogType.Log, new Regex("^\\[TagTraitSystem\\] Alpha$"));

            TagDiagnostics.Log("Alpha");
        }

        [Test]
        public void TagDiagnostics_LogWarning_AddsPrefix()
        {
            LogAssert.Expect(LogType.Warning, new Regex("^\\[TagTraitSystem\\] Alpha$"));

            TagDiagnostics.LogWarning("Alpha");
        }

        [Test]
        public void TagDiagnostics_LogError_AddsPrefix()
        {
            LogAssert.Expect(LogType.Error, new Regex("^\\[TagTraitSystem\\] Alpha$"));

            TagDiagnostics.LogError("Alpha");
        }

        [Test]
        public void TagDiagnostics_Log_ForwardsContext()
        {
            CapturingLogHandler logHandler = BeginCapture();
            GameObject context = CreateContext();

            TagDiagnostics.Log("Alpha", context);

            Assert.AreEqual(LogType.Log, logHandler.LastLogType);
            Assert.AreSame(context, logHandler.LastContext);
        }

        [Test]
        public void TagDiagnostics_LogWarning_ForwardsContext()
        {
            CapturingLogHandler logHandler = BeginCapture();
            GameObject context = CreateContext();

            TagDiagnostics.LogWarning("Alpha", context);

            Assert.AreEqual(LogType.Warning, logHandler.LastLogType);
            Assert.AreSame(context, logHandler.LastContext);
        }

        [Test]
        public void TagDiagnostics_LogError_ForwardsContext()
        {
            CapturingLogHandler logHandler = BeginCapture();
            GameObject context = CreateContext();

            TagDiagnostics.LogError("Alpha", context);

            Assert.AreEqual(LogType.Error, logHandler.LastLogType);
            Assert.AreSame(context, logHandler.LastContext);
        }

        [Test]
        public void TagDiagnostics_LogError_UsesErrorLogType()
        {
            LogAssert.Expect(LogType.Error, new Regex("Alpha"));

            TagDiagnostics.LogError("Alpha");
        }

        [Test]
        public void TagDiagnostics_LogWarning_UsesWarningLogTypeInEditor()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Alpha"));

            TagDiagnostics.LogWarning("Alpha");
        }

        [Test]
        public void TagDiagnostics_Log_UsesLogTypeInEditor()
        {
            LogAssert.Expect(LogType.Log, new Regex("Alpha"));

            TagDiagnostics.Log("Alpha");
        }

        private CapturingLogHandler BeginCapture()
        {
            originalLogHandler = Debug.unityLogger.logHandler;
            CapturingLogHandler logHandler = new CapturingLogHandler();
            Debug.unityLogger.logHandler = logHandler;
            return logHandler;
        }

        private GameObject CreateContext()
        {
            GameObject context = new GameObject("TagDiagnosticsContext");
            createdObjects.Add(context);
            return context;
        }

        private sealed class CapturingLogHandler : ILogHandler
        {
            public LogType LastLogType { get; private set; }
            public Object LastContext { get; private set; }

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                LastLogType = logType;
                LastContext = context;
            }

            public void LogException(Exception exception, Object context)
            {
                LastLogType = LogType.Exception;
                LastContext = context;
            }
        }
    }
}
