using System.Collections.Generic;
using UnityEngine;

namespace ObjViewer.ObjLoader
{
    /// <summary>
    /// 把 <see cref="ObjData"/> 构建为 Unity <see cref="Mesh"/>。
    /// 处理：1 基/负索引换算、多边形三角化（扇形）、顶点去重、法线补算。
    /// </summary>
    public static class MeshBuilder
    {
        public static Mesh Build(ObjData data)
        {
            Mesh mesh = new Mesh();
            mesh.name = "OBJ_Imported";

            if (data.Faces.Count == 0)
            {
                return mesh;
            }

            int posCount = data.Positions.Count;
            int uvCount = data.UVs.Count;
            int nrmCount = data.Normals.Count;
            bool hasNormals = nrmCount > 0;

            // 顶点去重：key = (posIdx, uvIdx, nrmIdx)，value = 顶点在最终数组中的下标
            Dictionary<(int, int, int), int> vertexMap = new Dictionary<(int, int, int), int>();
            List<Vector3> positions = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            // 子网格（材质组）的三角形索引
            List<List<int>> submeshTriangles = new List<List<int>>();
            int defaultSubmesh = 0;
            if (data.Groups.Count == 0)
            {
                submeshTriangles.Add(new List<int>());
            }
            else
            {
                for (int i = 0; i < data.Groups.Count; i++)
                {
                    submeshTriangles.Add(new List<int>());
                }
            }

            for (int faceIndex = 0; faceIndex < data.Faces.Count; faceIndex++)
            {
                ObjData.Face face = data.Faces[faceIndex];
                int submesh = face.GroupIndex >= 0 && face.GroupIndex < submeshTriangles.Count
                    ? face.GroupIndex
                    : defaultSubmesh;

                // 多边形（>=3 边）三角化为扇形
                List<int> triIndices = new List<int>();
                for (int v = 1; v < face.Vertices.Count - 1; v++)
                {
                    AddVertex(data, face.Vertices[0], posCount, uvCount, nrmCount, hasNormals, vertexMap, positions, uvs, normals, triIndices);
                    AddVertex(data, face.Vertices[v], posCount, uvCount, nrmCount, hasNormals, vertexMap, positions, uvs, normals, triIndices);
                    AddVertex(data, face.Vertices[v + 1], posCount, uvCount, nrmCount, hasNormals, vertexMap, positions, uvs, normals, triIndices);
                }

                submeshTriangles[submesh].AddRange(triIndices);
            }

            mesh.indexFormat = positions.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(positions);
            mesh.SetUVs(0, uvs);
            if (hasNormals)
            {
                mesh.SetNormals(normals);
            }

            mesh.subMeshCount = submeshTriangles.Count;
            for (int i = 0; i < submeshTriangles.Count; i++)
            {
                mesh.SetTriangles(submeshTriangles[i], i);
            }

            if (!hasNormals)
            {
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }

        private static void AddVertex(
            ObjData data,
            ObjData.FaceVertex fv,
            int posCount,
            int uvCount,
            int nrmCount,
            bool hasNormals,
            Dictionary<(int, int, int), int> vertexMap,
            List<Vector3> positions,
            List<Vector2> uvs,
            List<Vector3> normals,
            List<int> triangleIndices)
        {
            int posIdx = Resolve(fv.PositionIndex, posCount);
            int uvIdx = Resolve(fv.UVIndex, uvCount);
            int nrmIdx = Resolve(fv.NormalIndex, nrmCount);

            var key = (posIdx, uvIdx, nrmIdx);
            if (!vertexMap.TryGetValue(key, out int existing))
            {
                existing = positions.Count;
                vertexMap[key] = existing;

                positions.Add(posIdx >= 0 && posIdx < data.Positions.Count
                    ? data.Positions[posIdx]
                    : Vector3.zero);

                uvs.Add(uvIdx >= 0 && uvIdx < data.UVs.Count
                    ? data.UVs[uvIdx]
                    : Vector2.zero);

                if (hasNormals)
                {
                    normals.Add(nrmIdx >= 0 && nrmIdx < data.Normals.Count
                        ? data.Normals[nrmIdx]
                        : Vector3.zero);
                }
            }

            triangleIndices.Add(existing);
        }

        /// <summary>把 OBJ 的带符号 1 基索引转成 0 基索引；负数从末尾倒数。</summary>
        private static int Resolve(int signedIndex, int count)
        {
            if (signedIndex == -1)
            {
                return -1;
            }
            if (signedIndex > 0)
            {
                return signedIndex - 1;
            }
            return count + signedIndex; // 负索引：-1 -> count-1
        }
    }
}
