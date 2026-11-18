using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Runtime fallback for card art: when a Card has no local sprite but has an imageUrl, the portrait is downloaded
/// once, cached on disk (persistentDataPath/image_cache) and in memory, then handed to the view that asked.
/// Lets new sheet rows show art before the editor generator has downloaded their images.
/// </summary>
public class RemoteImageCache : MonoBehaviour
{
    static RemoteImageCache instance;

    readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    readonly Dictionary<string, List<Action<Sprite>>> pending = new Dictionary<string, List<Action<Sprite>>>();
    int running;
    const int MaxConcurrent = 4;
    readonly Queue<string> queue = new Queue<string>();

    static RemoteImageCache Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("RemoteImageCache");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<RemoteImageCache>();
            }
            return instance;
        }
    }

    /// <summary>Calls <paramref name="onLoaded"/> with the sprite (or null on failure). Play mode only.</summary>
    public static void Request(string url, Action<Sprite> onLoaded)
    {
        if (string.IsNullOrEmpty(url) || !Application.isPlaying || onLoaded == null) return;
        Instance.Enqueue(url, onLoaded);
    }

    void Enqueue(string url, Action<Sprite> onLoaded)
    {
        if (sprites.TryGetValue(url, out Sprite cached))
        {
            onLoaded(cached);
            return;
        }
        if (pending.TryGetValue(url, out var waiting))
        {
            waiting.Add(onLoaded);
            return;
        }
        pending[url] = new List<Action<Sprite>> { onLoaded };
        queue.Enqueue(url);
        Pump();
    }

    void Pump()
    {
        while (running < MaxConcurrent && queue.Count > 0)
        {
            running++;
            StartCoroutine(Load(queue.Dequeue()));
        }
    }

    static string CachePath(string url)
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            foreach (char c in url) { hash ^= c; hash *= 1099511628211UL; }
            return Path.Combine(Application.persistentDataPath, "image_cache", hash.ToString("x16") + ".img");
        }
    }

    IEnumerator Load(string url)
    {
        Texture2D texture = null;
        string cachePath = CachePath(url);

        if (File.Exists(cachePath))
        {
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(File.ReadAllBytes(cachePath))) texture = tex;
                else Destroy(tex);
            }
            catch (Exception) { texture = null; }
        }

        if (texture == null)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = 20;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    texture = DownloadHandlerTexture.GetContent(request);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                        File.WriteAllBytes(cachePath, request.downloadHandler.data);
                    }
                    catch (Exception) { }
                }
            }
        }

        Sprite sprite = null;
        if (texture != null)
        {
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "remote_" + Path.GetFileNameWithoutExtension(url);
        }
        sprites[url] = sprite;

        if (pending.TryGetValue(url, out var callbacks))
        {
            pending.Remove(url);
            foreach (var cb in callbacks)
            {
                try { cb(sprite); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        running--;
        Pump();
    }
}
