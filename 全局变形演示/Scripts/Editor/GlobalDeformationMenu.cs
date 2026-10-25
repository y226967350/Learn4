#if UNITY_EDITOR
using ObjViewer.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ObjViewer.EditorTools
{
    public static class GlobalDeformationMenu
    {
        [MenuItem("Tools/全局变形演示/在场景中创建演示对象")]
        private static void CreateDemoObject()
        {
            GlobalDeformationDemo existing = Object.FindObjectOfType<GlobalDeformationDemo>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            GameObject demoObject = new GameObject("全局变形演示");
            Undo.RegisterCreatedObjectUndo(demoObject, "Create Global Deformation Demo");
            demoObject.AddComponent<GlobalDeformationDemo>();
            Selection.activeGameObject = demoObject;
            EditorSceneManager.MarkSceneDirty(demoObject.scene);
            Debug.Log("[全局变形演示] 已创建演示对象。进入 Play 模式即可加载内置花瓶模型。");
        }

        [MenuItem("Tools/全局变形演示/打开 StreamingAssets 示例目录")]
        private static void RevealSamples()
        {
            string path = Application.streamingAssetsPath;
            if (System.IO.Directory.Exists(path))
            {
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                Debug.LogWarning("[全局变形演示] StreamingAssets 目录不存在。");
            }
        }
    }
}
#endif
