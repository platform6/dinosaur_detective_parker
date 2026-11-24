using UnityEngine;

namespace AnimalsVsMonsters
{
    /// <summary>
    /// Keeps a blob shadow on the ground beneath its parent, shrinking and fading with height.
    /// Sells depth in 2.5D for jumping and floating characters.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class GroundShadow : MonoBehaviour
    {
        [SerializeField] private float fadeHeight = 3f;
        [SerializeField] private float minScale = 0.5f;
        [SerializeField] private float minAlpha = 0.15f;

        private SpriteRenderer _sr;
        private Vector3 _baseScale;
        private float _baseAlpha;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            _baseAlpha = _sr.color.a;
        }

        private void LateUpdate()
        {
            Transform owner = transform.parent;
            if (owner == null) return;

            if (!GroundUtil.TryGetGroundY(owner.position, out float groundY))
            {
                _sr.enabled = false;
                return;
            }

            _sr.enabled = true;
            float height = Mathf.Max(0f, owner.position.y - groundY);
            float t = Mathf.Clamp01(height / fadeHeight);

            Vector3 p = owner.position;
            transform.position = new Vector3(p.x, groundY + 0.02f, p.z);
            transform.localScale = _baseScale * Mathf.Lerp(1f, minScale, t);
            Color c = _sr.color;
            c.a = Mathf.Lerp(_baseAlpha, _baseAlpha * minAlpha, t);
            _sr.color = c;
        }
    }
}
