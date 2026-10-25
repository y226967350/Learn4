using UnityEngine;

namespace ObjViewer.Demo
{
    /// <summary>
    /// 场景里没有手动放置演示控制器时，进入 Play 模式自动创建一份，
    /// 这样打开当前示例场景后直接点击 Play 就能看到完整演示。
    /// </summary>
    internal static class GlobalDeformationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDemoExists()
        {
            if (Object.FindObjectOfType<GlobalDeformationDemo>() != null)
            {
                return;
            }

            GameObject demoObject = new GameObject("全局变形演示");
            demoObject.AddComponent<GlobalDeformationDemo>();
        }
    }
}
