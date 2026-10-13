// UNTESTED. Written without access to the game. Target: BepInEx 6 (Unity IL2CPP).
// MiSide x Minecraft: real Minecraft block textures and HUD (hotbar, hearts) are read at runtime
// from the player's own Minecraft Java jar (%APPDATA%\.minecraft\versions\<ver>\<ver>.jar).
// Nothing from Minecraft is shipped inside the mod.
// Controls: E = block menu, 1-9 / mouse wheel = pick slot, right mouse = place,
// left mouse = break your own block, F = light the TNT you look at.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace MiSideMinecraft
{
    [BepInPlugin("com.gorikla34.miside.minecraft", "MiSide x Minecraft", "1.1.0")]
    public class Plugin : BasePlugin
    {
        public static Plugin Instance;

        public override void Load()
        {
            Instance = this;
            ClassInjector.RegisterTypeInIl2Cpp<MCRunner>();
            AddComponent<MCRunner>();
            Log.LogInfo("MiSide x Minecraft 1.1.0 loaded (untested build).");
        }

        public static void Say(string s) { try { Instance.Log.LogInfo(s); } catch { } }
    }

    // ---------- Minecraft assets, read from the player's own game ----------
    public struct Spr
    {
        public Texture2D Tex;
        public Rect Uv;
        public bool Ok { get { return Tex != null; } }
    }

    public static class MC
    {
        public static readonly string[] BlockFiles =
            { "dirt", "stone", "cobblestone", "oak_planks", "glass", "bricks", "sand", "stone_bricks", "tnt_side" };
        public static readonly string[] BlockNames =
            { "Dirt", "Stone", "Cobblestone", "Oak Planks", "Glass", "Bricks", "Sand", "Stone Bricks", "TNT" };
        public const int TntIndex = 8;
        public const int GlassIndex = 4;

        public static Texture2D[] Blocks = new Texture2D[BlockFiles.Length];
        public static Spr Hotbar, Selection, HeartBox, HeartFull;
        public static string Status = "not loaded";

        static string FindJar()
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "versions");
            if (!Directory.Exists(root)) return null;
            string best = null; DateTime bestT = DateTime.MinValue;
            foreach (var dir in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(dir);
                string jar = Path.Combine(dir, name + ".jar");
                if (!File.Exists(jar)) continue;
                var t = File.GetLastWriteTime(jar);
                if (t > bestT) { bestT = t; best = jar; }
            }
            return best;
        }

        static byte[] Read(ZipArchive z, string path)
        {
            var e = z.GetEntry(path);
            if (e == null) return null;
            using (var s = e.Open())
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        static Texture2D Tex(byte[] data)
        {
            if (data == null) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(t, data)) return null;
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // newPath: separate sprite file (Minecraft 1.20.2+). oldPath: atlas region (older versions).
        static Spr Sprite(ZipArchive z, string newPath, string oldPath, int ox, int oy, int w, int h)
        {
            var t = Tex(Read(z, newPath));
            if (t != null) return new Spr { Tex = t, Uv = new Rect(0f, 0f, 1f, 1f) };
            t = Tex(Read(z, oldPath));
            if (t == null) return default(Spr);
            float tw = t.width, th = t.height;
            return new Spr { Tex = t, Uv = new Rect(ox / tw, 1f - (oy + h) / th, w / tw, h / th) };
        }

        public static void Load()
        {
            try
            {
                string jar = FindJar();
                if (jar == null)
                {
                    Status = "Minecraft Java not found in %APPDATA%\\.minecraft\\versions (start Minecraft once)";
                    Plugin.Say(Status);
                    return;
                }
                using (var z = ZipFile.OpenRead(jar))
                {
                    int ok = 0;
                    for (int i = 0; i < BlockFiles.Length; i++)
                    {
                        Blocks[i] = Tex(Read(z, "assets/minecraft/textures/block/" + BlockFiles[i] + ".png"));
                        if (Blocks[i] != null) ok++;
                    }
                    Hotbar = Sprite(z, "assets/minecraft/textures/gui/sprites/hud/hotbar.png",
                                       "assets/minecraft/textures/gui/widgets.png", 0, 0, 182, 22);
                    Selection = Sprite(z, "assets/minecraft/textures/gui/sprites/hud/hotbar_selection.png",
                                          "assets/minecraft/textures/gui/widgets.png", 0, 22, 24, 24);
                    HeartBox = Sprite(z, "assets/minecraft/textures/gui/sprites/hud/heart/container.png",
                                         "assets/minecraft/textures/gui/icons.png", 16, 0, 9, 9);
                    HeartFull = Sprite(z, "assets/minecraft/textures/gui/sprites/hud/heart/full.png",
                                          "assets/minecraft/textures/gui/icons.png", 52, 0, 9, 9);
                    Status = "Minecraft assets from " + Path.GetFileName(jar) + ": " + ok + "/" + BlockFiles.Length +
                             " blocks, hotbar=" + Hotbar.Ok + ", hearts=" + HeartFull.Ok;
                }
                Plugin.Say(Status);
            }
            catch (Exception e)
            {
                Status = "asset load failed: " + e.Message;
                Plugin.Say(Status + "\n" + e);
            }
        }
    }

    // ---------- Input: legacy Input, with a fallback to the new Input System via reflection ----------
    public static class In
    {
        static bool legacyBroken;
        static bool probed;
        static Type kbT, mouseT, keyT;

        static void Probe()
        {
            if (probed) return;
            probed = true;
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name != "Unity.InputSystem") continue;
                    kbT = a.GetType("UnityEngine.InputSystem.Keyboard");
                    mouseT = a.GetType("UnityEngine.InputSystem.Mouse");
                    keyT = a.GetType("UnityEngine.InputSystem.Key");
                }
                Plugin.Say("Input System fallback available: " + (kbT != null && mouseT != null && keyT != null));
            }
            catch (Exception e) { Plugin.Say("Input probe failed: " + e.Message); }
        }

        static bool NewKey(string key)
        {
            try
            {
                Probe();
                if (kbT == null || keyT == null) return false;
                object kb = kbT.GetProperty("current").GetValue(null);
                if (kb == null) return false;
                object k = Enum.Parse(keyT, key);
                var idx = kbT.GetProperty("Item", new Type[] { keyT });
                object ctl = idx.GetValue(kb, new object[] { k });
                return (bool)ctl.GetType().GetProperty("wasPressedThisFrame").GetValue(ctl);
            }
            catch { return false; }
        }

        static bool NewMouse(string button)
        {
            try
            {
                Probe();
                if (mouseT == null) return false;
                object m = mouseT.GetProperty("current").GetValue(null);
                if (m == null) return false;
                object ctl = mouseT.GetProperty(button).GetValue(m);
                return (bool)ctl.GetType().GetProperty("wasPressedThisFrame").GetValue(ctl);
            }
            catch { return false; }
        }

        public static bool E() { return Key(KeyCode.E, "E"); }
        public static bool F() { return Key(KeyCode.F, "F"); }
        public static bool Digit(int i) { return Key((KeyCode)((int)KeyCode.Alpha1 + i), "Digit" + (i + 1)); }

        static bool Key(KeyCode code, string newName)
        {
            if (!legacyBroken)
            {
                try { if (Input.GetKeyDown(code)) return true; }
                catch { legacyBroken = true; Plugin.Say("Legacy Input unavailable, using Input System."); }
            }
            return NewKey(newName);
        }

        public static bool Mouse(int button)
        {
            if (!legacyBroken)
            {
                try { if (Input.GetMouseButtonDown(button)) return true; }
                catch { legacyBroken = true; }
            }
            return NewMouse(button == 0 ? "leftButton" : "rightButton");
        }

        public static float Scroll()
        {
            if (legacyBroken) return 0f;
            try { return Input.GetAxis("Mouse ScrollWheel"); } catch { return 0f; }
        }
    }

    // ---------- The mod itself ----------
    public class Blk
    {
        public GameObject Go;
        public int Kind;
        public bool Lit;
        public float Fuse;
        public int Gx, Gy, Gz;
    }

    public class MCRunner : MonoBehaviour
    {
        public MCRunner(IntPtr ptr) : base(ptr) { }

        const float Reach = 6f, BlastRadius = 3.5f, FuseSeconds = 3f;

        readonly Dictionary<string, Blk> byCell = new Dictionary<string, Blk>();
        readonly Dictionary<int, Blk> byId = new Dictionary<int, Blk>();
        int slot;
        bool menuOpen;
        bool started;
        Camera cam;
        float nextCamSearch, nextMitaSearch;
        Transform mita;

        void Start()
        {
            MC.Load();
            started = true;
        }

        static string Cell(int x, int y, int z) { return x + "," + y + "," + z; }

        Camera Cam()
        {
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy) return cam;
            if (Time.time < nextCamSearch) return cam;
            nextCamSearch = Time.time + 1f;
            Camera best = Camera.main;
            if (best == null)
            {
                foreach (var c in Camera.allCameras)
                    if (c != null && c.enabled && c.targetTexture == null && (best == null || c.depth > best.depth))
                        best = c;
            }
            if (best != cam) Plugin.Say("Using camera: " + (best != null ? best.name : "none"));
            cam = best;
            return cam;
        }

        void Update()
        {
            if (!started) return;

            if (In.E())
            {
                menuOpen = !menuOpen;
                Cursor.visible = menuOpen;
                Cursor.lockState = menuOpen ? CursorLockMode.None : CursorLockMode.Locked;
                Plugin.Say("Block menu " + (menuOpen ? "opened" : "closed"));
            }
            for (int i = 0; i < MC.BlockFiles.Length; i++)
                if (In.Digit(i)) slot = i;
            float wheel = In.Scroll();
            int n = MC.BlockFiles.Length;
            if (wheel > 0f) slot = (slot + n - 1) % n;
            if (wheel < 0f) slot = (slot + 1) % n;

            TickFuses();

            if (menuOpen) return;
            if (In.Mouse(1)) Place();
            if (In.Mouse(0)) BreakLookedAt();
            if (In.F()) Ignite();
        }

        bool Aim(out RaycastHit hit)
        {
            hit = default(RaycastHit);
            var c = Cam();
            if (c == null) return false;
            var hits = Physics.RaycastAll(c.transform.position, c.transform.forward, Reach, -5, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.distance < 0.35f) continue;      // skip the player's own collider
                if (h.distance < best) { best = h.distance; hit = h; found = true; }
            }
            return found;
        }

        void Place()
        {
            RaycastHit hit;
            if (!Aim(out hit)) { Plugin.Say("Place: nothing in reach"); return; }
            Vector3 p = hit.point + hit.normal * 0.5f;
            int gx = Mathf.RoundToInt(p.x), gy = Mathf.RoundToInt(p.y), gz = Mathf.RoundToInt(p.z);
            if (byCell.ContainsKey(Cell(gx, gy, gz))) return;
            var pos = new Vector3(gx, gy, gz);
            var c = Cam();
            if (c != null && Vector3.Distance(pos, c.transform.position) < 0.9f) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "MCBlock_" + MC.BlockNames[slot];
            go.transform.position = pos;
            var r = go.GetComponent<Renderer>();
            var tex = MC.Blocks[slot];
            if (tex != null) r.material.mainTexture = tex;
            else r.material.color = Color.gray;
            if (slot == MC.GlassIndex) SetTransparent(r.material);

            var b = new Blk { Go = go, Kind = slot, Gx = gx, Gy = gy, Gz = gz };
            byCell[Cell(gx, gy, gz)] = b;
            byId[go.GetInstanceID()] = b;
            Plugin.Say("Placed " + MC.BlockNames[slot] + " at " + pos);
            MitaReact(slot == MC.TntIndex ? "tnt_placed" : "built");
        }

        static void SetTransparent(Material m)
        {
            try
            {
                m.SetFloat("_Mode", 1f);                       // cutout: glass has hard transparent pixels
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = 2450;
            }
            catch { }
        }

        Blk Looked()
        {
            RaycastHit hit;
            if (!Aim(out hit)) return null;
            Blk b;
            return byId.TryGetValue(hit.collider.gameObject.GetInstanceID(), out b) ? b : null;
        }

        void Remove(Blk b)
        {
            byCell.Remove(Cell(b.Gx, b.Gy, b.Gz));
            byId.Remove(b.Go.GetInstanceID());
            UnityEngine.Object.Destroy(b.Go);
        }

        void BreakLookedAt()
        {
            var b = Looked();          // only blocks this mod placed, never the house
            if (b != null) Remove(b);
        }

        void Ignite()
        {
            var b = Looked();
            if (b == null || b.Kind != MC.TntIndex || b.Lit) return;
            b.Lit = true;
            b.Fuse = FuseSeconds;
            Plugin.Say("TNT lit");
        }

        void TickFuses()
        {
            List<Blk> boom = null;
            foreach (var b in byId.Values)
            {
                if (!b.Lit) continue;
                b.Fuse -= Time.deltaTime;
                var r = b.Go.GetComponent<Renderer>();
                r.material.color = Mathf.Sin(b.Fuse * 14f) > 0f ? new Color(1f, 0.45f, 0.45f) : Color.white;
                if (b.Fuse <= 0f) { if (boom == null) boom = new List<Blk>(); boom.Add(b); }
            }
            if (boom == null) return;
            foreach (var t in boom) Explode(t);
        }

        void Explode(Blk tnt)
        {
            Vector3 centre = tnt.Go.transform.position;
            var doomed = new List<Blk>();
            foreach (var b in byId.Values)
                if (Vector3.Distance(b.Go.transform.position, centre) <= BlastRadius) doomed.Add(b);
            // ONLY blocks placed by this mod are destroyed. House furniture and walls are untouched.
            foreach (var b in doomed) Remove(b);
            Plugin.Say("TNT exploded, removed " + doomed.Count + " blocks");
            MitaReact("explosion");
        }

        void MitaReact(string what)
        {
            if (mita == null && Time.time > nextMitaSearch)
            {
                nextMitaSearch = Time.time + 5f;
                foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>())
                    if (t.name.ToLower().Contains("mita")) { mita = t; Plugin.Say("Found Mita object: " + t.name); break; }
            }
            var c = Cam();
            if (mita == null || c == null) return;
            var look = c.transform.position - mita.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) mita.rotation = Quaternion.LookRotation(look);
            Plugin.Say("Mita reacts to: " + what);
        }

        // ---------- HUD drawn with Minecraft's own sprites ----------
        static void Draw(Spr s, Rect r)
        {
            if (s.Ok) GUI.DrawTextureWithTexCoords(r, s.Tex, s.Uv);
        }

        void OnGUI()
        {
            if (!started) return;
            float s = Mathf.Max(2f, Mathf.Round(Screen.height / 270f));
            float hx = (Screen.width - 182f * s) / 2f;
            float hy = Screen.height - 22f * s - 2f * s;

            Draw(MC.Hotbar, new Rect(hx, hy, 182f * s, 22f * s));
            for (int i = 0; i < 9 && i < MC.BlockFiles.Length; i++)
            {
                var t = MC.Blocks[i];
                if (t != null) GUI.DrawTexture(new Rect(hx + (3f + i * 20f) * s, hy + 3f * s, 16f * s, 16f * s), t);
            }
            Draw(MC.Selection, new Rect(hx + (slot * 20f - 1f) * s, hy - 1f * s, 24f * s, 24f * s));

            for (int i = 0; i < 10; i++)
            {
                var r = new Rect(hx + i * 8f * s, hy - 11f * s, 9f * s, 9f * s);
                Draw(MC.HeartBox, r);
                Draw(MC.HeartFull, r);
            }

            if (!MC.Hotbar.Ok)
                GUI.Label(new Rect(20, 20, 900, 40), "MiSide x Minecraft: " + MC.Status);

            if (!menuOpen) return;
            float cell = 20f * s, cols = 9f;
            float mx = (Screen.width - cols * cell) / 2f, my = Screen.height / 2f - cell;
            GUI.Box(new Rect(mx - 10f, my - 40f, cols * cell + 20f, cell + 80f), "Blocks (click to pick)");
            for (int i = 0; i < MC.BlockFiles.Length; i++)
            {
                var r = new Rect(mx + i * cell + 2f * s, my + 2f * s, 16f * s, 16f * s);
                if (MC.Blocks[i] != null) GUI.DrawTexture(r, MC.Blocks[i]);
                if (GUI.Button(r, "")) slot = i;
            }
            GUI.Label(new Rect(mx, my + cell + 6f, cols * cell, 30f), MC.BlockNames[slot]);
        }
    }
}
