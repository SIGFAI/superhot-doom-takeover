// SUPERHOT's red crystal men become Doom demons: imps with spikes and fire in their hands, zombiemen, and floating
// cacodemons. Every one keeps the game's own AI, hitboxes, bullet-time and shattering; some take several hits.
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Sigf.Kit;
using UnityEngine;

public enum Kind { Imp, Zombie, Caco }

public class DoomMonster : MonoBehaviour
{
    public Kind kind;
    public int hp = 1;
    public float nextHit;
    public Transform caco;
    public float baseY;
    public readonly List<Renderer> parts = new List<Renderer>();
    readonly Dictionary<Renderer, Material[]> original = new Dictionary<Renderer, Material[]>();
    public static readonly List<DoomMonster> Live = new List<DoomMonster>();
    void OnEnable() { Live.Add(this); }
    void OnDisable() { Live.Remove(this); }
    public static List<DoomMonster> Cacos()
    {
        var l = new List<DoomMonster>();
        foreach (var m in Live) if (m != null && m.kind == Kind.Caco && m.caco != null && !m.GetComponent<PejAiController>().IsDead) l.Add(m);
        return l;
    }
    public DoomBodySkin skin;
    public bool flashing { get => skin != null && skin.flashing; set { if (skin != null) skin.flashing = value; } }
    public Dictionary<Renderer, Material> enforce => skin.enforce;
    float phase, nextGrowl;
    public Transform fire;

    public void Track(Renderer r) { parts.Add(r); original[r] = r.sharedMaterials; }

    public void Reapply() { if (skin != null) skin.Reapply(); }

    void Update()
    {
        phase += Time.deltaTime;
        if (caco != null)
        {
            var p = G.Pos - transform.position; p.y = 0;
            if (p.sqrMagnitude > 0.01f) caco.rotation = Quaternion.Slerp(caco.rotation, Quaternion.LookRotation(p), 6f * Time.deltaTime);
            var lp = caco.localPosition; lp.y = baseY + Mathf.Sin(phase * 2f) * 0.12f; caco.localPosition = lp;
        }
        if (fire != null) fire.localScale = Vector3.one * (0.9f + 0.2f * Mathf.Sin(phase * 14f));
        if (Time.unscaledTime > nextGrowl)
        {
            nextGrowl = Time.unscaledTime + Random.Range(6f, 12f);
            if (kind != Kind.Zombie && Vector3.Distance(transform.position, G.Pos) < 22f)
                D.Sfx(Random.value < 0.5f ? "growl1" : "growl2", transform.position, 0.6f, kind == Kind.Caco ? 0.6f : 1f);
        }
    }

    public void Pain()
    {
        StartCoroutine(FlashWhite());
    }

    IEnumerator FlashWhite()
    {
        var white = D.Solid(new Color(1f, 0.95f, 0.8f));
        flashing = true;
        foreach (var r in parts) if (r != null) r.sharedMaterial = white;
        yield return new WaitForSecondsRealtime(0.14f);
        flashing = false;
        foreach (var r in parts) if (r != null && original.TryGetValue(r, out var ms)) r.sharedMaterials = ms;
    }
}

/// <summary>Lives on the enemy's body, which outlives the controller (the ragdoll and shards stay Doom coloured).</summary>
public class DoomBodySkin : MonoBehaviour
{
    public Kind kind;
    public bool flashing;
    public readonly Dictionary<Renderer, Material> enforce = new Dictionary<Renderer, Material>();
    public static readonly List<DoomBodySkin> All = new List<DoomBodySkin>();
    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public Transform socket;
    public readonly List<LineRenderer> lines = new List<LineRenderer>();
    float lastHide;

    public void Reapply()
    {
        if (socket != null && Time.unscaledTime - lastHide > 0.3f)
        {
            lastHide = Time.unscaledTime;
            foreach (var r in socket.GetComponentsInChildren<Renderer>(true)) if (r.enabled) r.enabled = false;
        }
        foreach (var l in lines) if (l != null && l.enabled) l.enabled = false;
        foreach (var kv in enforce)
        {
            var r = kv.Key;
            if (r == null) continue;
            if (r.sharedMaterial != kv.Value) r.sharedMaterial = kv.Value;
            if (kind == Kind.Caco && r is SkinnedMeshRenderer && r.enabled) r.enabled = false;
        }
    }

    static bool hooked;
    /// <summary>The game repaints its enemies red from its own scripts: put the Doom skin back right before each camera culls.</summary>
    public static void HookRender()
    {
        if (hooked) return;
        hooked = true;
        Camera.onPreCull += cam =>
        {
            FireTrailPatch.Paint();
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var m = All[i];
                if (m == null) { All.RemoveAt(i); continue; }
                if (!m.flashing) m.Reapply();
            }
        };
    }
}

public static class Monsters
{

    static int counter;
    /// <summary>Kinds the next skinned enemies must take (the demo asks for a precise cast).</summary>
    public static readonly Queue<Kind> Wanted = new Queue<Kind>();
    static readonly Kind[] cycle = { Kind.Imp, Kind.Caco, Kind.Zombie, Kind.Imp, Kind.Zombie, Kind.Caco };

    public static void Scan()
    {
        foreach (var e in G.Enemies())
        {
            if (e == null || e.GetComponent<DoomMonster>() != null || e.enemyBody == null) continue;
            Skin(e, Wanted.Count > 0 ? Wanted.Dequeue() : cycle[counter++ % cycle.Length]);
        }
    }

    static bool IsBodyRenderer(Renderer r) => r is SkinnedMeshRenderer || (r is MeshRenderer && r.name.StartsWith("m."));

    public static void Skin(PejAiController e, Kind kind)
    {
        var dm = e.gameObject.AddComponent<DoomMonster>();
        dm.kind = kind;
        dm.hp = kind == Kind.Imp ? 2 : kind == Kind.Caco ? 4 : 1;
        D.Total++;
        var body = e.enemyBody.transform;
        var bs = body.gameObject.AddComponent<DoomBodySkin>();
        bs.kind = kind;
        // the debug aim lines of the enemy AI draw long red lasers: off
        bs.lines.AddRange(e.GetComponentsInChildren<LineRenderer>(true));
        dm.skin = bs;
        string skin = kind == Kind.Imp ? "skin_imp.png" : kind == Kind.Zombie ? "skin_zombie.png" : "skin_caco.png";
        foreach (var r in body.GetComponentsInChildren<Renderer>(true))
        {
            if (!IsBodyRenderer(r)) continue;
            bool head = r.name.Contains("head");
            var m = kind == Kind.Zombie && head ? D.Unlit("skin_face.png", 1f) : D.Unlit(skin, kind == Kind.Caco ? 1f : 1f);
            r.sharedMaterial = m;
            dm.enforce[r] = m;
            if (r is SkinnedMeshRenderer)
            {
                if (kind == Kind.Caco) r.enabled = false;
                else dm.Track(r);
            }
        }
        var rig = body.Find("rig") ?? body;
        // imps and cacodemons throw fire: no pistol in their hands
        if (kind != Kind.Zombie) bs.socket = D.FindBone(rig, "PistolSocket");
        if (kind == Kind.Imp) BuildImp(dm, e, rig);
        else if (kind == Kind.Zombie) BuildZombie(dm, e, rig);
        else BuildCaco(dm, e);
    }

    static void BuildImp(DoomMonster dm, PejAiController e, Transform rig)
    {
        var head = D.FindBone(rig, "head");
        var spine = D.FindBone(rig, "chest", "spine");
        var up = Vector3.up; var fwd = e.transform.forward; var right = e.transform.right;
        var horn = D.Unlit("skin_imp.png", 1f);
        var eye = D.Solid(new Color(1f, 0.9f, 0.1f));
        if (head != null)
        {
            var c = head.position + up * 0.1f;
            D.Part(PrimitiveType.Cube, head, c + fwd * 0.12f + up * 0.04f + right * 0.05f, new Vector3(0.07f, 0.04f, 0.04f), eye, "ImpEye");
            D.Part(PrimitiveType.Cube, head, c + fwd * 0.12f + up * 0.04f - right * 0.05f, new Vector3(0.07f, 0.04f, 0.04f), eye, "ImpEye");
            D.Spike(head, c + up * 0.1f + right * 0.07f, c + up * 0.36f + right * 0.15f, 0.045f, horn);
            D.Spike(head, c + up * 0.1f - right * 0.07f, c + up * 0.36f - right * 0.15f, 0.045f, horn);
        }
        if (spine != null)
        {
            for (int i = -1; i <= 1; i++)
                D.Spike(spine, spine.position + right * (0.2f * i) - fwd * 0.1f + up * 0.1f, spine.position + right * (0.28f * i) - fwd * 0.3f + up * 0.28f, 0.07f, horn);
        }
        var hand = D.FindBone(rig, "righthand", "hand.r", "rhand", "forearm.r", "rightforearm");
        if (hand != null)
        {
            var f = D.Part(PrimitiveType.Sphere, hand, hand.position + fwd * 0.1f, Vector3.one * 0.2f, D.Unlit("fire.png", 1f), "ImpFire");
            dm.fire = f.transform;
            var l = Mix.Glow(f.transform.position, new Color(1f, 0.5f, 0.1f), 4f, 1.6f, f.transform);
            l.name = "ImpFireLight";
        }
    }

    static void BuildZombie(DoomMonster dm, PejAiController e, Transform rig)
    {
        var head = D.FindBone(rig, "head");
        if (head == null) return;
        var up = Vector3.up;
        // a dark-green combat helmet: a flattened half sphere on top of the head
        D.Part(PrimitiveType.Sphere, head, head.position + up * 0.17f, new Vector3(0.27f, 0.14f, 0.3f), D.Solid(new Color(0.18f, 0.28f, 0.14f)), "ZombieHelmet");
    }

    static void BuildCaco(DoomMonster dm, PejAiController e)
    {
        var root = new GameObject("Cacodemon").transform;
        root.SetParent(e.transform, false);
        dm.caco = root;
        dm.baseY = 1.25f;
        root.localPosition = new Vector3(0, dm.baseY, 0);
        root.rotation = e.transform.rotation;
        var flesh = D.Unlit("skin_caco.png", 2f);
        var wp = root.position; var f = root.forward; var u = Vector3.up; var r = root.right;
        // body: a big red ball
        var b = D.Part(PrimitiveType.Sphere, root, wp, Vector3.one * 1.5f, flesh, "CacoBody");
        dm.Track(b.GetComponent<Renderer>());
        // one huge yellow eye with a green iris and a black pupil
        var eyeWhite = D.Solid(new Color(1f, 0.95f, 0.5f));
        var iris = D.Solid(new Color(0.15f, 0.85f, 0.2f));
        var pupil = D.Solid(Color.black);
        dm.Track(D.Part(PrimitiveType.Sphere, root, wp + f * 0.62f + u * 0.25f, Vector3.one * 0.5f, eyeWhite, "CacoEye").GetComponent<Renderer>());
        dm.Track(D.Part(PrimitiveType.Sphere, root, wp + f * 0.84f + u * 0.25f, Vector3.one * 0.26f, iris, "CacoIris").GetComponent<Renderer>());
        dm.Track(D.Part(PrimitiveType.Sphere, root, wp + f * 0.95f + u * 0.25f, new Vector3(0.1f, 0.2f, 0.1f), pupil, "CacoPupil").GetComponent<Renderer>());
        // a wide black mouth full of white teeth
        dm.Track(D.Part(PrimitiveType.Sphere, root, wp + f * 0.58f - u * 0.3f, new Vector3(0.95f, 0.42f, 0.45f), pupil, "CacoMouth").GetComponent<Renderer>());
        var tooth = D.Solid(new Color(0.95f, 0.92f, 0.8f));
        for (int i = -3; i <= 3; i++)
        {
            float x = i * 0.12f; float z = 0.78f - Mathf.Abs(i) * 0.04f;
            dm.Track(D.Spike(root, wp + r * x + f * z - u * 0.17f, wp + r * x + f * (z + 0.02f) - u * 0.32f, 0.03f, tooth).GetComponent<Renderer>());
            dm.Track(D.Spike(root, wp + r * x + f * (z - 0.05f) - u * 0.43f, wp + r * x + f * (z - 0.03f) - u * 0.28f, 0.03f, tooth).GetComponent<Renderer>());
        }
        // horns
        var horn = D.Solid(new Color(0.85f, 0.8f, 0.6f));
        dm.Track(D.Spike(root, wp + u * 0.6f + r * 0.28f, wp + u * 0.98f + r * 0.4f, 0.07f, horn).GetComponent<Renderer>());
        dm.Track(D.Spike(root, wp + u * 0.6f - r * 0.28f, wp + u * 0.98f - r * 0.4f, 0.07f, horn).GetComponent<Renderer>());
        // short spikes around the body
        for (int i = 0; i < 8; i++)
        {
            var d = Quaternion.Euler(0, i * 45f, 0) * (-f);
            dm.Track(D.Spike(root, wp + d * 0.68f, wp + d * 0.95f + u * 0.1f, 0.06f, horn).GetComponent<Renderer>());
        }
        var glow = Mix.Glow(wp, new Color(1f, 0.15f, 0.1f), 5f, 1.2f, root);
        glow.name = "CacoGlow";
    }
}

// Hits: SUPERHOT's own kill call lands here. Demons that have more health show pain (flash, blood, grunt) and survive.
[HarmonyPatch(typeof(PejAiBody), nameof(PejAiBody.Kill))]
public static class BodyKillPatch
{
    public static bool Force;
    /// <summary>Hit points one call takes away (the shotgun deals up to 3).</summary>
    public static int Damage = 1;

    static bool Prefix(PejAiBody __instance)
    {
        var c = __instance.GetComponentInParent<PejAiController>();
        if (c == null || c.IsDead) return true;
        var dm = c.GetComponent<DoomMonster>();
        var pos = c.transform.position + Vector3.up * 1.2f;
        if (dm != null) dm.Reapply();
        if (dm != null && !Force && dm.hp > Damage)
        {
            if (Time.unscaledTime < dm.nextHit) return false;
            dm.nextHit = Time.unscaledTime + 0.25f;
            dm.hp -= Damage;
            dm.Pain();
            D.Gore(pos, 4, 5f, 0.12f);
            D.Sfx("pain", pos, 0.9f, dm.kind == Kind.Caco ? 0.55f : 1f);
            D.Flash = new Color(1f, 0.1f, 0.05f);
            G.Shake(0.4f);
            return false;
        }
        bool caco = dm != null && dm.kind == Kind.Caco;
        D.Gore(pos, caco ? 14 : 9, caco ? 9f : 7f, caco ? 0.2f : 0.15f, dm != null && dm.kind == Kind.Zombie ? "skin_zombie.png" : (dm != null && dm.kind == Kind.Imp ? "skin_imp.png" : "skin_caco.png"));
        D.Sfx("death", pos, 1f, caco ? 0.6f : 1f);
        var l = Mix.Glow(pos, new Color(1f, 0.2f, 0.1f), 7f, 3f);
        Object.Destroy(l.gameObject, 0.35f);
        D.Kills++;
        D.GrinUntil = Time.unscaledTime + 1.6f;
        return true;
    }
}

// Enemy fire becomes fireballs: orange fire sphere with a glow, flying through bullet time.
[HarmonyPatch(typeof(Bullet), nameof(Bullet.LaunchInDirectionWithSpeed))]
static class FireballPatch
{
    static void Postfix(Bullet __instance)
    {
        if (__instance.transform.Find("DoomFireball") != null) return;
        var f = D.Part(PrimitiveType.Sphere, __instance.transform, __instance.transform.position, Vector3.one * 0.4f, D.Unlit("fire.png", 1f), "DoomFireball");
        var l = Mix.Glow(f.transform.position, new Color(1f, 0.5f, 0.1f), 6f, 2f, f.transform);
        l.name = "DoomFireLight";
        f.AddComponent<Spin>();
        D.Sfx("fireball", __instance.transform.position, 0.45f, Random.Range(0.9f, 1.2f));
    }
}

public class Spin : MonoBehaviour
{
    void Update() => transform.Rotate(300f * Time.deltaTime, 200f * Time.deltaTime, 0);
}

// The player would have died: in god mode the hit still shows on the status bar (health drops, face winces).
[HarmonyPatch(typeof(PlayerActions), nameof(PlayerActions.Kill), new[] { typeof(Vector3), typeof(bool) })]
public static class PlayerHitPatch
{
    static float last;
    static void Prefix()
    {
        if (!MainDebug.godmodeActive || Time.unscaledTime < last + 0.6f) return;
        Hurt();
    }

    public static void Hurt()
    {
        last = Time.unscaledTime;
        D.Health = Mathf.Max(12, D.Health - (D.Armor > 0 ? 5 : 9));
        if (D.Armor > 0) D.Armor = Mathf.Max(0, D.Armor - 10);
        D.HurtUntil = Time.unscaledTime + 1.2f;
        D.FlashScreen(new Color(1f, 0f, 0f), 0.45f);
        D.Sfx("pain", null, 0.8f, 0.7f);
    }
}

// The level's tutorial hints ("press E to HOTSWITCH...") would sit over the Doom screen: keep them off.
[HarmonyPatch(typeof(TextManager), nameof(TextManager.SetStaticSubtitle))]
static class NoStaticSubtitle { static bool Prefix() => false; }

[HarmonyPatch(typeof(TextManager), nameof(TextManager.SetStaticUptitle))]
static class NoStaticUptitle { static bool Prefix() => false; }

// Fireball trails burn orange instead of SUPERHOT red.
[HarmonyPatch(typeof(TrailSimpleStretch), nameof(TrailSimpleStretch.reStart))]
public static class FireTrailPatch
{
    public static readonly List<TrailSimpleStretch> All = new List<TrailSimpleStretch>();

    static void Postfix(TrailSimpleStretch __instance)
    {
        if (!All.Contains(__instance)) All.Add(__instance);
        Paint();
    }

    /// <summary>The game swaps trail materials itself: paint them fire-orange again before every camera culls.</summary>
    public static void Paint()
    {
        var m = D.Solid(new Color(1f, 0.55f, 0.1f));
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var t = All[i];
            if (t == null) { All.RemoveAt(i); continue; }
            foreach (var r in t.renderers) if (r != null && r.sharedMaterial != m) r.sharedMaterial = m;
        }
    }
}

// The game's own giant words ("WILL", kill counts, "NEW ENDLESS ARENA UNLOCKED") would cover the Doom scene: only the mod's words show.
public static class Words
{
    public static bool Allow;
    public static void Show(string w) { Allow = true; try { G.Words(w); } finally { Allow = false; } }
}

[HarmonyPatch(typeof(TextManager), nameof(TextManager.DisplayQuick), new[] { typeof(string[]), typeof(bool), typeof(float) })]
static class NoGameWords { static bool Prefix() => Words.Allow; }

[HarmonyPatch(typeof(TextManager), nameof(TextManager.DisplayQuick), new[] { typeof(string), typeof(float) })]
static class NoGameWord { static bool Prefix() => Words.Allow; }

