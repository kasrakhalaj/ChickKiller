using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>Tiny frame-by-frame sprite animator (avoids an Animator Controller per enemy variant).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float frameRate = 10f;

        private SpriteRenderer spriteRenderer;
        private float time;

        private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

        public void Play(Sprite[] newFrames, float fps)
        {
            frames = newFrames;
            frameRate = fps;
            time = Random.value * 2f; // desync neighbours
            Apply();
        }

        private void Update()
        {
            time += Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            if (frames == null || frames.Length == 0) return;
            int index = (int)(time * frameRate) % frames.Length;
            spriteRenderer.sprite = frames[index];
        }
    }
}
