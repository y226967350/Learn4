using UnityEngine;

namespace ObjViewer.Deformation
{
    /// <summary>
    /// CPU 顶点变形算法。所有方法都从原始顶点数组计算，永远不累积误差，
    /// 因此可以实时拖动滑杆反复变形，也可以随时无损恢复。
    /// </summary>
    public static class MeshDeformer
    {
        public static Bounds CalculateBounds(Vector3[] vertices)
        {
            if (vertices == null || vertices.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Length; i++)
            {
                bounds.Encapsulate(vertices[i]);
            }
            return bounds;
        }

        /// <summary>
        /// 锥化：沿 <paramref name="axis"/> 方向按高度线性缩放另外两个方向。
        /// amount = 0 时不变；底部保持不变，顶部缩放为 1 + amount。
        /// </summary>
        public static void ApplyTaper(
            Vector3[] source,
            Vector3[] destination,
            Bounds bounds,
            DeformationAxis axis,
            float amount)
        {
            Vector3 axisDir = DeformationAxisUtil.ToVector(axis);
            Vector3 pivot = bounds.center;
            float min = Vector3.Dot(bounds.min, axisDir);
            float size = Vector3.Dot(bounds.max, axisDir) - min;

            for (int i = 0; i < source.Length; i++)
            {
                Vector3 p = source[i];
                float t = size > 1e-6f ? Mathf.Clamp01((Vector3.Dot(p, axisDir) - min) / size) : 0f;
                float scale = Mathf.Max(0.001f, 1f + amount * t);

                Vector3 relative = p - pivot;
                float axial = Vector3.Dot(relative, axisDir);
                Vector3 perpendicular = relative - axisDir * axial;
                destination[i] = pivot + axisDir * axial + perpendicular * scale;
            }
        }

        /// <summary>
        /// 扭曲：截面的旋转角随高度线性增加。总角度为 0 时不变。
        /// </summary>
        public static void ApplyTwist(
            Vector3[] source,
            Vector3[] destination,
            Bounds bounds,
            DeformationAxis axis,
            float totalAngleDegrees)
        {
            if (Mathf.Abs(totalAngleDegrees) < 1e-5f)
            {
                System.Array.Copy(source, destination, source.Length);
                return;
            }

            Vector3 axisDir = DeformationAxisUtil.ToVector(axis);
            Vector3 pivot = bounds.center;
            float min = Vector3.Dot(bounds.min, axisDir);
            float size = Vector3.Dot(bounds.max, axisDir) - min;

            for (int i = 0; i < source.Length; i++)
            {
                Vector3 p = source[i];
                float t = size > 1e-6f ? Mathf.Clamp01((Vector3.Dot(p, axisDir) - min) / size) : 0f;

                // 以模型中部为 0 度，向两端对称扭转，看起来更自然。
                float angle = totalAngleDegrees * (t - 0.5f);
                Quaternion rotation = Quaternion.AngleAxis(angle, axisDir);
                destination[i] = pivot + rotation * (p - pivot);
            }
        }

        /// <summary>
        /// 弯曲：把沿 axis 方向的直线弯成一段圆弧。总角度为 0 时不变。
        /// 模型中部保持不动，两端向 <see cref="DeformationAxisUtil.GetBendDirection"/> 指定的方向弯曲。
        /// </summary>
        public static void ApplyBend(
            Vector3[] source,
            Vector3[] destination,
            Bounds bounds,
            DeformationAxis axis,
            float totalAngleDegrees)
        {
            if (Mathf.Abs(totalAngleDegrees) < 1e-4f)
            {
                System.Array.Copy(source, destination, source.Length);
                return;
            }

            Vector3 axisDir = DeformationAxisUtil.ToVector(axis);
            Vector3 bendDir = DeformationAxisUtil.GetBendDirection(axis);
            Vector3 pivot = bounds.center;
            float length = Vector3.Dot(bounds.max, axisDir) - Vector3.Dot(bounds.min, axisDir);

            if (length <= 1e-6f)
            {
                System.Array.Copy(source, destination, source.Length);
                return;
            }

            float theta = totalAngleDegrees * Mathf.Deg2Rad;
            float radius = length / theta; // 有符号半径，负角度自动向反方向弯曲

            for (int i = 0; i < source.Length; i++)
            {
                Vector3 relative = source[i] - pivot;
                float along = Vector3.Dot(relative, axisDir);
                float arcAngle = along / length * theta; // along / radius

                float newAlong = radius * Mathf.Sin(arcAngle);
                float bendOffset = radius * (1f - Mathf.Cos(arcAngle));

                destination[i] = source[i]
                    + axisDir * (newAlong - along)
                    + bendDir * bendOffset;
            }
        }

        /// <summary>用初始化后的 n x n x n FFD 控制网格变形。</summary>
        public static void ApplyFfd(Vector3[] source, Vector3[] destination, FfdLattice lattice)
        {
            if (lattice == null || !lattice.IsInitialized)
            {
                System.Array.Copy(source, destination, source.Length);
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                destination[i] = lattice.Sample(source[i]);
            }
        }
    }
}
