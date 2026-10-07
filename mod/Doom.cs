// Shared helpers of the Doom takeover: materials, sounds, meshes, state (health, kills, messages).
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public static class D
{
    // ---------- state shown on the status bar ----------
    public static int Health = 100, Armor = 0, Kills, Total;
    public static string Weapon = "SHOTGUN";
    public static float GrinUntil, HurtUntil, RageUntil, FlashUntil, GlanceUntil;
    public static Color Flash = Color.red;
    public static string Message = "";
    public static float MessageUntil;

    public static void Msg(string s, float seconds = 3f) { Message = s; MessageUntil = Time.unscaledTime + seconds; }
    public static void FlashScreen(Color c, float seconds = 0.35f) { Flash = c; FlashUntil = Time.unscaledTime + seconds; }

    // ---------- materials ----------
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    public static Texture2D Tex(string name, bool repeat = true)
    {
        var t = Mix.Texture(name, false);
        t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        return t;
    }

    /// <summary>Unlit textured material (flat "sector light", like the original engine). Cached per name and tiling.</summary>
    public static Material Unlit(string tex, float tile = 1f)
    {
        string key = tex + "@" + tile;
        if (mats.TryGetValue(key, out var m)) return m;
        m = new Material(Shader.Find("Unlit/Texture")) { mainTexture = Tex(tex) };
        m.mainTextureScale = new Vector2(tile, tile);
        mats[key] = m;
        return m;
    }

    public static Material Solid(Color c)
    {
        string key = "c" + c;
        if (mats.TryGetValue(key, out var m)) return m;
        m = new Material(Shader.Find("Unlit/Color")) { color = c };
        mats[key] = m;
        return m;
    }

    // ---------- sounds ----------
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    public static AudioClip Clip(string n)
    {
        if (clips.TryGetValue(n, out var c)) return c;
        try { c = Mix.Sound(n + ".wav"); } catch (System.Exception e) { Mix.Warn("sound " + n + ": " + e.Message); c = null; }
        clips[n] = c;
        return c;
    }
    public static void Sfx(string n, Vector3? pos = null, float vol = 1f, float pitch = 1f)
    {
        var c = Clip(n);
        if (c != null) Mix.Play(c, pos, vol, pitch);
    }

    // ---------- shapes ----------
    public static GameObject Part(PrimitiveType t, Transform parent, Vector3 worldPos, Vector3 worldScale, Material m, string name = "DoomPart")
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = m;
        go.transform.position = worldPos;
        go.transform.SetParent(parent, true);
        SetWorldScale(go.transform, worldScale);
        return go;
    }

    public static void SetWorldScale(Transform t, Vector3 w)
    {
        var p = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(w.x / Mathf.Max(1e-5f, Mathf.Abs(p.x)), w.y / Mathf.Max(1e-5f, Mathf.Abs(p.y)), w.z / Mathf.Max(1e-5f, Mathf.Abs(p.z)));
    }

    static Mesh coneMesh;
    /// <summary>A unit cone (radius 0.5, height 1, apex up) for horns and spikes.</summary>
    public static Mesh Cone()
    {
        if (coneMesh != null) return coneMesh;
        const int n = 10;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        v.Add(new Vector3(0, 1, 0)); uv.Add(new Vector2(0.5f, 1));
        for (int i = 0; i <= n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f)); uv.Add(new Vector2(i / (float)n, 0));
        }
        for (int i = 1; i <= n; i++) { tri.Add(0); tri.Add(i + 1); tri.Add(i); }
        int c = v.Count; v.Add(Vector3.zero); uv.Add(new Vector2(0.5f, 0.5f));
        for (int i = 1; i <= n; i++) { tri.Add(c); tri.Add(i); tri.Add(i + 1); }
        coneMesh = new Mesh { name = "DoomCone" };
        coneMesh.SetVertices(v); coneMesh.SetUVs(0, uv); coneMesh.SetTriangles(tri, 0); coneMesh.RecalculateNormals(); coneMesh.RecalculateBounds();
        return coneMesh;
    }

    /// <summary>A cone from base to tip in world space.</summary>
    public static GameObject Spike(Transform parent, Vector3 baseWorld, Vector3 tipWorld, float radius, Material m)
    {
        var go = new GameObject("DoomSpike");
        go.AddComponent<MeshFilter>().sharedMesh = Cone();
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        var dir = tipWorld - baseWorld;
        go.transform.position = baseWorld;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        go.transform.SetParent(parent, true);
        SetWorldScale(go.transform, new Vector3(radius * 2f, dir.magnitude, radius * 2f));
        return go;
    }

    public static Transform FindBone(Transform root, params string[] names)
    {
        foreach (var n in names)
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLower().Contains(n.ToLower())) return t;
        return null;
    }

    /// <summary>Chunks of meat flung from pos, with colliders, in the given skin; they pile up on the floor.</summary>
    public static void Gore(Vector3 pos, int count, float power, float size, string skin = "skin_caco.png")
    {
        var m = Unlit(skin, 1f);
        for (int i = 0; i < count; i++)
        {
            var c = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
            c.name = "DoomGib";
            c.GetComponent<Renderer>().sharedMaterial = m;
            c.transform.position = pos + Random.insideUnitSphere * 0.3f;
            c.transform.localScale = new Vector3(Random.Range(0.4f, 1.1f), Random.Range(0.4f, 1.1f), Random.Range(0.4f, 1.1f)) * size;
            var rb = c.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            var away = pos - G.Pos; away.y = 0; away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            rb.velocity = (away * 0.9f + Random.onUnitSphere * 0.8f + Vector3.up * 0.7f) * power * Random.Range(0.4f, 1f);
            rb.angularVelocity = Random.insideUnitSphere * 12f;
            Object.Destroy(c, Random.Range(3f, 5f));
        }
    }

    /// <summary>Raycasts down from above pos to find the floor.</summary>
    public static bool Floor(Vector3 pos, out Vector3 hit)
    {
        if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out var h, 12f, ~0, QueryTriggerInteraction.Ignore)) { hit = h.point; return true; }
        hit = pos; return false;
    }
}
