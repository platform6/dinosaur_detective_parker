using UnityEngine;

namespace AnimalsVsMonsters
{
    /// <summary>Keeps a sprite facing the camera. Y-axis only by default so sprites stay upright.</summary>
    public class Billboard : MonoBehaviour
    {
        [SerializeField] private bool lockYAxis = true;
        private Transform _cam;

        private void LateUpdate()
        {
            if (_cam == null)
            {
                if (Camera.main == null) return;
                _cam = Camera.main.transform;
            }

            if (lockYAxis)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(_cam.forward, Vector3.up);
                if (fwd.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            else
            {
                transform.rotation = _cam.rotation;
            }
        }
    }
}
