using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChickenShooter.Player
{
    /// <summary>
    /// The only place gameplay reads input. Wraps the "Gameplay" map of the GameControls
    /// Input Actions asset (New Input System) and exposes clean values/events to other components.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string actionMapName = "Gameplay";

        private InputActionMap map;
        private InputAction moveAction;
        private InputAction fireAction;
        private InputAction missileAction;
        private InputAction specialAction;
        private InputAction pauseAction;

        public Vector2 Move => moveAction != null ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
        public bool FireHeld => fireAction != null && fireAction.IsPressed();
        public bool SpecialHeld => specialAction != null && specialAction.IsPressed();

        public event Action MissilePressed;
        public event Action PausePressed;

        private void Awake()
        {
            map = actions.FindActionMap(actionMapName, throwIfNotFound: true);
            moveAction = map.FindAction("Move", true);
            fireAction = map.FindAction("Fire", true);
            missileAction = map.FindAction("Missile", true);
            specialAction = map.FindAction("Special", true);
            pauseAction = map.FindAction("Pause", true);
        }

        private void OnEnable()
        {
            missileAction.performed += OnMissile;
            pauseAction.performed += OnPause;
            map.Enable();
        }

        private void OnDisable()
        {
            missileAction.performed -= OnMissile;
            pauseAction.performed -= OnPause;
            map.Disable();
        }

        private void OnMissile(InputAction.CallbackContext _) => MissilePressed?.Invoke();
        private void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
    }
}
