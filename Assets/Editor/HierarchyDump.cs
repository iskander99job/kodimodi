#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

public static class HierarchyDump
{
    [MenuItem("Tools/Copy Selected Hierarchy")]
    private static void Dump()
    {
        var sb = new StringBuilder();
        foreach (var go in Selection.gameObjects)
            Walk(go.transform, 0, sb);

        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("Иерархия скопирована в буфер обмена");
    }

    private static void Walk(Transform t, int depth, StringBuilder sb)
    {
        string indent = new string(' ', depth * 2);
        sb.Append(indent).Append(t.name);
        if (!t.gameObject.activeSelf) sb.Append(" [off]");
        sb.AppendLine();

        foreach (var c in t.GetComponents<Component>())
        {
            // пропускаем шум: пустые ссылки и служебные компоненты
            if (c == null || c is Transform || c is CanvasRenderer) continue;

            sb.Append(indent).Append("  • ").AppendLine(c.GetType().Name);

            if (c is MonoBehaviour)
            {
                var so = new SerializedObject(c);
                var p = so.GetIterator();
                bool enter = true;
                while (p.NextVisible(enter))
                {
                    enter = false;
                    if (p.name == "m_Script") continue;

                    // выводим только заполненные ссылки
                    if (p.propertyType == SerializedPropertyType.ObjectReference
                        && p.objectReferenceValue != null)
                    {
                        sb.Append(indent).Append("      ")
                            .Append(p.displayName).Append(" → ")
                            .AppendLine(p.objectReferenceValue.name);
                    }
                }
            }
        }

        foreach (Transform child in t)
            Walk(child, depth + 1, sb);
    }
}
#endif