using UnityEngine;

namespace AnimalsVsMonsters
{
    /// <summary>
    /// Basic enemy: wanders around its spawn point, chases the player when close,
    /// and deals contact damage. Enable "floating" for hovering enemies (no gravity,
    /// hovers a set height above the ground and bobs gently, rising over obstacles).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyAI25D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float detectRange = 6f;
        [SerializeField] private float loseRange = 9f;
        [SerializeField] private float wanderRadius = 3f;
        [SerializeField] private float contactRange = 0.9f;
        [SerializeField] private int contactDamage = 1;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Floating")]
        [SerializeField] private bool floating;
        [SerializeField] private float hoverHeight = 0.6f;
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float bobFrequency = 0.8f;
        [SerializeField] private float verticalStiffness = 4f;

        private Rigidbody _rb;
        private Vector3 _home;
        private Vector3 _wanderPoint;
        private float _nextWanderTime;
        private bool _chasing;
        private float _bobPhase;

        public bool IsFloating => floating;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (floating) _rb.useGravity = false;
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _home = transform.position;
            _wanderPoint = _home;
            _bobPhase = Random.value * Mathf.PI * 2f;
        }

        private void Start()
        {
            if (target == null)
            {
                var player = FindFirstObjectByType<PlayerController25D>();
                if (player != null) target = player.transform;
            }
        }

        private void FixedUpdate()
        {
            Vector3 dest = transform.position;
            float speed = moveSpeed;

            if (target != null)
            {
                float dist = FlatDistance(transform.position, target.position);
                _chasing = _chasing ? dist < loseRange : dist < detectRange;

                if (_chasing)
                {
                    dest = target.position;
                    if (dist < contactRange)
                    {
                        var h = target.GetComponent<Health>();
                        if (h != null) h.TakeDamage(contactDamage, transform.position);
                    }
                }
            }

            if (!_chasing)
            {
                if (Time.time > _nextWanderTime || FlatDistance(transform.position, _wanderPoint) < 0.3f)
                {
                    Vector2 r = Random.insideUnitCircle * wanderRadius;
                    _wanderPoint = _home + new Vector3(r.x, 0f, r.y);
                    _nextWanderTime = Time.time + Random.Range(2f, 4f);
                }
                dest = _wanderPoint;
                speed *= 0.5f;
            }

            Vector3 dir = dest - transform.position;
            dir.y = 0f;
            Vector3 planar = dir.sqrMagnitude > 0.01f ? dir.normalized * speed : Vector3.zero;
            float vy = floating ? HoverVelocity() : _rb.linearVelocity.y;
            _rb.linearVelocity = new Vector3(planar.x, vy, planar.z);

            if (spriteRenderer != null && Mathf.Abs(planar.x) > 0.05f)
                spriteRenderer.flipX = planar.x < 0f;
        }

        private float HoverVelocity()
        {
            float groundY = GroundUtil.TryGetGroundY(transform.position, out float y) ? y : _home.y;
            float bob = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f + _bobPhase) * bobAmplitude;
            float targetY = groundY + hoverHeight + bob;
            return (targetY - transform.position.y) * verticalStiffness;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectRange);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(Application.isPlaying ? _home : transform.position, wanderRadius);
        }
    }
}
