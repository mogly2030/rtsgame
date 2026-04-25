using UnityEditor;
using UnityEngine;

namespace RTSEngine.EditorOnly
{
    public abstract class EditorBase<T> : Editor where T : MonoBehaviour
    {
        protected SerializedObject SO { private set; get; }
        protected T comp { private set; get; }

        public void OnEnable()
        {
            comp = (T)target;
            SO = new SerializedObject(comp);
        }
    }
}
