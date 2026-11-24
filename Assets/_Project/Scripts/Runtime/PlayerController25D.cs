using UnityEngine;
using UnityEngine.InputSystem;

namespace AnimalsVsMonsters
{
    /// <summary>
    /// 2.5D player controller: 3D physics movement on the XZ plane with a 2D sprite visual.
    /// Reads input from an InputActionAsset (keyboard/mouse + gamepad via the project's
    /// InputSystem_Actions "Player" map: Move, Jump, Attack, Sprint).
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class PlayerController25D : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMap = "Player";

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sprintMultiplier = 1.6f;
        [SerializeField] private float acceleration = 40f;
        [SerializeField] private float jumpVelocity = 6.5f;
        [SerializeField] private float extraGravity = 15f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float groundCheckDistance = 0.15f;

        [Header("Attack")]
        [SerializeField] private float attackRange = 1.2f;
        [SerializeField] private float attackRadius = 0.8f;
        [SerializeField] private int attackDamage = 1;
        [SerializeField] private float attackCooldown = 0.35f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Rigidbody _rb;
        private CapsuleCollider _col;
        private InputAction _move, _jump, _attack, _sprint;
        private Vector2 _moveInput;
        private bool _jumpQueued;
        private float _nextAttackTime;
        private float _facing = 1f;
        private Transform _cam;

        public bool IsGrounded { get; private set; }
        public float Facing => _facing;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<CapsuleCollider>();
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (Camera.main != null) _cam = Camera.main.transform;

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMap, true);
                _move = map.FindAction("Move");
                _jump = map.FindAction("Jump");
                _attack = map.FindAction("Attack");
                _sprint = map.FindAction("Sprint");
            }
            else
            {
                Debug.LogWarning($"{name}: no InputActionAsset assigned to PlayerController25D.", this);
            }
        }

        private void OnEnable() => inputActions?.FindActionMap(actionMap)?.Enable();
        private void OnDisable() => inputActions?.FindActionMap(actionMap)?.Disable();

        private void Update()
        {
            _moveInput = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            if (_jump != null && _jump.WasPressedThisFrame()) _jumpQueued = true;
            if (_attack != null && _attack.WasPressedThisFrame()) TryAttack();

            if (Mathf.Abs(_moveInput.x) > 0.1f) _facing = Mathf.Sign(_moveInput.x);
            if (spriteRenderer != null) spriteRenderer.flipX = _facing < 0f;
        }

        private void FixedUpdate()
        {
            GroundCheck();

            // Camera-relative movement on the XZ plane.
            Vector3 fwd = Vector3.forward, right = Vector3.right;
            if (_cam != null)
            {
                fwd = Vector3.ProjectOnPlane(_cam.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(_cam.right, Vector3.up).normalized;
            }

            float speed = moveSpeed * (_sprint != null && _sprint.IsPressed() ? sprintMultiplier : 1f);
            Vector3 target = (right * _moveInput.x + fwd * _moveInput.y) * speed;
            Vector3 vel = _rb.linearVelocity;
            Vector3 planar = Vector3.MoveTowards(new Vector3(vel.x, 0f, vel.z), target, acceleration * Time.fixedDeltaTime);
            vel.x = planar.x;
            vel.z = planar.z;

            if (_jumpQueued && IsGrounded) vel.y = jumpVelocity;
            _jumpQueued = false;

            if (!IsGrounded) vel.y -= extraGravity * Time.fixedDeltaTime;
            _rb.linearVelocity = vel;
        }

        private void GroundCheck()
        {
            float r = _col.radius * 0.9f;
            Vector3 origin = transform.position + _col.center + Vector3.down * (_col.height * 0.5f - _col.radius) + Vector3.up * 0.05f;
            IsGrounded = Physics.SphereCast(origin, r, Vector3.down, out _, groundCheckDistance + 0.05f, groundMask, QueryTriggerInteraction.Ignore);
        }

        private void TryAttack()
        {
            if (Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + attackCooldown;

            Vector3 center = transform.position + Vector3.right * (_facing * attackRange) + Vector3.up * 0.5f;
            foreach (var hit in Physics.OverlapSphere(center, attackRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.attachedRigidbody == _rb) continue;
                var h = hit.GetComponentInParent<Health>();
                if (h != null && h.gameObject != gameObject) h.TakeDamage(attackDamage, transform.position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            float f = Application.isPlaying ? _facing : 1f;
            Gizmos.DrawWireSphere(transform.position + Vector3.right * (f * attackRange) + Vector3.up * 0.5f, attackRadius);
        }
    }
}
