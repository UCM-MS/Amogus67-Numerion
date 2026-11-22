"""Create the real Unity WebGL companion project without changing the native HDRP project.

Only first-party gameplay/assets are copied. The licensed Odin/POLYGON source packs are never copied.
The projection is repeatable: rerun after native gameplay changes, before opening the Web project.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil


def method_end(text: str, signature: str) -> tuple[int, int]:
    start = text.index(signature)
    brace = text.index("{", start)
    depth, i, state = 1, brace + 1, "code"
    while i < len(text):
        c, n = text[i], text[i + 1:i + 2]
        if state == "line":
            if c == "\n": state = "code"
        elif state == "block":
            if c == "*" and n == "/": state = "code"; i += 1
        elif state in ("string", "char"):
            if c == "\\": i += 1
            elif c == ('"' if state == "string" else "'"): state = "code"
        elif state == "verbatim":
            if c == '"':
                if n == '"': i += 1
                else: state = "code"
        else:
            if c == "/" and n == "/": state = "line"; i += 1
            elif c == "/" and n == "*": state = "block"; i += 1
            elif c == "@" and n == '"': state = "verbatim"; i += 1
            elif c == '"': state = "string"
            elif c == "'": state = "char"
            elif c == "{": depth += 1
            elif c == "}":
                depth -= 1
                if depth == 0: return start, i + 1
        i += 1
    raise ValueError("Unclosed C# method: " + signature)


def replace_method(text: str, signature: str, replacement: str) -> str:
    start, end = method_end(text, signature)
    return text[:start] + replacement.strip() + text[end:]


WEB_GRAPHICS = '''public void ShowGraphics()
        {
            Open("Akadémia v tvojom prehliadači.", "GRAFIKA / UNITY WEBGL 2");
            Label(body, game.Graphics.LastStatus, 26, Ink);
            Label(body, "Slnečné tiene, normálové mapy, hmla a materiály používajú webový renderer Unity. Zdrojové textúry zostávajú do 4K; predvolená kvalita 2K šetrí pamäť. Vyššia kvalita nezvyšuje rozlíšenie pôvodných obrázkov.", 24, Muted);
            int textureSize = game.Save.TextureSize;
            var textures = ButtonNode(body, TextureLabel(textureSize), null, Cream);
            textures.onClick.AddListener(() => { textureSize = textureSize >= 4096 ? 2048 : 4096; ButtonText(textures).text = TextureLabel(textureSize); });
            Label(body, "FPS nie sú obmedzené na 60. Obraz plánuje prehliadač podľa displeja a výkonu zariadenia; VRR závisí aj od systému a monitora. Rozlíšenie sleduje okno a hustotu pixelov displeja.", 24, Ink);
            Label(body, $"Aktuálny obraz: {Screen.width} × {Screen.height}  ·  WebGL 2", 24, Purple);
            ButtonNode(body, "Použiť a uložiť kvalitu", () => {
                game.Save.TextureSize = textureSize;
                NativePerformance.Apply(game.Save); ProfileStore.Save(game.Save);
                game.Notify("Kvalita uložená · " + TextureLabel(textureSize)); ShowGraphics();
            }, Lavender);
            ButtonNode(body, "Pokračovať v hre", CloseModal, Lime);
        }'''

WEB_VR = '''public void ShowVR()
        {
            Open("Hraj na obrazovke.", "UNITY WEB / OVLÁDANIE");
            Label(body, game.VR.Status, 26, Ink);
            Label(body, "WASD alebo šípky: pohyb. Medzerník: skok. Pravé tlačidlo myši: kamera. E: interakcia. M: mapa. K: učivo. J: denník. Esc: menu. Podporovaný je aj gamepad.", 24, Muted);
            Label(body, "Na dotykovom displeji zapni ovládanie v hornej lište. Ľavá páčka ovláda pohyb, pravá kameru a tlačidlá skok a objavovanie.", 24, Muted);
            ButtonNode(body, "Pokračovať v akadémii", CloseModal, Lime);
        }'''

WEB_MATERIAL = '''public static Material Get(string key, Color color, float metallic = 0, float smoothness = 0.45f, float glow = 0)
        {
            if (Cache.TryGetValue(key, out var existing) && existing) return existing;
            var shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "Lumen Web / " + key, enableInstancing = true };
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            if (glow > 0) { material.SetColor("_EmissionColor", color * glow); material.EnableKeyword("_EMISSION"); }
            Cache[key] = material;
            return material;
        }'''

WEB_SURFACE = '''public static Material GetSurface(string surface, Color tint, float tiles = 1, float metallic = 0, float smoothness = 0.45f)
        {
            string key = "surface-" + surface + "-" + ColorUtility.ToHtmlStringRGB(tint) + "-" + tiles + "-" + metallic + "-" + smoothness;
            if (Cache.TryGetValue(key, out var existing) && existing) return existing;
            var material = Get(key, Color.Lerp(Color.white, tint, surface == "grass" ? 0.4f : 0.66f), metallic, smoothness);
            var albedo = Resources.Load<Texture2D>("Textures/" + surface + "-albedo");
            var normal = Resources.Load<Texture2D>("Textures/" + surface + "-normal");
            var mask = Resources.Load<Texture2D>("Textures/" + surface + "-mask");
            var scale = Vector2.one * Mathf.Max(.5f, tiles);
            if (albedo) { material.mainTexture = albedo; material.mainTextureScale = scale; }
            if (normal) {
                material.SetTexture("_BumpMap", normal); material.SetTextureScale("_BumpMap", scale);
                material.SetFloat("_BumpScale", surface == "grass" ? .35f : .55f); material.EnableKeyword("_NORMALMAP");
            }
            // Our linear HDRP masks also pack Standard's metallic(R), occlusion(G), smoothness(A).
            if (mask) {
                material.SetTexture("_OcclusionMap", mask); material.SetTextureScale("_OcclusionMap", scale);
                material.SetFloat("_OcclusionStrength", .8f);
                material.SetTexture("_MetallicGlossMap", mask); material.SetTextureScale("_MetallicGlossMap", scale);
                material.SetFloat("_GlossMapScale", surface == "grass" ? .65f : .9f);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
            return material;
        }'''

WEB_SAVE = '''public static void Save(GameSave save)
        {
            if (save == null) return;
            SaveNormalization.Normalize(save);
            ProfileRules.Normalize(save.Profile);
            save.Companions ??= new CompanionCollection(); save.Tutorial ??= new AcademyTutorial();
            CompanionRules.Normalize(save.Companions);
            save.Artifacts ??= new ArtifactInventory(); ArtifactRules.Normalize(save.Artifacts);
            save.BattleParty ??= new List<string>();
            save.TextureSize = save.TextureSize >= 4096 ? 4096 : 2048;
            try { WebProfileStorage.Write(SaveNormalization.ToJson(save)); }
            catch (Exception e) { Debug.LogWarning("Uloženie v prehliadači: " + e.Message); }
        }'''

WEB_LOAD = '''public static GameSave Load()
        {
            foreach (string key in new[] { "lumen-unity-web-v1", "lumen-unity-web-v1.backup" })
            {
                if (!PlayerPrefs.HasKey(key)) continue;
                try {
                    var save = SaveNormalization.NormalizeSaveJson(PlayerPrefs.GetString(key));
                    if (save == null || save.Profile == null) continue;
                    ProfileRules.Normalize(save.Profile);
                    save.Collected ??= new List<string>(); save.Purchases ??= new List<string>();
                    save.Companions ??= new CompanionCollection(); save.Tutorial ??= new AcademyTutorial();
                    CompanionRules.Normalize(save.Companions);
                    save.Artifacts ??= new ArtifactInventory(); ArtifactRules.Normalize(save.Artifacts);
                    save.BattleParty ??= new List<string>();
                    if (float.IsNaN(save.Tutorial.Distance) || float.IsInfinity(save.Tutorial.Distance)) save.Tutorial.Distance = 0;
                    save.Tutorial.Distance = Mathf.Clamp(save.Tutorial.Distance, 0, 8);
                    save.TextureSize = save.TextureSize >= 4096 ? 4096 : 2048;
                    return save;
                } catch (Exception e) { Debug.LogWarning("Načítanie uloženia v prehliadači: " + e.Message); }
            }
            return new GameSave { TextureSize = 2048 };
        }'''


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument("--destination", type=Path)
    args = parser.parse_args()
    source = args.source.resolve()
    destination = (args.destination or source.parent / "lumen-unity-web").resolve()
    if not (source / "Assets/Lumen/Runtime/LumenGame.cs").is_file(): raise SystemExit("Not a LUMEN Unity source project")
    if destination.parent != source.parent or destination.name != "lumen-unity-web" or destination == source:
        raise SystemExit("Safety check: destination must be the sibling directory lumen-unity-web")
    templates = source / "Tools/WebPort"
    copied: dict[str, str] = {}

    def write(relative: str, content: str) -> None:
        path = destination / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8", newline="\n")

    def copy(path: Path) -> None:
        relative = path.relative_to(source)
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
        if path.suffix in (".cs", ".json", ".asmdef"):
            copied[relative.as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()

    # Explicit allowlist excludes paid plug-ins, native HDRP/XR settings, Editor scripts, and QA smoke runners.
    for folder in ("Core", "Runtime", "World", "Networking"):
        for path in (source / "Assets/Lumen" / folder).rglob("*"):
            if not path.is_file(): continue
            relative = path.relative_to(source / "Assets/Lumen" / folder)
            if "Tests" in relative.parts or "Smoke" in path.name or path.name.startswith("NativeVRRuntime"): continue
            if path.suffix in (".cs", ".meta", ".asmdef"): copy(path)
    for folder in ("Content", "Textures", "Fonts"):
        for path in (source / "Assets/Lumen/Resources" / folder).rglob("*"):
            if path.is_file(): copy(path)
    for name in ("Explorer.prefab", "Explorer.prefab.meta"):
        copy(source / "Assets/Lumen/Resources/Generated" / name)

    world_path = "Assets/Lumen/World/WorldTypes.cs"
    world = (destination / world_path).read_text(encoding="utf-8-sig")
    world = replace_method(world, "public static Material Get(", WEB_MATERIAL)
    world = replace_method(world, "public static Material GetSurface(", WEB_SURFACE)
    world = world.replace("renderer.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;", "// Browser text is a regular transparent forward pass.")
    write(world_path, world)
    game_path = "Assets/Lumen/Runtime/LumenGame.cs"
    game = (destination / game_path).read_text(encoding="utf-8-sig")
    game = game.replace("if (!headless && !Application.isEditor && Display.main.systemWidth > 0)", "if (!headless && !Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer && Display.main.systemWidth > 0)")
    write(game_path, game)
    profile_path = "Assets/Lumen/Runtime/ProfileStore.cs"
    profile = (destination / profile_path).read_text(encoding="utf-8-sig")
    profile = replace_method(profile, "public static GameSave Load()", WEB_LOAD)
    profile = replace_method(profile, "public static void Save(GameSave save)", WEB_SAVE)
    write(profile_path, profile)
    session_path = "Assets/Lumen/Networking/NetworkSession.cs"
    session = (destination / session_path).read_text(encoding="utf-8-sig")
    unavailable = '{ SetStatus("Priamy UDP/LAN režim prehliadač nepodporuje. V menu Hrať spolu použi spoločné HTTPS miestnosti pre PC aj web."); return false; }'
    for signature in ("public bool StartHost(", "public bool Join(", "public bool StartDedicatedServer("):
        start, end = method_end(session, signature)
        body = session.index("{", start)
        session = session[:body] + unavailable + session[end:]
    write(session_path, session)
    hud_path = "Assets/Lumen/Runtime/LumenHud.cs"
    hud = (destination / hud_path).read_text(encoding="utf-8-sig")
    # Keep LumenHud.Online.cs unchanged: both players use the same HTTPS rooms and cloud UI.
    for signature, replacement in (("public void ShowGraphics()", WEB_GRAPHICS), ("public void ShowVR()", WEB_VR)):
        hud = replace_method(hud, signature, replacement)
    hud = hud.replace("HDRP · ray tracing · path tracing · 4K · FPS", "WebGL 2 · tiene · textúry 2K / 4K · výkon")
    hud = hud.replace("Voliteľné PC VR · OpenXR headset a ovládače", "Klávesnica · myš · gamepad · dotykové ovládanie")
    hud = hud.replace('MenuCard("Virtuálna realita",', 'MenuCard("Ovládanie a PC VR",')
    hud = hud.replace('ButtonNode(options, "PC VR", ShowVR,', 'ButtonNode(options, "Ovládanie", ShowVR,')
    hud = hud.replace('ButtonNode(row, "Hrať s priateľmi", ShowMultiplayer,', 'ButtonNode(row, "Online svet", ShowMultiplayer,')
    hud = hud.replace('ButtonNode(body, "Ukončiť hru", () => { ProfileStore.Save(game.Save); Application.Quit(); }, Cream, Muted, 22);', 'ButtonNode(body, "Uložiť a otvoriť pôvodný svet", () => { ProfileStore.Save(game.Save); Application.OpenURL("/classic"); }, Cream, Muted, 22);')
    write(hud_path, hud)

    for name, target in {
        "GraphicsController.cs": "Assets/Lumen/Graphics/GraphicsController.cs",
        "NativePerformance.cs": "Assets/Lumen/Runtime/NativePerformance.cs",
        "WorldText.shader": "Assets/Lumen/Graphics/WorldText.shader",
        "NativeVRRuntime.cs": "Assets/Lumen/Runtime/NativeVRRuntime.cs",
        "WebPlatform.cs": "Assets/Lumen/Runtime/WebPlatform.cs",
        "WebTouchControls.cs": "Assets/Lumen/Runtime/WebTouchControls.cs",
        "LumenWeb.jslib": "Assets/Plugins/WebGL/LumenWeb.jslib",
        "LumenWebBuilder.cs": "Assets/Lumen/Editor/LumenWebBuilder.cs",
        "link.xml": "Assets/Lumen/link.xml",
        "index.html": "Assets/WebGLTemplates/Lumen/index.html",
    }.items(): write(target, (templates / name).read_text(encoding="utf-8-sig"))

    manifest = json.loads((source / "Packages/manifest.json").read_text(encoding="utf-8-sig"))
    for name in list(manifest["dependencies"]):
        if "render-pipelines" in name or name.startswith("com.unity.xr.") or name == "com.unity.test-framework": del manifest["dependencies"][name]
    write("Packages/manifest.json", json.dumps(manifest, indent=2) + "\n")
    copy(source / "ProjectSettings/ProjectVersion.txt")
    write(".gitignore", "[Ll]ibrary/\n[Tt]emp/\n[Oo]bj/\n[Ll]ogs/\n[Uu]ser[Ss]ettings/\nBuilds/\nArtifacts/\n*.csproj\n*.sln\n")
    version = next(line.split(":", 1)[1].strip() for line in (source / "ProjectSettings/ProjectVersion.txt").read_text().splitlines() if line.startswith("m_EditorVersion:"))
    write("WEB-PROJECTION.json", json.dumps({"source": str(source), "engine": version, "renderer": "Built-in Forward", "target": "WebGL 2", "network": "shared HTTPS rooms, co-op and authoritative pet battles; native UDP unavailable", "save": "PlayerPrefs / IndexedDB local backup plus opt-in shared cloud profile", "textureMaxSize": 4096, "defaultTextureSize": 2048, "frameScheduling": "browser requestAnimationFrame; no fixed 60 FPS cap", "paidSourceAssetsIncluded": False, "sourceHashes": copied}, indent=2) + "\n")
    write("README.md", f"# LUMEN Unity Web\n\nReal Unity WebGL projection of the native game. Shared C# includes grades 7–9 mathematics, ten companions, turn-based battles, artifacts, tutorial, menus and character physics. PC and web use the same HTTPS rooms and cloud profile API. Native HDRP is replaced by Built-in Forward for WebGL 2.\n\nBuild with Unity {version} plus Web Build Support:\n\n```powershell\nUnity.exe -batchmode -nographics -quit -projectPath <this-project> -buildTarget WebGL -executeMethod Lumen.Editor.LumenWebBuilder.Build -logFile <log-path>\n```\n\nOutput: `Builds/WebGL/`. Serve over HTTP(S), not file://. Gzip fallback is enabled. Textures retain up to 4096 px, with default 2K quality and selectable 4K. Browser animation scheduling follows the display without a fixed 60 FPS cap.\n\nLocal saves use IndexedDB; linking a private cloud transfer code moves the shared profile between PC and web. Configure the same HTTPS origin in both clients. Direct UDP, native DXR/path tracing and PC OpenXR are platform-specific. Optional paid native decorative prefabs are excluded; first-party world geometry and gameplay remain shared. The OG game remains at `/classic`. No paid source packs are copied.\n")
    print(json.dumps({"project": str(destination), "copiedSourceFiles": len(copied), "buildMethod": "Lumen.Editor.LumenWebBuilder.Build", "renderPipeline": "Built-in Forward"}))


if __name__ == "__main__": main()
