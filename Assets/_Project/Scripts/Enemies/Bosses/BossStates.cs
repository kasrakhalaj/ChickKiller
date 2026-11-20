using System.Collections.Generic;
using ChickenShooter.Combat;
using ChickenShooter.Core;
using ChickenShooter.Data;
using UnityEngine;

namespace ChickenShooter.Enemies.Bosses
{
    /// <summary>Maps the data enum to state objects (the profile asset never references code directly).</summary>
    public static class BossAttackFactory
    {
        public static BossState Create(BossAttack attack) => attack switch
        {
            BossAttack.EggRain => new EggRainState(),
            BossAttack.ChargeDash => new ChargeDashState(),
            BossAttack.AimedBurst => new AimedBurstState(),
            BossAttack.FeatherSpiral => new FeatherSpiralState(),
            BossAttack.RadialBurst => new RadialBurstState(),
            BossAttack.LaneBarrage => new LaneBarrageState(),
            BossAttack.HomingSalvo => new HomingSalvoState(),
            BossAttack.Teleport => new TeleportState(),
            BossAttack.SummonMinions => new SummonState(),
            _ => new AimedBurstState()
        };
    }

    // =============================================================== movement / flow states

    /// <summary>Flies in from above, invulnerable, then starts fighting.</summary>
    public class BossIntroState : BossState
    {
        private Vector2 target;

        public override void Enter()
        {
            boss.Invulnerable = true;
            target = CameraBounds.ViewportToWorld(new Vector2(0.5f, 0.72f));
        }

        protected override void OnTick(float dt)
        {
            if (boss.MoveTowards(target, 3.5f, dt) || time > 4f) boss.NextAction();
        }

        public override void Exit() => boss.Invulnerable = false;
    }

    /// <summary>Glides to a random point in the upper screen, then rests briefly.</summary>
    public class BossRepositionState : BossState
    {
        private Vector2 target;
        private bool arrived;
        private float rest;

        public override void Enter()
        {
            target = boss.RandomHoverPoint();
            rest = boss.Cooldown(boss.Profile.restTime);
        }

        protected override void OnTick(float dt)
        {
            if (!arrived)
            {
                arrived = boss.MoveTowards(target, boss.Speed, dt) || time > 3f;
                return;
            }
            rest -= dt;
            if (rest <= 0f) boss.NextAction();
        }
    }

    /// <summary>Health threshold crossed: invulnerable roar, shockwave, banner, optional minion summon.</summary>
    public class BossPhaseShiftState : BossState
    {
        private const float Duration = 1.4f;

        public override void Enter()
        {
            boss.Invulnerable = true;
            boss.Telegraph(Duration);
            Vfx.Spawn(boss.Profile.phaseShiftVfx, boss.Position, 1.5f);
            GameEvents.RaiseScreenShake(0.35f, 0.6f);
            GameEvents.RaiseBossPhaseChanged(boss.PhaseIndex, boss.Phase.label);
        }

        protected override void OnTick(float dt)
        {
            if (time < Duration) return;
            if (boss.Phase.minionsPerSummon > 0) boss.SummonMinions(boss.Phase.minionsPerSummon);
            boss.NextAction();
        }

        public override void Exit() => boss.Invulnerable = false;
    }

    // =============================================================== attack states

    /// <summary>Sweeps across the top of the screen dropping dense egg fans.</summary>
    public class EggRainState : BossState
    {
        private Vector2 from, to;
        private bool inPosition;
        private float shotTimer;

        public override void Enter()
        {
            bool leftFirst = boss.Position.x > boss.View.center.x;
            from = CameraBounds.ViewportToWorld(new Vector2(leftFirst ? 0.12f : 0.88f, 0.76f));
            to = CameraBounds.ViewportToWorld(new Vector2(leftFirst ? 0.88f : 0.12f, 0.76f));
        }

        protected override void OnTick(float dt)
        {
            if (!inPosition)
            {
                inPosition = boss.MoveTowards(from, boss.Speed * 1.5f, dt);
                return;
            }
            bool done = boss.MoveTowards(to, boss.Speed * 0.9f, dt);
            shotTimer -= dt;
            if (shotTimer <= 0f)
            {
                shotTimer = boss.Cooldown(0.16f);
                boss.FireFan(boss.Profile.egg, boss.Muzzle, Vector2.down, boss.Density(3), 36f,
                             boss.ShotSpeed * 0.85f, boss.Profile.eggTint);
            }
            if (done) boss.NextAction();
        }
    }

    /// <summary>Telegraphs, rams toward the player's position and bursts into a feather ring at the end.</summary>
    public class ChargeDashState : BossState
    {
        private enum Step { Telegraph, Dash, Burst }
        private Step step;
        private float telegraph;
        private Vector2 target;

        public override void Enter()
        {
            telegraph = boss.Cooldown(0.85f);
            boss.Telegraph(telegraph);
        }

        protected override void OnTick(float dt)
        {
            switch (step)
            {
                case Step.Telegraph:
                    if (time < telegraph) return;
                    Vector2 p = boss.PlayerPosition;
                    float floor = boss.View.yMin + boss.Renderer.bounds.extents.y * 0.7f;
                    target = new Vector2(p.x, Mathf.Max(p.y + 0.5f, floor));
                    step = Step.Dash;
                    break;
                case Step.Dash:
                    if (boss.MoveTowards(target, boss.DashSpeed, dt) || time > telegraph + 2.5f) step = Step.Burst;
                    break;
                case Step.Burst:
                    boss.FireRing(boss.Profile.feather, boss.Position, boss.Density(14), boss.ShotSpeed, boss.Profile.featherTint,
                                  Random.Range(0f, 30f));
                    GameEvents.RaiseScreenShake(0.3f, 0.3f);
                    boss.NextAction(); // next is a Reposition (flies back up)
                    break;
            }
        }
    }

    /// <summary>Several rapid volleys of aimed feathers.</summary>
    public class AimedBurstState : BossState
    {
        private int volleysLeft;
        private float timer;

        public override void Enter()
        {
            volleysLeft = boss.Density(3);
            timer = boss.Cooldown(0.35f);
            boss.Telegraph(timer);
        }

        protected override void OnTick(float dt)
        {
            timer -= dt;
            if (timer > 0f) return;
            if (volleysLeft-- <= 0)
            {
                boss.NextAction();
                return;
            }
            Vector2 dir = (boss.PlayerPosition - boss.Muzzle).normalized;
            boss.FireFan(boss.Profile.feather, boss.Muzzle, dir, boss.Density(5), 34f, boss.ShotSpeed * 1.3f, boss.Profile.featherTint);
            timer = boss.Cooldown(0.4f);
        }
    }

    /// <summary>Rotating multi-arm spiral of feathers (bullet-hell pattern).</summary>
    public class FeatherSpiralState : BossState
    {
        private const float Duration = 3.2f;
        private float angle;
        private float timer;

        protected override void OnTick(float dt)
        {
            timer -= dt;
            if (timer <= 0f)
            {
                timer = boss.Cooldown(0.085f);
                int arms = boss.Density(3);
                boss.FireRing(boss.Profile.feather, boss.Position, arms, boss.ShotSpeed * 0.8f, boss.Profile.featherTint, angle);
                angle += boss.Phase.enraged ? 17f : 13f;
            }
            if (time >= Duration) boss.NextAction();
        }
    }

    /// <summary>Expanding egg rings, each rotated slightly so the gaps move.</summary>
    public class RadialBurstState : BossState
    {
        private int ringsLeft;
        private float timer;
        private float offset;

        public override void Enter()
        {
            ringsLeft = boss.Density(3);
            timer = boss.Cooldown(0.4f);
            boss.Telegraph(timer);
        }

        protected override void OnTick(float dt)
        {
            timer -= dt;
            if (timer > 0f) return;
            if (ringsLeft-- <= 0)
            {
                boss.NextAction();
                return;
            }
            boss.FireRing(boss.Profile.egg, boss.Position, boss.Density(14), boss.ShotSpeed * 0.75f, boss.Profile.eggTint, offset);
            offset += 360f / 28f;
            timer = boss.Cooldown(0.5f);
        }
    }

    /// <summary>Shows red warning columns (one always on the player), then floods those lanes with bullets.</summary>
    public class LaneBarrageState : BossState
    {
        private readonly List<float> lanes = new();
        private readonly List<GameObject> warnings = new();
        private float warnTime;
        private float fireTime;
        private float shotTimer;

        public override void Enter()
        {
            Rect v = boss.View;
            lanes.Add(boss.PlayerPosition.x);
            int extra = boss.Density(2);
            for (int i = 0; i < extra; i++) lanes.Add(Random.Range(v.xMin + 1f, v.xMax - 1f));

            warnTime = boss.Cooldown(0.9f);
            fireTime = 0.9f;
            foreach (float x in lanes) warnings.Add(CreateWarning(x, v));
            boss.Telegraph(warnTime);
        }

        private GameObject CreateWarning(float x, Rect v)
        {
            var go = new GameObject("LaneWarning");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = boss.Profile.laneWarningSprite;
            if (boss.Profile.laneWarningMaterial != null) sr.sharedMaterial = boss.Profile.laneWarningMaterial;
            sr.sortingOrder = 1;
            sr.color = new Color(1f, 0.15f, 0.1f, 0.35f);
            Vector2 size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            go.transform.position = new Vector3(x, v.center.y, 0f);
            go.transform.localScale = new Vector3(0.9f / size.x, v.height / size.y, 1f);
            return go;
        }

        protected override void OnTick(float dt)
        {
            if (time < warnTime)
            {
                float blink = 0.25f + 0.2f * Mathf.Sin(time * 30f);
                foreach (GameObject w in warnings)
                    if (w != null) w.GetComponent<SpriteRenderer>().color = new Color(1f, 0.15f, 0.1f, blink);
                return;
            }

            shotTimer -= dt;
            if (shotTimer <= 0f)
            {
                shotTimer = 0.06f;
                float top = boss.View.yMax - 0.3f;
                foreach (float x in lanes)
                    boss.Fire(boss.Profile.feather, new Vector2(x + Random.Range(-0.15f, 0.15f), top), Vector2.down,
                              boss.ShotSpeed * 2f, boss.Profile.featherTint);
            }
            if (time >= warnTime + fireTime) boss.NextAction();
        }

        public override void Exit()
        {
            foreach (GameObject w in warnings)
                if (w != null) Object.Destroy(w);
            warnings.Clear();
        }
    }

    /// <summary>Fan of slow homing orbs.</summary>
    public class HomingSalvoState : BossState
    {
        private int left;
        private float timer;

        public override void Enter()
        {
            left = boss.Density(5);
            timer = boss.Cooldown(0.4f);
            boss.Telegraph(timer);
        }

        protected override void OnTick(float dt)
        {
            timer -= dt;
            if (timer > 0f) return;
            if (left-- <= 0)
            {
                boss.NextAction();
                return;
            }
            float a = Random.Range(-70f, 70f);
            Vector2 dir = Quaternion.Euler(0f, 0f, a) * Vector2.down;
            boss.Fire(boss.Profile.orb, boss.Muzzle, dir, boss.ShotSpeed * 0.7f, boss.Profile.orbTint);
            timer = boss.Cooldown(0.14f);
        }
    }

    /// <summary>Fades out (invulnerable), reappears somewhere else and releases a feather ring.</summary>
    public class TeleportState : BossState
    {
        private enum Step { FadeOut, FadeIn }
        private Step step;
        private const float Fade = 0.35f;

        public override void Enter()
        {
            boss.Invulnerable = true;
            Vfx.Spawn(boss.Profile.teleportVfx, boss.Position, 1.6f);
        }

        protected override void OnTick(float dt)
        {
            if (step == Step.FadeOut)
            {
                boss.SetAlpha(1f - Mathf.Clamp01(time / Fade));
                if (time < Fade) return;
                Vector2 p = boss.RandomHoverPoint();
                boss.Body.position = p;
                boss.transform.position = p;
                Vfx.Spawn(boss.Profile.teleportVfx, p, 1.6f);
                step = Step.FadeIn;
                time = 0f;
                return;
            }

            boss.SetAlpha(Mathf.Clamp01(time / Fade));
            if (time < Fade) return;
            boss.SetAlpha(1f);
            boss.Invulnerable = false;
            boss.FireRing(boss.Profile.feather, boss.Position, boss.Density(16), boss.ShotSpeed, boss.Profile.featherTint,
                          Random.Range(0f, 20f));
            boss.NextAction();
        }

        public override void Exit()
        {
            boss.SetAlpha(1f);
            boss.Invulnerable = false;
        }
    }

    /// <summary>Calls a wave of small diving chicks.</summary>
    public class SummonState : BossState
    {
        public override void Enter()
        {
            boss.Telegraph(0.6f);
            Vfx.Spawn(boss.Profile.phaseShiftVfx, boss.Position, 0.8f);
        }

        protected override void OnTick(float dt)
        {
            if (time < 0.6f) return;
            boss.SummonMinions(Mathf.Max(3, boss.Phase.minionsPerSummon));
            boss.NextAction();
        }
    }
}
