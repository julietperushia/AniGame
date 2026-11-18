using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Development helper (editor only, safe to delete).
/// - Writes compile results and editor errors to Logs/AniGameBridge/bridge_log.txt
/// - Polls Logs/AniGameBridge/command.txt every 2 seconds and runs one whitelisted command:
///   refresh | build | campaign[:nodownload] | validate | scenes | settings | database | simulate | status | tick:on/off | render:name | open:SceneName | play | stop | shot:name
///   ui:Type.Method[:arg] | cheat | newgame   (play-mode testing helpers)
/// This lets an external assistant verify the project without clicking in the editor.
/// </summary>
[InitializeOnLoad]
public static class AniGameDevBridge
{
    const string Dir = "Logs/AniGameBridge";
    static readonly string CommandPath = Path.Combine(Dir, "command.txt");
    static readonly string LogPath = Path.Combine(Dir, "bridge_log.txt");
    static readonly string StatusPath = Path.Combine(Dir, "compile_status.txt");
    static double nextPoll;
    static bool ticking;

    static AniGameDevBridge()
    {
        try { Directory.CreateDirectory(Dir); } catch { }
        CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompiled;
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
        Append($"Domain loaded (Unity {Application.unityVersion}, playing={EditorApplication.isPlaying})");
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Append($"Play mode: {change} (scene {scene}, frame {Time.frameCount}, focused {UnityEditorInternal.InternalEditorUtility.isApplicationActive})");
    }

    static string Now => DateTime.Now.ToString("HH:mm:ss");

    static void Append(string line)
    {
        try { File.AppendAllText(LogPath, $"[{Now}] {line}\n"); } catch { }
    }

    static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
    {
        var errors = messages.Where(m => m.type == CompilerMessageType.Error).ToArray();
        int warnings = messages.Count(m => m.type == CompilerMessageType.Warning);
        string name = Path.GetFileName(assemblyPath);
        Append($"Compiled {name}: {errors.Length} errors, {warnings} warnings");
        foreach (var m in errors) Append($"  ERROR {m.file}({m.line},{m.column}): {m.message}");
        foreach (var m in messages.Where(x => x.type == CompilerMessageType.Warning && x.file != null && x.file.Contains("Assets/")).Take(30))
            Append($"  warn {m.file}({m.line}): {m.message}");
        try
        {
            File.WriteAllText(StatusPath, $"[{Now}] {name}: {errors.Length} errors\n" + string.Join("\n", errors.Select(e => $"{e.file}({e.line},{e.column}): {e.message}")));
        }
        catch { }
    }

    static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            string stack = string.Join(" | ", (stackTrace ?? "").Split('\n').Take(4));
            Append($"{type}: {condition}  @ {stack}");
        }
        else if (condition != null && condition.StartsWith("[AniGame]"))
        {
            Append(condition.Length > 600 ? condition.Substring(0, 600) : condition);
        }
    }

    static void Poll()
    {
        // While Unity is in the background Play mode does not advance frames; "tick:on" pumps them.
        if (ticking && EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 2.0;
        if (!File.Exists(CommandPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        string command;
        try
        {
            command = File.ReadAllText(CommandPath).Trim();
            File.Delete(CommandPath);
        }
        catch (Exception ex)
        {
            Append("Could not read command: " + ex.Message);
            return;
        }
        if (string.IsNullOrEmpty(command)) return;

        Append($"Command: {command}");
        try
        {
            Run(command);
            Append($"Command done: {command}");
        }
        catch (Exception ex)
        {
            Append($"Command FAILED: {command}: {ex}");
        }
    }

    /// <summary>Renders the scene's main Canvas offscreen (works while Unity is in the background).</summary>
    static void RenderCanvas(string file)
    {
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Append("render: no Canvas in scene"); return; }
        const int w = 1920, h = 1080;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var camGo = new GameObject("BridgeRenderCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        cam.targetTexture = rt;
        camGo.transform.position = new Vector3(0, 0, -100);

        var oldMode = canvas.renderMode;
        var oldCam = canvas.worldCamera;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 50;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Append($"render: wrote {file}");
        }
        finally
        {
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCam;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(camGo);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    /// <summary>ui:Type.Method[:arg] calls a (private) method on the first object of that type in the open scene (play mode testing).</summary>
    static void InvokeUi(string spec)
    {
        string arg = null;
        int colon = spec.IndexOf(':');
        if (colon > 0) { arg = spec.Substring(colon + 1); spec = spec.Substring(0, colon); }
        int dot = spec.LastIndexOf('.');
        if (dot <= 0) { Append("ui: expected Type.Method[:arg]"); return; }
        string typeName = spec.Substring(0, dot), methodName = spec.Substring(dot + 1);
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
        if (type == null) { Append($"ui: type {typeName} not found"); return; }
        var target = UnityEngine.Object.FindFirstObjectByType(type);
        if (target == null) { Append($"ui: no {typeName} in the scene"); return; }
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (var m in type.GetMethods(flags).Where(m => m.Name == methodName))
        {
            var ps = m.GetParameters();
            object[] args;
            if (ps.Length == 0) args = new object[0];
            else if (ps.Length == 1 && arg != null)
            {
                var pt = ps[0].ParameterType;
                if (pt == typeof(string)) args = new object[] { arg };
                else if (pt == typeof(int)) args = new object[] { int.Parse(arg) };
                else if (pt == typeof(PackDefinition)) args = new object[] { Packs.Find(arg) };
                else continue;
            }
            else continue;
            var result = m.Invoke(target, args);
            Append($"ui: {typeName}.{methodName}({arg}) -> {result ?? "ok"}");
            return;
        }
        Append($"ui: no matching {typeName}.{methodName} overload");
    }

    static void Run(string command)
    {
        string arg = "";
        int colon = command.IndexOf(':');
        if (colon > 0)
        {
            arg = command.Substring(colon + 1).Trim();
            command = command.Substring(0, colon).Trim();
        }

        bool playing = EditorApplication.isPlaying;
        switch (command.ToLowerInvariant())
        {
            case "refresh":
                AssetDatabase.Refresh();
                break;
            case "build":
                if (playing) { Append("Stop play mode first."); return; }
                Append(AniGameSetup.BuildEverything());
                break;
            case "scenes":
                if (playing) { Append("Stop play mode first."); return; }
                Append(AniGameSetup.BuildScenes());
                break;
            case "render":
                RenderCanvas(Path.Combine(Dir, (string.IsNullOrEmpty(arg) ? "render" : arg) + ".png"));
                break;
            case "tick":
                ticking = arg != "off";
                Append($"ticking={ticking}");
                break;
            case "status":
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                int rects = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Length;
                Append($"status: scene={scene.name} playing={playing} paused={EditorApplication.isPaused} frame={Time.frameCount} " +
                       $"realtime={Time.realtimeSinceStartup:0.0} timeScale={Time.timeScale} appActive={UnityEditorInternal.InternalEditorUtility.isApplicationActive} rects={rects}");
                break;
            case "settings":
                Append(AniGameSetup.ApplyProjectSettings());
                break;
            case "campaign":
                if (playing) { Append("Stop play mode first."); return; }
                Append(CampaignGenerator.Generate(new CampaignGenerator.Options { downloadImages = arg != "nodownload" }).ToString());
                break;
            case "validate":
                Append(CampaignGenerator.Generate(new CampaignGenerator.Options { validateOnly = true }).ToString());
                break;
            case "database":
                Append(AniGameSetup.BuildCardDatabase());
                break;
            case "simulate":
                Append(AniGameSetup.SimulateBalance());
                break;
            case "ui":
                InvokeUi(arg);
                break;
            case "cheat":
                PlayerProfile.Data.gold += 200000;
                PlayerProfile.Data.diamonds += 5000;
                PlayerProfile.Data.fusionOrbs += 60;
                PlayerProfile.Data.elementOrbs += 10;
                PlayerProfile.Data.talentOrbs += 10;
                PlayerProfile.Save();
                Append($"cheat: gold {PlayerProfile.Data.gold}, diamonds {PlayerProfile.Data.diamonds}");
                break;
            case "newgame":
                PlayerProfile.DeleteSave();
                PlayerProfile.Load();
                Append($"newgame: fresh save, {PlayerProfile.Data.cards.Count} card(s), {PlayerProfile.Data.gold} gold");
                break;
            case "open":
                if (playing) { Append("Stop play mode first."); return; }
                string path = arg.EndsWith(".unity") ? arg : $"Assets/Scenes/{arg}.unity";
                EditorSceneManager.OpenScene(path);
                break;
            case "play":
                EditorApplication.isPlaying = true;
                break;
            case "stop":
                EditorApplication.isPlaying = false;
                break;
            case "shot":
                string file = Path.Combine(Dir, (string.IsNullOrEmpty(arg) ? "screenshot" : arg) + ".png");
                ScreenCapture.CaptureScreenshot(file);
                Append($"Screenshot requested: {file}");
                break;
            default:
                Append($"Unknown command '{command}'");
                break;
        }
    }
}
