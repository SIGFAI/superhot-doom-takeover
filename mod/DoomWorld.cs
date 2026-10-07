// The level becomes a Doom tech base: tech-panel walls, hellish red floors, warm lights, and the camera renders
// at 200 lines with hard pixels like a 1993 shooter.
using System.Linq;
using Sigf.Kit;
using UnityEngine;

public class Pixelate : MonoBehaviour
{
    public int lines = 200;
    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        int w = Mathf.RoundToInt(lines * (float)src.width / src.height);
        var rt = RenderTexture.GetTemporary(w, lines, 0, src.format);
        rt.filterMode = FilterMode.Point;
        Graphics.Blit(src, rt);
        Graphics.Blit(rt, dst);
        RenderTexture.ReleaseTemporary(rt);
    }
}

public static class DoomWorld
{
    public static void Apply()
    {
        var wall = D.Unlit("wall_tech.png", 0.35f);
        var floor = D.Unlit("hell.png", 0.5f);
        int walls = 0, floors = 0, lights = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            if (r is SkinnedMeshRenderer || r is LineRenderer || r is ParticleSystemRenderer) continue;
            var ms = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < ms.Length; i++)
            {
                var m = ms[i];
                if (m == null) continue;
                string sh = m.shader.name;
                if (sh.StartsWith("XULM/Concrete"))
                {
                    bool flat = false;
                    string n = m.name.ToLower();
                    if (n.Contains("czysty")) ms[i] = D.Unlit("hell.png", 0.3f);
                    else ms[i] = wall;
                    if (flat) floors++; else walls++;
                    changed = true;
                }
                else if (sh == "XULM/Emissive")
                {
                    if (m.HasProperty("_Color")) m.color = new Color(1f, 0.62f, 0.35f);
                    lights++;
                }
            }
            if (changed) r.sharedMaterials = ms;
        }
        Mix.Log("DoomWorld walls=" + walls + " floors=" + floors + " lights=" + lights);
        var cam = Camera.main ?? Object.FindObjectOfType<Camera>();
        if (cam != null && cam.GetComponent<Pixelate>() == null) cam.gameObject.AddComponent<Pixelate>();
    }
}
