#if UNITY_EDITOR
using System;
using System.IO;
using ObjViewer.Deformation;
using ObjViewer.ObjLoader;
using UnityEditor;
using UnityEngine;

namespace ObjViewer.EditorTools
{
    /// <summary>
    /// 不依赖测试框架的算法自检：
    /// 直接读取 vase.obj，验证锥化、扭曲、弯曲和 FFD 的关键不变量。
    /// 可通过菜单运行，也可用 -executeMethod ...RunFromCommandLine 在批处理模式下运行。
    /// </summary>
    public static class DeformationSelfTest
    {
        private const float Epsilon = 1e-4f;

        [MenuItem("Tools/全局变形演示/运行变形算法自检")]
        public static void RunFromMenu()
        {
            bool success = Run(false);
            EditorUtility.DisplayDialog("变形算法自检",
                success ? "全部通过。详情见 Console。" : "存在失败项，详情见 Console。", "确定");
        }

        public static void RunFromCommandLine()
        {
            bool success = false;
            try
            {
                success = Run(true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(success ? 0 : 1);
            }
        }

        private static bool Run(bool verbose)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "vase.obj");
            if (!File.Exists(path))
            {
                Debug.LogError($"[自检] 找不到 {path}");
                return false;
            }

            ObjData data = ObjParser.Parse(File.ReadAllText(path));
            Mesh mesh = MeshBuilder.Build(data);
            bool ok = true;

            ok &= Check("OBJ 解析出顶点", data.Positions.Count > 1000);
            ok &= Check("Mesh 顶点数", mesh.vertexCount > 1000);
            ok &= Check("Mesh 三角面数", mesh.triangles.Length / 3 > 5000);

            Vector3[] source = mesh.vertices;
            Bounds bounds = MeshDeformer.CalculateBounds(source);
            Vector3[] destination = new Vector3[source.Length];

            TestTaper(source, destination, bounds, ref ok);
            TestTwist(source, destination, bounds, ref ok);
            TestBend(source, destination, bounds, ref ok);
            TestFfd(source, ref ok);
            TestFfdModification(bounds, ref ok);

            Debug.Log(ok ? "[自检] 全部通过 ✓" : "[自检] 存在失败项 ✗");

            UnityEngine.Object.DestroyImmediate(mesh);
            return ok;
        }

        private static void TestTaper(Vector3[] source, Vector3[] destination, Bounds bounds, ref bool ok)
        {
            MeshDeformer.ApplyTaper(source, destination, bounds, DeformationAxis.Y, 0.5f);
            float bottomTolerance = 0f;
            float topPerpError = 0f;

            int bottomCount = 0;
            int topCount = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (Mathf.Abs(source[i].y - bounds.min.y) < 1e-4f)
                {
                    bottomCount++;
                    bottomTolerance = Mathf.Max(bottomTolerance, (destination[i] - source[i]).magnitude);
                }
                if (Mathf.Abs(source[i].y - bounds.max.y) < 1e-4f)
                {
                    topCount++;
                    Vector2 before = new Vector2(source[i].x - bounds.center.x, source[i].z - bounds.center.z);
                    Vector2 after = new Vector2(destination[i].x - bounds.center.x, destination[i].z - bounds.center.z);
                    topPerpError = Mathf.Max(topPerpError, Mathf.Abs(after.magnitude - before.magnitude * 1.5f));
                }
            }

            ok &= Check("锥化：底截面保持不动", bottomCount > 0 && bottomTolerance < Epsilon,
                $"误差 {bottomTolerance:0.000000}");
            ok &= Check("锥化：顶截面放大 1.5 倍", topCount > 0 && topPerpError < Epsilon,
                $"误差 {topPerpError:0.000000}");
        }

        private static void TestTwist(Vector3[] source, Vector3[] destination, Bounds bounds, ref bool ok)
        {
            MeshDeformer.ApplyTwist(source, destination, bounds, DeformationAxis.Y, 180f);

            float lengthError = 0f;
            bool finite = true;
            for (int i = 0; i < source.Length; i++)
            {
                if (!IsFinite(destination[i])) finite = false;
                Vector3 a = source[i] - bounds.center;
                Vector3 b = destination[i] - bounds.center;
                lengthError = Mathf.Max(lengthError, Mathf.Abs(a.magnitude - b.magnitude));
            }

            ok &= Check("扭曲：所有顶点为有限值", finite);
            ok &= Check("扭曲：到中轴的距离保持不变", lengthError < Epsilon, $"误差 {lengthError:0.000000}");

            Vector3 middle = bounds.center;
            Vector3[] one = { middle };
            Vector3[] result = new Vector3[1];
            MeshDeformer.ApplyTwist(one, result, bounds, DeformationAxis.Y, 90f);
            ok &= Check("扭曲：高度中点保持不动", (result[0] - middle).magnitude < Epsilon);
        }

        private static void TestBend(Vector3[] source, Vector3[] destination, Bounds bounds, ref bool ok)
        {
            MeshDeformer.ApplyBend(source, destination, bounds, DeformationAxis.Y, 90f);

            bool finite = true;
            for (int i = 0; i < destination.Length; i++)
            {
                if (!IsFinite(destination[i])) finite = false;
            }

            Vector3[] one = { bounds.center };
            Vector3[] result = new Vector3[1];
            MeshDeformer.ApplyBend(one, result, bounds, DeformationAxis.Y, 90f);

            ok &= Check("弯曲：所有顶点为有限值", finite);
            ok &= Check("弯曲：模型中部为支点", (result[0] - bounds.center).magnitude < Epsilon,
                $"偏移 {(result[0] - bounds.center).magnitude:0.000000}");
        }

        private static void TestFfd(Vector3[] source, ref bool ok)
        {
            FfdLattice lattice = new FfdLattice();
            Bounds bounds = MeshDeformer.CalculateBounds(source);
            lattice.Initialize(bounds, 3);

            float maxError = 0f;
            for (int i = 0; i < source.Length; i++)
            {
                Vector3 mapped = lattice.Sample(source[i]);
                maxError = Mathf.Max(maxError, (mapped - source[i]).magnitude);
            }

            ok &= Check("FFD：未修改控制点时保持原模型", maxError < Epsilon, $"最大误差 {maxError:0.000000}");

            int topCenter = lattice.Index(1, 2, 1);
            Vector3 original = lattice.RestPoints[topCenter];
            Vector3 moved = original + new Vector3(0f, 0.4f, 0f);
            lattice.ControlPoints[topCenter] = moved;
            Vector3 sampled = lattice.Sample(original);

            ok &= Check("FFD：修改网格点后模型跟随", (sampled - moved).magnitude < Epsilon,
                $"偏差 {(sampled - moved).magnitude:0.000000}");
            ok &= Check("FFD：控制点数量为 n³", lattice.PointCount == 27, $"实际 {lattice.PointCount}");
        }

        private static void TestFfdModification(Bounds bounds, ref bool ok)
        {
            // 用合成点验证任意包围盒尺寸下的三线性映射。
            FfdLattice lattice = new FfdLattice();
            lattice.Initialize(bounds, 4);
            lattice.ControlPoints[lattice.Index(0, 1, 2)] += new Vector3(0.2f, -0.1f, 0.3f);

            Vector3 probe = new Vector3(
                Mathf.Lerp(bounds.min.x, bounds.max.x, 0.25f),
                Mathf.Lerp(bounds.min.y, bounds.max.y, 0.4f),
                Mathf.Lerp(bounds.min.z, bounds.max.z, 0.62f));
            Vector3 result = lattice.Sample(probe);

            ok &= Check("FFD：内部采样为有限值", IsFinite(result));
            ok &= Check("FFD：控制点位移会影响内部", (result - probe).sqrMagnitude > 1e-8f,
                $"位移 {(result - probe).magnitude:0.000000}");
        }

        private static bool Check(string name, bool condition, string detail = null)
        {
            if (condition)
            {
                Debug.Log($"[自检] ✓ {name}" + (string.IsNullOrEmpty(detail) ? "" : $"（{detail}）"));
            }
            else
            {
                Debug.LogError($"[自检] ✗ {name}" + (string.IsNullOrEmpty(detail) ? "" : $"（{detail}）"));
            }
            return condition;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
#endif
