using System;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Game.Framework
{
    public interface ILogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Exception(Exception exception);
    }

    public  class NetLogger : ILogger
    {
        //[Conditional("ENABLE_DEBUG_LOG")]
        public void Info(string message)
        {
#if DEBUG
            Debug.Log(message);
#endif
        }

        //[Conditional("ENABLE_DEBUG_LOG")]
        public void Warn(string message)
        {
#if DEBUG
            Debug.LogWarning(message);
#endif
        }

        //[Conditional("ENABLE_DEBUG_LOG")]
        public void Error(string message)
        {
#if DEBUG
            Debug.LogError(message);
#endif
        }

        //[Conditional("ENABLE_DEBUG_LOG")]
        public void Exception(Exception exception)
        {
#if DEBUG
            Debug.LogException(exception);
#endif
        }
    }
}
