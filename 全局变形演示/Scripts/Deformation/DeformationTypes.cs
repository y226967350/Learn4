using UnityEngine;

namespace ObjViewer.Deformation
{
    /// <summary>全局变形模式。</summary>
    public enum DeformationMode
    {
        None = 0,
        Taper = 1,
        Twist = 2,
        Bend = 3,
        Ffd = 4
    }

    /// <summary>锥化 / 扭曲 / 弯曲所围绕的模型局部坐标轴。</summary>
    public enum DeformationAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    public static class DeformationAxisUtil
    {
        public static Vector3 ToVector(DeformationAxis axis)
        {
            switch (axis)
            {
                case DeformationAxis.X: return Vector3.right;
                case DeformationAxis.Z: return Vector3.forward;
                default: return Vector3.up;
            }
        }

        public static string ToShortLabel(DeformationAxis axis)
        {
            switch (axis)
            {
                case DeformationAxis.X: return "X";
                case DeformationAxis.Z: return "Z";
                default: return "Y";
            }
        }

        /// <summary>
        /// 返回弯曲方向。轴向和弯曲方向共同构成弯曲平面，
        /// 另一个垂直方向在变形时保持不变。
        /// </summary>
        public static Vector3 GetBendDirection(DeformationAxis axis)
        {
            switch (axis)
            {
                case DeformationAxis.X: return Vector3.up;    // X 高度 -> 向 Y 弯
                case DeformationAxis.Z: return Vector3.right; // Z 高度 -> 向 X 弯
                default: return Vector3.right;                // Y 高度 -> 向 X 弯
            }
        }
    }
}
