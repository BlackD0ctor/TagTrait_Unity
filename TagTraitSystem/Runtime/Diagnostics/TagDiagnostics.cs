using System;
using UnityEngine;

namespace TagTraitSystem.Runtime.Diagnostics
{
    /// <summary>
    /// Provides centralized runtime logging for TagTraitSystem.
    /// </summary>
    public static class TagDiagnostics
    {
        private const string Prefix = "[TagTraitSystem] ";

        /// <summary>
        /// Writes an informational diagnostic message in Editor or development builds.
        /// </summary>
        /// <param name="message">The message body.</param>
        /// <param name="context">The optional Unity object context.</param>
        public static void Log(string message, UnityEngine.Object context = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(FormatMessage(message), context);
#endif
        }

        /// <summary>
        /// Writes a warning diagnostic message in Editor or development builds.
        /// </summary>
        /// <param name="message">The message body.</param>
        /// <param name="context">The optional Unity object context.</param>
        public static void LogWarning(string message, UnityEngine.Object context = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(FormatMessage(message), context);
#endif
        }

        /// <summary>
        /// Writes an error diagnostic message.
        /// </summary>
        /// <param name="message">The message body.</param>
        /// <param name="context">The optional Unity object context.</param>
        public static void LogError(string message, UnityEngine.Object context = null)
        {
            Debug.LogError(FormatMessage(message), context);
        }

        private static string FormatMessage(string message)
        {
            if (message != null && message.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return message;
            }

            return Prefix + message;
        }
    }
}
