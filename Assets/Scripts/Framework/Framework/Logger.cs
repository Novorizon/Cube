using System;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Game.Framework
{
    public static class Logger
    {
        [Conditional("ENABLE_DEBUG_LOG")]
        public static void Info(string message)
        {
            Debug.Log(message);
        }

        [Conditional("ENABLE_DEBUG_LOG")]
        public static void Warn(string message)
        {
            Debug.LogWarning(message);
        }

        [Conditional("ENABLE_DEBUG_LOG")]
        public static void Error(string message)
        {
            Debug.LogError(message);
        }

        [Conditional("ENABLE_DEBUG_LOG")]
        public static void Exception(Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
