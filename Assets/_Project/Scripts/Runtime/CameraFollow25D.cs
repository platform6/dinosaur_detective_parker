using UnityEngine;

namespace AnimalsVsMonsters
{
    /// <summary>Smoothly follows a target from a fixed angled offset (classic 2.5D view).</summary>
    public class CameraFollow25D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 4.5f, -9f);
        [SerializeField] private float pitch = 20f;
        [SerializeField] private float smoothTime = 0.2f;
        [SerializeField] private float lookAheadX = 1.5f;

        private Vector3 _velocity;
        private PlayerController25D _player;

        public void SetTarget(Transform t)
        {
            target = t;
            _player = t != null ? t.GetComponent<PlayerController25D>() : null;
        }

        private void Start()
        {
            if (target == null)
            {
                var p = FindFirstObjectByType<PlayerController25D>();
                if (p != null) SetTarget(p.transform);
            }
            else SetTarget(target);

            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            if (target != null) transform.position = target.position + offset;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float look = _player != null ? _player.Facing * lookAheadX : 0f;
            Vector3 desired = target.position + offset + Vector3.right * look;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
