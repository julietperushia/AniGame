using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>Native Open / Save file dialogs (Windows player and the Unity editor). Return null when cancelled or unsupported.</summary>
public static class FileDialogs
{
    public static bool Supported
    {
        get
        {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            return true;
#else
            return false;
#endif
        }
    }

    /// <param name="extension">without the dot, e.g. "anisave"</param>
    public static string OpenFile(string title, string description, string extension)
    {
#if UNITY_EDITOR
        string p = UnityEditor.EditorUtility.OpenFilePanel(title, "", extension);
        return string.IsNullOrEmpty(p) ? null : p;
#elif UNITY_STANDALONE_WIN
        return Run(title, description, extension, null, false);
#else
        return null;
#endif
    }

    public static string SaveFile(string title, string description, string extension, string defaultName)
    {
#if UNITY_EDITOR
        string p = UnityEditor.EditorUtility.SaveFilePanel(title, "", defaultName, extension);
        return string.IsNullOrEmpty(p) ? null : p;
#elif UNITY_STANDALONE_WIN
        return Run(title, description, extension, defaultName, true);
#else
        return null;
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    static string Run(string title, string description, string extension, string defaultName, bool save)
    {
        try
        {
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(ofn);
            ofn.filter = $"{description} (*.{extension})\0*.{extension}\0All files (*.*)\0*.*\0\0";
            string start = defaultName ?? "";
            ofn.file = start + new string('\0', Math.Max(16, 1024 - start.Length));
            ofn.maxFile = ofn.file.Length;
            ofn.fileTitle = new string('\0', 256);
            ofn.maxFileTitle = ofn.fileTitle.Length;
            ofn.title = title;
            ofn.defExt = extension;
            ofn.flags = save
                ? 0x00080000 | 0x00000800 | 0x00000002 | 0x00000008   // explorer, path must exist, overwrite prompt, no change dir
                : 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;  // explorer, file must exist, path must exist, no change dir
            bool ok = save ? GetSaveFileName(ofn) : GetOpenFileName(ofn);
            if (!ok) return null;
            string path = ofn.file;
            int nul = path.IndexOf('\0');
            return nul >= 0 ? path.Substring(0, nul) : path;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AniGame] File dialog failed: " + ex.Message);
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    class OpenFileName
    {
        public int structSize = 0;
        public IntPtr dlgOwner = IntPtr.Zero;
        public IntPtr instance = IntPtr.Zero;
        public string filter = null;
        public string customFilter = null;
        public int maxCustFilter = 0;
        public int filterIndex = 0;
        public string file = null;
        public int maxFile = 0;
        public string fileTitle = null;
        public int maxFileTitle = 0;
        public string initialDir = null;
        public string title = null;
        public int flags = 0;
        public short fileOffset = 0;
        public short fileExtension = 0;
        public string defExt = null;
        public IntPtr custData = IntPtr.Zero;
        public IntPtr hook = IntPtr.Zero;
        public string templateName = null;
        public IntPtr reservedPtr = IntPtr.Zero;
        public int reservedInt = 0;
        public int flagsEx = 0;
    }

    [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Unicode)]
    static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Unicode)]
    static extern bool GetSaveFileName([In, Out] OpenFileName ofn);
#endif
}
