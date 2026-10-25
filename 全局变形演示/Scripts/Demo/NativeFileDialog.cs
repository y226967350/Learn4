using System;
using UnityEngine;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ObjViewer.Demo
{
    /// <summary>
    /// 运行时打开 OBJ 文件选择器。
    /// 编辑器中使用 UnityEditor 的文件面板；Windows 独立程序中使用系统对话框。
    /// 其他平台请直接在面板的路径输入框中填写绝对路径。
    /// </summary>
    public static class NativeFileDialog
    {
        public static string OpenObjFile(string title = "选择 OBJ 模型")
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.OpenFilePanel(title, "", "obj");
#elif UNITY_STANDALONE_WIN
            return OpenWindowsFileDialog(title);
#else
            Debug.LogWarning("[全局变形演示] 当前平台不支持系统文件对话框，请在路径输入框中填写 OBJ 绝对路径。");
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int OFN_READONLY = 0x00000001;
        private const int OFN_OVERWRITEPROMPT = 0x00000002;
        private const int OFN_HIDEREADONLY = 0x00000004;
        private const int OFN_NOCHANGEDIR = 0x00000008;
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_FILEMUSTEXIST = 0x00001000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private class OpenFileName
        {
            public int structSize;
            public IntPtr dlgOwner;
            public IntPtr instance;
            public string filter;
            public string customFilter;
            public int maxCustFilter;
            public int filterIndex;
            public string file;
            public int maxFile;
            public string fileTitle;
            public int maxFileTitle;
            public string initialDir;
            public string title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public string defExt;
            public IntPtr custData;
            public IntPtr hook;
            public string templateName;
            public IntPtr reservedPtr;
            public int reservedInt;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileName([In, Out] OpenFileName dialog);

        private static string OpenWindowsFileDialog(string title)
        {
            try
            {
                OpenFileName dialog = new OpenFileName
                {
                    structSize = Marshal.SizeOf(typeof(OpenFileName)),
                    filter = "Wavefront OBJ (*.obj)\0*.obj\0所有文件 (*.*)\0*.*\0\0",
                    filterIndex = 1,
                    file = new string('\0', 4096),
                    maxFile = 4096,
                    title = title,
                    flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR
                };

                if (GetOpenFileName(dialog))
                {
                    return dialog.file.Split('\0')[0];
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[全局变形演示] 打开文件对话框失败：{e.Message}");
            }
            return null;
        }
#endif
    }
}
