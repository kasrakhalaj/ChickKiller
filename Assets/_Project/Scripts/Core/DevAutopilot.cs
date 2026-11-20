#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace ChickenShooter.Core
{
    /// <summary>
    /// DEV ONLY (stripped from release builds). Activated with the "-autopilot" command-line flag:
    /// jumps to a level/round, injects keyboard input through the Input System (move + fire + missile),
    /// captures screenshots at given times and logs gameplay events. Used for automated smoke tests.
    /// Args: -autopilot -apLevel 1 -apRound 1 -apShots 3,8,15 -apQuit 20 -apOut path -apGod
    /// </summary>
    public class DevAutopilot : MonoBehaviour
    {
        private int level = 1, round = 1;
        private float quitAfter = 20f;
        private float[] shots = { 3f, 8f, 15f };
        private string outDir;
        private bool god;
        private StreamWriter log;
        private bool holdSpecial;
        private Data.SpecialWeaponSO lastSpecial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autopilot") < 0) return;
            var go = new GameObject("[DevAutopilot]");
            DontDestroyOnLoad(go);
            go.AddComponent<DevAutopilot>();
        }

        private static string Arg(string name)
        {
            string[] a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        private void Awake()
        {
            if (Arg("-apLevel") is { } l) level = int.Parse(l);
            if (Arg("-apRound") is { } r) round = int.Parse(r);
            if (Arg("-apQuit") is { } q) quitAfter = float.Parse(q, System.Globalization.CultureInfo.InvariantCulture);
            if (Arg("-apShots") is { } s)
                shots = Array.ConvertAll(s.Split(','), x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture));
            god = Array.IndexOf(Environment.GetCommandLineArgs(), "-apGod") >= 0;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            outDir = Arg("-apOut") ?? Application.persistentDataPath;
            Directory.CreateDirectory(outDir);
            log = new StreamWriter(Path.Combine(outDir, $"autopilot_L{level}R{round}.log")) { AutoFlush = true };
            Subscribe();
        }

        private void Subscribe()
        {
            GameEvents.LevelStarted += lv => Log($"LevelStarted {lv.name}");
            GameEvents.RoundStarted += i => Log($"RoundStarted L{i.LevelNumber} R{i.RoundNumber}/{i.TotalRounds} {i.WaveType} '{i.Label}'");
            GameEvents.RoundCleared += r => Log($"RoundCleared {r}");
            GameEvents.LevelCompleted += lv => Log($"LevelCompleted {lv.name}");
            GameEvents.GameCompleted += () => Log("GameCompleted");
            GameEvents.LivesChanged += v => Log($"Lives {v}");
            GameEvents.PowerLevelChanged += (v, m) => Log($"Power {v}/{m}");
            GameEvents.MissilesChanged += v => Log($"Missiles {v}");
            GameEvents.MissileDetonated += _ => Log("MissileDetonated");
            GameEvents.BossSpawned += n => Log($"BossSpawned {n}");
            GameEvents.BossHealthChanged += (c, m) => { if (c <= 0) Log("BossHealth 0"); };
            GameEvents.BossDefeated += () => Log("BossDefeated");
            GameEvents.BossPhaseChanged += (i, label) => Log($"BossPhase {i} '{label}'");
            GameEvents.GameOver += () => Log("GameOver");
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error) Log($"[{type}] {msg}\n{stack}");
            };
        }

        private void Log(string msg) => log?.WriteLine($"{Time.realtimeSinceStartup:F1}s  {msg}");

        private IEnumerator Start()
        {
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(1.5f);
            if (SceneManager.GetActiveScene().name == "MainMenu")
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "shot_menu.png"));
                yield return new WaitForSecondsRealtime(0.5f);
            }

            GameManager gm = GameManager.Instance;
            PlayerRunState state = PlayerRunState.CreateNew(gm.Config);
            state.levelIndex = level - 1;
            state.roundIndex = round - 1;
            if (god) state.lives = 99;
            if (Arg("-apPower") is { } pw) state.powerLevel = int.Parse(pw);
            bool specials = Array.IndexOf(Environment.GetCommandLineArgs(), "-apSpecials") >= 0;
            gm.StartFrom(state);
            Log($"Autopilot start L{level} R{round} power {state.powerLevel}");
            if (specials)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                int pick = Arg("-apSpecial") is { } sp ? int.Parse(sp) : 0;
                gm.AddSpecialAmmo(pick, gm.Config.specialWeapons[pick].ammoPerPickup);
                GameEvents.SpecialWeaponChanged += (w, a) => { if (w != lastSpecial) { lastSpecial = w; Log($"Special -> {(w ? w.displayName : "none")} ({a:0.#})"); } };
            }
            holdSpecial = specials;

            float start = Time.realtimeSinceStartup;
            int shot = 0;
            float nextMissile = Array.IndexOf(Environment.GetCommandLineArgs(), "-apNoMissile") >= 0 ? float.MaxValue : 6f;
            while (Time.realtimeSinceStartup - start < quitAfter)
            {
                float t = Time.realtimeSinceStartup - start;
                InjectInput(t, ref nextMissile);
                if (shot < shots.Length && t >= shots[shot])
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(outDir, $"shot_L{level}R{round}_{shots[shot]:00}s.png"));
                    shot++;
                }
                yield return null;
            }

            Log($"Autopilot end. Score={gm.State.score} Lives={gm.State.lives} Round={gm.State.roundIndex + 1}");
            Application.Quit();
        }

        private void InjectInput(float t, ref float nextMissile)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            bool left = Mathf.Sin(t * 1.3f) > 0.25f;
            bool right = Mathf.Sin(t * 1.3f) < -0.25f;
            bool missile = t >= nextMissile && t < nextMissile + 0.1f;
            if (t >= nextMissile + 0.1f) nextMissile += 9f;

            var keys = new System.Collections.Generic.List<Key> { Key.Space };
            if (left) keys.Add(Key.A);
            if (right) keys.Add(Key.D);
            if (missile) keys.Add(Key.X);
            if (holdSpecial)
            {
                keys.Add(Key.C);
            }
            InputSystem.QueueStateEvent(kb, new KeyboardState(keys.ToArray()));
        }

        private void OnDestroy() => log?.Dispose();
    }
}
#endif
