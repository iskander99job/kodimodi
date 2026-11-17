#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BackroomsCharacterLighting
{
    [MenuItem("Tools/Backrooms/Enable Probes On Selected Characters")]
    static void EnableProbes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        int count = 0;
        var visited = new System.Collections.Generic.HashSet<Renderer>();
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (!selected.scene.IsValid()) continue;
            foreach (Renderer renderer in selected.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if (!visited.Add(renderer)) continue;
                Undo.RecordObject(renderer, "Enable light probes");
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                if (renderer is MeshRenderer meshRenderer)
                    meshRenderer.receiveGI = ReceiveGI.LightProbes;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
                count++;
            }
        }
        Debug.Log("Light probes enabled on " + count +
                  " renderers. Materials and existing probe anchors are unchanged. " +
                  "For spawned prefabs, apply these renderer overrides to the prefab.");
    }
}
#endif