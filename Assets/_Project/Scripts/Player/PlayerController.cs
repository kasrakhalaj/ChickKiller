using ChickenShooter.Core;
using UnityEngine;

namespace ChickenShooter.Player
{
    /// <summary>4-directional ship movement, clamped to the visible screen area.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerInputReader))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Current { get; private set; }

        [SerializeField] private float moveSpeed = 9f;
        [SerializeField] private float acceleration = 60f;
        [Tooltip("Extra margin kept between the ship and the screen edge (world units).")]
        [SerializeField] private Vector2 screenPadding = new Vector2(0.55f, 0.45f);
        [SerializeField] private float maxTilt = 12f;
        [SerializeField] private Transform visual;

        private Rigidbody2D body;
        private PlayerInputReader input;
        private Vector2 velocity;

        public bool ControlsEnabled { get; set; } = true;
        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            input = GetComponent<PlayerInputReader>();
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void FixedUpdate()
        {
            Vector2 desired = ControlsEnabled ? input.Move * moveSpeed : Vector2.zero;
            velocity = Vector2.MoveTowards(velocity, desired, acceleration * Time.fixedDeltaTime);

            Vector2 next = body.position + velocity * Time.fixedDeltaTime;
            Rect r = CameraBounds.WorldRect;
            next.x = Mathf.Clamp(next.x, r.xMin + screenPadding.x, r.xMax - screenPadding.x);
            next.y = Mathf.Clamp(next.y, r.yMin + screenPadding.y, r.yMax - screenPadding.y);
            body.MovePosition(next);

            if (visual != null)
            {
                float tilt = -velocity.x / moveSpeed * maxTilt;
                visual.localRotation = Quaternion.Euler(0f, 0f, tilt);
            }
        }
    }
}
