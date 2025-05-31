using UnityEngine;

namespace CameraSystem
{
    public class BillboardSprite : MonoBehaviour
    {
        private Transform _camTransform;

        private void Start()
        {
            if (UnityEngine.Camera.main)
            {
                _camTransform = UnityEngine.Camera.main.transform;
            }
        }

        private void LateUpdate()
        {
            transform.forward = _camTransform.forward;
        }
    }
}
