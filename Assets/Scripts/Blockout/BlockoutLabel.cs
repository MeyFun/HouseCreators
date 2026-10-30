using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HouseCreators.Blockout
{
    // Подпись для макета уровня: видна только в окне Scene (включённые Gizmos), в игре не рисуется.
    public class BlockoutLabel : MonoBehaviour
    {
        [TextArea] public string text;
        public Color color = Color.white;

#if UNITY_EDITOR
        static GUIStyle style;

        void OnDrawGizmos()
        {
            if (string.IsNullOrEmpty(text)) return;
            style ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, alignment = TextAnchor.LowerCenter };
            style.normal.textColor = color;
            Gizmos.color = color;
            Gizmos.DrawSphere(transform.position, 0.6f);
            Handles.Label(transform.position + Vector3.up * 1.5f, text, style);
        }
#endif
    }
}
