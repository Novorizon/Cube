///------------------------------------
/// Author：Novorizon
/// Mail：novorizon@hotmail.com
/// Date：2022-10-11
/// Description：单例Mono
///------------------------------------
using UnityEngine;

namespace Game.Framework
{
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T instance;
        private static bool isQuitting;
        private static readonly object syncRoot = new object();
        public virtual string RootName => typeof(T).Name;

        public static T Instance
        {
            get
            {
                if (isQuitting)
                    return null;

                lock (syncRoot)
                {
                    if (instance != null)
                        return instance;

                    instance = FindObjectOfType<T>(true);
                    if (instance != null)
                    {
                        DontDestroyOnLoad(instance.gameObject);
                        return instance;
                    }

                    GameObject go = new GameObject(typeof(T).Name);
                    if (go != null)
                    {
                        instance = go.AddComponent<T>();
                    }
                    if (instance != null)
                    {
                        DontDestroyOnLoad(instance.gameObject);
                    }
                    return instance;
                }
            }
        }

        protected virtual void OnApplicationQuit()
        {
            isQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        //万一禁用 Domain Reload，重置
        //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        //static void ResetStatics()
        //{
        //    instance = null;
        //    isQuitting = false;
        //}
    }
}
