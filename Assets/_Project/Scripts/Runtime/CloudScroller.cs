using UnityEngine;

namespace AnimalsVsMonsters
{
    /// <summary>Slowly drifts the sky backdrop texture sideways so the clouds move.</summary>
    [RequireComponent(typeof(Renderer))]
    public class CloudScroller : MonoBehaviour
    {
        [Tooltip("Texture widths per second. 0.004 = one full loop every ~4 minutes.")]
        [SerializeField] private float speed = 0.004f;

        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private float _offset;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            _offset = Mathf.Repeat(_offset + speed * Time.deltaTime, 1f);
            _renderer.GetPropertyBlock(_block);
            _block.SetVector(BaseMapST, new Vector4(1f, 1f, _offset, 0f));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
