using System;
using System.Collections.Generic;
using System.IO;
using ObjViewer.ObjLoader;
using UnityEngine;

namespace ObjViewer.Viewer
{
    /// <summary>
    /// 模型浏览入口：加载 .obj 文件 -> 构建 Mesh -> 生成 GameObject，并暴露顶点访问接口。
    /// 用法：
    ///   1. 把本脚本挂到场景中的空物体上；
    ///   2. 在 Inspector 里填 <see cref="objPath"/>（相对 StreamingAssets 或绝对路径）；
    ///   3. 运行时调用 <see cref="LoadObj"/>，或勾选 <see cref="loadOnStart"/> 自动加载。
    /// 顶点访问：<see cref="ObjData"/> 保存原始数据，<see cref="Mesh"/> 保存构建后的网格。
    /// </summary>
    public class ModelViewer : MonoBehaviour
    {
        [Header("加载设置")]
        [Tooltip("obj 文件路径：相对 Assets/StreamingAssets 或绝对路径")]
        public string objPath = "model.obj";

        [Tooltip("是否在 Start 时自动加载")]
        public bool loadOnStart = true;

        [Tooltip("生成的模型挂到哪个父物体下；留空则新建一个根物体")]
        public Transform parent;

        [Header("材质")]
        [Tooltip("默认材质；留空则新建一个 Standard 材质")]
        public Material defaultMaterial;

        [Header("运行时状态（只读）")]
        public GameObject loadedObject;
        public Mesh loadedMesh;
        public ObjData objData;

        /// <summary>每次成功构建 Mesh 后触发，参数为加载器自身和来源名称。</summary>
        public event Action<ModelViewer, string> ModelLoaded;

        /// <summary>原始顶点位置（与 Mesh.vertices 对应）。这是未来功能扩展的核心访问点。</summary>
        public List<Vector3> VertexPositions => objData?.Positions;

        /// <summary>顶点总数。</summary>
        public int VertexCount => objData?.Positions.Count ?? 0;

        private void Start()
        {
            if (loadOnStart)
            {
                LoadObj();
            }
        }

        /// <summary>从 <see cref="objPath"/> 加载 obj 模型。</summary>
        public void LoadObj()
        {
            string text = ReadObjText(objPath);
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogError($"[ModelViewer] 无法读取 obj 文件：{objPath}");
                return;
            }

            LoadObjFromText(text, objPath);
        }

        /// <summary>从文本内容加载（供运行时动态加载或编辑器调用）。</summary>
        public void LoadObjFromText(string objText, string sourceName = "runtime")
        {
            Mesh previousMesh = loadedMesh;

            objData = ObjParser.Parse(objText);
            loadedMesh = MeshBuilder.Build(objData);

            ReplaceLoadedObject(sourceName);
            AssignMeshAndMaterial();

            if (previousMesh != null && previousMesh != loadedMesh)
            {
                Destroy(previousMesh);
            }

            Debug.Log($"[ModelViewer] 已加载 {sourceName}：顶点 {VertexCount}，面 {objData.Faces.Count}");
            ModelLoaded?.Invoke(this, sourceName);
        }

        private void ReplaceLoadedObject(string name)
        {
            if (loadedObject != null)
            {
                Destroy(loadedObject);
            }

            loadedObject = new GameObject(name);
            loadedObject.transform.SetParent(parent != null ? parent : transform, false);
        }

        private void AssignMeshAndMaterial()
        {
            MeshFilter mf = loadedObject.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = loadedObject.AddComponent<MeshFilter>();
            }
            mf.sharedMesh = loadedMesh;

            MeshRenderer mr = loadedObject.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                mr = loadedObject.AddComponent<MeshRenderer>();
            }

            Material mat = defaultMaterial != null
                ? defaultMaterial
                : CreateDefaultMaterial();

            Material[] materials = new Material[loadedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = mat;
            }
            mr.sharedMaterials = materials;
        }

        private Material CreateDefaultMaterial()
        {
            // 本项目使用 URP，必须优先使用 URP/Lit，否则会显示为粉色。
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            Material mat = new Material(shader);
            mat.name = "OBJ Default Material";
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", new Color(0.65f, 0.72f, 0.8f));
            }
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", new Color(0.65f, 0.72f, 0.8f));
            }
            return mat;
        }

        /// <summary>读取文件：优先绝对路径，其次相对 StreamingAssets。</summary>
        private static string ReadObjText(string path)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            string streamingPath = Path.Combine(Application.streamingAssetsPath, path);
            if (File.Exists(streamingPath))
            {
                return File.ReadAllText(streamingPath);
            }

            return null;
        }

        // ---------- 顶点访问 API（供未来功能扩展） ----------

        /// <summary>按 0 基下标读取顶点位置。</summary>
        public Vector3 GetVertex(int index)
        {
            if (objData == null || index < 0 || index >= objData.Positions.Count)
            {
                Debug.LogWarning($"[ModelViewer] 顶点下标越界：{index}");
                return Vector3.zero;
            }
            return objData.Positions[index];
        }

        /// <summary>把顶点坐标从模型局部空间转换到世界空间。</summary>
        public Vector3 VertexToWorld(int index)
        {
            return loadedObject.transform.TransformPoint(GetVertex(index));
        }

        /// <summary>在所有顶点处生成标记小球，用于可视化验证顶点访问（演示用）。</summary>
        public void SpawnVertexMarkers(float radius = 0.01f, int maxCount = 100000)
        {
            if (objData == null)
            {
                Debug.LogWarning("[ModelViewer] 尚未加载模型");
                return;
            }

            GameObject markersRoot = new GameObject("VertexMarkers");
            markersRoot.transform.SetParent(loadedObject.transform, false);

            int count = Mathf.Min(objData.Positions.Count, maxCount);
            for (int i = 0; i < count; i++)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = $"v{i}";
                marker.transform.SetParent(markersRoot.transform, false);
                marker.transform.localPosition = objData.Positions[i];
                marker.transform.localScale = Vector3.one * (radius * 2f);
                Destroy(marker.GetComponent<Collider>());
            }
            Debug.Log($"[ModelViewer] 已生成 {count} 个顶点标记");
        }
    }
}
