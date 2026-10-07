// Doom pickups lying in the lab: medikit, armor and the BFG9000. Walk into one: the status bar reacts; the BFG
// vaporises every demon in the room with green lightning.
using System.Collections;
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;
using UnityEngine.AI;

public enum Item { Medikit, Armor, Bfg, Shells }

public class DoomPickup : MonoBehaviour
{
    public Item item;
    float t0;
    Vector3 basePos;
    void Start() { t0 = Random.value * 6f; basePos = transform.position; }
    void Update()
    {
        t0 += Time.deltaTime;
        transform.position = basePos + Vector3.up * (0.15f + 0.12f * Mathf.Sin(t0 * 3f));
        transform.Rotate(0, 90f * Time.deltaTime, 0);
    }
}

public static class Pickups
{
    public static readonly List<DoomPickup> Live = new List<DoomPickup>();

    public static DoomPickup Spawn(Item item, Vector3 floorPos)
    {
        var root = new GameObject("DoomPickup_" + item);
        root.transform.position = floorPos + Vector3.up * 0.8f;
        var p = root.transform.position;
        Color glow;
        if (item == Item.Medikit)
        {
            D.Part(PrimitiveType.Cube, root.transform, p, new Vector3(0.7f, 0.28f, 0.5f), D.Solid(new Color(0.92f, 0.92f, 0.88f)));
            D.Part(PrimitiveType.Cube, root.transform, p + Vector3.up * 0.02f, new Vector3(0.45f, 0.3f, 0.12f), D.Solid(new Color(0.9f, 0.1f, 0.08f)));
            D.Part(PrimitiveType.Cube, root.transform, p + Vector3.up * 0.02f, new Vector3(0.12f, 0.3f, 0.45f), D.Solid(new Color(0.9f, 0.1f, 0.08f)));
            glow = new Color(1f, 0.3f, 0.3f);
        }
        else if (item == Item.Shells)
        {
            D.Part(PrimitiveType.Cube, root.transform, p, new Vector3(0.6f, 0.3f, 0.4f), D.Solid(new Color(0.75f, 0.12f, 0.08f)));
            D.Part(PrimitiveType.Cube, root.transform, p + Vector3.up * 0.16f, new Vector3(0.62f, 0.05f, 0.42f), D.Solid(new Color(0.95f, 0.8f, 0.2f)));
            for (int i = -1; i <= 1; i++) D.Part(PrimitiveType.Cylinder, root.transform, p + new Vector3(0.18f * i, 0.26f, 0), new Vector3(0.12f, 0.1f, 0.12f), D.Solid(new Color(0.9f, 0.2f, 0.1f)));
            glow = new Color(1f, 0.8f, 0.2f);
        }
        else if (item == Item.Armor)
        {
            D.Part(PrimitiveType.Cube, root.transform, p, new Vector3(0.7f, 0.55f, 0.28f), D.Solid(new Color(0.15f, 0.65f, 0.2f)));
            D.Part(PrimitiveType.Cube, root.transform, p, new Vector3(0.5f, 0.35f, 0.31f), D.Solid(new Color(0.1f, 0.4f, 0.12f)));
            D.Part(PrimitiveType.Cube, root.transform, p + Vector3.up * 0.3f, new Vector3(0.74f, 0.08f, 0.3f), D.Solid(new Color(0.9f, 0.8f, 0.2f)));
            glow = new Color(0.2f, 1f, 0.3f);
        }
        else
        {
            D.Part(PrimitiveType.Cube, root.transform, p, new Vector3(0.95f, 0.22f, 0.3f), D.Solid(new Color(0.22f, 0.24f, 0.22f)));
            D.Part(PrimitiveType.Sphere, root.transform, p + new Vector3(0.52f, 0, 0), new Vector3(0.36f, 0.36f, 0.36f), D.Unlit("plasma.png", 1f));
            D.Part(PrimitiveType.Cube, root.transform, p + new Vector3(-0.3f, -0.2f, 0), new Vector3(0.14f, 0.3f, 0.14f), D.Solid(new Color(0.18f, 0.2f, 0.18f)));
            D.Part(PrimitiveType.Sphere, root.transform, p + new Vector3(0.1f, 0.15f, 0), new Vector3(0.2f, 0.2f, 0.2f), D.Unlit("plasma.png", 1f));
            root.transform.localScale = Vector3.one * 1.25f;
            glow = new Color(0.2f, 1f, 0.2f);
        }
        Mix.Glow(p + Vector3.up * 0.3f, glow, 6f, 2f, root.transform);
        var pk = root.AddComponent<DoomPickup>();
        pk.item = item;
        Live.Add(pk);
        return pk;
    }

    /// <summary>A pickup on the floor near the player, on the navmesh, in front of where they look.</summary>
    public static DoomPickup SpawnNearPlayer(Item item, float dist = 7f)
    {
        for (int i = 0; i < 12; i++)
        {
            var guess = G.Ahead(dist + Random.Range(-2f, 3f)) + G.Cam.transform.right * Random.Range(-4f, 4f);
            if (NavMesh.SamplePosition(guess, out var nh, 3f, NavMesh.AllAreas) && D.Floor(nh.position, out var fl))
                return Spawn(item, new Vector3(nh.position.x, fl.y, nh.position.z));
        }
        return null;
    }

    public static void Tick()
    {
        for (int i = Live.Count - 1; i >= 0; i--)
        {
            var p = Live[i];
            if (p == null) { Live.RemoveAt(i); continue; }
            var d = p.transform.position - G.Pos; d.y = 0;
            float dy = Mathf.Abs(p.transform.position.y - G.Pos.y);
            if (d.magnitude < 1.5f && dy < 3.5f) { Collect(p); Live.RemoveAt(i); }
        }
    }

    public static void Collect(DoomPickup p)
    {
        D.Sfx("pickup", null, 0.9f);
        D.FlashScreen(new Color(1f, 0.9f, 0.2f), 0.3f);
        var pos = p.transform.position;
        Object.Destroy(p.gameObject);
        switch (p.item)
        {
            case Item.Medikit: D.Health = Mathf.Min(100, D.Health + 25); D.Msg("Picked up a medikit."); break;
            case Item.Shells: Shotgun.Give(12); D.Msg("Picked up 12 shotgun shells."); break;
            case Item.Armor: D.Armor = 100; D.Msg("Picked up the armor."); break;
            case Item.Bfg:
                D.Msg("You got the BFG9000!  Oh, yes.", 4f);
                D.Weapon = "BFG9000";
                D.RageUntil = Time.unscaledTime + 3.5f;
                Mix.Run(Blast(), "BFG");
                break;
        }
        D.Sfx("pickup", pos, 0.5f, 1.5f);
    }

    /// <summary>The BFG: a green flash, then a bolt of lightning into every demon, one after the other.</summary>
    public static IEnumerator Blast()
    {
        yield return Mix.Wait(0.5f);
        D.Sfx("bfg", null, 1f);
        D.FlashScreen(new Color(0.2f, 1f, 0.2f), 0.9f);
        G.Shake(2f);
        var targets = G.Enemies();
        targets.Sort((a, b) => Vector3.Distance(a.transform.position, G.Pos).CompareTo(Vector3.Distance(b.transform.position, G.Pos)));
        var green = D.Solid(new Color(0.3f, 1f, 0.3f));
        foreach (var e in targets)
        {
            if (e == null || e.IsDead) continue;
            var from = G.Pos + Vector3.up * 1.4f + G.Cam.transform.right * 0.3f;
            var to = e.transform.position + Vector3.up * 1.2f;
            var go = new GameObject("BfgBolt");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = green; lr.startWidth = 0.25f; lr.endWidth = 0.6f; lr.positionCount = 7;
            for (int i = 0; i < 7; i++)
            {
                var pt = Vector3.Lerp(from, to, i / 6f);
                if (i > 0 && i < 6) pt += Random.insideUnitSphere * 0.5f;
                lr.SetPosition(i, pt);
            }
            Object.Destroy(go, 0.5f);
            var l = Mix.Glow(to, new Color(0.3f, 1f, 0.3f), 9f, 4f);
            Object.Destroy(l.gameObject, 0.5f);
            BodyKillPatch.Force = true;
            try { if (PejAiManager.CURRENT != null) PejAiManager.CURRENT.ExplodeEnemy(e); }
            finally { BodyKillPatch.Force = false; }
            G.Shake(0.8f);
            yield return Mix.Wait(0.22f);
        }
        D.Msg("The demons are vaporised.", 3f);
        yield return Mix.Wait(6f);
        D.Weapon = "SHOTGUN";
    }
}
