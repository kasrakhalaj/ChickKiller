using ChickenShooter.Core;
using UnityEngine;

namespace ChickenShooter.Levels
{
    /// <summary>Event-driven screen shake (listens to GameEvents.ScreenShakeRequested).</summary>
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private float frequency = 28f;

        private Vector3 origin;
        private float intensity;
        private float timeLeft;
        private float duration;

        private void Awake() => origin = transform.localPosition;
        private void OnEnable() => GameEvents.ScreenShakeRequested += Shake;
        private void OnDisable() => GameEvents.ScreenShakeRequested -= Shake;

        private void Shake(float amount, float time)
        {
            if (amount < intensity * (timeLeft / Mathf.Max(0.001f, duration))) return; // keep the stronger shake
            intensity = amount;
            duration = timeLeft = time;
        }

        private void LateUpdate()
        {
            if (timeLeft <= 0f)
            {
                transform.localPosition = origin;
                return;
            }
            timeLeft -= Time.unscaledDeltaTime;
            float strength = intensity * Mathf.Clamp01(timeLeft / duration);
            float t = Time.unscaledTime * frequency;
            var offset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * (2f * strength);
            transform.localPosition = origin + offset;
        }
    }
}
