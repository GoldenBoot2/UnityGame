using System.Collections.Generic;
using UnityEngine;

namespace GoldenBoot.BlockDodge
{
    // Block Dodge: drag left/right to dodge falling blocks. The whole game is built in code at start-up.
    public class Game : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<Game>() != null) return;
            new GameObject("Game").AddComponent<Game>();
        }

        enum State { Menu, Playing, Paused, Over, About }

        class Block
        {
            public Transform t;
            public SpriteRenderer sr;
            public Vector2 size;
            public float speed;
            public bool passed;
        }

        const float PlayerRadius = 0.35f;
        const string BestKey = "best";

        Camera cam;
        Transform player;
        SpriteRenderer playerSr;
        Sprite blockSprite, circleSprite;
        readonly List<Block> blocks = new List<Block>();
        readonly Stack<Block> pool = new Stack<Block>();

        State state = State.Menu;
        int score, best;
        bool newBest;
        float elapsed, spawnTimer, hintTimer, hitFlash;
        float halfW, halfH;
        float targetX, dragOffset;
        bool dragging;

        // GUI
        int styledFor = -1;
        float u; // 1% of the short screen side, in pixels
        GUIStyle title, big, text, sub, button, secondary, tonal, card, hud;
        Texture2D iconTex, dividerTex, scrimTex;

        void Start()
        {
            Application.targetFrameRate = 60;
            best = PlayerPrefs.GetInt(BestKey, 0);

            cam = new GameObject("Camera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Art.Background;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // Draw the sprites at the screen's own pixel density (1 texture pixel = 1 screen pixel),
            // so the rounded edges stay sharp instead of being magnified and blurred.
            float screenPpu = Mathf.Max(32f, Screen.height / (2f * cam.orthographicSize));
            int r = Mathf.Max(4, Mathf.RoundToInt(0.12f * screenPpu));
            int bs = 2 * r + 4;
            Texture2D bt = Art.RoundedRect(bs, bs, r, Color.white);
            blockSprite = Sprite.Create(bt, new Rect(0, 0, bs, bs), new Vector2(0.5f, 0.5f), screenPpu, 0,
                SpriteMeshType.FullRect, new Vector4(r + 1, r + 1, r + 1, r + 1));
            int d = Mathf.CeilToInt(PlayerRadius * 2f * screenPpu);
            Texture2D ct = Art.RoundedRect(d, d, d * 0.5f, Color.white);
            circleSprite = Sprite.Create(ct, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), d);

            var p = new GameObject("Player");
            playerSr = p.AddComponent<SpriteRenderer>();
            playerSr.sprite = circleSprite;
            playerSr.color = Art.Player;
            playerSr.sortingOrder = 1;
            player = p.transform;
            player.localScale = Vector3.one * PlayerRadius * 2f;

            iconTex = Art.Icon(256, true, true);
            dividerTex = Art.RoundedRect(4, 4, 0f, Art.Divider);
            scrimTex = Art.RoundedRect(4, 4, 0f, Art.Scrim);

            UpdateBounds();
            ResetField();
            SetState(State.Menu);
        }

        void UpdateBounds()
        {
            halfH = cam.orthographicSize;
            halfW = halfH * cam.aspect;
        }

        void ResetField()
        {
            foreach (var b in blocks) { b.t.gameObject.SetActive(false); pool.Push(b); }
            blocks.Clear();
            player.position = new Vector3(0f, -halfH + 1.6f, 0f);
            targetX = 0f;
            dragging = false;
            score = 0;
            elapsed = 0f;
            spawnTimer = 0.6f;
            hitFlash = 0f;
            playerSr.color = Art.Player;
        }

        void StartGame()
        {
            ResetField();
            newBest = false;
            hintTimer = 2.5f;
            SetState(State.Playing);
        }

        void SetState(State s)
        {
            state = s;
            // The player belongs to the game field; menu screens show only the UI.
            player.gameObject.SetActive(s != State.Menu && s != State.About);
            Screen.sleepTimeout = s == State.Playing ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && state == State.Playing) SetState(State.Paused);
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && state == State.Playing) SetState(State.Paused);
        }

        void Update()
        {
            UpdateBounds();

            if (Input.GetKeyDown(KeyCode.Escape)) // Android Back
            {
                switch (state)
                {
                    case State.Playing: SetState(State.Paused); break;
                    case State.Paused: SetState(State.Playing); break;
                    case State.Over:
                    case State.About: ResetField(); SetState(State.Menu); break;
                    case State.Menu: Application.Quit(); break;
                }
            }

            if (hitFlash > 0f)
            {
                hitFlash -= Time.deltaTime;
                playerSr.color = Color.Lerp(Art.Player, Color.white, Mathf.PingPong(hitFlash * 8f, 1f));
            }

            if (state != State.Playing) return;
            float dt = Time.deltaTime;
            elapsed += dt;
            if (hintTimer > 0f) hintTimer -= dt;

            MovePlayer(dt);
            SpawnBlocks(dt);
            MoveBlocks(dt);
        }

        void MovePlayer(float dt)
        {
            if (Input.GetMouseButtonDown(0))
            {
                dragging = true;
                dragOffset = player.position.x - cam.ScreenToWorldPoint(Input.mousePosition).x;
            }
            if (Input.GetMouseButtonUp(0)) dragging = false;
            if (dragging && Input.GetMouseButton(0))
                targetX = cam.ScreenToWorldPoint(Input.mousePosition).x + dragOffset;

            float key = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) key -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) key += 1f;
            if (key != 0f) targetX = player.position.x + key * 9f * dt;

            float limit = halfW - PlayerRadius - 0.1f;
            targetX = Mathf.Clamp(targetX, -limit, limit);
            Vector3 pos = player.position;
            pos.x = Mathf.MoveTowards(pos.x, targetX, 30f * dt);
            pos.y = -halfH + 1.6f;
            player.position = pos;
        }

        void SpawnBlocks(float dt)
        {
            spawnTimer -= dt;
            if (spawnTimer > 0f) return;
            spawnTimer = Mathf.Max(0.26f, 0.85f - elapsed * 0.012f) * Random.Range(0.8f, 1.2f);

            Block b = pool.Count > 0 ? pool.Pop() : NewBlock();
            b.size = new Vector2(Random.Range(0.7f, 1.8f), Random.Range(0.45f, 0.65f));
            b.speed = Mathf.Min(4f + elapsed * 0.12f, 11f) * Random.Range(0.9f, 1.15f);
            b.sr.size = b.size;
            b.passed = false;
            float lim = Mathf.Max(0f, halfW - b.size.x * 0.5f);
            b.t.position = new Vector3(Random.Range(-lim, lim), halfH + b.size.y, 0f);
            b.t.gameObject.SetActive(true);
            blocks.Add(b);
        }

        Block NewBlock()
        {
            var go = new GameObject("Block");
            var b = new Block { t = go.transform, sr = go.AddComponent<SpriteRenderer>() };
            b.sr.sprite = blockSprite;
            b.sr.drawMode = SpriteDrawMode.Sliced;
            b.sr.color = Art.Block;
            return b;
        }

        void MoveBlocks(float dt)
        {
            Vector2 p = player.position;
            float hitR = PlayerRadius * 0.9f;
            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                Block b = blocks[i];
                Vector3 bp = b.t.position;
                bp.y -= b.speed * dt;
                b.t.position = bp;

                Vector2 h = b.size * 0.5f;
                float cx = Mathf.Clamp(p.x, bp.x - h.x, bp.x + h.x);
                float cy = Mathf.Clamp(p.y, bp.y - h.y, bp.y + h.y);
                if ((p - new Vector2(cx, cy)).sqrMagnitude < hitR * hitR)
                {
                    GameOver();
                    return;
                }

                // A point the moment the block is dodged (its top edge is below the player).
                if (!b.passed && bp.y + h.y < p.y - PlayerRadius)
                {
                    b.passed = true;
                    score++;
                }

                if (bp.y < -halfH - 1f)
                {
                    b.t.gameObject.SetActive(false);
                    blocks.RemoveAt(i);
                    pool.Push(b);
                }
            }
        }

        void GameOver()
        {
            hitFlash = 0.6f;
            if (score > best)
            {
                best = score;
                newBest = true;
                PlayerPrefs.SetInt(BestKey, best);
                PlayerPrefs.Save();
            }
            SetState(State.Over);
        }

        // ---------- UI (IMGUI) ----------

        void BuildStyles()
        {
            styledFor = Screen.width * 10000 + Screen.height;
            u = Mathf.Min(Screen.width, Screen.height) / 100f;

            title = Label(Mathf.RoundToInt(10f * u), Art.Text, FontStyle.Bold);
            big = Label(Mathf.RoundToInt(14f * u), Art.Text, FontStyle.Bold);
            hud = Label(Mathf.RoundToInt(9f * u), Art.Text, FontStyle.Bold);
            text = Label(Mathf.RoundToInt(4.8f * u), Art.Text, FontStyle.Normal);
            sub = Label(Mathf.RoundToInt(4.2f * u), Art.SubText, FontStyle.Normal);

            int bh = Mathf.RoundToInt(13f * u);
            button = Button(bh, Art.Accent, Art.AccentPressed, Art.OnAccent);
            secondary = Button(bh, Art.Card, Art.CardPressed, Art.Text);
            tonal = Button(bh, Art.Tonal, Art.TonalPressed, Art.Text);

            int r = Mathf.RoundToInt(5f * u);
            card = new GUIStyle();
            card.normal.background = Art.RoundedRect(r * 2 + 2, r * 2 + 2, r, Art.Card);
            card.border = new RectOffset(r, r, r, r);
        }

        static GUIStyle Label(int size, Color color, FontStyle fs)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = fs,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            s.normal.textColor = color;
            return s;
        }

        static GUIStyle Button(int height, Color bg, Color pressed, Color fg)
        {
            int r = height / 2;
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(height * 0.36f),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(r, r, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            Texture2D n = Art.RoundedRect(r * 2 + 2, height, r, bg);
            Texture2D a = Art.RoundedRect(r * 2 + 2, height, r, pressed);
            s.normal.background = n; s.hover.background = n; s.focused.background = n;
            s.active.background = a; s.onNormal.background = n; s.onActive.background = a;
            s.normal.textColor = fg; s.hover.textColor = fg; s.active.textColor = fg; s.focused.textColor = fg;
            return s;
        }

        void OnGUI()
        {
            if (styledFor != Screen.width * 10000 + Screen.height) BuildStyles();

            Rect sa = Screen.safeArea;
            // IMGUI y runs from the top; safeArea y from the bottom.
            Rect area = new Rect(sa.x, Screen.height - sa.yMax, sa.width, sa.height);
            float cx = area.x + area.width * 0.5f;
            float bw = Mathf.Min(area.width * 0.7f, 90f * u);
            float bh = 13f * u;

            switch (state)
            {
                case State.Menu:
                    GUI.Label(Centered(cx, area.y + area.height * 0.28f, area.width, 14f * u), "Block Dodge", title);
                    GUI.Label(Centered(cx, area.y + area.height * 0.28f + 11f * u, area.width, 7f * u), "Best  " + best, sub);
                    if (GUI.Button(Centered(cx, area.y + area.height * 0.62f, bw, bh), "Play", button)) StartGame();
                    if (GUI.Button(Centered(cx, area.y + area.height * 0.62f + bh + 4f * u, bw, bh), "About", secondary))
                        SetState(State.About);
                    break;

                case State.Playing:
                    GUI.Label(Centered(cx, area.y + 9f * u, area.width, 12f * u), score.ToString(), hud);
                    if (hintTimer > 0f)
                        GUI.Label(Centered(cx, area.y + area.height * 0.55f, area.width, 8f * u), "Drag to move", sub);
                    break;

                case State.Paused:
                {
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), scrimTex);
                    GUI.Label(Centered(cx, area.y + 9f * u, area.width, 12f * u), score.ToString(), hud);
                    Rect c = Centered(cx, area.y + area.height * 0.5f, bw + 12f * u, 52f * u);
                    GUI.Box(c, GUIContent.none, card);
                    GUI.Label(new Rect(c.x, c.y + 5f * u, c.width, 10f * u), "Paused", text);
                    if (GUI.Button(new Rect(c.x + 6f * u, c.y + 18f * u, bw, bh), "Resume", button)) SetState(State.Playing);
                    if (GUI.Button(new Rect(c.x + 6f * u, c.y + 18f * u + bh + 3f * u, bw, bh), "Menu", tonal))
                    { ResetField(); SetState(State.Menu); }
                    break;
                }

                case State.Over:
                {
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), scrimTex);
                    Rect c = Centered(cx, area.y + area.height * 0.5f, bw + 12f * u, 78f * u);
                    GUI.Box(c, GUIContent.none, card);
                    GUI.Label(new Rect(c.x, c.y + 5f * u, c.width, 8f * u), "Game over", sub);
                    GUI.Label(new Rect(c.x, c.y + 13f * u, c.width, 18f * u), score.ToString(), big);
                    GUI.Label(new Rect(c.x, c.y + 32f * u, c.width, 8f * u), newBest ? "New best" : "Best  " + best, sub);
                    if (GUI.Button(new Rect(c.x + 6f * u, c.y + 44f * u, bw, bh), "Play again", button)) StartGame();
                    if (GUI.Button(new Rect(c.x + 6f * u, c.y + 44f * u + bh + 3f * u, bw, bh), "Menu", tonal))
                    { ResetField(); SetState(State.Menu); }
                    break;
                }

                case State.About:
                {
                    Rect c = Centered(cx, area.y + area.height * 0.5f, bw + 12f * u, 84f * u);
                    GUI.Box(c, GUIContent.none, card);
                    float icon = 22f * u;
                    GUI.DrawTexture(new Rect(cx - icon * 0.5f, c.y + 6f * u, icon, icon), iconTex, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(c.x, c.y + 30f * u, c.width, 9f * u), "Block Dodge", text);
                    GUI.Label(new Rect(c.x, c.y + 38f * u, c.width, 7f * u), "Version " + Application.version, sub);
                    GUI.DrawTexture(new Rect(c.x + 6f * u, c.y + 48f * u, c.width - 12f * u, Mathf.Max(1f, 0.3f * u)), dividerTex);
                    RowLeftRight(new Rect(c.x + 6f * u, c.y + 51f * u, c.width - 12f * u, 10f * u), "Developer", "GoldenBoot");
                    if (GUI.Button(new Rect(c.x + 6f * u, c.y + 84f * u - bh - 6f * u, bw, bh), "Back", tonal))
                        SetState(State.Menu);
                    break;
                }
            }
        }

        void RowLeftRight(Rect r, string left, string right)
        {
            var l = new GUIStyle(sub) { alignment = TextAnchor.MiddleLeft };
            var rt = new GUIStyle(text) { alignment = TextAnchor.MiddleRight };
            GUI.Label(r, left, l);
            GUI.Label(r, right, rt);
        }

        static Rect Centered(float cx, float cy, float w, float h)
        {
            return new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
        }
    }
}
