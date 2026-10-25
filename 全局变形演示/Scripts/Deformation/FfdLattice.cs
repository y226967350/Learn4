using UnityEngine;

namespace ObjViewer.Deformation
{
    /// <summary>
    /// n x n x n 控制点自由变形网格。
    ///
    /// 初始状态：根据模型的局部空间包围盒（AABB）均分生成 n^3 个控制点。
    /// 终止状态：直接修改 <see cref="ControlPoints"/> 中的坐标，再用三线性插值
    /// 把模型顶点映射到变形后的控制网格中。
    ///
    /// 控制点按 x 最慢、z 最快的顺序展平：
    /// index = (i * n + j) * n + k，其中 i/j/k 分别对应 X/Y/Z 方向的格点序号。
    /// </summary>
    public class FfdLattice
    {
        public const int MinResolution = 2;
        public const int MaxResolution = 8;

        public int Resolution { get; private set; }

        /// <summary>用于归一化顶点的模型局部 AABB。初始化后保持不变。</summary>
        public Bounds Bounds { get; private set; }

        /// <summary>初始控制点坐标（由包围盒均分得到）。</summary>
        public Vector3[] RestPoints { get; private set; }

        /// <summary>当前控制点坐标。修改它即可驱动模型变形。</summary>
        public Vector3[] ControlPoints { get; private set; }

        public int PointCount => Resolution * Resolution * Resolution;

        public bool IsInitialized => ControlPoints != null;

        public void Initialize(Bounds bounds, int resolution)
        {
            Resolution = Mathf.Clamp(resolution, MinResolution, MaxResolution);

            // 避免扁平模型（某一维尺寸为 0）导致除零；控制点仍然严格由包围盒生成。
            Vector3 size = bounds.size;
            const float minSize = 1e-5f;
            size.x = Mathf.Max(size.x, minSize);
            size.y = Mathf.Max(size.y, minSize);
            size.z = Mathf.Max(size.z, minSize);
            bounds.size = size;
            Bounds = bounds;

            int count = PointCount;
            RestPoints = new Vector3[count];
            ControlPoints = new Vector3[count];

            float inv = 1f / (Resolution - 1);
            for (int i = 0; i < Resolution; i++)
            {
                for (int j = 0; j < Resolution; j++)
                {
                    for (int k = 0; k < Resolution; k++)
                    {
                        Vector3 p = new Vector3(
                            Mathf.LerpUnclamped(bounds.min.x, bounds.max.x, i * inv),
                            Mathf.LerpUnclamped(bounds.min.y, bounds.max.y, j * inv),
                            Mathf.LerpUnclamped(bounds.min.z, bounds.max.z, k * inv));

                        int index = Index(i, j, k);
                        RestPoints[index] = p;
                        ControlPoints[index] = p;
                    }
                }
            }
        }

        public int Index(int i, int j, int k)
        {
            return (i * Resolution + j) * Resolution + k;
        }

        public void GetCoord(int index, out int i, out int j, out int k)
        {
            int n = Resolution;
            i = index / (n * n);
            int rem = index - i * n * n;
            j = rem / n;
            k = rem - j * n;
        }

        /// <summary>三线性插值：返回模型局部坐标 position 在控制网格中的新位置。</summary>
        public Vector3 Sample(Vector3 position)
        {
            int n = Resolution;
            if (ControlPoints == null || n < 2)
            {
                return position;
            }

            Vector3 bmin = Bounds.min;
            Vector3 bsize = Bounds.size;

            float gx = ToGridCoordinate(position.x, bmin.x, bsize.x, n);
            float gy = ToGridCoordinate(position.y, bmin.y, bsize.y, n);
            float gz = ToGridCoordinate(position.z, bmin.z, bsize.z, n);

            int x0 = Mathf.Min((int)gx, n - 2);
            int y0 = Mathf.Min((int)gy, n - 2);
            int z0 = Mathf.Min((int)gz, n - 2);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            int z1 = z0 + 1;

            float tx = gx - x0;
            float ty = gy - y0;
            float tz = gz - z0;

            Vector3 p000 = ControlPoints[Index(x0, y0, z0)];
            Vector3 p100 = ControlPoints[Index(x1, y0, z0)];
            Vector3 p010 = ControlPoints[Index(x0, y1, z0)];
            Vector3 p110 = ControlPoints[Index(x1, y1, z0)];
            Vector3 p001 = ControlPoints[Index(x0, y0, z1)];
            Vector3 p101 = ControlPoints[Index(x1, y0, z1)];
            Vector3 p011 = ControlPoints[Index(x0, y1, z1)];
            Vector3 p111 = ControlPoints[Index(x1, y1, z1)];

            Vector3 p00 = Vector3.LerpUnclamped(p000, p100, tx);
            Vector3 p10 = Vector3.LerpUnclamped(p010, p110, tx);
            Vector3 p01 = Vector3.LerpUnclamped(p001, p101, tx);
            Vector3 p11 = Vector3.LerpUnclamped(p011, p111, tx);
            Vector3 p0 = Vector3.LerpUnclamped(p00, p10, ty);
            Vector3 p1 = Vector3.LerpUnclamped(p01, p11, ty);
            return Vector3.LerpUnclamped(p0, p1, tz);
        }

        private static float ToGridCoordinate(float value, float min, float size, int n)
        {
            if (size <= 1e-8f)
            {
                return 0f;
            }
            float normalized = Mathf.Clamp01((value - min) / size);
            return normalized * (n - 1);
        }

        public void ResetPoint(int index)
        {
            if (ControlPoints == null || index < 0 || index >= ControlPoints.Length)
            {
                return;
            }
            ControlPoints[index] = RestPoints[index];
        }

        public void ResetAll()
        {
            if (ControlPoints == null)
            {
                return;
            }
            System.Array.Copy(RestPoints, ControlPoints, RestPoints.Length);
        }

        public bool IsPointModified(int index)
        {
            if (ControlPoints == null || index < 0 || index >= ControlPoints.Length)
            {
                return false;
            }
            return (ControlPoints[index] - RestPoints[index]).sqrMagnitude > 1e-10f;
        }

        public bool IsAnyPointModified()
        {
            if (ControlPoints == null)
            {
                return false;
            }
            for (int i = 0; i < ControlPoints.Length; i++)
            {
                if ((ControlPoints[i] - RestPoints[i]).sqrMagnitude > 1e-10f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
