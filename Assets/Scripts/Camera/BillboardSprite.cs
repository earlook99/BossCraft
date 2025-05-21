using UnityEngine;

namespace Camera
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
            // transform.LookAt(transform.position + camTransform.forward, camTransform.up);
            
            transform.forward = _camTransform.forward;
        }
    }
}
