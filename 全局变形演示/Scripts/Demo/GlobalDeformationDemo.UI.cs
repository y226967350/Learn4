using System;
using System.IO;
using ObjViewer.Deformation;
using UnityEngine;

namespace ObjViewer.Demo
{
    public partial class GlobalDeformationDemo
    {
        private static readonly Color AccentColor = new Color(0.24f, 0.78f, 0.88f, 1f);
        private static readonly Color TextPrimaryColor = new Color(0.91f, 0.95f, 0.98f, 1f);
        private static readonly Color TextMutedColor = new Color(0.57f, 0.66f, 0.74f, 1f);
        private static readonly Color TextDimColor = new Color(0.42f, 0.50f, 0.58f, 1f);

        private bool showPanel = true;
        private Rect panelRect = new Rect(14f, 14f, 396f, 720f);
        private Vector2 scrollPosition;

        private bool uiStylesReady;
        private Font uiFont;
        private bool ownsUiFont;
        private Texture2D panelTexture;
        private Texture2D buttonTexture;
        private Texture2D buttonHoverTexture;
        private Texture2D buttonActiveTexture;
        private Texture2D selectedTexture;
        private Texture2D selectedHoverTexture;
        private Texture2D fieldTexture;

        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle helpStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle statusStyle;
        private GUIStyle errorStyle;

        private void OnGUI()
        {
            if (!started)
            {
                return;
            }

            EnsureUiStyles();

            float panelHeight = Mathf.Max(260f, Screen.height - 28f);
            panelRect = new Rect(14f, 14f, 396f, panelHeight);

            GUI.DrawTexture(panelRect, panelTexture);
            GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, 4f, panelRect.height), selectedTexture);

            GUILayout.BeginArea(new Rect(
                panelRect.x + 18f,
                panelRect.y + 14f,
                panelRect.width - 32f,
                panelRect.height - 28f));

            scrollPosition = GUILayout.BeginScrollView(
                scrollPosition,
                false,
                false,
                GUIStyle.none,
                GUI.skin.verticalScrollbar);

            DrawPanelContent();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPanelContent()
        {
            GUILayout.Label("全局变形演示", titleStyle);
            GUILayout.Label("OBJ · 锥化 · 扭曲 · 弯曲 · FFD", subtitleStyle);
            GUILayout.Space(8);

            DrawModelSection();
            DrawSeparator();
            DrawModeSection();

            if (mode != DeformationMode.None)
            {
                DrawSeparator();
                DrawParameterSection();
            }

            DrawSeparator();
            DrawFooter();
        }

        private void DrawModelSection()
        {
            GUILayout.Label("模型 / MODEL", sectionStyle);

            string displayName = string.IsNullOrEmpty(modelPath) ? "尚未加载模型" : Path.GetFileName(modelPath);
            GUILayout.Label(displayName, labelStyle);

            if (vertexCount > 0)
            {
                GUILayout.Label($"{vertexCount:N0} 顶点   ·   {triangleCount:N0} 三角面", smallStyle);
            }
            else
            {
                GUILayout.Label("请选择 .obj 文件", smallStyle);
            }

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("导入 OBJ…", buttonStyle, GUILayout.Height(30)))
            {
                string selected = NativeFileDialog.OpenObjFile();
                if (!string.IsNullOrEmpty(selected))
                {
                    LoadModelFromPath(selected);
                }
            }
            if (GUILayout.Button("重新加载", buttonStyle, GUILayout.Height(30)))
            {
                if (!string.IsNullOrEmpty(modelPath))
                {
                    LoadModelFromPath(modelPath);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            pathInput = GUILayout.TextField(pathInput ?? string.Empty, textFieldStyle, GUILayout.Height(25));
            if (GUILayout.Button("加载", buttonStyle, GUILayout.Width(54), GUILayout.Height(25)))
            {
                LoadModelFromPath(pathInput);
            }
            GUILayout.EndHorizontal();

            if (samplePaths.Count > 0)
            {
                GUILayout.Space(3);
                GUILayout.Label("内置示例", smallStyle);
                for (int row = 0; row < samplePaths.Count; row += 2)
                {
                    GUILayout.BeginHorizontal();
                    for (int col = 0; col < 2 && row + col < samplePaths.Count; col++)
                    {
                        string samplePath = samplePaths[row + col];
                        string sampleName = Path.GetFileNameWithoutExtension(samplePath);
                        GUIStyle style = IsCurrentModelPath(samplePath) ? selectedButtonStyle : buttonStyle;
                        if (GUILayout.Button(sampleName, style, GUILayout.Height(26)))
                        {
                            LoadModelFromPath(samplePath);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        private void DrawModeSection()
        {
            GUILayout.Label("变形模式 / DEFORMATION", sectionStyle);
            GUILayout.Space(3);

            GUILayout.BeginHorizontal();
            DrawModeButton("原始", DeformationMode.None);
            DrawModeButton("锥化 Taper", DeformationMode.Taper);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawModeButton("扭曲 Twist", DeformationMode.Twist);
            DrawModeButton("弯曲 Bend", DeformationMode.Bend);
            GUILayout.EndHorizontal();

            DrawModeButton("FFD 自由变形", DeformationMode.Ffd);

            GUILayout.Space(3);
            GUILayout.Label(GetModeDescription(), helpStyle);
        }

        private void DrawModeButton(string label, DeformationMode target)
        {
            GUIStyle style = mode == target ? selectedButtonStyle : buttonStyle;
            if (GUILayout.Button(label, style, GUILayout.Height(32)))
            {
                SetMode(target);
            }
        }

        private string GetModeDescription()
        {
            switch (mode)
            {
                case DeformationMode.Taper:
                    return "沿高度轴线性缩放截面：底部不变，顶部按比例收放。";
                case DeformationMode.Twist:
                    return "截面随高度旋转；总角度从上端到下端逐渐生效。";
                case DeformationMode.Bend:
                    return "模型中部保持为支点，两端向弯曲方向弯成圆弧。";
                case DeformationMode.Ffd:
                    return "初始控制点由包围盒均分生成；修改控制点坐标即可观察模型变化。";
                default:
                    return "当前显示原始模型。选择一种变形开始演示。";
            }
        }

        private void DrawParameterSection()
        {
            GUILayout.Label($"变形参数 · {CurrentModeName()}", sectionStyle);

            if (mode == DeformationMode.Ffd)
            {
                DrawFfdControls();
                return;
            }

            DrawAxisSelector();

            switch (mode)
            {
                case DeformationMode.Taper:
                    DrawFloatSlider("收放量", ref taperAmount, -0.9f, 3.0f, "0.00");
                    GUILayout.Label("0 = 不缩放；正值上宽下窄，负值上窄下宽。", helpStyle);
                    break;
                case DeformationMode.Twist:
                    DrawFloatSlider("总扭转角", ref twistAngle, -540f, 540f, "0°");
                    GUILayout.Label("正值绕高度轴逆时针扭转，0° 表示不扭转。", helpStyle);
                    break;
                case DeformationMode.Bend:
                    DrawFloatSlider("弯曲角度", ref bendAngle, -180f, 180f, "0°");
                    GUILayout.Label("正负角度决定弯曲方向；180° 可弯成半圆弧。", helpStyle);
                    break;
            }
        }

        private void DrawAxisSelector()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("高度轴", labelStyle, GUILayout.Width(62));
            DrawAxisButton("X", DeformationAxis.X);
            DrawAxisButton("Y", DeformationAxis.Y);
            DrawAxisButton("Z", DeformationAxis.Z);
            GUILayout.EndHorizontal();
        }

        private void DrawAxisButton(string label, DeformationAxis target)
        {
            GUIStyle style = axis == target ? selectedButtonStyle : buttonStyle;
            if (GUILayout.Button(label, style, GUILayout.Width(38), GUILayout.Height(24)))
            {
                SetAxis(target);
            }
        }

        private void DrawFloatSlider(string label, ref float value, float min, float max, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(66));
            float newValue = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label(newValue.ToString(format), smallStyle, GUILayout.Width(52));
            GUILayout.EndHorizontal();

            if (!Mathf.Approximately(value, newValue))
            {
                value = newValue;
                ApplyDeformation();
            }
        }

        private void DrawFfdControls()
        {
            GUILayout.Label("① 初始：由包围盒生成 n×n×n 控制点", helpStyle);
            GUILayout.Label("② 终止：拖动控制点或修改 X/Y/Z 坐标", helpStyle);
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.Label("每轴点数 n", labelStyle, GUILayout.Width(72));
            int newResolution = Mathf.RoundToInt(GUILayout.HorizontalSlider(ffdResolution, 2, 6));
            GUILayout.Label(newResolution.ToString(), smallStyle, GUILayout.Width(22));
            GUILayout.EndHorizontal();

            if (newResolution != ffdResolution)
            {
                InitializeFfd(newResolution);
            }

            if (lattice != null)
            {
                GUILayout.Label(
                    $"网格 {lattice.Resolution} × {lattice.Resolution} × {lattice.Resolution} = {lattice.PointCount} 个控制点",
                    smallStyle);
                GUILayout.Label(
                    $"包围盒  min ({Fmt(originalBounds.min)})   max ({Fmt(originalBounds.max)})",
                    smallStyle);
            }

            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重新生成网格", buttonStyle, GUILayout.Height(26)))
            {
                InitializeFfd(ffdResolution);
            }
            if (GUILayout.Button("全部控制点复位", buttonStyle, GUILayout.Height(26)))
            {
                ResetAllFfdPoints();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(showFfdGrid ? "控制网格：显示" : "控制网格：隐藏",
                    showFfdGrid ? selectedButtonStyle : buttonStyle, GUILayout.Height(26)))
            {
                showFfdGrid = !showFfdGrid;
                UpdateFfdVisibility();
            }
            if (GUILayout.Button(showInitialGrid ? "初始网格：显示" : "初始网格：隐藏",
                    showInitialGrid ? selectedButtonStyle : buttonStyle, GUILayout.Height(26)))
            {
                showInitialGrid = !showInitialGrid;
                if (ffdView != null)
                {
                    ffdView.SetInitialGridVisible(showInitialGrid);
                }
            }
            GUILayout.EndHorizontal();

            if (lattice == null)
            {
                if (GUILayout.Button("初始化 FFD 网格", selectedButtonStyle, GUILayout.Height(30)))
                {
                    InitializeFfd(ffdResolution);
                }
                return;
            }

            GUILayout.Space(5);
            lattice.GetCoord(selectedFfdPoint, out int i, out int j, out int k);
            GUILayout.Label(
                $"选中控制点  {selectedFfdPoint + 1}/{lattice.PointCount}   (i={i}, j={j}, k={k})",
                labelStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ 上一个", buttonStyle, GUILayout.Height(25)))
            {
                CycleSelectedFfdPoint(-1);
            }
            if (GUILayout.Button("下一个 ▶", buttonStyle, GUILayout.Height(25)))
            {
                CycleSelectedFfdPoint(1);
            }
            GUILayout.EndHorizontal();

            Vector3 rest = lattice.RestPoints[selectedFfdPoint];
            Vector3 current = lattice.ControlPoints[selectedFfdPoint];
            bool modified = lattice.IsPointModified(selectedFfdPoint);
            GUILayout.Label($"初始 ({Fmt(rest)})" + (modified ? "   ← 已修改" : ""),
                modified ? labelStyle : smallStyle);

            float pad = Mathf.Max(originalBounds.size.magnitude * 0.75f, 0.1f);
            float x = current.x;
            float y = current.y;
            float z = current.z;

            DrawCoordinateSlider("X", ref x,
                Mathf.Min(originalBounds.min.x, current.x) - pad,
                Mathf.Max(originalBounds.max.x, current.x) + pad);
            DrawCoordinateSlider("Y", ref y,
                Mathf.Min(originalBounds.min.y, current.y) - pad,
                Mathf.Max(originalBounds.max.y, current.y) + pad);
            DrawCoordinateSlider("Z", ref z,
                Mathf.Min(originalBounds.min.z, current.z) - pad,
                Mathf.Max(originalBounds.max.z, current.z) + pad);

            if (!Mathf.Approximately(x, current.x)
                || !Mathf.Approximately(y, current.y)
                || !Mathf.Approximately(z, current.z))
            {
                Vector3 next = new Vector3(x, y, z);
                lattice.ControlPoints[selectedFfdPoint] = next;
                ApplyDeformation();
                if (ffdView != null)
                {
                    ffdView.Refresh();
                }
            }

            GUILayout.Label($"当前 ({Fmt(lattice.ControlPoints[selectedFfdPoint])})", smallStyle);

            if (GUILayout.Button("选中控制点归位", buttonStyle, GUILayout.Height(25)))
            {
                ResetSelectedFfdPoint();
            }

            GUILayout.Label("青色=初始点 · 橙色=已修改 · 黄色=选中 · 灰线=初始网格", helpStyle);
            GUILayout.Label("提示：在场景中直接按住并拖动黄色小球也可以修改。", helpStyle);
        }

        private void DrawCoordinateSlider(string label, ref float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(18));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label(value.ToString("0.000"), smallStyle, GUILayout.Width(56));
            GUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            GUILayout.Label("视图 / VIEW", sectionStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("恢复原始模型", buttonStyle, GUILayout.Height(28)))
            {
                ResetToOriginal();
            }
            if (GUILayout.Button("重置视角", buttonStyle, GUILayout.Height(28)))
            {
                FrameCameraNow();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("左键旋转 · 右键/中键平移 · 滚轮缩放", helpStyle);
            if (mode == DeformationMode.Ffd)
            {
                GUILayout.Label("FFD：点击小球选中，拖动小球或使用滑杆修改坐标", helpStyle);
            }

            if (!string.IsNullOrEmpty(statusText) && Time.unscaledTime < statusExpireTime)
            {
                GUILayout.Space(5);
                GUILayout.Label(statusText, statusIsError ? errorStyle : statusStyle);
            }
        }

        private void DrawSeparator()
        {
            GUILayout.Space(8);
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(rect, selectedTexture);
            }
            GUILayout.Space(6);
        }

        private bool IsCurrentModelPath(string path)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(modelPath))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(path), Path.GetFullPath(modelPath), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string Fmt(Vector3 v)
        {
            return $"{v.x:0.000}, {v.y:0.000}, {v.z:0.000}";
        }

        // ====================================================================
        // 样式
        // ====================================================================

        private void EnsureUiStyles()
        {
            if (uiStylesReady)
            {
                return;
            }

            LoadUiFont();

            panelTexture = MakeTexture(new Color(0.045f, 0.058f, 0.075f, 0.97f));
            buttonTexture = MakeTexture(new Color(0.115f, 0.155f, 0.195f, 1f));
            buttonHoverTexture = MakeTexture(new Color(0.16f, 0.22f, 0.27f, 1f));
            buttonActiveTexture = MakeTexture(new Color(0.20f, 0.28f, 0.34f, 1f));
            selectedTexture = MakeTexture(new Color(0.22f, 0.68f, 0.80f, 1f));
            selectedHoverTexture = MakeTexture(new Color(0.28f, 0.78f, 0.88f, 1f));
            fieldTexture = MakeTexture(new Color(0.075f, 0.095f, 0.12f, 1f));

            titleStyle = MakeLabelStyle(21, TextPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            subtitleStyle = MakeLabelStyle(11, TextMutedColor, TextAnchor.MiddleLeft, FontStyle.Normal);
            sectionStyle = MakeLabelStyle(12, AccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            labelStyle = MakeLabelStyle(13, TextPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Normal);
            smallStyle = MakeLabelStyle(11, TextMutedColor, TextAnchor.MiddleLeft, FontStyle.Normal);
            smallStyle.wordWrap = true;
            helpStyle = MakeLabelStyle(11, TextDimColor, TextAnchor.UpperLeft, FontStyle.Normal);
            helpStyle.wordWrap = true;

            buttonStyle = MakeButtonStyle(buttonTexture, buttonHoverTexture, buttonActiveTexture, TextPrimaryColor);
            selectedButtonStyle = MakeButtonStyle(selectedTexture, selectedHoverTexture, selectedHoverTexture,
                new Color(0.025f, 0.10f, 0.12f, 1f));

            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                font = uiFont,
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(8, 6, 4, 4),
                margin = new RectOffset(0, 4, 0, 0)
            };
            textFieldStyle.normal.background = fieldTexture;
            textFieldStyle.focused.background = fieldTexture;
            textFieldStyle.normal.textColor = TextPrimaryColor;
            textFieldStyle.focused.textColor = TextPrimaryColor;

            statusStyle = MakeLabelStyle(11, new Color(0.55f, 0.88f, 0.72f), TextAnchor.UpperLeft, FontStyle.Normal);
            statusStyle.wordWrap = true;
            errorStyle = MakeLabelStyle(11, new Color(1f, 0.55f, 0.48f), TextAnchor.UpperLeft, FontStyle.Normal);
            errorStyle.wordWrap = true;

            uiStylesReady = true;
        }

        private void LoadUiFont()
        {
            try
            {
                uiFont = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "Microsoft YaHei UI",
                    "Microsoft YaHei",
                    "SimHei",
                    "Noto Sans CJK SC",
                    "PingFang SC",
                    "Arial Unicode MS"
                }, 14);
                ownsUiFont = uiFont != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[全局变形演示] 无法创建中文系统字体：{e.Message}");
            }

            if (uiFont == null)
            {
                uiFont = GUI.skin.font;
            }
        }

        private GUIStyle MakeLabelStyle(int size, Color color, TextAnchor anchor, FontStyle fontStyle)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = size,
                alignment = anchor,
                fontStyle = fontStyle,
                richText = false,
                wordWrap = false,
                padding = new RectOffset(0, 0, 1, 1),
                margin = new RectOffset(0, 0, 1, 1)
            };
            style.normal.textColor = color;
            return style;
        }

        private GUIStyle MakeButtonStyle(Texture2D normal, Texture2D hover, Texture2D active, Color textColor)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                font = uiFont,
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(6, 6, 4, 4),
                margin = new RectOffset(0, 3, 0, 3),
                wordWrap = false
            };
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = active;
            style.focused.background = normal;
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
            return style;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void DisposeUiResources()
        {
            DestroyTexture(panelTexture);
            DestroyTexture(buttonTexture);
            DestroyTexture(buttonHoverTexture);
            DestroyTexture(buttonActiveTexture);
            DestroyTexture(selectedTexture);
            DestroyTexture(selectedHoverTexture);
            DestroyTexture(fieldTexture);

            panelTexture = null;
            buttonTexture = null;
            buttonHoverTexture = null;
            buttonActiveTexture = null;
            selectedTexture = null;
            selectedHoverTexture = null;
            fieldTexture = null;

            if (ownsUiFont && uiFont != null)
            {
                Destroy(uiFont);
            }
            uiFont = null;
            ownsUiFont = false;
            uiStylesReady = false;
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }
    }
}
