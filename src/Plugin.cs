// UNTESTED. Written without access to the game. Target: BepInEx 6 (Unity IL2CPP).
// MiSide x Minecraft: hotbar + inventory, block placing, TNT that only breaks your own blocks,
// and simple Mita reactions.
// Controls: E = inventory, 1-5 / mouse wheel = pick slot, right mouse = place,
// left mouse = remove your block, F = ignite the TNT you look at.
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace MiSideMinecraft
{
    [BepInPlugin("com.gorikla34.miside.minecraft", "MiSide x Minecraft", "1.0.0")]
    public class Plugin : BasePlugin
    {
        public static Plugin Instance;

        public override void Load()
        {
            Instance = this;
            ClassInjector.RegisterTypeInIl2Cpp<MCBlock>();
            ClassInjector.RegisterTypeInIl2Cpp<MCRunner>();
            AddComponent<MCRunner>();
            Log.LogInfo("MiSide x Minecraft loaded (untested build).");
        }
    }

    public class MCBlock : MonoBehaviour
    {
        public MCBlock(IntPtr ptr) : base(ptr) { }
        public int Kind;
        public bool Lit;
        public float FuseLeft;
    }

    public class MCRunner : MonoBehaviour
    {
        public MCRunner(IntPtr ptr) : base(ptr) { }

        static readonly string[] Names = { "Dirt", "Stone", "Wood", "Glass", "TNT" };
        static readonly Color[] Colors =
        {
            new Color(0.45f, 0.30f, 0.18f), new Color(0.55f, 0.55f, 0.55f),
            new Color(0.62f, 0.46f, 0.25f), new Color(0.7f, 0.9f, 1f, 0.5f), new Color(0.85f, 0.15f, 0.15f)
        };
        const int TntIndex = 4;
        const float Reach = 6f, BlockSize = 1f, BlastRadius = 3.5f, FuseSeconds = 3f;

        readonly List<GameObject> placed = new List<GameObject>();
        int slot;
        bool inventoryOpen;
        Transform mita;
        float nextMitaSearch;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                inventoryOpen = !inventoryOpen;
                // the game probably locks the cursor; free it while the inventory is open
                Cursor.visible = inventoryOpen;
                Cursor.lockState = inventoryOpen ? CursorLockMode.None : CursorLockMode.Locked;
            }
            for (int i = 0; i < Names.Length; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) slot = i;
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (wheel > 0f) slot = (slot + 1) % Names.Length;
            if (wheel < 0f) slot = (slot + Names.Length - 1) % Names.Length;

            TickFuses();

            if (inventoryOpen || Camera.main == null) return;
            if (Input.GetMouseButtonDown(1)) Place();
            if (Input.GetMouseButtonDown(0)) RemoveLookedAt();
            if (Input.GetKeyDown(KeyCode.F)) IgniteTnt();
        }

        bool Aim(out RaycastHit hit)
        {
            var cam = Camera.main;
            return Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, Reach);
        }

        static Vector3 Snap(Vector3 p) => new Vector3(
            Mathf.Round(p.x / BlockSize) * BlockSize,
            Mathf.Round(p.y / BlockSize) * BlockSize,
            Mathf.Round(p.z / BlockSize) * BlockSize);

        void Place()
        {
            if (!Aim(out var hit)) return;
            var pos = Snap(hit.point + hit.normal * (BlockSize * 0.5f));
            if (Vector3.Distance(pos, Camera.main.transform.position) < 0.9f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "MCBlock_" + Names[slot];
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * BlockSize;
            var r = go.GetComponent<Renderer>();
            r.material.color = Colors[slot];
            var tag = go.AddComponent<MCBlock>();
            tag.Kind = slot;
            placed.Add(go);
            MitaReact(slot == TntIndex ? "tnt_placed" : "built");
        }

        void RemoveLookedAt()
        {
            if (!Aim(out var hit)) return;
            var b = hit.collider.GetComponent<MCBlock>();   // only our blocks, never the house
            if (b == null) return;
            placed.Remove(b.gameObject);
            UnityEngine.Object.Destroy(b.gameObject);
        }

        void IgniteTnt()
        {
            if (!Aim(out var hit)) return;
            var b = hit.collider.GetComponent<MCBlock>();
            if (b == null || b.Kind != TntIndex || b.Lit) return;
            b.Lit = true;
            b.FuseLeft = FuseSeconds;
        }

        // Fuses are handled in Update (no coroutines: simpler and safer under IL2CPP).
        void TickFuses()
        {
            foreach (var go in placed.ToArray())
            {
                if (go == null) { placed.Remove(go); continue; }
                var b = go.GetComponent<MCBlock>();
                if (b == null || !b.Lit) continue;
                b.FuseLeft -= Time.deltaTime;
                var r = go.GetComponent<Renderer>();
                r.material.color = Mathf.Sin(b.FuseLeft * 12f) > 0 ? Color.white : Colors[TntIndex];
                if (b.FuseLeft <= 0f) Explode(go);
            }
        }

        void Explode(GameObject tnt)
        {
            Vector3 centre = tnt.transform.position;
            placed.Remove(tnt);
            UnityEngine.Object.Destroy(tnt);
            // ONLY blocks this mod placed are broken. House furniture and walls are untouched.
            foreach (var go in placed.ToArray())
            {
                if (go == null) { placed.Remove(go); continue; }
                if (Vector3.Distance(go.transform.position, centre) <= BlastRadius)
                {
                    placed.Remove(go);
                    UnityEngine.Object.Destroy(go);
                }
            }
            MitaReact("explosion");
        }

        // Mita reaction placeholder: the real object name/components are unknown, so it searches by name.
        void MitaReact(string what)
        {
            if (mita == null && Time.time > nextMitaSearch)
            {
                nextMitaSearch = Time.time + 5f;
                foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>())
                    if (t.name.ToLower().Contains("mita")) { mita = t; Plugin.Instance.Log.LogInfo("Found Mita object: " + t.name); break; }
            }
            if (mita == null || Camera.main == null) return;
            var look = Camera.main.transform.position - mita.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) mita.rotation = Quaternion.LookRotation(look);
            Plugin.Instance.Log.LogInfo("Mita reacts to: " + what);
        }

        void OnGUI()
        {
            float w = 54f, gap = 6f, total = Names.Length * (w + gap);
            float x0 = (Screen.width - total) / 2f, y = Screen.height - w - 20f;
            for (int i = 0; i < Names.Length; i++)
                DrawSlot(new Rect(x0 + i * (w + gap), y, w, w), i, i == slot);

            if (!inventoryOpen) return;
            GUI.Box(new Rect(Screen.width / 2f - 200, Screen.height / 2f - 90, 400, 180), "Inventory (click to pick)");
            for (int i = 0; i < Names.Length; i++)
            {
                var rect = new Rect(Screen.width / 2f - 185 + i * 74, Screen.height / 2f - 50, 66, 66);
                DrawSlot(rect, i, i == slot);
                if (GUI.Button(rect, "")) slot = i;
            }
        }

        void DrawSlot(Rect r, int i, bool selected)
        {
            var old = GUI.color;
            GUI.color = selected ? Color.white : new Color(1, 1, 1, 0.6f);
            GUI.Box(r, "");
            GUI.color = Colors[i];
            GUI.DrawTexture(new Rect(r.x + 8, r.y + 8, r.width - 16, r.height - 24), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.yMax - 16, r.width, 16), Names[i]);
            GUI.color = old;
        }
    }
}
