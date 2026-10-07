// The player's Doom shotgun: a model in front of the camera, fired with F. Nine pellets, a muzzle flash, a kick,
// real damage to the demons (pain, gore, kills). Firing also lets a little SUPERHOT time pass, like moving does.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public static class Shotgun
{
    public static int Ammo = 24;
    static GameObject vm, flashObj;
    static Light flashLight;
    static float kick, flashT, nextFire;
    static Transform muzzle;

    static void Build(Camera cam)
    {
        vm = new GameObject("DoomShotgunViewmodel");
        vm.transform.SetParent(cam.transform, false);
        vm.transform.localPosition = new Vector3(0.2f, -0.22f, 0.42f);
        var steel = D.Solid(new Color(0.34f, 0.36f, 0.38f));
        var dark = D.Solid(new Color(0.14f, 0.15f, 0.16f));
        var wood = D.Solid(new Color(0.5f, 0.28f, 0.12f));
        // chunky pump shotgun: twin-looking barrel, wooden pump, dark receiver, wooden stock
        Piece(PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0.34f), new Vector3(0.085f, 0.36f, 0.085f), new Vector3(90, 0, 0), steel);
        Piece(PrimitiveType.Cylinder, new Vector3(0, -0.045f, 0.3f), new Vector3(0.075f, 0.27f, 0.075f), new Vector3(90, 0, 0), dark);
        Piece(PrimitiveType.Cube, new Vector3(0, -0.06f, 0.22f), new Vector3(0.12f, 0.085f, 0.24f), Vector3.zero, wood);
        Piece(PrimitiveType.Cube, new Vector3(0, -0.02f, -0.04f), new Vector3(0.11f, 0.14f, 0.26f), Vector3.zero, dark);
        Piece(PrimitiveType.Cube, new Vector3(0, -0.12f, -0.2f), new Vector3(0.09f, 0.18f, 0.14f), new Vector3(-18, 0, 0), wood);
        var m = new GameObject("Muzzle");
        muzzle = m.transform;
        muzzle.SetParent(vm.transform, false);
        muzzle.localPosition = new Vector3(0, 0.02f, 0.72f);
        // muzzle flash: a white-hot core, an orange fireball and four thin yellow spikes
        flashObj = new GameObject("MuzzleFlash");
        flashObj.transform.SetParent(muzzle, false);
        FlashPart(PrimitiveType.Sphere, Vector3.zero, new Vector3(0.2f, 0.2f, 0.26f), D.Solid(new Color(1f, 0.97f, 0.75f)));
        FlashPart(PrimitiveType.Sphere, new Vector3(0, 0, 0.08f), new Vector3(0.34f, 0.34f, 0.2f), D.Unlit("fire.png", 1f));
        for (int i = 0; i < 4; i++)
        {
            var sp = D.Spike(flashObj.transform, muzzle.position, muzzle.position + muzzle.TransformDirection(Quaternion.Euler(0, 0, i * 45f) * Vector3.up) * 0.35f, 0.025f, D.Solid(new Color(1f, 0.85f, 0.2f)));
            sp.transform.SetParent(flashObj.transform, true);
            sp = D.Spike(flashObj.transform, muzzle.position, muzzle.position - muzzle.TransformDirection(Quaternion.Euler(0, 0, i * 45f) * Vector3.up) * 0.35f, 0.025f, D.Solid(new Color(1f, 0.85f, 0.2f)));
        }
        flashObj.SetActive(false);
        flashLight = Mix.Glow(muzzle.position, new Color(1f, 0.7f, 0.2f), 9f, 4f, muzzle);
        flashLight.enabled = false;
    }

    static void FlashPart(PrimitiveType t, Vector3 pos, Vector3 scale, Material mat)
    {
        var g = GameObject.CreatePrimitive(t);
        Object.Destroy(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().sharedMaterial = mat;
        g.transform.SetParent(flashObj.transform, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
    }

    static void Piece(PrimitiveType t, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var g = GameObject.CreatePrimitive(t);
        Object.Destroy(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().sharedMaterial = mat;
        g.transform.SetParent(vm.transform, false);
        g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localEulerAngles = euler;
    }

    public static void Tick()
    {
        var cam = G.Cam;
        if (cam == null) return;
        if (vm == null || vm.transform.parent != cam.transform) { if (vm != null) Object.Destroy(vm); Build(cam); }
        if (Input.GetKeyDown(KeyCode.F)) Fire();
        kick = Mathf.MoveTowards(kick, 0f, Time.unscaledDeltaTime * 5f);
        vm.transform.localPosition = new Vector3(0.2f, -0.22f, 0.42f) + new Vector3(0, 0.03f, -0.12f) * kick;
        vm.transform.localEulerAngles = new Vector3(-12f * kick, 0, 0);
        bool f = Time.unscaledTime < flashT;
        flashObj.SetActive(f); flashLight.enabled = f;
        vm.SetActive(D.Weapon == "SHOTGUN");
    }

    /// <summary>Fires one blast from the camera. Returns the number of demons hit.</summary>
    public static int Fire()
    {
        if (Time.unscaledTime < nextFire) return 0;
        if (Ammo <= 0) { D.Msg("Out of shells.", 1.5f); nextFire = Time.unscaledTime + 0.5f; return 0; }
        var cam = G.Cam.transform;
        Ammo--;
        nextFire = Time.unscaledTime + 0.5f;
        kick = 1f; flashT = Time.unscaledTime + 0.12f;
        D.Sfx("shotgun", null, 0.9f);
        D.FlashScreen(new Color(1f, 0.8f, 0.3f), 0.1f);
        G.Shake(0.3f);
        var hits = new Dictionary<PejAiBody, int>();
        var cacos = DoomMonster.Cacos();
        for (int i = 0; i < 9; i++)
        {
            var dir = (cam.forward + cam.right * Random.Range(-0.055f, 0.055f) + cam.up * Random.Range(-0.035f, 0.035f)).normalized;
            float wall = 45f; Vector3 puff = Vector3.zero; PejAiBody body = null;
            if (Physics.Raycast(cam.position, dir, out var h, 45f, ~0, QueryTriggerInteraction.Ignore))
            {
                wall = h.distance; puff = h.point;
                body = h.collider.GetComponentInParent<PejAiBody>();
            }
            // cacodemons are balls bigger than the body hitbox: test the pellet against the ball too
            if (body == null)
                foreach (var c in cacos)
                {
                    var center = c.caco.position;
                    var toC = center - cam.position;
                    float along = Vector3.Dot(toC, dir);
                    if (along > 0f && along < wall && (toC - dir * along).magnitude < 0.75f) { body = c.GetComponent<PejAiController>().enemyBody; break; }
                }
            if (body != null) { hits.TryGetValue(body, out int n); hits[body] = n + 1; }
            else if (wall < 45f) Mix.Burst(puff, new Color(1f, 0.8f, 0.4f), 3, 3f, 0.05f, 0.5f);
        }
        int total = 0;
        foreach (var kv in hits)
        {
            if (kv.Key == null) continue;
            total++;
            BodyKillPatch.Damage = Mathf.Clamp(kv.Value / 3 + 1, 1, 3);
            try { kv.Key.Kill(); } finally { BodyKillPatch.Damage = 1; }
        }
        PulseTime();
        return total;
    }

    static void PulseTime()
    {
        if (TimeControl.forcedTimeScale >= 0f) return; // the demo or the BFG is driving time
        G.ForceTime(0.6f);
        Mix.After(0.45f, () => { if (Mathf.Approximately(TimeControl.forcedTimeScale, 0.6f)) G.Release(); }, "shotgunTime");
    }

    public static void Give(int shells) { Ammo = Mathf.Min(99, Ammo + shells); }
}
