using UnityEngine;
using Unity.Netcode;

namespace Managers
{
    public abstract class SingletonNetwork<T> : NetworkBehaviour where T : NetworkBehaviour
    {
        public static T Instance { get; protected set; }

        protected virtual void Awake()
        {
            if (Instance == null)
            {
                Instance = this as T;
                DontDestroyOnLoad(gameObject); 
            }
            else Destroy(gameObject);
        }
    }
}