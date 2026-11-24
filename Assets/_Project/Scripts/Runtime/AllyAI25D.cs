using UnityEngine;

namespace AnimalsVsMonsters
{
    /// <summary>
    /// Friendly animal companion: follows the player, and attacks monsters that come close
    /// (but never strays too far from the player). Has no Health, so the player's attacks
    /// and monsters ignore it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AllyAI25D : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Follow")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float followDistance = 1.8f;
        [SerializeField] private float catchUpDistance = 5f;
        [SerializeField] private float catchUpMultiplier = 1.6f;
        [SerializeField] private float teleportDistance = 15f;

        [Header("Help fight")]
        [SerializeField] private float enemyDetectRange = 5f;
        [SerializeField] private float leashRange = 7f;
        [SerializeField] private float contactRange = 1f;
        [SerializeField] private int damage = 1;
        [SerializeField] private float attackCooldown = 0.8f;

        private Rigidbody _rb;
        private PlayerController25D _playerController;
        private Health _target;
        private float _nextScanTime, _nextAttackTime;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Start()
        {
            if (player == null)
            {
                var p = FindFirstObjectByType<PlayerController25D>();
                if (p != null) player = p.transform;
            }
            if (player == null) return;
            _playerController = player.GetComponent<PlayerController25D>();

            // Don't shove the player around.
            foreach (var mine in GetComponentsInChildren<Collider>())
            foreach (var theirs in player.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(mine, theirs);
        }

        private void FixedUpdate()
        {
            if (player == null) return;

            float toPlayer = FlatDistance(transform.position, player.position);
            if (toPlayer > teleportDistance)
            {
                _rb.position = player.position - Vector3.right * Facing() * followDistance;
                _rb.linearVelocity = Vector3.zero;
                return;
            }

            if (Time.time >= _nextScanTime)
            {
                _nextScanTime = Time.time + 0.25f;
                _target = FindTarget();
            }

            Vector3 dest;
            float speed = moveSpeed;
            if (_target != null)
            {
                dest = _target.transform.position;
                if (FlatDistance(transform.position, dest) < contactRange && Time.time >= _nextAttackTime)
                {
                    _nextAttackTime = Time.time + attackCooldown;
                    _target.TakeDamage(damage, transform.position);
                }
            }
            else
            {
                // Trail just behind the player.
                dest = player.position - Vector3.right * Facing() * followDistance;
                if (toPlayer > catchUpDistance) speed *= catchUpMultiplier;
            }

            Vector3 dir = dest - transform.position;
            dir.y = 0f;
            float stopDist = _target != null ? contactRange * 0.6f : 0.3f;
            Vector3 planar = dir.magnitude > stopDist ? dir.normalized * speed : Vector3.zero;
            _rb.linearVelocity = new Vector3(planar.x, _rb.linearVelocity.y, planar.z);

            if (spriteRenderer != null)
            {
                if (Mathf.Abs(planar.x) > 0.05f) spriteRenderer.flipX = planar.x < 0f;
                else if (_target == null) spriteRenderer.flipX = Facing() < 0f;
            }
        }

        private Health FindTarget()
        {
            Health best = null;
            float bestDist = enemyDetectRange;
            foreach (var enemy in FindObjectsByType<EnemyAI25D>(FindObjectsSortMode.None))
            {
                var h = enemy.GetComponent<Health>();
                if (h == null || h.IsDead) continue;
                if (FlatDistance(enemy.transform.position, player.position) > leashRange) continue;
                float d = FlatDistance(transform.position, enemy.transform.position);
                if (d < bestDist) { bestDist = d; best = h; }
            }
            return best;
        }

        private float Facing() => _playerController != null ? _playerController.Facing : 1f;

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, enemyDetectRange);
        }
    }
}
