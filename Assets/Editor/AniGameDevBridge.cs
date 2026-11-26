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
///   ui:Type.Method[:arg] | cheat | newgame | buff:all|none|id | character:Name|gallery:KEY   (testing helpers)
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

    // Multi-line command files run line by line; "wait:0.3" pauses the queue (seconds, real time).
    static readonly System.Collections.Generic.Queue<string> queue = new System.Collections.Generic.Queue<string>();
    static double queueReadyAt;

    static void Poll()
    {
        // While Unity is in the background Play mode does not advance frames; "tick:on" pumps them.
        if (ticking && EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        RunQueue();
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
        foreach (var line in command.Split('\n'))
            if (line.Trim().Length > 0) queue.Enqueue(line.Trim());
        RunQueue();
    }

    static void RunQueue()
    {
        while (queue.Count > 0 && EditorApplication.timeSinceStartup >= queueReadyAt)
        {
            string line = queue.Dequeue();
            if (line.StartsWith("wait:"))
            {
                double.TryParse(line.Substring(5), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double secs);
                queueReadyAt = EditorApplication.timeSinceStartup + secs;
                continue;
            }
            Execute(line);
        }
    }

    static void Execute(string command)
    {
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

    /// <summary>Renders every root Canvas (scene + effect overlays, in sort order) offscreen
    /// (works while Unity is in the background).</summary>
    static void RenderCanvas(string file, int w = 1920, int h = 1080)
    {
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(c => c != null && c.isRootCanvas && c.isActiveAndEnabled)
            .OrderBy(c => c.sortingOrder).ToList();
        if (canvases.Count == 0) { Append("render: no Canvas in scene"); return; }
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var camGo = new GameObject("BridgeRenderCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        cam.targetTexture = rt;
        camGo.transform.position = new Vector3(0, 0, -100);

        var old = canvases.Select(c => (c, c.renderMode, c.worldCamera, c.planeDistance)).ToList();
        try
        {
            for (int i = 0; i < canvases.Count; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = cam;
                canvases[i].planeDistance = 50 - i * 5;
            }
            // Let the canvas scalers adapt to this render size (protected CanvasScaler.Handle)
            var handle = typeof(UnityEngine.UI.CanvasScaler).GetMethod("Handle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (var c in canvases)
            {
                var ad = c.GetComponent<AdaptiveCanvasScaler>();
                if (ad != null) ad.Apply();
                var sc = c.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (sc != null && handle != null) handle.Invoke(sc, null);
            }
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
            Append($"render: wrote {file} ({canvases.Count} canvas)");
        }
        finally
        {
            foreach (var o in old)
            {
                if (o.c == null) continue;
                o.c.renderMode = o.renderMode;
                o.c.worldCamera = o.worldCamera;
                o.c.planeDistance = o.planeDistance;
            }
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(camGo);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    /// <summary>
    /// talenttest:PlayerTalents|EnemyTalents[|Rarity][|battles] - headless battles between two equal cards
    /// (average Roll, Neutral) with the given talents. Reports results, rounds and the first battle's log.
    /// </summary>
    static string TalentTest(string spec)
    {
        var db = CardDatabase.Instance;
        if (db == null) return "talenttest: no database";
        var parts = (spec ?? "").Split('|');
        var rarity = parts.Length > 2 && Enum.TryParse(parts[2], true, out CardRarity r) ? r : CardRarity.God;
        int n = parts.Length > 3 && int.TryParse(parts[3], out int c) ? c : 30;
        int pw = 0, ew = 0, dr = 0, rounds = 0, maxRounds = 0, events = 0;
        var sb = new System.Text.StringBuilder();
        CardRuntime Make(string names)
        {
            var inst = new CardInstance { uid = 1, rarity = rarity, build = new BuildData { element = CardElement.Neutral } };
            var rt = CardRuntime.ForBuild(inst, inst.build);
            rt.talents.Clear();
            rt.talent = null;
            foreach (var name in (names ?? "").Split(','))
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var t = db.FindTalent(name.Trim());
                if (t == null) sb.AppendLine($"  unknown talent '{name}'");
                rt.AddTalent(t);
            }
            return rt;
        }
        for (int i = 0; i < n; i++)
        {
            var p = Make(parts[0]);
            var e = Make(parts.Length > 1 ? parts[1] : "");
            var engine = new BattleEngine(p, e, i + 1);
            var st = engine.RunToEnd();
            if (st == BattleState.PlayerWon) pw++; else if (st == BattleState.EnemyWon) ew++; else dr++;
            rounds += engine.Round; maxRounds = Mathf.Max(maxRounds, engine.Round); events += engine.Events.Count;
            if (i == 0)
                foreach (var ev in engine.Events.Take(140))
                    if (!string.IsNullOrEmpty(ev.text) && ev.type != BattleEventType.TurnStart)
                        sb.AppendLine("    " + System.Text.RegularExpressions.Regex.Replace(ev.text, "<.*?>", ""));
        }
        return $"talenttest {spec}: player {pw} / enemy {ew} / draw {dr} of {n}, avg rounds {rounds / (float)n:0.0} (max {maxRounds}), avg events {events / n}\n" + sb;
    }

    /// <summary>
    /// teamsim:battles[|attackScale][|avg 0/1][|slots] - 3v3 simulation. slots = 6 comma-separated entries
    /// "Active/Passive@Rarity" (any part may be empty = random). Full report in Logs/AniGameBridge/teamsim.txt.
    /// </summary>
    static string TeamSim(string spec)
    {
        var p = (spec ?? "").Split('|');
        var s = new TeamSimSettings();
        if (p.Length > 0 && int.TryParse(p[0], out int n)) s.battles = n;
        if (p.Length > 1 && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sc)) s.attackScale = sc;
        if (p.Length > 2) s.averageRolls = p[2] == "1";
        if (p.Length > 3)
        {
            var slots = p[3].Split(',');
            for (int i = 0; i < 6 && i < slots.Length; i++)
            {
                var x = slots[i];
                var at = x.Split('@');
                if (at.Length > 1 && Enum.TryParse(at[1], true, out CardRarity r)) { s.slots[i].setRarity = true; s.slots[i].rarity = r; }
                var tl = at[0].Split('/');
                s.slots[i].talent = tl[0].Trim();
                if (tl.Length > 1) s.slots[i].talent2 = tl[1].Trim();
            }
        }
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var report = TeamSimulator.Run(s);
        var text = report.Summary() + "\n" + report.sampleLog;
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(StatusPath), "teamsim.txt"), System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", ""));
        return $"teamsim {spec} ({watch.ElapsedMilliseconds} ms)\n" + System.Text.RegularExpressions.Regex.Replace(report.Summary(false), "<.*?>", "");
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
            {
                // render:name or render:name@WIDTHxHEIGHT (e.g. render:gacha@2130x1000 to check wide windows)
                string rname = string.IsNullOrEmpty(arg) ? "render" : arg;
                int rw = 1920, rh = 1080;
                int at = rname.IndexOf('@');
                if (at > 0)
                {
                    var dims = rname.Substring(at + 1).Split('x');
                    if (dims.Length == 2) { int.TryParse(dims[0], out rw); int.TryParse(dims[1], out rh); }
                    rname = rname.Substring(0, at);
                }
                RenderCanvas(Path.Combine(Dir, rname + ".png"), Mathf.Clamp(rw, 320, 4096), Mathf.Clamp(rh, 240, 4096));
                break;
            }
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
            case "savetest":
            {
                // Round trip: export, check, import the same file, compare
                var before = JsonUtility.ToJson(PlayerProfile.Data);
                string saveErr = SaveTransfer.Export(null, out string exportFile);
                if (saveErr != null) { Append("savetest export: " + saveErr); break; }
                saveErr = SaveTransfer.Peek(exportFile, out var info);
                Append($"savetest: exported {exportFile} ({new FileInfo(exportFile).Length / 1024} KB) peek={saveErr ?? "ok"} | {System.Text.RegularExpressions.Regex.Replace(SaveTransfer.Describe(info), "<.*?>", "")}");
                saveErr = SaveTransfer.Import(exportFile, out string warn);
                var after = JsonUtility.ToJson(PlayerProfile.Data);
                Append($"savetest: import={saveErr ?? "ok"} warning={warn ?? "none"} identical={before == after} cards={PlayerProfile.Data.cards.Count} character={PlayerProfile.CharacterName}");
                break;
            }
            case "art":
                if (playing) { Append("Stop play mode first."); return; }
                Append(AniGameSetup.GenerateUiArt());
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
            case "buff":
                if (arg == "all") { PlayerProfile.Data.buffs = Buffs.All.Select(b => b.id).ToList(); }
                else if (arg == "none") { PlayerProfile.Data.buffs.Clear(); }
                else if (Buffs.Find(arg) != null && !PlayerProfile.Has(arg)) PlayerProfile.Data.buffs.Add(arg);
                PlayerProfile.Save();
                Append($"buffs: {string.Join(", ", PlayerProfile.Data.buffs)}");
                break;
            case "character":
                // character:Name|gallery:KEY  (testing helper)
                var parts = arg.Split('|');
                Append("character: " + (PlayerProfile.SetCharacter(parts[0], parts.Length > 1 ? parts[1] : "gallery:L1F1") ?? "ok"));
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
            case "rect":
            {
                // rect:ObjectName - logs a UI element's RectTransform (debug layout in play mode)
                foreach (var t in UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t.name != arg) continue;
                    var corners = new Vector3[4];
                    t.GetWorldCorners(corners);
                    var comps = string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name));
                    Append($"rect {t.name}: active={t.gameObject.activeInHierarchy} pos={t.anchoredPosition} size={t.sizeDelta} pivot={t.pivot} aMin={t.anchorMin} aMax={t.anchorMax} scale={t.localScale} world0={corners[0]} world2={corners[2]} parent={t.parent?.name} comps={comps}");
                }
                break;
            }
            case "talenttest":
                Append(TalentTest(arg));
                break;
            case "teamsim":
                Append(TeamSim(arg));
                break;
            case "talentsync":
                Append(TalentDescriptionSync.SyncAll());
                break;
            case "talentdesc":
            {
                // talentdesc[:Name] - prints the description of one talent (or all)
                foreach (string guid in AssetDatabase.FindAssets("t:Talent"))
                {
                    var t = AssetDatabase.LoadAssetAtPath<Talent>(AssetDatabase.GUIDToAssetPath(guid));
                    if (t == null || (!string.IsNullOrEmpty(arg) && t.DisplayName != arg)) continue;
                    Append($"[{(t.IsActive ? "A" : "P")}] {t.DisplayName} (auto={t.autoDescription}, synced={t.DescriptionInSync}): {t.Description}");
                    if (!string.IsNullOrEmpty(arg)) { Append("  Common: " + t.DescriptionFor(CardRarity.Common)); Append("  God: " + t.DescriptionFor(CardRarity.God)); }
                }
                break;
            }
            case "quality":
            {
                // quality[:index] - shows the quality levels, or sets the editor's active one and saves the project settings
                var names = QualitySettings.names;
                if (int.TryParse(arg, out int q) && q >= 0 && q < names.Length)
                {
                    QualitySettings.SetQualityLevel(q, true);
                    AssetDatabase.SaveAssets();
                    EditorApplication.ExecuteMenuItem("File/Save Project");
                }
                Append($"quality: {QualitySettings.GetQualityLevel()} = {names[QualitySettings.GetQualityLevel()]} (texture limit {QualitySettings.globalTextureMipmapLimit}, AA {QualitySettings.antiAliasing}) of [{string.Join(", ", names)}]");
                break;
            }
            case "statuspreview":
            {
                // statuspreview[:off] - shows a test card with many buffs/debuffs on the open canvas (no save changes)
                var old = GameObject.Find("StatusPreview");
                if (old != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(old); else UnityEngine.Object.DestroyImmediate(old); }
                if (arg == "off") { Append("statuspreview: removed"); break; }
                var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).FirstOrDefault(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace);
                var card = PlayerProfile.Data.cards.FirstOrDefault();
                if (canvas == null || card == null) { Append("statuspreview: no canvas or card"); break; }
                var view = CardView.Build(canvas.transform, "StatusPreview");
                var rt = CardRuntime.ForBuild(card, card.build);
                rt.silenceTurns = 2; rt.burnStacks = 2; rt.healingReduction = 0.75f; rt.poisonStacks = 1; rt.blindTurns = 1; rt.hypnoStacks = 1;
                rt.defense = rt.baseDefense / 2; rt.attack = rt.baseAttack * 3 / 2;
                rt.shield = 400; rt.shieldTurns = 2; rt.regenerationStacks = 2; rt.hasEndurance = true; rt.enduranceTurns = 3; rt.lifesteal = 20; rt.frozen = arg == "frozen";
                view.Bind(rt);
                view.SetVitals(rt.maxHealth * 6 / 10, rt.maxHealth, 40, 100, true, rt.maxHealth / 4); // HP 60% + a 25% barrier
                if (!Application.isPlaying) foreach (var child in view.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.DontSave; // never saved into the scene
                ((RectTransform)view.transform).anchoredPosition = Vector2.zero;
                view.transform.SetAsLastSibling();
                Append($"statuspreview: shown ({rt.StatusSummary()})");
                break;
            }
            case "matrixtry":
                // matrixtry:Talent.field=value;... - talent matrix with numbers changed in memory only (restored after)
                Append(FloorCheck.MatrixTry(arg));
                break;
            case "floorcheck":
                // floorcheck[:Talent1,Talent2] - the slot cards vs every campaign floor (headless): casts and full-bar turns with no talent
                Append(FloorCheck.Run(arg));
                break;
            case "talentmatrix":
            {
                // talentmatrix[:battlesPerPair][|Rarity] - every talent vs every talent; CSVs in Logs/AniGameBridge/TalentMatrix
                var tp = (arg ?? "").Split('|');
                var ms = new TalentMatrixSettings();
                if (tp.Length > 0 && int.TryParse(tp[0], out int per)) ms.battlesPerPair = per;
                if (tp.Length > 1 && Enum.TryParse(tp[1], true, out CardRarity mr)) ms.rarity = mr;
                var mres = TalentMatrix.Run(ms);
                TalentMatrixWindow.Export(mres, Path.Combine(Path.GetDirectoryName(StatusPath), "TalentMatrix"));
                Append(mres.SummaryText(12));
                break;
            }
            case "hiddentest":
                // hiddentest[:on|off] - the Test Mode switch for the Hidden Location (PlayerPrefs, not the save)
                if (arg == "on" || arg == "off") PlayerProfile.HiddenTestMode = arg == "on";
                Append($"hiddentest: {(PlayerProfile.HiddenTestMode ? "on" : "off")}");
                break;
            case "hiddengen":
                // hiddengen[:refetch] - builds the Hidden Location (fetches characters from AniList the first time)
                Append(HiddenLocationGenerator.Generate(arg == "refetch"));
                break;
            case "hiddensim":
            {
                // hiddensim[:battlesPerFloor] - every Hidden Location floor vs random teams of 3 max-stat God cards
                var hs = new HiddenSimSettings();
                if (int.TryParse(arg, out int hb)) hs.battlesPerFloor = hb;
                var hr = HiddenSimulator.Run(hs);
                string dir = Path.Combine(Path.GetDirectoryName(StatusPath), "HiddenSim");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "hidden_floors.csv"), hr.FloorsCsv(), new System.Text.UTF8Encoding(true));
                File.WriteAllText(Path.Combine(dir, "hidden_talents.csv"), hr.TalentsCsv(), new System.Text.UTF8Encoding(true));
                File.WriteAllText(Path.Combine(dir, "hidden_samples.csv"), hr.SamplesCsv(), new System.Text.UTF8Encoding(true));
                Append(hr.SummaryText());
                break;
            }
            case "menu":
                Append(EditorApplication.ExecuteMenuItem(arg) ? $"menu: {arg}" : $"menu: '{arg}' not found");
                break;
            case "click":
            {
                // click:GameObjectName - presses the first active Button with that name (play mode testing)
                var btn = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == arg && b.isActiveAndEnabled);
                if (btn == null) Append($"click: no active button '{arg}'");
                else { btn.onClick.Invoke(); Append($"click: {arg}"); }
                break;
            }
            case "testbattle":
            {
                // testbattle:PlayerTalents|EnemyTalents (play mode): fights the next floor with those talents
                var tp = (arg ?? "").Split('|');
                GameSession.TestPlayerTalents = tp[0];
                GameSession.TestEnemyTalents = tp.Length > 1 ? tp[1] : null;
                GameSession.StartBattle(PlayerProfile.HighestUnlockedLocation, PlayerProfile.NextFloor(PlayerProfile.HighestUnlockedLocation));
                Append($"testbattle: {arg}");
                break;
            }
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
