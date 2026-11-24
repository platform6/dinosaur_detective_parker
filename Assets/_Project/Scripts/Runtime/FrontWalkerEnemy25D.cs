using System.Collections;
using UnityEngine;

namespace AnimalsVsMonsters
{
    /// <summary>
    /// Enemy that marches straight from the back of the field toward the camera, ignoring the
    /// invisible front boundary, and falls off the front edge. Hurts the player on contact on
    /// the way. After falling it can respawn at the back at a random X to march again.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FrontWalkerEnemy25D : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 1.8f;
        [SerializeField] private int contactDamage = 1;
        [SerializeField] private float contactRange = 0.8f;

        [Header("Falling / respawn")]
        [Tooltip("Name of the invisible front wall this walker should pass through.")]
        [SerializeField] private string frontBoundaryName = "Bound_Front";
        [SerializeField] private float removeBelowY = -8f;
        [SerializeField] private bool respawnAtBack = true;
        [SerializeField] private float respawnDelay = 2.5f;
        [SerializeField] private float backZ = 10.5f;
        [SerializeField] private float respawnHalfWidth = 36f;
        [Tooltip("Spin applied while falling, for comic effect (degrees/sec around the view axis).")]
        [SerializeField] private float fallSpin = 180f;

        private Rigidbody _rb;
        private Transform _visual;
        private Transform _player;
        private Health _playerHealth;
        private bool _respawning;
        private float _lastZ, _nextStuckCheck, _sidestep;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) _visual = sr.transform;
        }

        private void Start()
        {
            var boundary = GameObject.Find(frontBoundaryName);
            if (boundary != null)
                foreach (var mine in GetComponentsInChildren<Collider>())
                foreach (var theirs in boundary.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(mine, theirs);

            var p = FindFirstObjectByType<PlayerController25D>();
            if (p != null)
            {
                _player = p.transform;
                _playerHealth = p.GetComponent<Health>();
            }
        }

        private void FixedUpdate()
        {
            if (_respawning) return;

            // March toward the camera (-Z), keep whatever vertical speed gravity gives us.
            _rb.linearVelocity = new Vector3(_sidestep, _rb.linearVelocity.y, -walkSpeed);

            // Blocked by a crate/platform? Sidestep around it.
            if (Time.time >= _nextStuckCheck)
            {
                bool stuck = Mathf.Abs(transform.position.z - _lastZ) < 0.2f && _sidestep == 0f;
                _sidestep = stuck ? (Random.value < 0.5f ? -walkSpeed : walkSpeed) : 0f;
                _lastZ = transform.position.z;
                _nextStuckCheck = Time.time + (stuck ? 0.6f : 1f);
            }

            if (_playerHealth != null && Mathf.Abs(_player.position.y - transform.position.y) < 1.5f)
            {
                Vector3 d = _player.position - transform.position;
                d.y = 0f;
                if (d.magnitude < contactRange) _playerHealth.TakeDamage(contactDamage, transform.position);
            }

            if (transform.position.y < removeBelowY)
            {
                if (respawnAtBack) StartCoroutine(Respawn());
                else Destroy(gameObject);
            }
        }

        private void Update()
        {
            // Tumble once airborne past the front edge (Billboard would otherwise reset rotation).
            if (_visual != null && !_respawning && transform.position.y < -0.3f)
            {
                var bb = _visual.GetComponent<Billboard>();
                if (bb != null && bb.enabled) bb.enabled = false;
                _visual.Rotate(0f, 0f, fallSpin * Time.deltaTime, Space.Self);
            }
        }

        private IEnumerator Respawn()
        {
            _respawning = true;
            SetVisible(false);
            _rb.isKinematic = true;
            yield return new WaitForSeconds(respawnDelay);

            Vector3 pos = new Vector3(Random.Range(-respawnHalfWidth, respawnHalfWidth), 0.05f, backZ);
            transform.position = pos;
            _rb.position = pos;
            if (_visual != null)
            {
                _visual.localRotation = Quaternion.identity;
                var bb = _visual.GetComponent<Billboard>();
                if (bb != null) bb.enabled = true;
            }
            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
            SetVisible(true);
            _respawning = false;
        }

        private void SetVisible(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = on;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = on;
        }
    }
}
