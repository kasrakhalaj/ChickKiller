using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>
    /// Solid-colour hit flash (white by default) using the SpriteFlash shader and a MaterialPropertyBlock,
    /// so each enemy flashes independently while sharing one material. The flash fades out quickly.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFlasher : MonoBehaviour
    {
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        [SerializeField] private float defaultDuration = 0.09f;

        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock block;
        private float amount;
        private float fadeSpeed;
        private Color color = Color.white;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            block = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            amount = 0f;
            Apply();
        }

        /// <summary>Flashes the sprite to <paramref name="flashColor"/> and fades back.</summary>
        public void Flash(Color flashColor, float duration = -1f)
        {
            color = flashColor;
            amount = 1f;
            fadeSpeed = 1f / Mathf.Max(0.01f, duration > 0f ? duration : defaultDuration);
            Apply();
        }

        public void Flash() => Flash(Color.white);

        private void LateUpdate()
        {
            if (amount <= 0f) return;
            amount = Mathf.Max(0f, amount - fadeSpeed * Time.unscaledDeltaTime);
            Apply();
        }

        private void Apply()
        {
            if (spriteRenderer == null) return;
            spriteRenderer.GetPropertyBlock(block);
            block.SetFloat(FlashAmountId, amount);
            block.SetColor(FlashColorId, color);
            spriteRenderer.SetPropertyBlock(block);
        }
    }
}
