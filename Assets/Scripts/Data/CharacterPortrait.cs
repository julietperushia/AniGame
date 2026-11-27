using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Imports the player's character picture: Windows file picker (editor: Unity's file panel), a file path or an image URL.
/// The picture is centre-cropped to the card art shape and saved as PNG in persistentDataPath.
/// </summary>
public static class CharacterPortrait
{
    public const float Aspect = 5f / 6f;   // card art shape (width / height)
    public const int MaxHeight = 900;

    /// <summary>Picture imported but not saved yet (the character screen copies it to PlayerProfile.PortraitPath on save).</summary>
    public static string PendingPath => Path.Combine(Application.persistentDataPath, "character_portrait_new.png");

    /// <summary>Opens a file picker. Returns the chosen path or null.</summary>
    public static string PickFile()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Choose your character picture", "", "png,jpg,jpeg");
        return string.IsNullOrEmpty(path) ? null : path;
#elif UNITY_STANDALONE_WIN
        try
        {
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(ofn);
            ofn.filter = "Images (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0\0";
            ofn.file = new string(new char[1024]);
            ofn.maxFile = ofn.file.Length;
            ofn.fileTitle = new string(new char[256]);
            ofn.maxFileTitle = ofn.fileTitle.Length;
            ofn.title = "Choose your character picture";
            ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008; // explorer, file must exist, path must exist, no change dir
            return GetOpenFileName(ofn) ? ofn.file.TrimEnd('\0') : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AniGame] File picker failed: " + ex.Message);
            return null;
        }
#else
        return null;
#endif
    }

    public static bool CanPickFiles
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

    /// <summary>Loads, crops and saves the picture to <see cref="PendingPath"/>. Returns an error message or null.</summary>
    public static string ImportFile(string path, out Sprite preview)
    {
        preview = null;
        path = (path ?? "").Trim().Trim('"');
        if (path.Length == 0) return "No file chosen";
        if (!File.Exists(path)) return "File not found: " + path;
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception ex) { return "Could not read the file: " + ex.Message; }
        return ImportBytes(bytes, out preview);
    }

    public static string ImportBytes(byte[] bytes, out Sprite preview)
    {
        string err = Crop(bytes, out byte[] png, out preview);
        if (err != null) return err;
        try { File.WriteAllBytes(PendingPath, png); }
        catch (Exception ex) { preview = null; return "Could not save the picture: " + ex.Message; }
        return null;
    }

    /// <summary>Crops a PNG/JPG to the card art shape without saving it. Returns an error or null.</summary>
    public static string Crop(byte[] bytes, out byte[] png, out Sprite preview)
    {
        png = null;
        preview = null;
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (bytes == null || !src.LoadImage(bytes))
        {
            UnityEngine.Object.Destroy(src);
            return "That file is not a PNG or JPG picture";
        }
        var cropped = CropResize(src, Aspect, MaxHeight);
        UnityEngine.Object.Destroy(src);
        png = cropped.EncodeToPNG();
        preview = Sprite.Create(cropped, new Rect(0, 0, cropped.width, cropped.height), new Vector2(0.5f, 0.5f), 100f);
        return null;
    }

    /// <summary>Reads a file and crops it (see <see cref="Crop"/>).</summary>
    public static string CropFile(string path, out byte[] png, out Sprite preview)
    {
        png = null; preview = null;
        path = (path ?? "").Trim().Trim('"');
        if (path.Length == 0) return "No file chosen";
        if (!File.Exists(path)) return "File not found: " + path;
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception ex) { return "Could not read the file: " + ex.Message; }
        return Crop(bytes, out png, out preview);
    }

    /// <summary>Downloads an image URL and crops it. Calls done(error, png, preview).</summary>
    public static IEnumerator DownloadAndCrop(string url, Action<string, byte[], Sprite> done)
    {
        using (var req = UnityWebRequest.Get(url.Trim()))
        {
            req.timeout = 20;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { done?.Invoke("Download failed: " + req.error, null, null); yield break; }
            string err = Crop(req.downloadHandler.data, out byte[] png, out Sprite preview);
            done?.Invoke(err, png, preview);
        }
    }

    /// <summary>Downloads an image URL, then imports it. Calls done(error, preview).</summary>
    public static IEnumerator ImportUrl(string url, Action<string, Sprite> done)
    {
        using (var req = UnityWebRequest.Get(url.Trim()))
        {
            req.timeout = 20;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                done?.Invoke("Download failed: " + req.error, null);
                yield break;
            }
            string err = ImportBytes(req.downloadHandler.data, out Sprite preview);
            done?.Invoke(err, preview);
        }
    }

    /// <summary>Saves the pending picture as the character portrait.</summary>
    public static bool CommitPending()
    {
        try
        {
            if (!File.Exists(PendingPath)) return false;
            File.Copy(PendingPath, PlayerProfile.PortraitPath, true);
            File.Delete(PendingPath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AniGame] Could not save the portrait: " + ex.Message);
            return false;
        }
    }

    static Texture2D CropResize(Texture2D src, float aspect, int maxHeight)
    {
        int w = src.width, h = src.height;
        float a = (float)w / h;
        Rect crop = a > aspect
            ? new Rect((w - h * aspect) * 0.5f, 0f, h * aspect, h)
            : new Rect(0f, (h - w / aspect) * 0.5f, w, w / aspect);
        int outH = Mathf.Max(8, Mathf.Min(maxHeight, Mathf.RoundToInt(crop.height)));
        int outW = Mathf.Max(8, Mathf.RoundToInt(outH * aspect));
        var rt = RenderTexture.GetTemporary(outW, outH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt, new Vector2(crop.width / w, crop.height / h), new Vector2(crop.x / w, crop.y / h));
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var dst = new Texture2D(outW, outH, TextureFormat.RGBA32, false);
        dst.ReadPixels(new Rect(0, 0, outW, outH), 0, 0);
        dst.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        dst.wrapMode = TextureWrapMode.Clamp;
        return dst;
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
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
#endif
}
