using UnityEngine;

namespace LowoUN.Util {
    public abstract class SingletonSimple<T> where T : new () {
        private static T self;
        public static T Self {
            get {
                if (self == null)
                    self = new T ();
                return self;
            }
        }
    }
    public abstract class SingletonMono<T> : MonoBehaviour where T : SingletonMono<T> {
        public static GameObject container;
        protected static T instance = null;
        public static T Self {
            get {
                if (container == null) {
                    container = new GameObject ();
                    //SingletonNode.SetParent(container.transform);
                }
                if (instance == null) {
                    string name = typeof (T).Name;
                    if (name != null) {
                        instance = container.GetComponent (typeof (T)) as T;
                        if (instance == null) {
                            instance = container.AddComponent (typeof (T)) as T;
                            instance.name = name;
                        } else {
                            Debug.Log ("Singleton Type already Existed! (" + name + ")");
                        }
                    }
                }
                return instance;
            }
        }
        public virtual void Awake () {
            DontDestroyOnLoad (gameObject);

            if (container == null) {
                container = this.gameObject;
            }
        }
        public virtual void OnDestroy () {
            container = null;
            instance = null;
        }
    }
}