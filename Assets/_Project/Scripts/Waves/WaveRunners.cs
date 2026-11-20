using System.Collections;
using System.Collections.Generic;
using ChickenShooter.Combat;
using ChickenShooter.Core;
using ChickenShooter.Data;
using ChickenShooter.Enemies;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Waves
{
    /// <summary>
    /// Strategy interface: each WaveType has its own runner. The RoundManager only knows this interface,
    /// so adding a new kind of round = adding a new runner class, without touching the round loop.
    /// </summary>
    public interface IWaveRunner
    {
        /// <summary>Spawns the wave and completes when every spawned entity has been removed.</summary>
        IEnumerator Run(WaveDataSO wave, WaveModifiers modifiers);
    }

    public static class WaveRunnerFactory
    {
        public static IWaveRunner Create(WaveType type, MonoBehaviour host) => type switch
        {
            WaveType.Formation => new FormationWaveRunner(host),
            WaveType.Asteroids => new AsteroidWaveRunner(host),
            WaveType.Boss => new BossWaveRunner(),
            _ => new FormationWaveRunner(host)
        };
    }

    /// <summary>
    /// Counts live WaveEntities so a runner can wait for "wave cleared". Entities that will spawn later
    /// (free agents on a timer) are reserved up-front so the wave can't end before they appear.
    /// </summary>
    public class AliveCounter
    {
        public int Alive { get; private set; }

        public void Reserve(int count) => Alive += count;

        public void Track(WaveEntity e, bool reserved = false)
        {
            if (!reserved) Alive++;
            e.Removed += _ => Alive--;
        }
    }

    /// <summary>Spawns the wave's free agents on their timers (shared by all wave types).</summary>
    public static class RogueSpawner
    {
        public static void Begin(MonoBehaviour host, WaveDataSO wave, WaveModifiers mods, AliveCounter counter)
        {
            if (wave.rogues == null) return;
            foreach (RogueSpawn entry in wave.rogues)
            {
                if (entry.enemy == null || !(entry.enemy.prefab is RogueEnemy)) continue;
                counter.Reserve(entry.count);
                host.StartCoroutine(SpawnEntry(entry, mods, counter));
            }
        }

        private static IEnumerator SpawnEntry(RogueSpawn entry, WaveModifiers mods, AliveCounter counter)
        {
            yield return new WaitForSeconds(entry.startDelay);
            var prefab = (RogueEnemy)entry.enemy.prefab;
            for (int i = 0; i < entry.count; i++)
            {
                RogueEnemy rogue = PoolManager.Spawn(prefab, new Vector3(0f, 100f), Quaternion.identity);
                rogue.Init(entry.enemy, mods);
                counter.Track(rogue, reserved: true);
                if (i < entry.count - 1) yield return new WaitForSeconds(entry.interval);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------

    public class FormationWaveRunner : IWaveRunner
    {
        private readonly MonoBehaviour host;

        public FormationWaveRunner(MonoBehaviour host) => this.host = host;

        public IEnumerator Run(WaveDataSO wave, WaveModifiers mods)
        {
            var go = new GameObject($"Formation_{wave.name}");
            go.transform.SetParent(host.transform, false);
            var formation = go.AddComponent<FormationController>();
            formation.Setup(wave, mods);

            EnemyDataSO[] slotEnemies = AssignSlots(wave, formation.SlotCount);
            var counter = new AliveCounter();
            RogueSpawner.Begin(host, wave, mods, counter);

            for (int i = 0; i < formation.SlotCount; i++)
            {
                EnemyDataSO enemy = slotEnemies[i];
                bool mirrored = wave.mirrorAlternate && (i % 2 == 1);
                Vector2 slot = formation.GetSlotPosition(i);
                List<Vector3> path = wave.entryPath != null
                    ? wave.entryPath.BuildWaypoints(slot, mirrored)
                    : new List<Vector3> { new Vector3(slot.x, CameraBounds.WorldRect.yMax + 1f), slot };

                var prefab = (Chicken)enemy.prefab;
                Chicken chicken = PoolManager.Spawn(prefab, path[0], Quaternion.identity);
                chicken.Init(enemy, mods, formation, i, path, i * wave.entryStagger, wave.entrySpeed);
                formation.AddMember(chicken);
                counter.Track(chicken);
            }

            while (counter.Alive > 0) yield return null;
            Object.Destroy(go);
        }

        /// <summary>Main enemy everywhere, then flock specials placed on random distinct slots.</summary>
        private static EnemyDataSO[] AssignSlots(WaveDataSO wave, int slotCount)
        {
            var result = new EnemyDataSO[slotCount];
            for (int i = 0; i < slotCount; i++) result[i] = wave.enemy;
            if (wave.flockSpecials == null) return result;

            var free = new List<int>(slotCount);
            for (int i = 0; i < slotCount; i++) free.Add(i);
            foreach (FlockSpecial special in wave.flockSpecials)
            {
                if (special.enemy == null || !(special.enemy.prefab is Chicken)) continue;
                for (int n = 0; n < special.count && free.Count > 0; n++)
                {
                    int pick = Random.Range(0, free.Count);
                    result[free[pick]] = special.enemy;
                    free.RemoveAt(pick);
                }
            }
            return result;
        }
    }

    // ---------------------------------------------------------------------------------------------

    public class AsteroidWaveRunner : IWaveRunner
    {
        private readonly MonoBehaviour host;

        public AsteroidWaveRunner(MonoBehaviour host) => this.host = host;

        public IEnumerator Run(WaveDataSO wave, WaveModifiers mods)
        {
            var counter = new AliveCounter();
            RogueSpawner.Begin(host, wave, mods, counter);
            Pickup food = wave.enemy != null ? wave.enemy.foodPrefab : null;

            for (int i = 0; i < wave.asteroidCount; i++)
            {
                Rect cam = CameraBounds.WorldRect;
                Vector2 pos = new Vector2(Random.Range(cam.xMin + 0.5f, cam.xMax - 0.5f), cam.yMax + 1.5f);
                float speed = Random.Range(wave.asteroidSpeed.x, wave.asteroidSpeed.y) * mods.Speed;
                Vector2 dir = Quaternion.Euler(0f, 0f, Random.Range(-18f, 18f)) * Vector2.down;
                Sprite sprite = wave.asteroidSprites != null && wave.asteroidSprites.Length > 0
                    ? wave.asteroidSprites[Random.Range(0, wave.asteroidSprites.Length)]
                    : null;
                float scale = Random.Range(wave.asteroidScale.x, wave.asteroidScale.y);
                int hp = Mathf.Max(1, Mathf.RoundToInt(wave.asteroidHealth * mods.Health));

                Asteroid a = PoolManager.Spawn(wave.asteroidPrefab, pos, Quaternion.identity);
                a.Init(sprite, scale, hp, dir * speed, Random.Range(-60f, 60f), food, wave.asteroidFoodDropChance);
                counter.Track(a);

                float wait = Random.Range(wave.asteroidSpawnInterval.x, wave.asteroidSpawnInterval.y);
                yield return new WaitForSeconds(wait / Mathf.Max(0.1f, mods.FireRate));
            }

            while (counter.Alive > 0) yield return null;
        }
    }

    // ---------------------------------------------------------------------------------------------

    public class BossWaveRunner : IWaveRunner
    {
        public IEnumerator Run(WaveDataSO wave, WaveModifiers mods)
        {
            if (wave.bossPrefab == null || wave.enemy == null || wave.bossProfile == null)
            {
                Debug.LogWarning($"[BossWaveRunner] Wave '{wave.name}' has no boss prefab/data. Skipping.");
                yield break;
            }

            var counter = new AliveCounter();
            Rect cam = CameraBounds.WorldRect;
            BossController boss = PoolManager.Spawn(wave.bossPrefab, new Vector3(cam.center.x, cam.yMax + 2.5f), Quaternion.identity);
            boss.Init(wave.enemy, mods, wave.bossProfile);
            counter.Track(boss);

            while (counter.Alive > 0) yield return null;
        }
    }
}
