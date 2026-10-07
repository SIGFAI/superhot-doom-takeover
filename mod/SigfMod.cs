// DOOM TAKEOVER: SUPERHOT's white lab turns into a Doom tech base at 200 lines of hard pixels. The red crystal men
// become imps, zombiemen and floating cacodemons; their bullets are fireballs; the status bar shows the marine's face;
// medikits, armor and the BFG9000 lie around. Time still moves only when you move.
using System.Collections;
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public class SigfMod : MixMod
{
    public override void OnLoad() => G.StartLevel = "LevelTest#59 LabEndless2";

    AudioSource music;

    public override void OnReady()
    {
        DoomWorld.Apply();
        DoomBodySkin.HookRender();
        Mix.After(1.5f, DoomWorld.Apply);
        foreach (var sp in Object.FindObjectsOfType<PejAiSpawner>()) sp.MaxEnemiesOnScene = Mathf.Max(sp.MaxEnemiesOnScene, 4);
        Monsters.Scan();
        Mix.Every(0.25f, Monsters.Scan, "scan");
        Mix.Every(5f, () =>
        {
            if (G.Enemies().Count < 4) G.SpawnEnemy(G.Ahead(13f) + Random.insideUnitSphere * 3f);
        }, "spawner");
        Mix.Every(20f, () =>
        {
            if (Pickups.Live.Count < 2) Pickups.SpawnNearPlayer(Random.value < 0.3f ? Item.Bfg : (Random.value < 0.5f ? Item.Medikit : (Random.value < 0.5f ? Item.Shells : Item.Armor)));
        }, "pickups");
        StartMusic();
        D.Msg("Demons have invaded the lab.  Press F to fire the shotgun.", 6f);
    }

    void StartMusic()
    {
        var c = D.Clip("music");
        if (c == null) return;
        var go = new GameObject("DoomMusic");
        Object.DontDestroyOnLoad(go);
        music = go.AddComponent<AudioSource>();
        music.clip = c; music.loop = true; music.volume = 0.32f; music.Play();
    }

    public override void OnUpdate()
    {
        Pickups.Tick(); Shotgun.Tick();
        if (demoTime >= 0f && !Mathf.Approximately(TimeControl.forcedTimeScale, demoTime)) G.ForceTime(demoTime);
    }

    public override void OnGUI() => DoomHud.Draw();

    /// <summary>A walkable spot roughly ahead of the camera that the camera can see (nothing in between).</summary>
    static Vector3 Spot(float dist, float side)
    {
        var cam = G.Cam.transform;
        for (int k = 0; k < 10; k++)
        {
            var want = G.Ahead(dist - k * 0.6f) + cam.right * side * (1f - k * 0.08f);
            if (UnityEngine.AI.NavMesh.SamplePosition(want, out var nh, 2f, UnityEngine.AI.NavMesh.AllAreas)
                && !Physics.Linecast(cam.position, nh.position + Vector3.up * 1.2f, Env, QueryTriggerInteraction.Ignore))
                return nh.position;
        }
        return G.Ahead(5f);
    }

    /// <summary>The time scale the demo wants (the game's own level flow releases forced time now and then).</summary>
    static float demoTime = -1f;
    static void T(float scale) { demoTime = scale; G.ForceTime(scale); }

    static int Env => 1 << LayerMask.NameToLayer("Environment");

    static void Hit(PejAiController e)
    {
        if (e != null && !e.IsDead && e.enemyBody != null) e.enemyBody.Kill();
    }

    /// <summary>Points the player's view at a world position (yaw on the body, pitch on the camera look).</summary>
    static void AimAt(Vector3 p)
    {
        var d = p - G.Cam.transform.position;
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        G.Player.transform.rotation = Quaternion.Euler(0, yaw, 0);
        foreach (var l in G.Player.GetComponentsInChildren<CameraLook>()) l.rotationY = pitch;
    }

    static Vector3 Chest(PejAiController e)
    {
        var dm = e.GetComponent<DoomMonster>();
        return dm != null && dm.caco != null ? dm.caco.position : e.transform.position + Vector3.up * 1.2f;
    }

    /// <summary>Shoots at an enemy until it is dead (or shots run out), in real time.</summary>
    static IEnumerator ShootDown(PejAiController e, float gap = 0.8f, int maxShots = 8)
    {
        for (int i = 0; i < maxShots && e != null && !e.IsDead; i++)
        {
            AimAt(Chest(e));
            yield return null;
            Shotgun.Fire();
            yield return Mix.Wait(gap);
        }
    }

    /// <summary>True when nothing but demons stands between the two points.</summary>
    static bool Clear(Vector3 a, Vector3 b)
    {
        if (!Physics.Linecast(a, b, out var h, ~0, QueryTriggerInteraction.Ignore)) return true;
        return h.collider.GetComponentInParent<PejAiBody>() != null || h.collider.GetComponentInParent<PejAiController>() != null;
    }

    static PejAiController NearestVisible(float max)
    {
        PejAiController best = null; float bd = max;
        foreach (var e in G.Enemies())
        {
            float d = Vector3.Distance(e.transform.position, G.Pos);
            if (d < bd && Clear(G.Cam.transform.position, Chest(e))) { bd = d; best = e; }
        }
        return best;
    }

    static Vector3 FloorPos()
    {
        return UnityEngine.AI.NavMesh.SamplePosition(G.Pos, out var nh, 3f, UnityEngine.AI.NavMesh.AllAreas) ? nh.position : G.Pos - Vector3.up * 1.6f;
    }

    static IEnumerator WalkTo(Vector3 target, float speed = 4.5f, bool fight = false)
    {
        var cc = G.Player.GetComponent<CharacterController>();
        var path = new UnityEngine.AI.NavMeshPath();
        var corners = new List<Vector3>();
        if (UnityEngine.AI.NavMesh.CalculatePath(FloorPos(), target, UnityEngine.AI.NavMesh.AllAreas, path)) corners.AddRange(path.corners);
        else corners.Add(target);
        float shot = 0f;
        int ci = corners.Count > 1 ? 1 : 0;
        for (float t = 0; t < 8f && ci < corners.Count; t += Time.unscaledDeltaTime)
        {
            var d = corners[ci] - (FloorPos()); d.y = 0;
            if (d.magnitude < 0.5f) { ci++; continue; }
            if (fight && (shot -= Time.unscaledDeltaTime) <= 0f)
            {
                var n = NearestVisible(9f);
                if (n != null) { AimAt(Chest(n)); Shotgun.Fire(); shot = 0.7f; }
            }
            else if (!fight || NearestVisible(9f) == null)
            {
                var look = G.Pos + d.normalized * 5f; look.y = G.Cam.transform.position.y;
                AimAt(look);
            }
            cc.Move(d.normalized * speed * Time.unscaledDeltaTime + Vector3.down * 0.05f);
            yield return null;
        }
    }

    /// <summary>Turns the player (the game's own teleport-in-place) toward the longest open view.</summary>
    static float lastYaw = -999f;
    static IEnumerator FaceOpen(bool turnAway = false)
    {
        var o = G.Cam.transform.position;
        float best = -1f; Vector3 bestDir = G.Cam.transform.forward;
        for (int i = 0; i < 24; i++)
        {
            var q = Quaternion.Euler(0, i * 15f, 0);
            if (turnAway && Mathf.Abs(Mathf.DeltaAngle(i * 15f, lastYaw)) < 70f) continue;
            float score = 99f;
            foreach (float a in new[] { -14f, 0f, 14f })
            {
                var d = q * Quaternion.Euler(0, a, 0) * Vector3.forward;
                float len = Physics.Raycast(o, d, out var h, 16f, Env, QueryTriggerInteraction.Ignore) ? h.distance : 16f;
                score = Mathf.Min(score, len);
            }
            if (score > best) { best = score; bestDir = q * Vector3.forward; }
        }
        Mix.Log("FaceOpen best=" + best);
        float yaw = Quaternion.LookRotation(bestDir).eulerAngles.y;
        lastYaw = yaw;
        G.Player.transform.rotation = Quaternion.Euler(0, yaw, 0);
        var looks = G.Player.GetComponentsInChildren<CameraLook>();
        foreach (var l in looks) l.rotationY = 0f;
        yield return Mix.Wait(0.4f);
        Mix.Log("faced yaw " + yaw);
    }

    public override IEnumerator Demo()
    {
        T(1f);
        float t0 = Time.unscaledTime;
        yield return FaceOpen();
        Shotgun.Ammo = 24;
        // 1. the Doom lab and its demons walk in (time runs slow so the eye can follow)
        T(0.25f);
        D.Kills = 0;
        Monsters.Wanted.Enqueue(Kind.Imp); Monsters.Wanted.Enqueue(Kind.Caco); Monsters.Wanted.Enqueue(Kind.Zombie);
        var imp = G.SpawnEnemy(Spot(6f, -2f));
        var caco = G.SpawnEnemy(Spot(8f, 0.5f));
        var zed = G.SpawnEnemy(Spot(6f, 2.5f));
        Mix.Log("demo spawned " + (imp != null) + (caco != null) + (zed != null));
        Mix.Say("DOOM TAKEOVER", 4f, new Color(1f, 0.2f, 0.1f), 0.13f, 96);
        Words.Show("DOOM|HOT");
        yield return Mix.Wait(4.5f);

        // 2. time freezes: fireballs hang in the air while the shotgun blasts the cacodemon (pain flash, gore)
        T(0.03f);
        Mix.Say("time only moves when you move", 3.5f, Color.white, 0.82f, 40);
        yield return Mix.Wait(1.5f);
        yield return ShootDown(caco, 1.0f, 6);
        if (D.Health == 100) PlayerHitPatch.Hurt();
        yield return Mix.Wait(0.8f);

        // 3. the other kills
        T(0.12f);
        yield return ShootDown(imp, 0.9f, 4);
        yield return ShootDown(zed, 0.9f, 3);
        yield return Mix.Wait(1f);

        // 4. the BFG9000 in front of the player, a new wave behind it
        Monsters.Wanted.Enqueue(Kind.Imp); Monsters.Wanted.Enqueue(Kind.Zombie); Monsters.Wanted.Enqueue(Kind.Caco); Monsters.Wanted.Enqueue(Kind.Imp);
        G.SpawnEnemy(Spot(9f, -3f)); G.SpawnEnemy(Spot(10f, 3f)); G.SpawnEnemy(Spot(11f, 0f)); G.SpawnEnemy(Spot(8f, 1f));
        T(0.25f);
        var bfgSpot = Spot(3.5f, 0f);
        Pickups.Spawn(Item.Bfg, bfgSpot);
        yield return Mix.Wait(2.5f);
        AimAt(bfgSpot + Vector3.up * 0.8f);
        yield return WalkTo(bfgSpot);
        yield return Mix.Wait(5f);

        // 5. a medikit
        var medSpot = Spot(3.5f, 1f);
        Pickups.Spawn(Item.Medikit, medSpot);
        yield return Mix.Wait(1f);
        AimAt(medSpot + Vector3.up * 0.8f);
        yield return WalkTo(medSpot);
        yield return Mix.Wait(3f);

        // 6. push into the demons, shotgun blazing, so the picture keeps moving while more keep coming
        T(0.35f);
        Shotgun.Give(20);
        Mix.Say("RIP AND TEAR", 3f, new Color(1f, 0.2f, 0.1f), 0.13f, 96);
        while (Time.unscaledTime - t0 < 100f)
        {
            if (G.Enemies().Count < 5)
            {
                yield return FaceOpen();
                G.SpawnEnemy(Spot(10f, Random.Range(-3f, 3f))); G.SpawnEnemy(Spot(8f, Random.Range(-3f, 3f)));
                yield return Mix.Wait(1.5f);
            }
            if (D.Health < 50)
            {
                var med = Spot(4f, 0f);
                Pickups.Spawn(Item.Medikit, med);
                AimAt(med + Vector3.up * 0.8f);
                yield return WalkTo(med, 3.5f, true);
            }
            var target = NearestVisible(14f);
            var from = G.Pos;
            if (target != null)
            {
                AimAt(Chest(target));
                var to = target.transform.position - G.Pos; to.y = 0;
                if (to.magnitude > 6f) yield return WalkTo(target.transform.position - to.normalized * 4.5f, 3.2f, true);
                yield return ShootDown(target, 0.7f, 5);
            }
            else
            {
                yield return FaceOpen(true);
                yield return WalkTo(Spot(6f, 0f), 3.2f, true);
            }
            if ((G.Pos - from).magnitude < 1.5f && target == null) yield return FaceOpen(true);
            yield return Mix.Wait(0.8f);
        }
    }
}
