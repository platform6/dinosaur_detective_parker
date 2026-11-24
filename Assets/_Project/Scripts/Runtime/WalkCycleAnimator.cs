using UnityEngine;

namespace AnimalsVsMonsters
{
    /// <summary>
    /// Flipbook sprite animation driven by movement: plays the walk frames while moving
    /// (faster when moving faster, e.g. sprinting), shows an idle frame when standing still
    /// and an air frame while jumping/falling. Sprite flipping is left to the controller.
    /// </summary>
    public class WalkCycleAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] walkFrames;
        [Tooltip("Shown when standing still. Defaults to the first walk frame.")]
        [SerializeField] private Sprite idleFrame;
        [Tooltip("Shown while airborne. Leave empty to keep walking frames in the air.")]
        [SerializeField] private Sprite airFrame;

        [SerializeField] private float framesPerSecond = 10f;
        [Tooltip("Movement speed at which the walk plays at exactly framesPerSecond.")]
        [SerializeField] private float referenceSpeed = 5f;
        [SerializeField] private float moveThreshold = 0.2f;

        private Rigidbody _rb;
        private PlayerController25D _player;
        private float _frameClock;

        private void Awake()
        {
            _rb = GetComponentInParent<Rigidbody>();
            _player = GetComponentInParent<PlayerController25D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            if (spriteRenderer == null || walkFrames == null || walkFrames.Length == 0) return;

            Vector3 v = _rb != null ? _rb.linearVelocity : Vector3.zero;
            float speed = new Vector2(v.x, v.z).magnitude;
            bool grounded = _player == null || _player.IsGrounded;

            if (!grounded && airFrame != null)
            {
                spriteRenderer.sprite = airFrame;
            }
            else if (speed > moveThreshold)
            {
                _frameClock += Time.deltaTime * framesPerSecond * (speed / Mathf.Max(0.01f, referenceSpeed));
                spriteRenderer.sprite = walkFrames[(int)_frameClock % walkFrames.Length];
            }
            else
            {
                _frameClock = 0f;
                spriteRenderer.sprite = idleFrame != null ? idleFrame : walkFrames[0];
            }
        }
    }
}
