using System;
using System.Collections;
using UnityEngine;

namespace AnimalsVsMonsters
{
    /// <summary>Simple hit-point component with knockback, hit flash and death.</summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private float invulnerableTime = 0.5f;
        [SerializeField] private float knockbackForce = 5f;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private SpriteRenderer flashRenderer;

        public int Current { get; private set; }
        public int Max => maxHealth;
        public bool IsDead => Current <= 0;

        public event Action<int, int> OnHealthChanged; // current, max
        public event Action OnDied;

        private float _invulnUntil;

        private void Awake()
        {
            Current = maxHealth;
            if (flashRenderer == null) flashRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void TakeDamage(int amount, Vector3 sourcePosition)
        {
            if (IsDead || Time.time < _invulnUntil) return;
            _invulnUntil = Time.time + invulnerableTime;

            Current = Mathf.Max(0, Current - amount);
            OnHealthChanged?.Invoke(Current, maxHealth);

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 dir = transform.position - sourcePosition;
                dir.y = 0f;
                rb.AddForce((dir.normalized + Vector3.up * 0.5f) * knockbackForce, ForceMode.VelocityChange);
            }

            if (flashRenderer != null) StartCoroutine(Flash());

            if (IsDead)
            {
                OnDied?.Invoke();
                if (destroyOnDeath) Destroy(gameObject, 0.1f);
            }
        }

        public void Heal(int amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            OnHealthChanged?.Invoke(Current, maxHealth);
        }

        private IEnumerator Flash()
        {
            Color original = flashRenderer.color;
            flashRenderer.color = new Color(1f, 0.3f, 0.3f, original.a);
            yield return new WaitForSeconds(0.1f);
            if (flashRenderer != null) flashRenderer.color = original;
        }
    }
}
