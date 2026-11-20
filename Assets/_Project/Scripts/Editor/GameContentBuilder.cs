using System;
using System.IO;
using System.Linq;
using ChickenShooter.Combat;
using ChickenShooter.Data;
using ChickenShooter.Enemies;
using ChickenShooter.Levels;
using ChickenShooter.Player;
using ChickenShooter.Pooling;
using ChickenShooter.UI;
using ChickenShooter.Waves;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChickenShooter.EditorTools
{
    /// <summary>
    /// Generates all game content from code: physics layers, input actions, VFX, prefabs,
    /// ScriptableObject data (weapon, enemies, entry paths, waves, levels, config) and the 4 scenes.
    /// Idempotent: re-running updates existing assets in place (GUIDs and references are preserved).
    /// Menu: Tools/Chick Killer/Build Game Content.
    /// </summary>
    public static class GameContentBuilder
    {
        private const string P = "Assets/_Project";
        private const string TP = "Assets/ThirdParty/";

        private const int LayerPlayer = 6, LayerPlayerShot = 7, LayerEnemy = 8, LayerEnemyShot = 9, LayerPickup = 10;

        // ------------------------------------------------------------------ build context

        private class Ctx
        {
            public InputActionAsset input;
            public Font font;
            public GameObject vfxExplosion, vfxFeathers, vfxEggSplat, vfxSpark, vfxShockwave, vfxHeal, vfxRevive, vfxCharge;
            public Projectile laser;
            public EnemyProjectile egg, needle, sniperBolt, slimeOrb, clusterBomb, shard;
            public Pickup foodDrumstick, foodBurger, gift;
            public Missile missile;
            public Chicken chicken, henArmored, henZombie, henNurse;
            public RogueEnemy stinger, bomber, gunner, minion;
            public SpecialWeaponSO[] specialWeapons;
            public SpecialWeaponPickup specialCrate;
            public Asteroid asteroid;
            public BossController boss;
            public WeaponDataSO weapon;
            public GameObject player;
            public LevelDataSO[] levels;
        }

        [MenuItem("Tools/Chick Killer/Build Game Content")]
        public static void BuildAll()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string f in new[] { "Prefabs", "Prefabs/VFX", "Prefabs/Projectiles", "Prefabs/Enemies", "Materials", "Resources", "Data/Paths" })
                    EnsureFolder($"{P}/{f}");
                // Prefabs moved into sub-folders by newer builder versions.
                foreach (string old in new[] { "Prefabs/Egg.prefab", "Prefabs/Chicken.prefab" })
                    if (AssetDatabase.LoadAssetAtPath<GameObject>($"{P}/{old}") != null) AssetDatabase.DeleteAsset($"{P}/{old}");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            var ctx = new Ctx();
            SetupPhysicsLayers();
            ctx.input = BuildInputActions();
            ctx.font = LoadUiFont();
            BuildVfx(ctx);
            BuildPrefabs(ctx);
            BuildData(ctx);
            BuildScenes(ctx);
            ProjectSetup.Run();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GameContentBuilder] Build complete.");
        }

        // ================================================================== project settings

        private static void SetupPhysicsLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(LayerPlayer).stringValue = "Player";
            layers.GetArrayElementAtIndex(LayerPlayerShot).stringValue = "PlayerProjectile";
            layers.GetArrayElementAtIndex(LayerEnemy).stringValue = "Enemy";
            layers.GetArrayElementAtIndex(LayerEnemyShot).stringValue = "EnemyProjectile";
            layers.GetArrayElementAtIndex(LayerPickup).stringValue = "Pickup";
            tagManager.ApplyModifiedPropertiesWithoutUndo();

            int[] ours = { LayerPlayer, LayerPlayerShot, LayerEnemy, LayerEnemyShot, LayerPickup };
            (int, int)[] allowed =
            {
                (LayerPlayer, LayerEnemy), (LayerPlayer, LayerEnemyShot), (LayerPlayer, LayerPickup),
                (LayerPlayerShot, LayerEnemy), (LayerPlayerShot, LayerEnemyShot)
            };

            for (int i = 0; i < 32; i++)
            for (int j = 0; j < 32; j++)
            {
                if (!ours.Contains(i) && !ours.Contains(j)) continue;
                bool collide = allowed.Contains((i, j)) || allowed.Contains((j, i));
                Physics2D.IgnoreLayerCollision(i, j, !collide);
            }

            // Persist the matrix into ProjectSettings/Physics2DSettings.asset.
            Object physics2D = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset")[0];
            var so = new SerializedObject(physics2D);
            SerializedProperty matrix = so.FindProperty("m_LayerCollisionMatrix");
            if (matrix != null && matrix.isArray)
            {
                for (int i = 0; i < 32 && i < matrix.arraySize; i++)
                {
                    uint mask = 0;
                    for (int j = 0; j < 32; j++)
                        if (!Physics2D.GetIgnoreLayerCollision(i, j)) mask |= 1u << j;
                    matrix.GetArrayElementAtIndex(i).longValue = mask;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static InputActionAsset BuildInputActions()
        {
            string path = $"{P}/Input/GameControls.inputactions";
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "GameControls";
            InputActionMap map = asset.AddActionMap("Gameplay");

            InputAction move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            InputAction fire = map.AddAction("Fire", InputActionType.Button);
            fire.AddBinding("<Keyboard>/space");
            fire.AddBinding("<Mouse>/leftButton");
            fire.AddBinding("<Gamepad>/buttonSouth");

            InputAction special = map.AddAction("Special", InputActionType.Button);
            special.AddBinding("<Mouse>/rightButton");
            special.AddBinding("<Keyboard>/c");
            special.AddBinding("<Gamepad>/rightTrigger");


            InputAction missile = map.AddAction("Missile", InputActionType.Button);
            missile.AddBinding("<Keyboard>/x");
            missile.AddBinding("<Gamepad>/buttonEast");

            InputAction pause = map.AddAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Keyboard>/p");
            pause.AddBinding("<Gamepad>/start");

            File.WriteAllText(path, asset.ToJson());
            Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        // ================================================================== VFX

        private static void BuildVfx(Ctx c)
        {
            Material spark = ParticleMaterial("Mat_Spark", "Kenney_ParticlePack/spark_05.png");
            Material soft = ParticleMaterial("Mat_Soft", "Kenney_ParticlePack/circle_05.png");
            Material ring = ParticleMaterial("Mat_Ring", "Kenney_ParticlePack/circle_01.png");
            Material feather = ParticleMaterial("Mat_Feather", null, Art("feather"));

            c.vfxExplosion = ParticlePrefab("VFX_Explosion", spark, new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.1f),
                burst: 28, speed: new Vector2(2f, 7f), size: new Vector2(0.15f, 0.45f), life: new Vector2(0.25f, 0.6f));
            c.vfxFeathers = ParticlePrefab("VFX_Feathers", feather, Color.white, new Color(1f, 0.92f, 0.7f),
                burst: 14, speed: new Vector2(1f, 3.5f), size: new Vector2(0.2f, 0.4f), life: new Vector2(0.6f, 1.1f), gravity: 0.25f);
            c.vfxEggSplat = ParticlePrefab("VFX_EggSplat", soft, new Color(1f, 0.95f, 0.5f), Color.white,
                burst: 10, speed: new Vector2(1f, 3f), size: new Vector2(0.08f, 0.22f), life: new Vector2(0.2f, 0.45f));
            c.vfxSpark = ParticlePrefab("VFX_HitSpark", spark, new Color(0.6f, 0.9f, 1f), Color.white,
                burst: 6, speed: new Vector2(1.5f, 4f), size: new Vector2(0.08f, 0.2f), life: new Vector2(0.1f, 0.25f));
            c.vfxShockwave = ParticlePrefab("VFX_Shockwave", ring, new Color(1f, 1f, 1f, 0.8f), new Color(0.6f, 0.9f, 1f, 0.8f),
                burst: 1, speed: Vector2.zero, size: new Vector2(2f, 2f), life: new Vector2(0.7f, 0.7f), growTo: 18f);
            c.vfxHeal = ParticlePrefab("VFX_HealPulse", ring, new Color(0.4f, 1f, 0.5f, 0.7f), new Color(0.7f, 1f, 0.7f, 0.7f),
                burst: 1, speed: Vector2.zero, size: new Vector2(1f, 1f), life: new Vector2(0.6f, 0.6f), growTo: 18f);
            c.vfxRevive = ParticlePrefab("VFX_Revive", spark, new Color(0.5f, 1f, 0.4f), new Color(0.9f, 1f, 0.6f),
                burst: 16, speed: new Vector2(1f, 3.5f), size: new Vector2(0.12f, 0.3f), life: new Vector2(0.3f, 0.7f));
            c.vfxCharge = ParticlePrefab("VFX_Charge", spark, new Color(1f, 0.9f, 0.3f), new Color(1f, 0.5f, 0.2f),
                burst: 10, speed: new Vector2(1.5f, 4f), size: new Vector2(0.1f, 0.25f), life: new Vector2(0.15f, 0.35f));
        }

        private const string FlashShader = "ChickenShooter/SpriteFlash";

        /// <summary>Material using the SpriteFlash shader. Intensity > 1 makes the sprite bloom.</summary>
        private static Material FlashMat(string name, float intensity)
        {
            string path = $"{P}/Materials/{name}.mat";
            Shader shader = Shader.Find(FlashShader);
            if (shader == null) throw new Exception("[GameContentBuilder] Shader " + FlashShader + " not found");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetFloat("_Intensity", intensity);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void UseMat(GameObject go, Material mat) => go.GetComponent<SpriteRenderer>().sharedMaterial = mat;

        private static Material ParticleMaterial(string name, string texturePath, Sprite sprite = null)
        {
            string path = $"{P}/Materials/{name}.mat";
            Shader shader = Shader.Find(FlashShader) ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.mainTexture = (sprite != null ? sprite : LoadSprite(texturePath)).texture;
            if (mat.HasProperty("_Intensity")) mat.SetFloat("_Intensity", 1.8f); // particles bloom
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject ParticlePrefab(string name, Material mat, Color a, Color b, int burst, Vector2 speed,
                                                 Vector2 size, Vector2 life, float gravity = 0f, float growTo = 0f)
        {
            var go = new GameObject(name);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ConfigureBurst(ps, mat, a, b, burst, speed, size, life, gravity, growTo);
            go.AddComponent<PooledParticle>();
            return SavePrefab(go, $"{P}/Prefabs/VFX/{name}.prefab");
        }

        private static void ConfigureBurst(ParticleSystem ps, Material mat, Color a, Color b, int burst, Vector2 speed,
                                           Vector2 size, Vector2 life, float gravity, float growTo)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.duration = Mathf.Max(0.1f, life.y);
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(burst * 2, 10);
            main.stopAction = ParticleSystemStopAction.Callback;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = growTo > 0f
                ? new ParticleSystem.MinMaxCurve(growTo / Mathf.Max(0.01f, size.y), AnimationCurve.EaseInOut(0f, 0.05f, 1f, 1f))
                : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingOrder = 20;
        }

        // ================================================================== prefabs

        private static void BuildPrefabs(Ctx c)
        {
            // --- Player laser
            {
                GameObject go = SpriteObject("PlayerLaser", "Kenney_SpaceShooterRedux/Lasers/laserBlue01.png", 4, LayerPlayerShot);
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(0.12f, 0.5f);
                AddBody(go, continuous: true);
                UseMat(go, FlashMat("Mat_PlayerGlow", 2.2f));
                Projectile p = go.AddComponent<Projectile>();
                Set(p, "hitVfx", c.vfxSpark);
                c.laser = SavePrefab(go, $"{P}/Prefabs/PlayerLaser.prefab").GetComponent<Projectile>();
            }

            BuildEnemyProjectiles(c);

            // --- Food & gift pickups
            c.foodDrumstick = PickupPrefab<FoodPickup>("Food_Drumstick", "FoodSmooth/drumstick.png", 0.26f, 0.75f);
            c.foodBurger = PickupPrefab<FoodPickup>("Food_Burger", "FoodSmooth/burger.png", 0.24f, 0.75f);
            c.gift = PickupPrefab<GiftBoxPickup>("GiftBox", "Kenney_SpaceShooterRedux/Power-ups/bolt_gold.png", 1.6f, 0.17f);

            // --- Missile
            {
                GameObject go = SpriteObject("Missile", "Kenney_SpaceShooterRedux/Lasers/laserRed08.png", 9, 0, 1.6f);
                GameObject flame = SpriteObject("Flame", "Kenney_SpaceShooterRedux/Effects/fire08.png", 8, 0);
                flame.transform.SetParent(go.transform, false);
                flame.transform.localPosition = new Vector3(0f, -0.35f, 0f);
                flame.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                Missile m = go.AddComponent<Missile>();
                Set(m, "shockwaveVfx", c.vfxShockwave);
                Set(m, "explosionVfx", c.vfxExplosion);
                c.missile = SavePrefab(go, $"{P}/Prefabs/Missile.prefab").GetComponent<Missile>();
            }

            BuildEnemies(c);

            // --- Asteroid
            {
                GameObject go = SpriteObject("Asteroid", "Kenney_SpaceShooterRedux/Meteors/meteorBrown_big1.png", 4, LayerEnemy);
                AddCircle(go, 0.45f);
                AddBody(go, continuous: false);
                UseMat(go, FlashMat("Mat_Enemy", 1f));
                go.AddComponent<SpriteFlasher>();
                Asteroid a = go.AddComponent<Asteroid>();
                Set(a, "destroyVfx", c.vfxExplosion);
                Set(a, "hitVfx", c.vfxSpark);
                c.asteroid = SavePrefab(go, $"{P}/Prefabs/Asteroid.prefab").GetComponent<Asteroid>();
            }

            // --- Boss (one prefab, per-level look/stats come from EnemyDataSO)
            {
                GameObject go = SpriteObject("Boss", "Bevouliin_FlappyChicken/fly-frame-1.png", 6, LayerEnemy);
                SpriteAnimator anim = go.AddComponent<SpriteAnimator>();
                AddCircle(go, 0.4f);
                AddBody(go, continuous: false);
                UseMat(go, FlashMat("Mat_Enemy", 1f));
                go.AddComponent<SpriteFlasher>();
                BossController b = go.AddComponent<BossController>();
                Set(b, "animator", anim);
                c.boss = SavePrefab(go, $"{P}/Prefabs/Boss.prefab").GetComponent<BossController>();
            }

            // --- Weapon data (needed by the player prefab)
            c.weapon = BuildWeapon(c);
            BuildSpecialWeapons(c);

            // --- Player
            {
                var go = new GameObject("Player") { layer = LayerPlayer };
                Rigidbody2D rb = AddBody(go, continuous: true);
                rb.freezeRotation = true;
                rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
                AddCircle(go, 0.3f);

                // "Visual" is the tilt pivot (PlayerController banks it); "Hull" is the ship sprite.
                var visual = new GameObject("Visual") { layer = LayerPlayer };
                visual.transform.SetParent(go.transform, false);
                GameObject hull = SpriteObject("Hull", PlayerShipSprite(), 10, LayerPlayer, 0.24f);
                hull.transform.SetParent(visual.transform, false);

                GameObject exhaust = new GameObject("Exhaust");
                exhaust.transform.SetParent(visual.transform, false);
                exhaust.transform.localPosition = new Vector3(0f, -0.5f, 0f);
                BuildExhaust(exhaust, ParticleMaterial("Mat_Flame", "Kenney_ParticlePack/flame_02.png"));

                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(go.transform, false);
                muzzle.localPosition = new Vector3(0f, 0.15f, 0f);

                PlayerInputReader input = go.AddComponent<PlayerInputReader>();
                Set(input, "actions", c.input);
                PlayerController pc = go.AddComponent<PlayerController>();
                Set(pc, "visual", visual.transform);
                PlayerWeapon weapon = go.AddComponent<PlayerWeapon>();
                Set(weapon, "weapon", c.weapon);
                Set(weapon, "muzzle", muzzle);
                PlayerHealth health = go.AddComponent<PlayerHealth>();
                SetArray(health, "blinkRenderers", new Object[] { hull.GetComponent<SpriteRenderer>() });
                Set(health, "deathVfx", c.vfxExplosion);
                Set(health, "hitVfx", c.vfxSpark);
                MissileLauncher launcher = go.AddComponent<MissileLauncher>();
                Set(launcher, "missilePrefab", c.missile);

                // Laser beam (used by the Laser Beam special weapon)
                var beamRoot = new GameObject("LaserBeam");
                beamRoot.transform.SetParent(muzzle, false);
                GameObject beamGlow = SpriteObject("BeamGlow", Art("soft_glow"), 8, LayerPlayer);
                beamGlow.transform.SetParent(beamRoot.transform, false);
                UseMat(beamGlow, FlashMat("Mat_PlayerGlow", 2.2f));
                GameObject beamSprite = SpriteObject("BeamCore", Art("bullet_glow"), 9, LayerPlayer);
                beamSprite.transform.SetParent(beamRoot.transform, false);
                UseMat(beamSprite, FlashMat("Mat_PlayerGlow", 2.2f));
                GameObject beamFlare = SpriteObject("MuzzleFlare", Art("orb_glow"), 11, LayerPlayer);
                beamFlare.transform.SetParent(beamRoot.transform, false);
                UseMat(beamFlare, FlashMat("Mat_PlayerGlow", 2.2f));
                LaserBeam beam = beamRoot.AddComponent<LaserBeam>();
                Set(beam, "beamRenderer", beamSprite.GetComponent<SpriteRenderer>());
                Set(beam, "glowRenderer", beamGlow.GetComponent<SpriteRenderer>());
                Set(beam, "muzzleFlare", beamFlare.GetComponent<SpriteRenderer>());
                SetFloat(beam, "width", 0.6f);
                Set(beam, "hitVfx", c.vfxSpark);
                SetInt(beam, "hitMask", (1 << LayerEnemy) | (1 << LayerEnemyShot));

                PlayerSpecialWeapons specials = go.AddComponent<PlayerSpecialWeapons>();
                Set(specials, "muzzle", muzzle);
                Set(specials, "beam", beam);

                // Coloured aura around the ship while a special weapon is loaded
                GameObject aura = SpriteObject("SpecialAura", Art("soft_glow"), 9, LayerPlayer, 2.6f);
                aura.transform.SetParent(go.transform, false);
                UseMat(aura, FlashMat("Mat_Aura", 1.3f));
                Set(specials, "aura", aura.GetComponent<SpriteRenderer>());
                Set(specials, "acquireVfx", c.vfxCharge);

                c.player = SavePrefab(go, $"{P}/Prefabs/Player.prefab");
            }
        }

        private static void BuildExhaust(GameObject go, Material mat)
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.3f), new Color(0.4f, 0.7f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 45f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.04f;
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(-3f, -2f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingOrder = 9;
        }

        // ------------------------------------------------------------------ enemy projectiles

        private static void BuildEnemyProjectiles(Ctx c)
        {
            const string L = "Kenney_SpaceShooterRedux/Lasers/";

            // Egg: classic dropped egg, spins.
            c.egg = EnemyShot<EnemyProjectile>("Egg", "FoodSmooth/egg.png", 0.17f, 0.9f, c.vfxEggSplat, null, halo: 0.85f);

            // Needle: thin aimed stinger, aligned to velocity.
            c.needle = EnemyShot<EnemyProjectile>("Needle", L + "laserRed07.png", 1f, 0.07f, c.vfxSpark, halo: 0.45f, configure:
                p => SetBool(p, "alignToVelocity", true));

            // Sniper bolt: big aimed red bolt, cannot be shot down (dodge it!).
            c.sniperBolt = EnemyShot<EnemyProjectile>("SniperBolt", L + "laserRed14.png", 1.2f, 0.1f, c.vfxSpark, halo: 0.8f, configure: p =>
            {
                SetBool(p, "alignToVelocity", true);
                SetBool(p, "shootable", false);
            });

            // Slime orb: slow homing projectile of zombie hens.
            c.slimeOrb = EnemyShot<HomingProjectile>("SlimeOrb", L + "laserGreen14.png", 0.65f, 0.2f, c.vfxEggSplat, null, halo: 0.8f);

            // Shard: small spinning star, product of cluster bombs.
            c.shard = EnemyShot<EnemyProjectile>("Shard", L + "laserRed08.png", 0.45f, 0.18f, c.vfxSpark, halo: 0.4f, configure:
                p => SetFloat(p, "scoreWhenShot", 0f));

            // Cluster bomb: blinking egg that bursts into shards.
            c.clusterBomb = EnemyShot<ClusterProjectile>("ClusterBomb", "FoodSmooth/egg.png", 0.21f, 0.9f, c.vfxExplosion, halo: 1f, configure: p =>
            {
                Set(p, "shardPrefab", c.shard);
                Set(p, "burstVfx", c.vfxExplosion);
                SetFloat(p, "scoreWhenShot", 25f);
            });
        }

        private static T EnemyShot<T>(string name, string sprite, float scale, float radius, GameObject breakVfx,
                                      Action<T> configure, float halo = 0.7f) where T : EnemyProjectile
        {
            GameObject go = SpriteObject(name, sprite, 6, LayerEnemyShot, scale);
            AddCircle(go, radius);

            // Every hostile projectile carries the same red glow so it reads as "danger" on any background.
            GameObject haloGo = SpriteObject("HostileHalo", Art("soft_glow"), 5, LayerEnemyShot);
            haloGo.transform.SetParent(go.transform, false);
            haloGo.transform.localScale = Vector3.one * (halo / Art("soft_glow").bounds.size.x / scale);
            haloGo.GetComponent<SpriteRenderer>().color = new Color(1f, 0.08f, 0.04f, 0.9f);
            UseMat(haloGo, FlashMat("Mat_EnemyGlow", 1.6f));
            AddBody(go, continuous: true);
            T p = go.AddComponent<T>();
            Set(p, "breakVfx", breakVfx);
            configure?.Invoke(p);
            return SavePrefab(go, $"{P}/Prefabs/Projectiles/{name}.prefab").GetComponent<T>();
        }

        // ------------------------------------------------------------------ enemies

        private static void BuildEnemies(Ctx c)
        {
            // Flock members: same Chicken FSM, mechanics come from ability components.
            c.chicken = FlockPrefab("Chicken", "Bevouliin_FlappyChicken/fly-frame-1.png", 0.38f, null);
            c.henArmored = FlockPrefab("Hen_Helmet", "Bevouliin_Birds/HelmetBird/flying-frame-1.png", 0.36f, go =>
            {
                ArmorPlating armor = go.AddComponent<ArmorPlating>();
                Set(armor, "pingVfx", c.vfxSpark);
                Set(armor, "breakVfx", c.vfxExplosion);
            });
            c.henZombie = FlockPrefab("Hen_Zombie", "Bevouliin_Birds/ZombieBird/flying-frame-1.png", 0.4f, go =>
            {
                ReviveOnDeath revive = go.AddComponent<ReviveOnDeath>();
                Set(revive, "downVfx", c.vfxFeathers);
                Set(revive, "reviveVfx", c.vfxRevive);
            });
            c.henNurse = FlockPrefab("Hen_Nurse", "Bevouliin_Birds/PinkBird/fly-frame-1.png", 0.34f, go =>
            {
                HealerAura aura = go.AddComponent<HealerAura>();
                Set(aura, "pulseVfx", c.vfxHeal);
            });

            // Free agents: each has its own behaviour state machine.
            c.stinger = RoguePrefab<StingerBird>("Rogue_Stinger", "Bevouliin_Birds/AngryBeeBird/flying-frame-1.png", 0.32f);
            c.bomber = RoguePrefab<BomberBird>("Rogue_Bomber", "Bevouliin_Birds/GrumpyBird/fly-frame-1.png", 0.3f);
            c.gunner = RoguePrefab<GunnerDuck>("Rogue_SniperDuck", "Bevouliin_Birds/CapDuck/idle-frame-1.png", 0.45f);
            c.minion = RoguePrefab<MinionDiver>("Rogue_MinionChick", "Bevouliin_FlappyChicken/fly-frame-1.png", 0.36f);
        }

        private static Chicken FlockPrefab(string name, string sprite, float radius, Action<GameObject> addAbilities)
        {
            GameObject go = SpriteObject(name, sprite, 5, LayerEnemy);
            SpriteAnimator anim = go.AddComponent<SpriteAnimator>();
            AddCircle(go, radius);
            AddBody(go, continuous: false);
            UseMat(go, FlashMat("Mat_Enemy", 1f));
            go.AddComponent<SpriteFlasher>();
            Chicken ch = go.AddComponent<Chicken>();   // before abilities: HealerAura requires a Chicken
            Set(ch, "animator", anim);
            addAbilities?.Invoke(go);
            return SavePrefab(go, $"{P}/Prefabs/Enemies/{name}.prefab").GetComponent<Chicken>();
        }

        private static T RoguePrefab<T>(string name, string sprite, float radius) where T : RogueEnemy
        {
            GameObject go = SpriteObject(name, sprite, 6, LayerEnemy);
            SpriteAnimator anim = go.AddComponent<SpriteAnimator>();
            AddCircle(go, radius);
            AddBody(go, continuous: false);
            UseMat(go, FlashMat("Mat_Enemy", 1f));
            go.AddComponent<SpriteFlasher>();
            T rogue = go.AddComponent<T>();
            Set(rogue, "animator", anim);
            return SavePrefab(go, $"{P}/Prefabs/Enemies/{name}.prefab").GetComponent<T>();
        }

        private static T PickupPrefab<T>(string name, string sprite, float scale, float radius) where T : Pickup
        {
            GameObject go = SpriteObject(name, sprite, 3, LayerPickup, scale);
            AddCircle(go, radius);
            AddBody(go, continuous: false);
            go.AddComponent<T>();
            return SavePrefab(go, $"{P}/Prefabs/{name}.prefab").GetComponent<T>();
        }

        // ================================================================== data

        private static WeaponDataSO BuildWeapon(Ctx c)
        {
            var w = LoadOrCreate<WeaponDataSO>($"{P}/Data/Weapons/Weapon_IonBlaster.asset");
            w.weaponName = "Ion Blaster";
            w.icon = LoadSprite("Kenney_SpaceShooterRedux/Power-ups/powerupBlue_bolt.png");
            w.projectilePrefab = c.laser;
            // 8 power levels (gift boxes +1, every hit -1). Damage per bullet dips at level 3 and the
            // higher levels win through wider patterns and fire rate instead of raw damage.
            w.powerLevels = new[]
            {
                Power("Single Laser", "laserBlue01", 1f, 16f, 7f, 1f,
                    S(0f, 0.45f, 0f)),
                Power("Twin Laser", "laserBlue07", 1f, 17f, 7.5f, 1f,
                    S(-0.2f, 0.4f, 0f), S(0.2f, 0.4f, 0f)),
                Power("Spread Shot", "laserBlue15", 0.85f, 18f, 8f, 1f,
                    S(0f, 0.45f, 0f), S(-0.15f, 0.4f, 14f), S(0.15f, 0.4f, -14f)),
                Power("Quad Spread", "laserBlue14", 0.85f, 18f, 8.5f, 1f,
                    S(-0.15f, 0.45f, 0f), S(0.15f, 0.45f, 0f), S(-0.3f, 0.4f, 12f), S(0.3f, 0.4f, -12f)),
                Power("Penta Wave", "laserBlue02", 0.8f, 19f, 9f, 1f,
                    S(-0.25f, 0.4f, 0f), S(0f, 0.45f, 0f), S(0.25f, 0.4f, 0f), S(-0.3f, 0.35f, 16f), S(0.3f, 0.35f, -16f)),
                Power("Hepta Fan", "laserBlue04", 0.6f, 19f, 9.5f, 1f,
                    S(-0.25f, 0.4f, 0f), S(0f, 0.45f, 0f), S(0.25f, 0.4f, 0f), S(-0.3f, 0.35f, 10f), S(0.3f, 0.35f, -10f),
                    S(-0.35f, 0.3f, 22f), S(0.35f, 0.3f, -22f)),
                Power("Plasma Fan", "laserBlue06", 0.6f, 20f, 10.5f, 1.1f,
                    S(-0.25f, 0.4f, 0f), S(0f, 0.45f, 0f), S(0.25f, 0.4f, 0f), S(-0.3f, 0.35f, 10f), S(0.3f, 0.35f, -10f),
                    S(-0.35f, 0.3f, 22f), S(0.35f, 0.3f, -22f)),
                Power("Ion Storm", "laserBlue12", 0.62f, 21f, 11f, 1.15f,
                    S(-0.25f, 0.4f, 0f), S(0f, 0.45f, 0f), S(0.25f, 0.4f, 0f), S(-0.3f, 0.35f, 8f), S(0.3f, 0.35f, -8f),
                    S(-0.35f, 0.3f, 16f), S(0.35f, 0.3f, -16f), S(-0.4f, 0.25f, 28f), S(0.4f, 0.25f, -28f))
            };
            EditorUtility.SetDirty(w);
            return w;
        }

        // ------------------------------------------------------------------ special weapons

        private static void BuildSpecialWeapons(Ctx c)
        {
            const string K = "Kenney_SpaceShooterRedux/";
            EnsureFolder($"{P}/Data/SpecialWeapons");

            Material trailMat = ParticleMaterial("Mat_Trail", null, Art("orb_glow"));

            // Glowing tintable bullet shared by the minigun and the flak cannon (with a coloured trail)
            GameObject bulletGo = SpriteObject("SpecialBullet", Art("bullet_glow"), 7, LayerPlayerShot);
            var bulletBox = bulletGo.AddComponent<BoxCollider2D>();
            bulletBox.isTrigger = true;
            bulletBox.size = new Vector2(0.2f, 0.7f);
            AddBody(bulletGo, continuous: true);
            AddTrail(bulletGo, trailMat, 0.18f, 0.12f, 6);
            UseMat(bulletGo, FlashMat("Mat_PlayerGlow", 2.2f));
            Projectile bulletComp = bulletGo.AddComponent<Projectile>();
            Set(bulletComp, "hitVfx", c.vfxSpark);
            Projectile specialBullet = SavePrefab(bulletGo, $"{P}/Prefabs/Projectiles/SpecialBullet.prefab").GetComponent<Projectile>();

            // Homing rocket projectile
            GameObject rocketGo = SpriteObject("HomingRocket", K + "Parts/gun09.png", 4, LayerPlayerShot, 1f);
            rocketGo.GetComponent<SpriteRenderer>().color = new Color(0.75f, 0.9f, 1f);
            GameObject flame = SpriteObject("Flame", K + "Effects/fire08.png", 3, LayerPlayerShot, 0.8f);
            flame.transform.SetParent(rocketGo.transform, false);
            flame.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            flame.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            AddTrail(rocketGo, trailMat, 0.3f, 0.16f, 3);
            var rocketBox = rocketGo.AddComponent<BoxCollider2D>();
            rocketBox.isTrigger = true;
            rocketBox.size = new Vector2(0.18f, 0.45f);
            AddBody(rocketGo, continuous: true);
            HomingRocket rocketComp = rocketGo.AddComponent<HomingRocket>();
            Set(rocketComp, "hitVfx", c.vfxExplosion);
            HomingRocket rocket = SavePrefab(rocketGo, $"{P}/Prefabs/Projectiles/HomingRocket.prefab").GetComponent<HomingRocket>();

            // Saw blade projectile
            GameObject sawGo = SpriteObject("SawBlade", K + "Effects/star3.png", 6, LayerPlayerShot, 4.4f);
            sawGo.GetComponent<SpriteRenderer>().color = new Color(0.85f, 0.95f, 1f);
            AddCircle(sawGo, 0.11f);
            AddTrail(sawGo, trailMat, 0.25f, 0.2f, 5, new Color(0.7f, 0.85f, 1f, 0.7f));
            AddBody(sawGo, continuous: true);
            SawBlade sawComp = sawGo.AddComponent<SawBlade>();
            Set(sawComp, "hitVfx", c.vfxSpark);
            SawBlade saw = SavePrefab(sawGo, $"{P}/Prefabs/Projectiles/SawBlade.prefab").GetComponent<SawBlade>();

            // Special weapon crate (icon set per drop)
            GameObject crateGo = SpriteObject("SpecialCrate", K + "Power-ups/powerupBlue_star.png", 3, LayerPickup, 1.9f);
            GameObject crateGlow = SpriteObject("Glow", Art("soft_glow"), 2, LayerPickup, 0.9f);
            crateGlow.transform.SetParent(crateGo.transform, false);
            UseMat(crateGlow, FlashMat("Mat_PickupGlow", 1.6f));
            AddCircle(crateGo, 0.17f);
            AddBody(crateGo, continuous: false);
            SpecialWeaponPickup crateComp = crateGo.AddComponent<SpecialWeaponPickup>();
            Set(crateComp, "iconRenderer", crateGo.GetComponent<SpriteRenderer>());
            Set(crateComp, "glowRenderer", crateGlow.GetComponent<SpriteRenderer>());
            c.specialCrate = SavePrefab(crateGo, $"{P}/Prefabs/SpecialCrate.prefab").GetComponent<SpecialWeaponPickup>();

            Sprite Icon(string name) => LoadSprite(K + "Power-ups/" + name + ".png");

            var rockets = LoadOrCreate<HomingRocketsWeaponSO>($"{P}/Data/SpecialWeapons/Special_TwinRockets.asset");
            rockets.displayName = "Twin Rockets";
            rockets.icon = Icon("powerupBlue_star");
            rockets.color = new Color(0.4f, 0.75f, 1f);
            rockets.ammoPerPickup = 12f;
            rockets.maxAmmo = 36f;
            rockets.rocketPrefab = rocket;
            rockets.rocketsPerShot = 2;
            rockets.damage = 3f;
            rockets.cooldown = 0.35f;

            var minigun = LoadOrCreate<MinigunWeaponSO>($"{P}/Data/SpecialWeapons/Special_BeeMinigun.asset");
            minigun.displayName = "Bee Minigun";
            minigun.icon = Icon("powerupYellow_bolt");
            minigun.color = new Color(1f, 0.85f, 0.2f);
            minigun.ammoPerPickup = 200f;
            minigun.maxAmmo = 600f;
            minigun.bulletPrefab = specialBullet;
            minigun.bulletSprite = null;
            minigun.bulletScale = 0.6f;
            minigun.fireRate = 22f;
            minigun.damage = 0.6f;

            var laser = LoadOrCreate<LaserBeamWeaponSO>($"{P}/Data/SpecialWeapons/Special_LaserBeam.asset");
            laser.displayName = "Laser Beam";
            laser.icon = Icon("powerupGreen_bolt");
            laser.color = new Color(0.3f, 1f, 0.85f);
            laser.continuous = true;
            laser.ammoPerPickup = 5f;
            laser.maxAmmo = 15f;
            laser.damagePerSecond = 14f;

            var flak = LoadOrCreate<FlakWeaponSO>($"{P}/Data/SpecialWeapons/Special_FlakCannon.asset");
            flak.displayName = "Flak Cannon";
            flak.icon = Icon("powerupRed_star");
            flak.color = new Color(1f, 0.5f, 0.2f);
            flak.ammoPerPickup = 10f;
            flak.maxAmmo = 30f;
            flak.pelletPrefab = specialBullet;
            flak.pelletSprite = Art("orb_glow");
            flak.pelletScale = 0.55f;
            flak.pellets = 9;
            flak.damage = 0.9f;

            var saws = LoadOrCreate<SawWeaponSO>($"{P}/Data/SpecialWeapons/Special_SawLauncher.asset");
            saws.displayName = "Saw Launcher";
            saws.icon = Icon("star_silver");
            saws.color = new Color(0.85f, 0.9f, 1f);
            saws.ammoPerPickup = 8f;
            saws.maxAmmo = 24f;
            saws.sawPrefab = saw;
            saws.damage = 3.5f;
            saws.speed = 10f;
            saws.cooldown = 0.5f;

            c.specialWeapons = new SpecialWeaponSO[] { rockets, minigun, laser, flak, saws };
            foreach (SpecialWeaponSO w in c.specialWeapons) EditorUtility.SetDirty(w);
        }

        private static BackgroundPropData Prop(string sprite, float x, float y, float width, float speed, float rotation, float respawn, float brightness) =>
            new BackgroundPropData
            {
                sprite = LoadSprite(sprite), viewportX = x, startViewportY = y, worldWidth = width,
                scrollSpeed = speed, rotationSpeed = rotation, respawnDelay = respawn,
                tint = new Color(brightness, brightness, brightness, 1f)
            };

        private static void SetProps(LevelDataSO level, params BackgroundPropData[] props)
        {
            level.theme.props = props;
            // Planets are drawn in front of every star layer (stars are infinitely far behind them).
            level.theme.propsInsertIndex = level.theme.layers.Length;
            EditorUtility.SetDirty(level);
        }

        private static ShotPattern S(float x, float y, float angle) => new ShotPattern(x, y, angle);

        private static WeaponPowerLevel Power(string label, string sprite, float damage, float speed, float rate, float scale,
                                              params ShotPattern[] shots) =>
            new WeaponPowerLevel
            {
                label = label, damage = damage, projectileSpeed = speed, fireRate = rate, scale = scale, tint = Color.white,
                projectileSprite = LoadSprite($"Kenney_SpaceShooterRedux/Lasers/{sprite}.png"), shots = shots
            };

        private static void BuildData(Ctx c)
        {
            Sprite[] Frames(string folder, string prefix, int count) => Enumerable.Range(1, count)
                .Select(i => LoadSprite($"{folder}/{prefix}{i}.png")).ToArray();

            Sprite[] henFly = Frames("Bevouliin_FlappyChicken", "fly-frame-", 4);
            Sprite[] helmetFly = Frames("Bevouliin_Birds/HelmetBird", "flying-frame-", 2);
            Sprite[] zombieFly = Frames("Bevouliin_Birds/ZombieBird", "flying-frame-", 4);
            Sprite[] beeFly = Frames("Bevouliin_Birds/AngryBeeBird", "flying-frame-", 2);
            Sprite[] duckFly = Frames("Bevouliin_Birds/CapDuck", "idle-frame-", 4);
            Sprite[] pinkFly = Frames("Bevouliin_Birds/PinkBird", "fly-frame-", 2);
            Sprite[] grumpyFly = Frames("Bevouliin_Birds/GrumpyBird", "fly-frame-", 4);

            Sprite[] brownRocks = { Rock("meteorBrown_big1"), Rock("meteorBrown_big2"), Rock("meteorBrown_big3"), Rock("meteorBrown_med1") };
            Sprite[] greyRocks = { Rock("meteorGrey_big1"), Rock("meteorGrey_big2"), Rock("meteorGrey_big4"), Rock("meteorGrey_med1") };
            Sprite[] mixedRocks = brownRocks.Concat(greyRocks).ToArray();

            // ---------------- entry paths (viewport space)
            EntryPathSO swoop = Path("Path_Swoop", PathSpace.Viewport,
                new Vector2(-0.08f, 1.1f), new Vector2(0.1f, 0.6f), new Vector2(0.35f, 0.42f), new Vector2(0.65f, 0.46f), new Vector2(0.72f, 0.64f));
            EntryPathSO loop = Path("Path_Loop", PathSpace.Viewport,
                new Vector2(0.3f, 1.15f), new Vector2(0.35f, 0.65f), new Vector2(0.2f, 0.47f), new Vector2(0.42f, 0.38f), new Vector2(0.65f, 0.47f), new Vector2(0.55f, 0.64f));
            EntryPathSO cascade = Path("Path_Cascade", PathSpace.RelativeToSlotX,
                new Vector2(0f, 1.15f), new Vector2(0.12f, 0.98f), new Vector2(-0.1f, 0.86f));
            EntryPathSO sweep = Path("Path_SideSweep", PathSpace.Viewport,
                new Vector2(-0.1f, 1.05f), new Vector2(0.3f, 0.95f), new Vector2(0.75f, 0.78f), new Vector2(0.9f, 0.5f), new Vector2(0.5f, 0.45f));
            EntryPathSO spiral = Path("Path_Spiral", PathSpace.Viewport,
                new Vector2(0.5f, 1.15f), new Vector2(0.8f, 0.62f), new Vector2(0.5f, 0.4f), new Vector2(0.2f, 0.55f), new Vector2(0.4f, 0.75f), new Vector2(0.6f, 0.62f));

            // ---------------- enemy roster
            // Flock members (Chicken prefabs, optionally with ability components)
            EnemyDataSO hen = Enemy("Enemy_SpaceHen", "Space Hen", c.chicken, henFly, Color.white, 1f, hp: 3, score: 100, move: 7f,
                Atk(c.egg, Color.white, 1, 0f, false, 4.2f, new Vector2(3.2f, 6.5f)), canDive: true, dive: 8f,
                c.foodDrumstick, 0.5f, 0.04f, c);
            EnemyDataSO helmet = Enemy("Enemy_HelmetHen", "Helmet Hen", c.henArmored, helmetFly, Color.white, 1.4f, hp: 3, score: 200, move: 7.5f,
                Atk(c.egg, new Color(0.75f, 0.8f, 0.95f), 3, 40f, false, 4.8f, new Vector2(3.5f, 6.5f)), canDive: true, dive: 9.5f,
                c.foodBurger, 0.5f, 0.06f, c);
            EnemyDataSO zombie = Enemy("Enemy_ZombieHen", "Zombie Space Hen", c.henZombie, zombieFly, Color.white, 0.95f, hp: 4, score: 250, move: 8f,
                Atk(c.slimeOrb, new Color(0.65f, 1f, 0.55f), 1, 0f, true, 3.8f, new Vector2(4.5f, 8.5f)), canDive: true, dive: 11.5f,
                c.foodDrumstick, 0.55f, 0.06f, c);
            zombie.diveTurnRate = 140f;
            EnemyDataSO nurse = Enemy("Enemy_NurseHen", "Nurse Hen", c.henNurse, pinkFly, Color.white, 1.35f, hp: 5, score: 300, move: 7f,
                new EnemyAttack(), canDive: false, dive: 0f, c.foodDrumstick, 1f, 0.25f, c);

            // Free agents (RogueEnemy prefabs with their own state machines)
            EnemyDataSO stinger = Enemy("Enemy_Stinger", "Stinger", c.stinger, beeFly, Color.white, 1.3f, hp: 6, score: 350, move: 6f,
                Atk(c.needle, Color.white, 1, 0f, true, 9f, Vector2.one), canDive: false, dive: 0f, c.foodBurger, 0.6f, 0.2f, c);
            EnemyDataSO bomber = Enemy("Enemy_Bomber", "Bomber Hen", c.bomber, grumpyFly, Color.white, 1.6f, hp: 10, score: 450, move: 3.2f,
                Atk(c.clusterBomb, new Color(0.4f, 0.4f, 0.45f), 1, 0f, false, 2.2f, new Vector2(1.3f, 2f)), canDive: false, dive: 0f,
                c.foodDrumstick, 1f, 0.35f, c);
            EnemyDataSO gunner = Enemy("Enemy_SniperDuck", "Sniper Duck", c.gunner, duckFly, Color.white, 0.9f, hp: 7, score: 400, move: 6f,
                Atk(c.sniperBolt, Color.white, 1, 0f, true, 11f, Vector2.one), canDive: false, dive: 0f, c.foodBurger, 0.8f, 0.3f, c);

            // Bosses (Phase 2 turns these into FSM bosses)
            EnemyDataSO boss1 = Enemy("Boss_L1_BigChick", "Big Chick", c.boss, henFly, new Color(1f, 0.95f, 0.85f), 3.0f, hp: 200, score: 0, move: 3f,
                Atk(c.egg, Color.white, 1, 0f, false, 4f, Vector2.one), canDive: false, dive: 0f, null, 0f, 0f, c);
            EnemyDataSO boss2 = Enemy("Boss_L2_ColahGherrmeziChick", "Colah Gherrmezi Chick", c.boss, helmetFly, new Color(0.6f, 0.7f, 0.85f), 4.4f, hp: 360, score: 0, move: 3f,
                Atk(c.egg, new Color(0.8f, 0.85f, 0.95f), 1, 0f, false, 5f, Vector2.one), canDive: false, dive: 0f, null, 0f, 0f, c);
            EnemyDataSO boss3 = Enemy("Boss_L3_GreenEmperorChick", "Invincible, Fearless, Sensual, Mysterious, Enchanting, Vigorous, Diligent, Overwhelming, Gorgeous, Passionate, Terrifying, Beautiful, Powerful, Green Emperor Chick", c.boss, zombieFly, new Color(0.85f, 0.55f, 1f), 3.7f, hp: 560, score: 0, move: 3f,
                Atk(c.egg, new Color(0.6f, 1f, 0.75f), 1, 0f, false, 6f, Vector2.one), canDive: false, dive: 0f, null, 0f, 0f, c);

            // ---------------- boss minions (small diving chicks, missile kills them instantly)
            EnemyDataSO minion1 = Minion("Minion_L1_Chick", c.minion, henFly, new Color(1f, 0.9f, 0.7f), 0.55f, 9f, c);
            EnemyDataSO minion2 = Minion("Minion_L2_MechaChick", c.minion, helmetFly, new Color(0.75f, 0.8f, 0.95f), 0.75f, 10.5f, c);
            EnemyDataSO minion3 = Minion("Minion_L3_VoidChick", c.minion, zombieFly, new Color(0.9f, 0.6f, 1f), 0.6f, 12f, c);

            // ---------------- boss FSM profiles: 3 health phases each, different attack sets per boss
            BossProfileSO giantHen = BossProfile("BossProfile_L1_BigChick", c, minion1, move: 4f, dash: 12f, rest: 0.55f, shotSpeed: 4.6f,
                eggTint: Color.white, featherTint: new Color(1f, 0.9f, 0.6f), orbTint: new Color(1f, 0.8f, 0.4f),
                Phase("BIG CHICK", 1f, 1f, 1f, 1f, false, 0, BossAttack.EggRain, BossAttack.AimedBurst, BossAttack.RadialBurst),
                Phase("PHASE 2 - FURY", 0.65f, 1.15f, 0.8f, 1.25f, false, 3,
                      BossAttack.EggRain, BossAttack.AimedBurst, BossAttack.ChargeDash, BossAttack.RadialBurst),
                Phase("ENRAGED!", 0.3f, 1.4f, 0.6f, 1.6f, true, 4,
                      BossAttack.ChargeDash, BossAttack.EggRain, BossAttack.RadialBurst, BossAttack.AimedBurst, BossAttack.SummonMinions));

            BossProfileSO mecha = BossProfile("BossProfile_L2_ColahGherrmeziChick", c, minion2, move: 4.5f, dash: 14f, rest: 0.45f, shotSpeed: 5.2f,
                eggTint: new Color(0.8f, 0.85f, 0.95f), featherTint: new Color(1f, 0.55f, 0.3f), orbTint: new Color(1f, 0.45f, 0.3f),
                Phase("COLAH GHERRMEZI CHICK", 1f, 1f, 1f, 1f, false, 0, BossAttack.LaneBarrage, BossAttack.HomingSalvo, BossAttack.AimedBurst),
                Phase("SYSTEMS OVERDRIVE", 0.65f, 1.15f, 0.8f, 1.3f, false, 3,
                      BossAttack.LaneBarrage, BossAttack.HomingSalvo, BossAttack.ChargeDash, BossAttack.RadialBurst),
                Phase("ENRAGED!", 0.3f, 1.4f, 0.6f, 1.6f, true, 4,
                      BossAttack.LaneBarrage, BossAttack.ChargeDash, BossAttack.HomingSalvo, BossAttack.RadialBurst, BossAttack.SummonMinions));

            BossProfileSO emperor = BossProfile("BossProfile_L3_GreenEmperorChick", c, minion3, move: 5f, dash: 15f, rest: 0.35f, shotSpeed: 5.5f,
                eggTint: new Color(0.85f, 0.7f, 1f), featherTint: new Color(0.85f, 0.5f, 1f), orbTint: new Color(0.6f, 1f, 0.5f),
                Phase("GREEN EMPEROR CHICK", 1f, 1f, 1f, 1f, false, 0, BossAttack.FeatherSpiral, BossAttack.Teleport, BossAttack.AimedBurst),
                Phase("COSMIC FURY", 0.65f, 1.2f, 0.8f, 1.4f, false, 4,
                      BossAttack.FeatherSpiral, BossAttack.Teleport, BossAttack.EggRain, BossAttack.HomingSalvo, BossAttack.LaneBarrage),
                Phase("ENRAGED!", 0.3f, 1.5f, 0.55f, 2f, true, 5,
                      BossAttack.FeatherSpiral, BossAttack.Teleport, BossAttack.ChargeDash, BossAttack.HomingSalvo,
                      BossAttack.SummonMinions, BossAttack.RadialBurst));

            // ---------------- waves: Level 1 (regular grids of hens; nurses, first bombers & snipers)
            WaveDataSO l1Grid = Formation("Wave_L1_Grid", "Hen Squadron", hen, FormationPattern.Grid, 3, 8, new Vector2(1.15f, 0.9f),
                swoop, true, entrySpeed: 7f, stagger: 0.1f, sway: 1.3f, swaySpeed: 0.3f, dive: new Vector2(3f, 6f), maxDivers: 2);
            WaveDataSO l1Nursed = With(Formation("Wave_L1_NursedFlock", "Nursed Flock", hen, FormationPattern.Grid, 4, 9, new Vector2(1.05f, 0.85f),
                    cascade, true, entrySpeed: 7.5f, stagger: 0.06f, sway: 1f, swaySpeed: 0.35f, dive: new Vector2(2.6f, 5f), maxDivers: 2),
                new[] { Sp(nurse, 3) }, new[] { Rg(gunner, 1, 6f, 0f) });
            WaveDataSO l1Rocks = With(Asteroids("Wave_L1_AsteroidBombers", hen, c, brownRocks, 18, 3, new Vector2(2f, 3.5f), new Vector2(0.4f, 0.9f)),
                null, new[] { Rg(bomber, 1, 4f, 0f), Rg(stinger, 1, 9f, 0f) });
            WaveDataSO l1Assault = With(Formation("Wave_L1_Assault", "Hen Assault", hen, FormationPattern.Grid, 3, 8, new Vector2(1.15f, 0.9f),
                    swoop, true, entrySpeed: 8f, stagger: 0.08f, sway: 1.4f, swaySpeed: 0.35f, dive: new Vector2(2f, 4f), maxDivers: 3),
                new[] { Sp(nurse, 2) }, new[] { Rg(gunner, 2, 5f, 6f), Rg(bomber, 1, 10f, 0f), Rg(stinger, 1, 14f, 0f) });
            WaveDataSO l1Boss = Boss("Wave_L1_Boss", boss1, c, giantHen);

            // ---------------- waves: Level 2 (helmet hens in angled V formations; snipers, stingers, bombers)
            WaveDataSO l2V = Formation("Wave_L2_HelmetChevron", "Helmet Chevron", helmet, FormationPattern.VShape, 2, 9, new Vector2(1.05f, 0.9f),
                sweep, true, entrySpeed: 8f, stagger: 0.08f, sway: 1.5f, swaySpeed: 0.4f, dive: new Vector2(2.5f, 4.5f), maxDivers: 3);
            WaveDataSO l2ArcGunners = With(Formation("Wave_L2_SteelArc", "Steel Arc", helmet, FormationPattern.Arc, 2, 9, new Vector2(1.05f, 0.9f),
                    loop, true, entrySpeed: 8f, stagger: 0.08f, sway: 0.8f, swaySpeed: 0.45f, dive: new Vector2(2.2f, 4f), maxDivers: 3),
                null, new[] { Rg(gunner, 2, 4f, 5f), Rg(stinger, 1, 8f, 0f) });
            l2ArcGunners.formationCenterViewport = new Vector2(0.5f, 0.76f);
            WaveDataSO l2RocksStingers = With(Asteroids("Wave_L2_AsteroidStingers", helmet, c, greyRocks, 24, 4, new Vector2(2.5f, 4.5f), new Vector2(0.3f, 0.7f)),
                null, new[] { Rg(stinger, 4, 3f, 4f), Rg(bomber, 1, 8f, 0f) });
            WaveDataSO l2Wedge = With(Formation("Wave_L2_IronWedge", "Iron Wedge", helmet, FormationPattern.VShape, 3, 7, new Vector2(1.0f, 0.82f),
                    swoop, true, entrySpeed: 8.5f, stagger: 0.07f, sway: 1.3f, swaySpeed: 0.45f, dive: new Vector2(2f, 3.8f), maxDivers: 3),
                new[] { Sp(nurse, 3) }, new[] { Rg(bomber, 2, 5f, 7f), Rg(gunner, 1, 9f, 0f) });
            WaveDataSO l2ArcStorm = With(Formation("Wave_L2_ArcStorm", "Arc Storm", helmet, FormationPattern.Arc, 2, 9, new Vector2(1.05f, 0.9f),
                    loop, true, entrySpeed: 9f, stagger: 0.07f, sway: 1f, swaySpeed: 0.5f, dive: new Vector2(1.8f, 3.5f), maxDivers: 3),
                null, new[] { Rg(stinger, 3, 4f, 5f), Rg(gunner, 2, 8f, 6f) });
            l2ArcStorm.formationCenterViewport = new Vector2(0.5f, 0.76f);
            WaveDataSO l2RocksBombers = With(Asteroids("Wave_L2_AsteroidBombers", helmet, c, greyRocks, 26, 4, new Vector2(2.5f, 4.5f), new Vector2(0.3f, 0.7f)),
                null, new[] { Rg(bomber, 2, 3f, 6f), Rg(stinger, 2, 6f, 6f) });
            WaveDataSO l2Boss = Boss("Wave_L2_Boss", boss2, c, mecha);

            // ---------------- waves: Level 3 (zombie hens in tight clusters; everything at once)
            WaveDataSO l3Diamond = Formation("Wave_L3_VoidDiamond", "Void Diamond", zombie, FormationPattern.Diamond, 3, 7, new Vector2(1.05f, 1.1f),
                spiral, true, entrySpeed: 9.5f, stagger: 0.07f, sway: 2f, swaySpeed: 0.5f, dive: new Vector2(1.4f, 2.6f), maxDivers: 4);
            WaveDataSO l3ColumnsStingers = With(Formation("Wave_L3_NebulaPillars", "Nebula Pillars", zombie, FormationPattern.Columns, 4, 9, new Vector2(1.0f, 0.85f),
                    cascade, true, entrySpeed: 10f, stagger: 0.05f, sway: 1.2f, swaySpeed: 0.55f, dive: new Vector2(1.2f, 2.4f), maxDivers: 4),
                null, new[] { Rg(stinger, 4, 4f, 4f) });
            WaveDataSO l3SwarmNurses = With(Formation("Wave_L3_UndeadSwarm", "Undead Swarm", zombie, FormationPattern.Grid, 4, 10, new Vector2(0.98f, 0.8f),
                    loop, true, entrySpeed: 10f, stagger: 0.05f, sway: 1.5f, swaySpeed: 0.6f, dive: new Vector2(1.2f, 2.2f), maxDivers: 5),
                new[] { Sp(nurse, 4) }, new[] { Rg(gunner, 2, 6f, 6f) });
            WaveDataSO l3RocksStingers = With(Asteroids("Wave_L3_AsteroidStingers", zombie, c, mixedRocks, 30, 5, new Vector2(3f, 5.5f), new Vector2(0.25f, 0.6f)),
                null, new[] { Rg(stinger, 3, 3f, 5f) });
            WaveDataSO l3DiamondGunners = With(Formation("Wave_L3_SniperDiamond", "Sniper Nest", zombie, FormationPattern.Diamond, 3, 7, new Vector2(1.05f, 1.1f),
                    spiral, true, entrySpeed: 9.5f, stagger: 0.07f, sway: 2f, swaySpeed: 0.5f, dive: new Vector2(1.8f, 3f), maxDivers: 3),
                null, new[] { Rg(gunner, 3, 4f, 5f), Rg(bomber, 2, 8f, 7f), Rg(stinger, 2, 10f, 5f) });
            WaveDataSO l3Finale = With(Formation("Wave_L3_Finale", "Final Swarm", zombie, FormationPattern.Grid, 4, 10, new Vector2(0.98f, 0.8f),
                    loop, true, entrySpeed: 10.5f, stagger: 0.05f, sway: 1.6f, swaySpeed: 0.6f, dive: new Vector2(1.4f, 2.4f), maxDivers: 4),
                new[] { Sp(nurse, 4) }, new[] { Rg(stinger, 3, 4f, 5f), Rg(gunner, 3, 7f, 5f), Rg(bomber, 2, 10f, 6f) });
            WaveDataSO l3Boss = Boss("Wave_L3_Boss", boss3, c, emperor);

            // ---------------- levels
            LevelDataSO level1 = Level("Level1_EarthOrbit", 1, "Earth Orbit", "Level1", "Music/Level_8bitSpaceShooter.mp3",
                new Color(0.02f, 0.04f, 0.12f),
                new[]
                {
                    Layer("Backgrounds/SBS_Nebula/BlueNebula4.png", 0.1f, Color.white),
                    ArtLayer("StarDust_Far", 0.32f, new Color(1f, 1f, 1f, 0.85f)),
                    ArtLayer("Stars_Near", 0.85f, Color.white)
                },
                Round(l1Grid), Round(l1Nursed, 1.1f, 1.05f, 1.1f, 1.1f), Round(l1Rocks, 1.15f, 1.1f, 1.2f, 1f),
                Round(l1Assault, 1.4f, 1.15f, 1.4f, 1.5f), Round(l1Boss));

            LevelDataSO level2 = Level("Level2_AsteroidBelt", 2, "Asteroid Belt & Jupiter", "Level2", "Music/PlanetaryAssault_Vitalezzz.mp3",
                new Color(0.08f, 0.04f, 0.02f),
                new[]
                {
                    Layer("Backgrounds/SBS_Nebula/BlueNebula1.png", 0.1f, new Color(1f, 0.62f, 0.4f)),
                    ArtLayer("StarDust_Far", 0.32f, new Color(1f, 1f, 1f, 0.85f)),
                    ArtLayer("Stars_Near", 0.85f, Color.white)
                },
                Round(l2V, 1.1f, 1.05f, 1.1f, 1.1f), Round(l2ArcGunners, 1.15f, 1.1f, 1.2f, 1.2f), Round(l2RocksStingers, 1.2f, 1.15f, 1.25f, 1f),
                Round(l2Wedge, 1.3f, 1.15f, 1.3f, 1.35f), Round(l2ArcStorm, 1.4f, 1.2f, 1.4f, 1.5f),
                Round(l2RocksBombers, 1.45f, 1.25f, 1.45f, 1f), Round(l2Boss));

            LevelDataSO level3 = Level("Level3_GalaxyDepths", 3, "Galaxy Depths", "Level3", "Music/OuterSpace_LeonardoPaz/06_battle_in_space_loop.ogg",
                new Color(0.05f, 0f, 0.08f),
                new[]
                {
                    Layer("Backgrounds/SBS_Nebula/PurpleNebula4.png", 0.1f, Color.white),
                    ArtLayer("StarDust_Far", 0.36f, new Color(1f, 1f, 1f, 0.85f)),
                    ArtLayer("Stars_Near", 0.95f, Color.white)
                },
                Round(l3Diamond, 1.2f, 1.1f, 1.2f, 1.2f), Round(l3ColumnsStingers, 1.25f, 1.15f, 1.25f, 1.3f),
                Round(l3SwarmNurses, 1.3f, 1.15f, 1.3f, 1.35f), Round(l3RocksStingers, 1.35f, 1.2f, 1.4f, 1f),
                Round(l3DiamondGunners, 1.4f, 1.2f, 1.45f, 1.45f), Round(l3ColumnsStingers, 1.5f, 1.25f, 1.5f, 1.55f),
                Round(l3RocksStingers, 1.55f, 1.3f, 1.6f, 1f), Round(l3SwarmNurses, 1.6f, 1.3f, 1.6f, 1.65f),
                Round(l3Finale, 1.75f, 1.35f, 1.75f, 1.8f), Round(l3Boss));

            // Drifting planets (the "Earth orbit", "Jupiter" and "deep galaxy" themes of the brief)
            SetProps(level1, Prop("Planets/EarthLike.png", 0.9f, 0.2f, 8f, 0.16f, 1f, 30f, 0.62f),
                             Prop("Planets/Moon.png", 0.08f, 0.82f, 2f, 0.3f, -3f, 35f, 0.6f));
            SetProps(level2, Prop("Planets/GasGiant.png", 1.0f, 0.4f, 11f, 0.11f, 0.5f, 30f, 0.5f),
                             Prop("Planets/RedPlanet.png", 0.06f, 0.86f, 2.4f, 0.26f, 2f, 40f, 0.6f));
            SetProps(level3, Prop("Planets/AlienPlanet.png", 0.06f, 0.5f, 5f, 0.18f, -1.5f, 35f, 0.6f));

            c.levels = new[] { level1, level2, level3 };

            var config = LoadOrCreate<GameConfigSO>($"{P}/Resources/GameConfig.asset");
            config.levels = c.levels;
            config.mainMenuScene = "MainMenu";
            config.startingLives = 3;
            config.startingMissiles = 2;
            config.basePowerLevel = 1;
            config.maxPowerLevel = 8;
            config.powerActsAsShield = true;  // shield rule (design decision, see report); false = assignment rule
            config.respawnPowerLevel = 3;
            config.respawnDelay = 1.4f;
            config.specialWeapons = c.specialWeapons;
            config.specialPickupPrefab = c.specialCrate;
            config.foodMeterMax = 100f;
            config.levelCompleteDelay = 3f;
            EditorUtility.SetDirty(config);

            // Remove data assets from older builder versions that are no longer generated.
            foreach (string folder in new[] { "Enemies", "Waves", "Paths", "SpecialWeapons", "Bosses" }) PruneFolder($"{P}/Data/{folder}");

            // Chance to drop a special-weapon crate: rare from basic flock members, common from elites.
            hen.specialDropChance = 0.02f;
            helmet.specialDropChance = 0.03f;
            zombie.specialDropChance = 0.03f;
            nurse.specialDropChance = 0.15f;
            stinger.specialDropChance = 0.25f;
            bomber.specialDropChance = 0.35f;
            gunner.specialDropChance = 0.35f;
            foreach (EnemyDataSO b in new[] { boss1, boss2, boss3 }) b.specialDropChance = 0f;
            foreach (EnemyDataSO e in new[] { hen, helmet, zombie, nurse, stinger, bomber, gunner }) EditorUtility.SetDirty(e);
        }

        private static Sprite Rock(string name) => LoadSprite($"Kenney_SpaceShooterRedux/Meteors/{name}.png");

        private static EntryPathSO Path(string name, PathSpace space, params Vector2[] points)
        {
            var p = LoadOrCreate<EntryPathSO>($"{P}/Data/Paths/{name}.asset");
            p.space = space;
            p.controlPoints = points;
            p.samplesPerSegment = 10;
            EditorUtility.SetDirty(p);
            return p;
        }

        private static EnemyAttack Atk(EnemyProjectile projectile, Color tint, int count, float spread, bool aimed, float speed, Vector2 interval) =>
            new EnemyAttack
            {
                projectile = projectile, tint = tint, count = count, spread = spread,
                aimAtPlayer = aimed, speed = speed, interval = interval
            };

        private static FlockSpecial Sp(EnemyDataSO enemy, int count) => new FlockSpecial { enemy = enemy, count = count };

        private static RogueSpawn Rg(EnemyDataSO enemy, int count, float delay, float interval) =>
            new RogueSpawn { enemy = enemy, count = count, startDelay = delay, interval = interval };

        private static WaveDataSO With(WaveDataSO w, FlockSpecial[] specials, RogueSpawn[] rogues = null)
        {
            w.flockSpecials = specials;
            w.rogues = rogues;
            EditorUtility.SetDirty(w);
            return w;
        }

        private static EnemyDataSO Enemy(string asset, string display, WaveEntity prefab, Sprite[] frames, Color tint, float scale,
                                         int hp, int score, float move, EnemyAttack attack, bool canDive, float dive,
                                         Pickup food, float foodChance, float giftChance, Ctx c)
        {
            var e = LoadOrCreate<EnemyDataSO>($"{P}/Data/Enemies/{asset}.asset");
            e.displayName = display;
            e.prefab = prefab;
            e.flyFrames = frames;
            e.frameRate = frames.Length <= 2 ? 7f : 10f;
            e.tint = tint;
            e.scale = scale;
            e.maxHealth = hp;
            e.moveSpeed = move;
            e.scoreValue = score;
            e.attack = attack;
            e.canDive = canDive;
            e.diveSpeed = dive;
            e.diveTurnRate = 90f;
            e.foodPrefab = food;
            e.foodDropChance = foodChance;
            e.giftPrefab = c.gift;
            e.giftDropChance = giftChance;
            e.deathVfx = c.vfxFeathers;
            e.hitVfx = c.vfxSpark;
            EditorUtility.SetDirty(e);
            return e;
        }

        private static WaveDataSO Formation(string asset, string label, EnemyDataSO enemy, FormationPattern pattern, int rows, int cols,
                                            Vector2 spacing, EntryPathSO path, bool mirror, float entrySpeed, float stagger,
                                            float sway, float swaySpeed, Vector2 dive, int maxDivers)
        {
            WaveDataSO w = NewWave(asset, label, WaveType.Formation, enemy);
            w.formation = pattern;
            w.rows = rows;
            w.columns = cols;
            w.spacing = spacing;
            w.formationCenterViewport = new Vector2(0.5f, 0.7f);
            w.entryPath = path;
            w.mirrorAlternate = mirror;
            w.entrySpeed = entrySpeed;
            w.entryStagger = stagger;
            w.swayAmplitude = sway;
            w.swaySpeed = swaySpeed;
            w.bobAmplitude = 0.15f;
            w.diveInterval = dive;
            w.maxSimultaneousDivers = maxDivers;
            EditorUtility.SetDirty(w);
            return w;
        }

        private static WaveDataSO Asteroids(string asset, EnemyDataSO enemyForDrops, Ctx c, Sprite[] sprites, int count, int hp,
                                            Vector2 speed, Vector2 interval)
        {
            WaveDataSO w = NewWave(asset, "Asteroid Field", WaveType.Asteroids, enemyForDrops);
            w.asteroidPrefab = c.asteroid;
            w.asteroidSprites = sprites;
            w.asteroidCount = count;
            w.asteroidHealth = hp;
            w.asteroidSpeed = speed;
            w.asteroidSpawnInterval = interval;
            w.asteroidScale = new Vector2(0.8f, 1.4f);
            w.asteroidFoodDropChance = 0.25f;
            EditorUtility.SetDirty(w);
            return w;
        }

        private static EnemyDataSO Minion(string asset, WaveEntity prefab, Sprite[] frames, Color tint, float scale, float diveSpeed, Ctx c)
        {
            EnemyDataSO m = Enemy(asset, "Chick", prefab, frames, tint, scale, hp: 2, score: 60, move: 6f,
                new EnemyAttack(), canDive: true, dive: diveSpeed, c.foodDrumstick, 0.3f, 0f, c);
            m.specialDropChance = 0f;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static BossPhase Phase(string label, float below, float speed, float cooldown, float density, bool enraged,
                                       int minions, params BossAttack[] attacks) =>
            new BossPhase
            {
                label = label, enterBelowHealth = below, speedMultiplier = speed, cooldownMultiplier = cooldown,
                densityMultiplier = density, enraged = enraged, minionsPerSummon = minions, attacks = attacks
            };

        private static BossProfileSO BossProfile(string asset, Ctx c, EnemyDataSO minion, float move, float dash, float rest,
                                                 float shotSpeed, Color eggTint, Color featherTint, Color orbTint,
                                                 params BossPhase[] phases)
        {
            EnsureFolder($"{P}/Data/Bosses");
            var b = LoadOrCreate<BossProfileSO>($"{P}/Data/Bosses/{asset}.asset");
            b.phases = phases;
            b.moveSpeed = move;
            b.dashSpeed = dash;
            b.restTime = rest;
            b.projectileSpeed = shotSpeed;
            b.egg = c.egg;
            b.feather = c.needle;
            b.orb = c.slimeOrb;
            b.eggTint = eggTint;
            b.featherTint = featherTint;
            b.orbTint = orbTint;
            b.minion = minion;
            b.enragedTint = new Color(1f, 0.4f, 0.35f);
            b.laneWarningSprite = Art("soft_glow");
            b.laneWarningMaterial = FlashMat("Mat_EnemyGlow", 1.6f);
            b.phaseShiftVfx = c.vfxShockwave;
            b.teleportVfx = c.vfxRevive;
            EditorUtility.SetDirty(b);
            return b;
        }

        private static WaveDataSO Boss(string asset, EnemyDataSO boss, Ctx c, BossProfileSO profile)
        {
            WaveDataSO w = NewWave(asset, boss.displayName, WaveType.Boss, boss);
            w.bossPrefab = c.boss;
            w.bossProfile = profile;
            EditorUtility.SetDirty(w);
            return w;
        }

        private static WaveDataSO NewWave(string asset, string label, WaveType type, EnemyDataSO enemy)
        {
            var w = LoadOrCreate<WaveDataSO>($"{P}/Data/Waves/{asset}.asset");
            w.label = label;
            w.waveType = type;
            w.enemy = enemy;
            w.flockSpecials = null;
            w.rogues = null;
            return w;
        }

        private static RoundDefinition Round(WaveDataSO wave, float health = 1f, float speed = 1f, float fire = 1f, float dive = 1f) =>
            new RoundDefinition
            {
                wave = wave, healthMultiplier = health, speedMultiplier = speed,
                fireRateMultiplier = fire, diveFrequencyMultiplier = dive
            };

        /// <summary>Parallax layer from project-generated art (Assets/_Project/Art/Generated).</summary>
        private static ParallaxLayerData ArtLayer(string name, float speed, Color tint) =>
            new ParallaxLayerData { sprite = Art(name), scrollSpeed = speed, tint = tint };

        private static ParallaxLayerData Layer(string sprite, float speed, Color tint) =>
            new ParallaxLayerData { sprite = LoadSprite(sprite), scrollSpeed = speed, tint = tint };

        private static LevelDataSO Level(string asset, int number, string display, string scene, string music, Color bg,
                                         ParallaxLayerData[] layers, params RoundDefinition[] rounds)
        {
            var l = LoadOrCreate<LevelDataSO>($"{P}/Data/Levels/{asset}.asset");
            l.levelNumber = number;
            l.displayName = display;
            l.sceneName = scene;
            l.music = AssetDatabase.LoadAssetAtPath<AudioClip>(TP + music);
            l.theme = new LevelTheme { backgroundColor = bg, layers = layers };
            l.rounds = rounds;
            EditorUtility.SetDirty(l);
            return l;
        }

        // ================================================================== scenes

        private const string PostFxPath = P + "/Settings/PostFX_Profile.asset";

        /// <summary>Bloom (only HDR-boosted sprites exceed the threshold) + a light vignette.</summary>
        private static void BuildPostFx()
        {
            EnsureFolder($"{P}/Settings");
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxPath) != null) AssetDatabase.DeleteAsset(PostFxPath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostFxPath);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(1.4f);
            bloom.scatter.Override(0.65f);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.5f);
            foreach (VolumeComponent comp in profile.components)
            {
                comp.name = comp.GetType().Name;
                AssetDatabase.AddObjectToAsset(comp, profile);
            }
            EditorUtility.SetDirty(profile);

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.supportsHDR = true;
                EditorUtility.SetDirty(urp);
            }
            AssetDatabase.SaveAssets();
        }

        private static void BuildScenes(Ctx c)
        {
            BuildPostFx();
            AssetDatabase.SaveAssets();
            // Opening a scene unloads unreferenced in-memory assets, so keep paths and reload after each OpenScene.
            string[] levelPaths = c.levels.Select(AssetDatabase.GetAssetPath).ToArray();
            string playerPath = AssetDatabase.GetAssetPath(c.player);

            foreach (string levelPath in levelPaths)
            {
                string sceneName = AssetDatabase.LoadAssetAtPath<LevelDataSO>(levelPath).sceneName;
                Scene scene = OpenCleanScene(sceneName);
                ReloadCommon(c, playerPath);
                BuildLevelScene(c, scene, AssetDatabase.LoadAssetAtPath<LevelDataSO>(levelPath));
            }

            Scene menu = OpenCleanScene("MainMenu");
            ReloadCommon(c, playerPath);
            BuildMenuScene(c, menu, AssetDatabase.LoadAssetAtPath<LevelDataSO>(levelPaths[levelPaths.Length - 1]));
        }

        private static void ReloadCommon(Ctx c, string playerPath)
        {
            c.player = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            c.font = LoadUiFont();
        }

        /// <summary>Kenney's vector font renders K and X like H, so UI uses Unity's built-in runtime font.</summary>
        private static Font LoadUiFont() =>
            AssetDatabase.LoadAssetAtPath<Font>(TP + "Fonts/RussoOne-Regular.ttf") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static Scene OpenCleanScene(string name)
        {
            Scene scene = EditorSceneManager.OpenScene($"{P}/Scenes/{name}.unity", OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Camera>() != null || root.GetComponent<Light2D>() != null) continue;
                Object.DestroyImmediate(root);
            }

            Camera cam = Object.FindAnyObjectByType<Camera>();
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxPath);
            if (cam == null)
            {
                cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            if (cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();
            cam.allowHDR = true;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            if (Object.FindAnyObjectByType<Light2D>() == null)
            {
                var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Global;
            }
            return scene;
        }

        private static void BuildLevelScene(Ctx c, Scene scene, LevelDataSO level)
        {
            ParallaxBackground bg = new GameObject("Background").AddComponent<ParallaxBackground>();

            var player = (GameObject)PrefabUtility.InstantiatePrefab(c.player);
            player.transform.position = new Vector3(0f, -4.3f, 0f);

            var levelGo = new GameObject("Level");
            RoundManager rounds = levelGo.AddComponent<RoundManager>();
            AudioSource music = levelGo.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.volume = 0.6f;
            levelGo.AddComponent<HitStop>();
            LevelBootstrap boot = levelGo.AddComponent<LevelBootstrap>();
            Set(boot, "levelData", level);
            Set(boot, "roundManager", rounds);
            Set(boot, "background", bg);
            Set(boot, "musicSource", music);
            Set(boot, "playerInput", player.GetComponent<PlayerInputReader>());

            BuildHud(c);
            BuildEventSystem();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildMenuScene(Ctx c, Scene scene, LevelDataSO menuTheme)
        {
            ParallaxBackground bg = new GameObject("Background").AddComponent<ParallaxBackground>();
            Set(bg, "themeOnStart", menuTheme);

            AudioSource menuMusic = new GameObject("MenuMusic").AddComponent<AudioSource>();
            menuMusic.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(TP + "Music/DarkSciFi_SRG774/title.ogg");
            menuMusic.loop = true;
            menuMusic.playOnAwake = true;
            menuMusic.volume = 0.6f;

            Canvas canvas = NewCanvas("MenuCanvas");
            RectTransform root = (RectTransform)canvas.transform;

            Text title = NewText(root, "Title", c.font, "CHICK KILLER", 96, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(1400, 140));
            title.color = new Color(1f, 0.85f, 0.3f);
            Text subtitle = NewText(root, "Subtitle", c.font, "MULTI-STAGE SPACE DEFENSE", 32, TextAnchor.MiddleCenter);
            Place(subtitle.rectTransform, new Vector2(0.5f, 0.68f), new Vector2(1200, 60));

            Button newGame = NewButton(root, "NewGameButton", c.font, "NEW GAME", "buttonGreen");
            Place((RectTransform)newGame.transform, new Vector2(0.5f, 0.5f), new Vector2(420, 80));
            Button cont = NewButton(root, "ContinueButton", c.font, "CONTINUE", "buttonBlue");
            Place((RectTransform)cont.transform, new Vector2(0.5f, 0.4f), new Vector2(420, 80));
            Button quit = NewButton(root, "QuitButton", c.font, "QUIT", "buttonRed");
            Place((RectTransform)quit.transform, new Vector2(0.5f, 0.3f), new Vector2(420, 80));

            Text hint = NewText(root, "ControlsHint", c.font,
                "WASD / ARROWS  MOVE     SPACE / LMB  FIRE     RMB / C  SPECIAL WEAPON     X  MEGA MISSILE     ESC  PAUSE",
                22, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0.08f), new Vector2(1600, 50));
            hint.color = new Color(0.8f, 0.85f, 1f, 0.8f);

            MainMenuUI menu = canvas.gameObject.AddComponent<MainMenuUI>();
            Set(menu, "newGameButton", newGame);
            Set(menu, "continueButton", cont);
            Set(menu, "quitButton", quit);

            BuildEventSystem();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ================================================================== HUD

        private static void BuildHud(Ctx c)
        {
            Canvas canvas = NewCanvas("HUD");
            RectTransform root = (RectTransform)canvas.transform;
            Font f = c.font;
            Sprite white = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // Lives (top-left)
            Image lifeIcon = NewImage(root, "LifeIcon", PlayerShipSprite(), Color.white);
            Anchor(lifeIcon.rectTransform, new Vector2(0, 1), new Vector2(40, -36), new Vector2(46, 46));
            lifeIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            lifeIcon.rectTransform.anchoredPosition = new Vector2(62f, -44f);
            lifeIcon.preserveAspect = true;
            Text lives = NewText(root, "LivesText", f, "X 3", 34, TextAnchor.MiddleLeft);
            Anchor(lives.rectTransform, new Vector2(0, 1), new Vector2(100, -40), new Vector2(200, 50));
            Text missiles = NewText(root, "MissilesText", f, "MISSILES  2", 26, TextAnchor.MiddleLeft);
            Anchor(missiles.rectTransform, new Vector2(0, 1), new Vector2(40, -95), new Vector2(400, 40));
            Text power = NewText(root, "PowerText", f, "POWER  1/3", 26, TextAnchor.MiddleLeft);
            Anchor(power.rectTransform, new Vector2(0, 1), new Vector2(40, -135), new Vector2(400, 40));
            Text specialLine = NewText(root, "SpecialLine", f, "SPECIAL  -", 26, TextAnchor.MiddleLeft);
            Anchor(specialLine.rectTransform, new Vector2(0, 1), new Vector2(40, -175), new Vector2(600, 40));

            // Level / round (top-center)
            Text levelRound = NewText(root, "LevelRoundText", f, "LEVEL 1 - ROUND 1/5", 30, TextAnchor.MiddleCenter);
            Anchor(levelRound.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(900, 50));

            // Score (top-right)
            Text score = NewText(root, "ScoreText", f, "SCORE  0", 34, TextAnchor.MiddleRight);
            Anchor(score.rectTransform, new Vector2(1, 1), new Vector2(-40, -40), new Vector2(600, 50));

            // Food meter (bottom-left)
            Text foodLabel = NewText(root, "FoodLabel", f, "FOOD", 22, TextAnchor.MiddleLeft);
            Anchor(foodLabel.rectTransform, new Vector2(0, 0), new Vector2(40, 70), new Vector2(200, 30));
            Image foodBg = NewImage(root, "FoodBarBg", white, new Color(0f, 0f, 0f, 0.6f));
            Anchor(foodBg.rectTransform, new Vector2(0, 0), new Vector2(40, 40), new Vector2(360, 26));
            Image foodFill = NewImage(foodBg.rectTransform, "FoodBarFill", white, new Color(1f, 0.7f, 0.2f));
            Stretch(foodFill.rectTransform, 3f);
            MakeFilled(foodFill);

            // Special weapon panel (bottom-center): icon, name, ammo bar
            var specialRoot = new GameObject("SpecialPanel", typeof(RectTransform), typeof(CanvasGroup));
            specialRoot.transform.SetParent(root, false);
            var specialRt = (RectTransform)specialRoot.transform;
            Anchor(specialRt, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(560, 104));
            specialRt.pivot = new Vector2(0.5f, 0f);
            CanvasGroup specialGroup = specialRoot.GetComponent<CanvasGroup>();
            specialGroup.blocksRaycasts = false;
            Image specialBg = NewImage(specialRt, "Bg", white, new Color(0.02f, 0.03f, 0.08f, 0.6f));
            specialBg.type = Image.Type.Sliced;
            Stretch(specialBg.rectTransform, 0f);
            Image specialIcon = NewImage(specialRt, "Icon", LoadSprite("Kenney_SpaceShooterRedux/Power-ups/powerupBlue_star.png"), Color.white);
            Anchor(specialIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(76, 76));
            specialIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            Text specialText = NewText(specialRt, "Name", f, "NO SPECIAL WEAPON", 34, TextAnchor.MiddleLeft);
            Anchor(specialText.rectTransform, new Vector2(0f, 1f), new Vector2(108f, -12f), new Vector2(300, 44));
            Text specialHint = NewText(specialRt, "Hint", f, "RIGHT CLICK / C", 18, TextAnchor.MiddleRight);
            Anchor(specialHint.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(220, 30));
            specialHint.color = new Color(0.8f, 0.85f, 1f, 0.7f);
            Image ammoBg = NewImage(specialRt, "AmmoBg", white, new Color(0f, 0f, 0f, 0.7f));
            Anchor(ammoBg.rectTransform, new Vector2(0f, 0f), new Vector2(108f, 18f), new Vector2(330, 22));
            Image specialAmmoFill = NewImage(ammoBg.rectTransform, "AmmoFill", white, Color.white);
            Stretch(specialAmmoFill.rectTransform, 3f);
            MakeFilled(specialAmmoFill);
            Text specialAmmoText = NewText(specialRt, "Ammo", f, "", 28, TextAnchor.MiddleRight);
            Anchor(specialAmmoText.rectTransform, new Vector2(1f, 0f), new Vector2(-16f, 12f), new Vector2(100, 36));

            // Boss bar (top-center under the round text)
            var bossRoot = new GameObject("BossBar", typeof(RectTransform));
            bossRoot.transform.SetParent(root, false);
            Anchor((RectTransform)bossRoot.transform, new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(900, 124));
            Text bossName = NewText((RectTransform)bossRoot.transform, "BossName", f, "BOSS", 24, TextAnchor.MiddleCenter);
            Anchor(bossName.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(1500, 80));
            bossName.alignment = TextAnchor.LowerCenter;
            bossName.horizontalOverflow = HorizontalWrapMode.Wrap;
            bossName.verticalOverflow = VerticalWrapMode.Overflow;
            bossName.supportRichText = true;
            bossName.color = new Color(1f, 0.2f, 0.18f);
            Image bossBg = NewImage((RectTransform)bossRoot.transform, "BossBarBg", white, new Color(0f, 0f, 0f, 0.7f));
            Anchor(bossBg.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(880, 26));
            Image bossFill = NewImage(bossBg.rectTransform, "BossBarFill", white, new Color(0.95f, 0.2f, 0.25f));
            Stretch(bossFill.rectTransform, 3f);
            MakeFilled(bossFill);

            // Banner (center)
            var banner = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup));
            banner.transform.SetParent(root, false);
            Place((RectTransform)banner.transform, new Vector2(0.5f, 0.55f), new Vector2(1400, 220));
            var bannerGroup = banner.GetComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.interactable = false;
            Text bannerTitle = NewText((RectTransform)banner.transform, "Title", f, "ROUND 1", 80, TextAnchor.MiddleCenter);
            Place(bannerTitle.rectTransform, new Vector2(0.5f, 0.65f), new Vector2(1400, 110));
            bannerTitle.color = new Color(1f, 0.85f, 0.3f);
            Text bannerSub = NewText((RectTransform)banner.transform, "Subtitle", f, "", 34, TextAnchor.MiddleCenter);
            Place(bannerSub.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(1400, 60));

            // Pause panel
            GameObject pause = Panel(root, "PausePanel", f, "PAUSED", out Text pauseInfo, out Button resume, "RESUME", out Button pauseMenu, "MAIN MENU");
            pauseInfo.text = "SPACE / LMB  FIRE     RMB / C  SPECIAL     X  MEGA MISSILE";
            // Game over panel
            GameObject over = Panel(root, "GameOverPanel", f, "GAME OVER", out Text overScore, out Button retry, "RETRY", out Button overMenu, "MAIN MENU");
            GameObject victory = Panel(root, "VictoryPanel", f, "VICTORY!", out Text victoryScore, out Button victoryMenu, "MAIN MENU", out _, null);

            GameHUD hud = canvas.gameObject.AddComponent<GameHUD>();
            Set(hud, "levelRoundText", levelRound);
            Set(hud, "scoreText", score);
            Set(hud, "livesText", lives);
            Set(hud, "missilesText", missiles);
            Set(hud, "powerText", power);
            Set(hud, "specialLineText", specialLine);
            Set(hud, "foodBarFill", foodFill);
            Set(hud, "specialPanel", specialGroup);
            Set(hud, "specialIcon", specialIcon);
            Set(hud, "specialText", specialText);
            Set(hud, "specialAmmoFill", specialAmmoFill);
            Set(hud, "specialAmmoText", specialAmmoText);
            Set(hud, "bossBarRoot", bossRoot);
            Set(hud, "bossBarFill", bossFill);
            Set(hud, "bossNameText", bossName);
            Set(hud, "bannerGroup", bannerGroup);
            Set(hud, "bannerTitle", bannerTitle);
            Set(hud, "bannerSubtitle", bannerSub);
            Set(hud, "pausePanel", pause);
            Set(hud, "resumeButton", resume);
            Set(hud, "pauseMenuButton", pauseMenu);
            Set(hud, "gameOverPanel", over);
            Set(hud, "retryButton", retry);
            Set(hud, "gameOverMenuButton", overMenu);
            Set(hud, "gameOverScoreText", overScore);
            Set(hud, "victoryPanel", victory);
            Set(hud, "victoryScoreText", victoryScore);
            Set(hud, "victoryMenuButton", victoryMenu);
        }

        private static GameObject Panel(RectTransform parent, string name, Font f, string title, out Text info,
                                        out Button first, string firstLabel, out Button second, string secondLabel)
        {
            Sprite white = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Image dim = NewImage(parent, name, null, new Color(0f, 0f, 0f, 0.65f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform, 0f);

            Image box = NewImage(dim.rectTransform, "Box", white, new Color(0.04f, 0.06f, 0.15f, 0.95f));
            box.type = Image.Type.Sliced;
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(760, 520));
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.8f, 1f, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);

            Text t = NewText(box.rectTransform, "Title", f, title, 84, TextAnchor.MiddleCenter);
            Place(t.rectTransform, new Vector2(0.5f, 0.8f), new Vector2(740, 110));
            t.color = new Color(1f, 0.82f, 0.3f);
            info = NewText(box.rectTransform, "Info", f, "", 28, TextAnchor.MiddleCenter);
            Place(info.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(720, 60));
            info.color = new Color(0.85f, 0.9f, 1f);

            first = NewButton(box.rectTransform, firstLabel + "Button", f, firstLabel, "buttonGreen");
            Place((RectTransform)first.transform, new Vector2(0.5f, secondLabel != null ? 0.38f : 0.3f), new Vector2(420, 80));
            second = null;
            if (secondLabel != null)
            {
                second = NewButton(box.rectTransform, secondLabel + "Button", f, secondLabel, "buttonBlue");
                Place((RectTransform)second.transform, new Vector2(0.5f, 0.18f), new Vector2(420, 80));
            }
            dim.gameObject.SetActive(false);
            return dim.gameObject;
        }

        // ================================================================== UI helpers

        private static Canvas NewCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetAsLastSibling();
        }

        private static Text NewText(RectTransform parent, string name, Font font, string value, int size, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = value;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.fontStyle = FontStyle.Normal;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            return t;
        }

        private static Image NewImage(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Button NewButton(RectTransform parent, string name, Font font, string label, string kenneySprite)
        {
            Image img = NewImage(parent, name, LoadSprite($"Kenney_SpaceShooterRedux/UI/{kenneySprite}.png"), Color.white);
            img.raycastTarget = true;
            img.type = Image.Type.Simple;
            Button b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            Text t = NewText(img.rectTransform, "Label", font, label, 34, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform, 0f);
            t.color = new Color(0.12f, 0.13f, 0.2f);
            Object.DestroyImmediate(t.GetComponent<Shadow>());
            return b;
        }

        private static void MakeFilled(Image img)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 0f;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        // ================================================================== generic helpers

        /// <summary>Player ship art supplied by the user (Assets/_Project/Art/PlayerShip.png).</summary>
        private static Sprite PlayerShipSprite()
        {
            string path = $"{P}/Art/PlayerShip.png";
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) throw new Exception($"[GameContentBuilder] Player ship sprite not found: {path}");
            return s;
        }

        private static Sprite Art(string name)
        {
            string path = $"{P}/Art/Generated/{name}.png";
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) throw new Exception($"[GameContentBuilder] Generated sprite not found: {path}");
            return s;
        }

        private static GameObject SpriteObject(string name, Sprite sprite, int order, int layer, float scale = 1f)
        {
            var go = new GameObject(name) { layer = layer };
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static void AddTrail(GameObject go, Material mat, float time, float width, int order, Color? color = null)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = mat;
            trail.time = time;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = width;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            Color c = color ?? Color.white;
            trail.startColor = c;
            trail.endColor = new Color(c.r, c.g, c.b, 0f);
            trail.sortingOrder = order;
            trail.emitting = true;
        }

        private static GameObject SpriteObject(string name, string spritePath, int order, int layer, float scale = 1f)
        {
            var go = new GameObject(name) { layer = layer };
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(spritePath);
            sr.sortingOrder = order;
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static Rigidbody2D AddBody(GameObject go, bool continuous)
        {
            // Dynamic + zero gravity + trigger colliders: reliable trigger callbacks between all moving objects
            // without any physical push-back.
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = continuous ? CollisionDetectionMode2D.Continuous : CollisionDetectionMode2D.Discrete;
            return rb;
        }

        private static CircleCollider2D AddCircle(GameObject go, float radius)
        {
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = radius;
            return col;
        }

        private static Sprite LoadSprite(string relative)
        {
            string path = TP + relative;
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) throw new Exception($"[GameContentBuilder] Sprite not found: {path}");
            return s;
        }

        private static readonly System.Collections.Generic.HashSet<string> generatedAssets = new();

        /// <summary>Deletes assets in a folder that this build run did not (re)generate.</summary>
        private static void PruneFolder(string folder)
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path) || generatedAssets.Contains(path)) continue;
                Debug.Log($"[GameContentBuilder] Removing stale asset {path}");
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p.propertyType == SerializedPropertyType.Integer) p.intValue = Mathf.RoundToInt(value);
            else p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            generatedAssets.Add(path);
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        private static GameObject SavePrefab(GameObject go, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) throw new Exception($"[GameContentBuilder] {target.GetType().Name}.{field} not found");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) throw new Exception($"[GameContentBuilder] {target.GetType().Name}.{field} not found");
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
