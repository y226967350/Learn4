using System.Collections.Generic;
using UnityEngine;

namespace ObjViewer.ObjLoader
{
    /// <summary>
    /// 纯数据容器：保存从 .obj 文件解析出的原始数据。
    /// 与 Unity 的 Mesh 解耦，便于未来直接读写顶点、面、法线等。
    /// </summary>
    public class ObjData
    {
        /// <summary>原始顶点位置（对应 obj 中的 v 行）。这是最核心的顶点访问入口。</summary>
        public List<Vector3> Positions = new List<Vector3>();

        /// <summary>纹理坐标（对应 vt 行）。可能为空。</summary>
        public List<Vector2> UVs = new List<Vector2>();

        /// <summary>法线（对应 vn 行）。可能为空。</summary>
        public List<Vector3> Normals = new List<Vector3>();

        /// <summary>
        /// 所有面。每个面保存一组索引（指向 Positions / UVs / Normals，1 为基，负数表示相对末尾）。
        /// 面可能是四边形及以上，构建 Mesh 时会被三角化。
        /// </summary>
        public List<Face> Faces = new List<Face>();

        /// <summary>子网格（材质组）信息，key 为材质名。用列表保证顺序稳定。</summary>
        public List<Group> Groups = new List<Group>();

        public struct FaceVertex
        {
            public int PositionIndex; // -1 表示缺失
            public int UVIndex;       // -1 表示缺失
            public int NormalIndex;   // -1 表示缺失
        }

        public struct Face
        {
            public List<FaceVertex> Vertices;
            public int GroupIndex;    // 属于哪个 Group（-1 表示默认组）
        }

        public struct Group
        {
            public string Name;       // 来自 g / o / usemtl
            public string Material;   // 来自 usemtl
            public int StartFace;     // 该组在 Faces 中的起始下标
            public int FaceCount;     // 该组包含的面数
        }
    }
}
