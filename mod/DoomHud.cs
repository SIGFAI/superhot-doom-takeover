// The Doom status bar: kills, health, the marine's face, armor, weapon, plus the top-left pickup messages and the
// full-screen colour flashes (pain red, pickup yellow, BFG green).
using Sigf.Kit;
using UnityEngine;

public static class DoomHud
{
    static GUIStyle label, big;
    static Texture2D px;

    static void Outlined(Rect r, string text, GUIStyle s, Color c, float k)
    {
        var prev = s.normal.textColor;
        s.normal.textColor = Color.black;
        foreach (var o in new[] { new Vector2(3, 3), new Vector2(-3, 3), new Vector2(3, -3), new Vector2(-3, -3) })
            GUI.Label(new Rect(r.x + o.x * k, r.y + o.y * k, r.width, r.height), text, s);
        s.normal.textColor = c;
        GUI.Label(r, text, s);
        s.normal.textColor = prev;
    }

    static string FaceName()
    {
        float t = Time.unscaledTime;
        if (t < D.RageUntil) return "face_rage.png";
        if (t < D.HurtUntil || D.Health < 30) return "face_hurt.png";
        if (t < D.GrinUntil) return "face_grin.png";
        if ((int)(t * 0.7f) % 4 == 2) return "face_left.png";
        return "face_a.png";
    }

    public static void Draw()
    {
        if (px == null) { px = new Texture2D(1, 1); px.SetPixel(0, 0, Color.white); px.Apply(); }
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        float k = Screen.height / 1080f, W = Screen.width, H = Screen.height, t = Time.unscaledTime;
        var prev = GUI.color;

        // colour flash over the whole screen
        if (t < D.FlashUntil)
        {
            var c = D.Flash; c.a = Mathf.Clamp01((D.FlashUntil - t) * 1.6f) * 0.45f;
            GUI.color = c; GUI.DrawTexture(new Rect(0, 0, W, H), px); GUI.color = prev;
        }

        // status bar
        float bh = 150f * k, y0 = H - bh;
        GUI.color = new Color(0.55f, 0.5f, 0.45f);
        GUI.DrawTextureWithTexCoords(new Rect(0, y0, W, bh), D.Tex("wall_tech.png"), new Rect(0, 0, W / (256f * k), bh / (256f * k)));
        GUI.color = new Color(0, 0, 0, 0.55f); GUI.DrawTexture(new Rect(0, y0, W, bh), px);
        GUI.color = new Color(0.8f, 0.2f, 0.1f); GUI.DrawTexture(new Rect(0, y0, W, 5f * k), px);
        GUI.color = prev;

        Block(W * 0.12f, y0, bh, k, "AMMO", Shotgun.Ammo.ToString(), Color.yellow);
        Block(W * 0.32f, y0, bh, k, "HEALTH", D.Health + "%", D.Health < 30 ? new Color(1f, 0.3f, 0.1f) : new Color(1f, 0.15f, 0.1f));
        Block(W * 0.68f, y0, bh, k, "ARMOR", D.Armor + "%", new Color(0.3f, 0.85f, 0.3f));
        Block(W * 0.88f, y0, bh, k, "KILLS", D.Kills.ToString(), new Color(1f, 0.6f, 0.1f));
        label.fontSize = Mathf.RoundToInt(24 * k);
        Outlined(new Rect(W * 0.12f - 150f * k, y0 + 118f * k, 300f * k, 30f * k), D.Weapon, label, new Color(1f, 0.75f, 0.3f), k);

        // the marine
        float fs = 126f * k;
        var fr = new Rect(W * 0.5f - fs / 2f, y0 + (bh - fs) / 2f + 4f * k, fs, fs);
        var face = D.Tex(FaceName(), false);
        bool flip = FaceName() == "face_left.png" && (int)(t * 0.7f) % 8 == 6;
        GUI.DrawTextureWithTexCoords(fr, face, flip ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1));

        // pickup / event message
        if (t < D.MessageUntil)
        {
            big.fontSize = Mathf.RoundToInt(38 * k);
            big.alignment = TextAnchor.UpperLeft;
            Outlined(new Rect(24f * k, 18f * k, W, 60f * k), D.Message, big, new Color(0.95f, 0.9f, 0.4f), k);
            big.alignment = TextAnchor.MiddleCenter;
        }
        GUI.color = prev;
    }

    static void Block(float cx, float y0, float bh, float k, string title, string value, Color color, int size = 78)
    {
        label.fontSize = Mathf.RoundToInt(26 * k);
        Outlined(new Rect(cx - 150f * k, y0 + 14f * k, 300f * k, 36f * k), title, label, new Color(0.8f, 0.8f, 0.75f), k);
        big.fontSize = Mathf.RoundToInt(size * k);
        Outlined(new Rect(cx - 200f * k, y0 + 44f * k, 400f * k, 100f * k), value, big, color, k);
    }
}
