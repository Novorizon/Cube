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

        public virtual string RootName => typeof(T).Name;

        public static bool HasInstance => instance != null && !isQuitting;

        public static T Instance
        {
            get
            {
                if (isQuitting)
                {
                    return null;
                }

                if (instance != null)
                {
                    return instance;
                }

                instance = FindObjectOfType<T>(true);

                if (instance != null)
                {
                    DontDestroyOnLoad(instance.gameObject);
                    return instance;
                }

                GameObject go = new GameObject(typeof(T).Name);
                instance = go.AddComponent<T>();
                DontDestroyOnLoad(go);

                return instance;
            }
        }
        
        public void Initialize()
        {
            _ = Instance;
        }

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = this as T;
                DontDestroyOnLoad(gameObject);
                OnSingletonAwake();
                return;
            }

            if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        protected virtual void OnSingletonAwake()
        {
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            isQuitting = false;
        }
    }
}