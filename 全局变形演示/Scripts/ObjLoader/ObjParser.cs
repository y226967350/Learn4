using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ObjViewer.ObjLoader
{
    /// <summary>
    /// 把 .obj 文本解析为 <see cref="ObjData"/>。
    /// 支持：v / vt / vn / f（含 v、v/vt、v//vn、v/vt/vn 及负索引）、g、o、usemtl、mtllib、注释。
    /// </summary>
    public static class ObjParser
    {
        public static ObjData Parse(string objText)
        {
            ObjData data = new ObjData();
            if (string.IsNullOrEmpty(objText))
            {
                return data;
            }

            // 允许带 UTF-8 BOM 的 OBJ 文件。
            objText = objText.TrimStart('﻿');

            // 当前组跟踪
            int currentGroupIndex = -1;
            string currentMaterial = null;

            using (StringReader reader = new StringReader(objText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line[0] == '#')
                    {
                        continue;
                    }

                    string[] tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length == 0)
                    {
                        continue;
                    }

                    switch (tokens[0])
                    {
                        case "v":
                            data.Positions.Add(ParseVector3(tokens));
                            break;

                        case "vt":
                            data.UVs.Add(ParseVector2(tokens));
                            break;

                        case "vn":
                            data.Normals.Add(ParseVector3(tokens));
                            break;

                        case "f":
                            ObjData.Face face = ParseFace(tokens);
                            face.GroupIndex = currentGroupIndex;
                            data.Faces.Add(face);
                            if (currentGroupIndex >= 0)
                            {
                                ObjData.Group g = data.Groups[currentGroupIndex];
                                g.FaceCount++;
                                data.Groups[currentGroupIndex] = g;
                            }
                            break;

                        case "g":
                        case "o":
                            currentGroupIndex = StartGroup(data, tokens.Length > 1 ? tokens[1] : "default", currentMaterial);
                            break;

                        case "usemtl":
                            currentMaterial = tokens.Length > 1 ? tokens[1] : null;
                            currentGroupIndex = StartGroup(data, currentMaterial ?? "default", currentMaterial);
                            break;

                        // mtllib 仅解析记录，实际材质贴图由调用方决定如何加载
                        case "mtllib":
                        case "s":
                        case "l":
                        case "p":
                            // 忽略这些行
                            break;
                    }
                }
            }

            return data;
        }

        private static int StartGroup(ObjData data, string name, string material)
        {
            ObjData.Group group = new ObjData.Group
            {
                Name = name,
                Material = material,
                StartFace = data.Faces.Count,
                FaceCount = 0
            };
            data.Groups.Add(group);
            return data.Groups.Count - 1;
        }

        private static Vector3 ParseVector3(string[] tokens)
        {
            float x = ParseFloat(tokens, 1);
            float y = ParseFloat(tokens, 2);
            float z = ParseFloat(tokens, 3);
            return new Vector3(x, y, z);
        }

        private static Vector2 ParseVector2(string[] tokens)
        {
            float u = ParseFloat(tokens, 1);
            float v = ParseFloat(tokens, 2);
            return new Vector2(u, v);
        }

        private static float ParseFloat(string[] tokens, int index)
        {
            if (index >= tokens.Length)
            {
                return 0f;
            }
            return float.Parse(tokens[index], CultureInfo.InvariantCulture);
        }

        private static ObjData.Face ParseFace(string[] tokens)
        {
            ObjData.Face face = new ObjData.Face
            {
                Vertices = new List<ObjData.FaceVertex>(tokens.Length - 1),
                GroupIndex = -1
            };

            for (int i = 1; i < tokens.Length; i++)
            {
                face.Vertices.Add(ParseFaceVertex(tokens[i]));
            }

            return face;
        }

        private static ObjData.FaceVertex ParseFaceVertex(string token)
        {
            // 支持：v、v/vt、v//vn、v/vt/vn
            string[] parts = token.Split('/');
            ObjData.FaceVertex fv = new ObjData.FaceVertex
            {
                PositionIndex = ParseSignedIndex(parts[0]),
                UVIndex = -1,
                NormalIndex = -1
            };

            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
            {
                fv.UVIndex = ParseSignedIndex(parts[1]);
            }
            if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
            {
                fv.NormalIndex = ParseSignedIndex(parts[2]);
            }

            return fv;
        }

        /// <summary>解析带符号索引：负数表示从末尾倒数（OBJ 规范），返回"带符号 1 基"值，由 MeshBuilder 转 0 基。</summary>
        private static int ParseSignedIndex(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return -1;
            }
            return int.Parse(s, CultureInfo.InvariantCulture);
        }
    }
}
