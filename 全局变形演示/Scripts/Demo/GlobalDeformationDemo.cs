using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ObjViewer.Deformation;
using ObjViewer.Viewer;
using UnityEngine;

namespace ObjViewer.Demo
{
    /// <summary>
    /// 全局变形演示的主控制器：
    /// 导入 OBJ -> 读取 Mesh 原始顶点 -> 选择锥化/扭曲/弯曲/FFD 实时变形。
    ///
    /// 所有变形每次都由原始顶点重新计算，不累积误差；点击“恢复原始模型”即可还原。
    /// </summary>
    [DisallowMultipleComponent]
    public partial class GlobalDeformationDemo : MonoBehaviour
    {
        public static GlobalDeformationDemo Instance { get; private set; }

        [Header("启动")]
        [SerializeField] private string defaultModelFile = "vase.obj";
        [SerializeField] private int initialFfdResolution = 3;
        [SerializeField] private bool autoLoadOnStart = true;

        [Header("外观")]
        [SerializeField] private Color modelColor = new Color(0.56f, 0.75f, 0.93f, 1f);
        [SerializeField] private Color backgroundColor = new Color(0.035f, 0.045f, 0.06f, 1f);

        // ---------- 场景与模型 ----------
        private Camera mainCamera;
        private CameraOrbit orbit;
        private ModelViewer viewer;
        private Material modelMaterial;
        private bool ownsModelMaterial;

        private Mesh workingMesh;
        private Vector3[] originalVertices;
        private Vector3[] originalNormals;
        private Vector3[] workingVertices;
        private Bounds originalBounds;
        private int vertexCount;
        private int triangleCount;
        private string modelPath = "";
        private string pathInput = "";
        private List<string> samplePaths = new List<string>();

        // ---------- 变形状态 ----------
        private DeformationMode mode = DeformationMode.None;
        private DeformationAxis axis = DeformationAxis.Y;
        private float taperAmount = 0.8f;
        private float twistAngle = 180f;
        private float bendAngle = 70f;

        // ---------- FFD ----------
        private FfdLattice lattice;
        private FfdLatticeView ffdView;
        private int ffdResolution = 3;
        private int selectedFfdPoint;
        private bool ffdInitialized;
        private bool showFfdGrid = true;
        private bool showInitialGrid = true;
        private bool draggingHandle;
        private Plane dragPlane;
        private Vector3 dragOffset;

        // ---------- 状态提示 ----------
        private string statusText = "";
        private bool statusIsError;
        private float statusExpireTime;

        private bool started;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[全局变形演示] 场景中已存在演示控制器，销毁重复实例。");
                Destroy(this);
                return;
            }

            Instance = this;
            ffdResolution = Mathf.Clamp(initialFfdResolution, FfdLattice.MinResolution, 6);
            SetupEnvironment();
        }

        private void Start()
        {
            started = true;
            RefreshSampleList();

            if (autoLoadOnStart && (viewer == null || viewer.loadedMesh == null))
            {
                LoadDefaultModel();
            }
            else if (viewer != null && viewer.loadedMesh != null)
            {
                HandleModelLoaded(viewer, viewer.objPath);
            }

            StartCoroutine(FrameCameraAfterStart());
        }

        private IEnumerator FrameCameraAfterStart()
        {
            // 等待 CameraOrbit.Start 初始化完毕，避免初始对焦被它覆盖。
            yield return null;
            yield return null;
            FrameCameraNow();
        }

        private void OnDestroy()
        {
            if (viewer != null)
            {
                viewer.ModelLoaded -= HandleModelLoaded;
            }

            ClearFfd();
            DisposeUiResources();

            if (ownsModelMaterial && modelMaterial != null)
            {
                Destroy(modelMaterial);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void SetupEnvironment()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
            }

            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = backgroundColor;

            orbit = mainCamera.GetComponent<CameraOrbit>();
            if (orbit == null)
            {
                orbit = mainCamera.gameObject.AddComponent<CameraOrbit>();
            }

            if (FindObjectOfType<Light>() == null)
            {
                GameObject lightObject = new GameObject("Directional Light");
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            }

            modelMaterial = CreateModelMaterial();
            ownsModelMaterial = true;

            viewer = FindObjectOfType<ModelViewer>();
            if (viewer == null)
            {
                GameObject viewerObject = new GameObject("OBJ 模型");
                viewerObject.transform.SetParent(transform, false);
                viewer = viewerObject.AddComponent<ModelViewer>();
            }

            viewer.loadOnStart = false;
            viewer.defaultMaterial = modelMaterial;
            viewer.ModelLoaded += HandleModelLoaded;
        }

        private Material CreateModelMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            Material material = new Material(shader)
            {
                name = "Global Deformation Model",
                hideFlags = HideFlags.DontSave
            };

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", modelColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", modelColor);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.05f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.55f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.55f);
            material.enableInstancing = true;
            return material;
        }

        private void RefreshSampleList()
        {
            samplePaths.Clear();
            try
            {
                if (Directory.Exists(Application.streamingAssetsPath))
                {
                    string[] files = Directory.GetFiles(Application.streamingAssetsPath, "*.obj");
                    for (int i = 0; i < files.Length; i++)
                    {
                        samplePaths.Add(files[i]);
                    }
                    samplePaths.Sort(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[全局变形演示] 扫描 StreamingAssets 失败：{e.Message}");
            }
        }

        private void LoadDefaultModel()
        {
            string streamingRoot = Application.streamingAssetsPath;
            string defaultPath = Path.Combine(streamingRoot, defaultModelFile);

            if (!File.Exists(defaultPath) && samplePaths.Count > 0)
            {
                // 优先选择花瓶模型，其次使用任意内置 OBJ。
                string vase = null;
                for (int i = 0; i < samplePaths.Count; i++)
                {
                    if (string.Equals(Path.GetFileName(samplePaths[i]), "vase.obj", StringComparison.OrdinalIgnoreCase))
                    {
                        vase = samplePaths[i];
                        break;
                    }
                }
                defaultPath = vase ?? samplePaths[0];
            }

            if (File.Exists(defaultPath))
            {
                LoadModelFromPath(defaultPath);
            }
            else
            {
                SetStatus("未找到内置 OBJ，请点击“导入 OBJ…”选择模型。", true);
            }
        }

        /// <summary>从绝对路径、StreamingAssets 相对路径或当前目录相对路径加载 OBJ。</summary>
        public void LoadModelFromPath(string path)
        {
            string resolved = ResolveModelPath(path);
            if (resolved == null)
            {
                SetStatus($"找不到 OBJ 文件：{path}", true);
                return;
            }

            try
            {
                string text = File.ReadAllText(resolved);
                if (string.IsNullOrWhiteSpace(text))
                {
                    SetStatus($"OBJ 文件为空：{Path.GetFileName(resolved)}", true);
                    return;
                }

                viewer.objPath = resolved;
                viewer.defaultMaterial = modelMaterial;
                viewer.LoadObjFromText(text, Path.GetFileName(resolved));
                pathInput = resolved;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus($"加载失败：{e.Message}", true);
            }
        }

        private static string ResolveModelPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            path = path.Trim().Trim('"');
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }

            string inStreaming = Path.Combine(Application.streamingAssetsPath, path);
            if (File.Exists(inStreaming))
            {
                return Path.GetFullPath(inStreaming);
            }

            return null;
        }

        /// <summary>ModelViewer 加载完成后由事件回调，建立新的原始顶点缓存。</summary>
        private void HandleModelLoaded(ModelViewer source, string sourceName)
        {
            ClearFfd();
            workingMesh = source != null ? source.loadedMesh : null;

            if (workingMesh == null || workingMesh.vertexCount == 0)
            {
                originalVertices = null;
                workingVertices = null;
                vertexCount = 0;
                triangleCount = 0;
                SetStatus("OBJ 中没有可用顶点。", true);
                return;
            }

            vertexCount = workingMesh.vertexCount;
            originalVertices = workingMesh.vertices;
            originalNormals = workingMesh.normals;
            workingVertices = new Vector3[originalVertices.Length];
            originalBounds = MeshDeformer.CalculateBounds(originalVertices);

            triangleCount = 0;
            for (int i = 0; i < workingMesh.subMeshCount; i++)
            {
                triangleCount += (int)(workingMesh.GetIndexCount(i) / 3);
            }

            if (source != null && !string.IsNullOrEmpty(source.objPath) && File.Exists(source.objPath))
            {
                modelPath = source.objPath;
                pathInput = source.objPath;
            }
            else
            {
                modelPath = sourceName ?? "";
                pathInput = modelPath;
            }

            mode = DeformationMode.None;
            draggingHandle = false;
            ApplyDeformation();
            FrameCameraNow();

            string boundsText = $"{originalBounds.size.x:0.##} × {originalBounds.size.y:0.##} × {originalBounds.size.z:0.##}";
            SetStatus($"已加载 {Path.GetFileName(modelPath)}：{vertexCount:N0} 顶点，包围盒 {boundsText}", false);
            Debug.Log($"[全局变形演示] 已加载 {modelPath}，顶点 {vertexCount}，三角面 {triangleCount}");
        }

        private void FrameCameraNow()
        {
            if (orbit != null && viewer != null && viewer.loadedObject != null)
            {
                orbit.FrameOn(viewer.loadedObject.transform);
            }
        }

        // ====================================================================
        // 变形调度
        // ====================================================================

        /// <summary>用当前模式和参数，从 originalVertices 重新计算 Mesh。</summary>
        private void ApplyDeformation()
        {
            if (workingMesh == null || originalVertices == null || workingVertices == null)
            {
                return;
            }

            if (mode == DeformationMode.None)
            {
                workingMesh.SetVertices(originalVertices);
                if (originalNormals != null && originalNormals.Length == originalVertices.Length)
                {
                    workingMesh.SetNormals(originalNormals);
                }
                else
                {
                    workingMesh.RecalculateNormals();
                }
                workingMesh.RecalculateBounds();
                return;
            }

            switch (mode)
            {
                case DeformationMode.Taper:
                    MeshDeformer.ApplyTaper(originalVertices, workingVertices, originalBounds, axis, taperAmount);
                    break;
                case DeformationMode.Twist:
                    MeshDeformer.ApplyTwist(originalVertices, workingVertices, originalBounds, axis, twistAngle);
                    break;
                case DeformationMode.Bend:
                    MeshDeformer.ApplyBend(originalVertices, workingVertices, originalBounds, axis, bendAngle);
                    break;
                case DeformationMode.Ffd:
                    if (lattice == null)
                    {
                        EnsureFfdInitialized();
                    }
                    MeshDeformer.ApplyFfd(originalVertices, workingVertices, lattice);
                    break;
                default:
                    Array.Copy(originalVertices, workingVertices, originalVertices.Length);
                    break;
            }

            workingMesh.SetVertices(workingVertices);
            workingMesh.RecalculateNormals();
            workingMesh.RecalculateBounds();
        }

        private void SetMode(DeformationMode newMode)
        {
            if (mode == newMode)
            {
                return;
            }

            mode = newMode;
            draggingHandle = false;

            if (mode == DeformationMode.Ffd)
            {
                EnsureFfdInitialized();
            }

            UpdateFfdVisibility();
            ApplyDeformation();

            if (mode == DeformationMode.Ffd && lattice != null)
            {
                SetStatus($"FFD：{ffdResolution}×{ffdResolution}×{ffdResolution} = {lattice.PointCount} 个控制点", false);
            }
        }

        private void SetAxis(DeformationAxis newAxis)
        {
            if (axis == newAxis)
            {
                return;
            }
            axis = newAxis;
            if (mode == DeformationMode.Taper || mode == DeformationMode.Twist || mode == DeformationMode.Bend)
            {
                ApplyDeformation();
            }
        }

        private void ResetToOriginal()
        {
            mode = DeformationMode.None;
            draggingHandle = false;
            if (lattice != null)
            {
                lattice.ResetAll();
            }
            if (ffdView != null)
            {
                ffdView.Refresh();
            }
            UpdateFfdVisibility();
            ApplyDeformation();
            SetStatus("已恢复原始模型。", false);
        }

        // ====================================================================
        // FFD
        // ====================================================================

        private void EnsureFfdInitialized()
        {
            if (!ffdInitialized || lattice == null)
            {
                InitializeFfd(ffdResolution);
            }
        }

        /// <summary>
        /// 初始状态：根据原始模型包围盒，生成 n×n×n 控制点。
        /// 此操作会重置已有的控制点修改。
        /// </summary>
        private void InitializeFfd(int resolution)
        {
            if (originalVertices == null || originalVertices.Length == 0)
            {
                SetStatus("请先加载模型，再初始化 FFD 网格。", true);
                return;
            }

            ffdResolution = Mathf.Clamp(resolution, FfdLattice.MinResolution, Mathf.Min(FfdLattice.MaxResolution, 6));

            if (ffdView != null)
            {
                Destroy(ffdView.gameObject);
                ffdView = null;
            }

            lattice = new FfdLattice();
            lattice.Initialize(originalBounds, ffdResolution);
            ffdInitialized = true;
            selectedFfdPoint = lattice.Index(ffdResolution / 2, ffdResolution - 1, ffdResolution / 2);
            draggingHandle = false;

            if (viewer != null && viewer.loadedObject != null)
            {
                GameObject viewObject = new GameObject("FFD 控制网格");
                viewObject.transform.SetParent(viewer.loadedObject.transform, false);
                ffdView = viewObject.AddComponent<FfdLatticeView>();
                ffdView.Build(lattice, selectedFfdPoint);
            }

            UpdateFfdVisibility();

            if (mode == DeformationMode.Ffd)
            {
                ApplyDeformation();
            }

            SetStatus($"FFD 初始网格已生成：{ffdResolution}×{ffdResolution}×{ffdResolution} 个控制点。", false);
        }

        private void UpdateFfdVisibility()
        {
            bool visible = mode == DeformationMode.Ffd && showFfdGrid && ffdView != null;
            if (ffdView != null)
            {
                ffdView.SetVisible(visible);
                if (visible)
                {
                    ffdView.SetInitialGridVisible(showInitialGrid);
                }
            }
        }

        private void ClearFfd()
        {
            if (ffdView != null)
            {
                Destroy(ffdView.gameObject);
                ffdView = null;
            }

            lattice = null;
            ffdInitialized = false;
            selectedFfdPoint = 0;
            draggingHandle = false;
            ffdResolution = Mathf.Clamp(initialFfdResolution, FfdLattice.MinResolution, 6);
        }

        private void SetSelectedFfdPoint(int index)
        {
            if (lattice == null)
            {
                return;
            }
            selectedFfdPoint = Mathf.Clamp(index, 0, lattice.PointCount - 1);
            if (ffdView != null)
            {
                ffdView.SetSelected(selectedFfdPoint);
            }
        }

        private void CycleSelectedFfdPoint(int delta)
        {
            if (lattice == null)
            {
                return;
            }
            int count = lattice.PointCount;
            SetSelectedFfdPoint((selectedFfdPoint + delta + count) % count);
        }

        private void SetSelectedFfdCoordinate(int component, float value)
        {
            if (lattice == null || selectedFfdPoint < 0 || selectedFfdPoint >= lattice.ControlPoints.Length)
            {
                return;
            }

            Vector3 p = lattice.ControlPoints[selectedFfdPoint];
            p[component] = value;
            lattice.ControlPoints[selectedFfdPoint] = p;

            ApplyDeformation();
            if (ffdView != null)
            {
                ffdView.Refresh();
            }
        }

        private void ResetSelectedFfdPoint()
        {
            if (lattice == null)
            {
                return;
            }
            lattice.ResetPoint(selectedFfdPoint);
            ApplyDeformation();
            if (ffdView != null)
            {
                ffdView.Refresh();
            }
        }

        private void ResetAllFfdPoints()
        {
            if (lattice == null)
            {
                return;
            }
            lattice.ResetAll();
            ApplyDeformation();
            if (ffdView != null)
            {
                ffdView.Refresh();
            }
            SetStatus("所有 FFD 控制点已复位。", false);
        }

        // ====================================================================
        // 交互
        // ====================================================================

        private void Update()
        {
            if (!started)
            {
                return;
            }
            UpdateFfdHandleInput();
        }

        private void UpdateFfdHandleInput()
        {
            if (mode != DeformationMode.Ffd || !showFfdGrid || ffdView == null || lattice == null || mainCamera == null)
            {
                draggingHandle = false;
                return;
            }

            if (Input.GetMouseButtonDown(0) && !IsPointerOverPanel())
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (ffdView.TryPick(ray, out FfdHandle handle))
                {
                    SetSelectedFfdPoint(handle.PointIndex);
                    draggingHandle = true;

                    // 在过控制点、朝向相机的平面上拖动，保证拖拽起点与小球中心对齐。
                    Vector3 worldPoint = ffdView.GetHandleWorldPosition(handle.PointIndex);
                    dragPlane = new Plane(mainCamera.transform.forward, worldPoint);
                    dragOffset = dragPlane.Raycast(ray, out float enter)
                        ? worldPoint - ray.GetPoint(enter)
                        : Vector3.zero;
                }
            }

            if (!draggingHandle)
            {
                return;
            }

            if (!Input.GetMouseButton(0))
            {
                draggingHandle = false;
                return;
            }

            Ray dragRay = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!dragPlane.Raycast(dragRay, out float dragDistance))
            {
                return;
            }

            Vector3 targetWorld = dragRay.GetPoint(dragDistance) + dragOffset;
            Vector3 localPoint = viewer.loadedObject.transform.InverseTransformPoint(targetWorld);
            lattice.ControlPoints[selectedFfdPoint] = localPoint;

            ApplyDeformation();
            ffdView.Refresh();
        }

        private bool IsPointerOverPanel()
        {
            if (!showPanel)
            {
                return false;
            }

            Vector3 mouse = Input.mousePosition;
            Vector2 guiPoint = new Vector2(mouse.x, Screen.height - mouse.y);
            return panelRect.Contains(guiPoint);
        }

        private bool IsPointerOverFfdHandle()
        {
            if (mode != DeformationMode.Ffd || !showFfdGrid || ffdView == null || mainCamera == null)
            {
                return false;
            }
            if (IsPointerOverPanel())
            {
                return false;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            return ffdView.TryPick(ray, out FfdHandle _);
        }

        /// <summary>CameraOrbit 在 Update 中查询此属性，避免操作 UI / 控制点时同时旋转相机。</summary>
        public static bool ShouldBlockCamera
        {
            get
            {
                GlobalDeformationDemo demo = Instance;
                if (demo == null || !demo.started)
                {
                    return false;
                }
                return demo.IsPointerOverPanel() || demo.draggingHandle || demo.IsPointerOverFfdHandle();
            }
        }

        private void SetStatus(string text, bool isError)
        {
            statusText = text;
            statusIsError = isError;
            statusExpireTime = Time.unscaledTime + 6f;
            if (isError)
            {
                Debug.LogWarning($"[全局变形演示] {text}");
            }
        }

        private string CurrentModeName()
        {
            switch (mode)
            {
                case DeformationMode.Taper: return "锥化";
                case DeformationMode.Twist: return "扭曲";
                case DeformationMode.Bend: return "弯曲";
                case DeformationMode.Ffd: return "FFD";
                default: return "原始";
            }
        }
    }
}
