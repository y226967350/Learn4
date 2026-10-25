using System.Collections.Generic;
using ObjViewer.Deformation;
using UnityEngine;
using UnityEngine.Rendering;

namespace ObjViewer.Demo
{
    /// <summary>
    /// FFD 控制网格的可视化：线框网格 + 可点击的小球控制点。
    /// 自身坐标位于被变形模型的局部空间，随模型一起变换。
    /// </summary>
    public class FfdLatticeView : MonoBehaviour
    {
        private static readonly Color PointColor = new Color(0.30f, 0.90f, 0.95f, 1f);
        private static readonly Color ModifiedPointColor = new Color(1.00f, 0.62f, 0.20f, 1f);
        private static readonly Color SelectedPointColor = new Color(1.00f, 0.88f, 0.28f, 1f);
        private static readonly Color GridColor = new Color(0.22f, 0.78f, 0.88f, 1f);
        private static readonly Color GhostGridColor = new Color(0.36f, 0.43f, 0.50f, 1f);

        public FfdLattice Lattice { get; private set; }
        public int SelectedIndex { get; private set; }

        private FfdHandle[] handles;
        private Renderer[] handleRenderers;
        private GameObject initialGridObject;
        private GameObject currentGridObject;
        private Mesh initialGridMesh;
        private Mesh currentGridMesh;
        private Material pointMaterial;
        private Material gridMaterial;
        private Material ghostGridMaterial;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 handleScale = Vector3.one * 0.05f;
        private bool initialGridVisible = true;

        public void Build(FfdLattice lattice, int selectedIndex)
        {
            Lattice = lattice;
            SelectedIndex = selectedIndex;

            CreateMaterials();
            propertyBlock = new MaterialPropertyBlock();

            initialGridMesh = BuildLineMesh("FFD Initial Grid", lattice.RestPoints);
            initialGridObject = CreateLineObject("初始控制网格", initialGridMesh, ghostGridMaterial);

            currentGridMesh = new Mesh { name = "FFD Current Grid" };
            currentGridMesh.MarkDynamic();
            UpdateLineMesh(currentGridMesh, lattice.ControlPoints);
            currentGridObject = CreateLineObject("当前控制网格", currentGridMesh, gridMaterial);

            BuildHandles();
            SetInitialGridVisible(initialGridVisible);
            Refresh();
        }

        public void SetSelected(int index)
        {
            if (Lattice == null)
            {
                return;
            }
            SelectedIndex = Mathf.Clamp(index, 0, Lattice.PointCount - 1);
            Refresh();
        }

        public void SetInitialGridVisible(bool visible)
        {
            initialGridVisible = visible;
            if (initialGridObject != null)
            {
                initialGridObject.SetActive(visible);
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public bool TryPick(Ray ray, out FfdHandle handle)
        {
            handle = null;
            if (!gameObject.activeInHierarchy)
            {
                return false;
            }

            if (Physics.Raycast(ray, out RaycastHit hit, 10000f, ~0, QueryTriggerInteraction.Ignore))
            {
                handle = hit.collider != null ? hit.collider.GetComponent<FfdHandle>() : null;
                return handle != null;
            }
            return false;
        }

        public Vector3 GetHandleWorldPosition(int index)
        {
            if (handles == null || index < 0 || index >= handles.Length || handles[index] == null)
            {
                return transform.position;
            }
            return handles[index].transform.position;
        }

        /// <summary>同步控制点位置、颜色和当前线框。修改 lattice.ControlPoints 后调用。</summary>
        public void Refresh()
        {
            if (Lattice == null)
            {
                return;
            }

            if (currentGridMesh != null)
            {
                UpdateLineMesh(currentGridMesh, Lattice.ControlPoints);
            }

            if (handles == null)
            {
                return;
            }

            for (int i = 0; i < handles.Length; i++)
            {
                FfdHandle handle = handles[i];
                if (handle == null)
                {
                    continue;
                }

                handle.transform.localPosition = Lattice.ControlPoints[i];
                float scale = i == SelectedIndex ? 1.45f : 1f;
                handle.transform.localScale = handleScale * scale;

                Renderer renderer = handleRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color color;
                if (i == SelectedIndex)
                {
                    color = SelectedPointColor;
                }
                else if (Lattice.IsPointModified(i))
                {
                    color = ModifiedPointColor;
                }
                else
                {
                    color = PointColor;
                }

                propertyBlock.Clear();
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void BuildHandles()
        {
            int count = Lattice.PointCount;
            handles = new FfdHandle[count];
            handleRenderers = new Renderer[count];

            Vector3 size = Lattice.Bounds.size;
            float diagonal = size.magnitude;
            float minSpacing = float.MaxValue;
            if (Lattice.Resolution > 1)
            {
                if (size.x > 1e-5f) minSpacing = Mathf.Min(minSpacing, size.x / (Lattice.Resolution - 1));
                if (size.y > 1e-5f) minSpacing = Mathf.Min(minSpacing, size.y / (Lattice.Resolution - 1));
                if (size.z > 1e-5f) minSpacing = Mathf.Min(minSpacing, size.z / (Lattice.Resolution - 1));
            }
            if (minSpacing == float.MaxValue)
            {
                minSpacing = Mathf.Max(diagonal, 1f);
            }

            float diameter = Mathf.Max(1e-6f, Mathf.Min(diagonal * 0.012f, minSpacing * 0.12f));
            handleScale = Vector3.one * diameter;

            for (int index = 0; index < count; index++)
            {
                Lattice.GetCoord(index, out int i, out int j, out int k);

                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"FFD Point ({i},{j},{k})";
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Lattice.ControlPoints[index];
                go.transform.localScale = handleScale;

                FfdHandle handle = go.AddComponent<FfdHandle>();
                handle.PointIndex = index;
                handle.I = i;
                handle.J = j;
                handle.K = k;

                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = pointMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                handles[index] = handle;
                handleRenderers[index] = renderer;
            }
        }

        private GameObject CreateLineObject(string objectName, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(transform, false);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private Mesh BuildLineMesh(string meshName, Vector3[] points)
        {
            Mesh mesh = new Mesh { name = meshName };
            UpdateLineMesh(mesh, points);
            return mesh;
        }

        private void UpdateLineMesh(Mesh mesh, Vector3[] points)
        {
            if (mesh == null || points == null || Lattice == null)
            {
                return;
            }

            List<Vector3> vertices = new List<Vector3>();
            int n = Lattice.Resolution;

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    for (int k = 0; k < n; k++)
                    {
                        int a = Lattice.Index(i, j, k);
                        if (i < n - 1) AddLine(vertices, points, a, Lattice.Index(i + 1, j, k));
                        if (j < n - 1) AddLine(vertices, points, a, Lattice.Index(i, j + 1, k));
                        if (k < n - 1) AddLine(vertices, points, a, Lattice.Index(i, j, k + 1));
                    }
                }
            }

            int[] indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0, false);
            mesh.RecalculateBounds();
        }

        private static void AddLine(List<Vector3> vertices, Vector3[] points, int a, int b)
        {
            vertices.Add(points[a]);
            vertices.Add(points[b]);
        }

        private void CreateMaterials()
        {
            if (pointMaterial == null)
            {
                pointMaterial = CreateUnlitMaterial("FFD Handle Material", Color.white);
                gridMaterial = CreateUnlitMaterial("FFD Grid Material", GridColor);
                ghostGridMaterial = CreateUnlitMaterial("FFD Ghost Grid Material", GhostGridColor);
            }
        }

        private static Material CreateUnlitMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave
            };

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        private void OnDestroy()
        {
            if (initialGridMesh != null) Destroy(initialGridMesh);
            if (currentGridMesh != null) Destroy(currentGridMesh);
            if (pointMaterial != null) Destroy(pointMaterial);
            if (gridMaterial != null) Destroy(gridMaterial);
            if (ghostGridMaterial != null) Destroy(ghostGridMaterial);
        }
    }
}
