using UnityEngine;

namespace UI
{
    public class FloatingLogo : MonoBehaviour
    {
        [Header("Float Settings")]
        [SerializeField] private float floatAmplitude = 10f;
        [SerializeField] private float floatSpeed = 1f;
        
        [Header("Rotation Settings")]
        [SerializeField] private bool enableRotation = true;
        [SerializeField] private float rotationAmplitude = 5f;
        [SerializeField] private float rotationSpeed = 0.8f;
        
        [Header("Scale Settings")]
        [SerializeField] private bool enableScale = false;
        [SerializeField] private float scaleAmplitude = 0.1f;
        [SerializeField] private float scaleSpeed = 0.6f;

        private RectTransform rectTransform;
        private Vector3 originalPosition;
        private Vector3 originalRotation;
        private Vector3 originalScale;
        
        private const float UPDATE_INTERVAL = 0.1f;
        
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            
            originalPosition = rectTransform.anchoredPosition;
            originalRotation = rectTransform.localEulerAngles;
            originalScale = rectTransform.localScale;
        }
        
        private void OnEnable()
        {
            ResetToOriginal();
            InvokeRepeating(nameof(UpdateAnimation), 0f, UPDATE_INTERVAL);
        }
        
        private void OnDisable()
        {
            CancelInvoke(nameof(UpdateAnimation));
        }
        
        private void UpdateAnimation()
        {
            float time = Time.time;
            
            ApplyFloating(time);
            
            if (enableRotation)
            {
                ApplyRotation(time);
            }
            
            if (enableScale)
            {
                ApplyScaling(time);
            }
        }

        private void ApplyFloating(float time)
        {
            float yOffset = Mathf.Sin(time * floatSpeed) * floatAmplitude;
            rectTransform.anchoredPosition = new Vector3(originalPosition.x, originalPosition.y + yOffset, originalPosition.z);
        }

        private void ApplyRotation(float time)
        {
            float rotationOffset = Mathf.Sin(time * rotationSpeed) * rotationAmplitude;
            rectTransform.localEulerAngles = new Vector3(originalRotation.x, originalRotation.y, originalRotation.z + rotationOffset);
        }

        private void ApplyScaling(float time)
        {
            float scaleOffset = 1f + Mathf.Sin(time * scaleSpeed) * scaleAmplitude;
            rectTransform.localScale = originalScale * scaleOffset;
        }
        
        public void SetFloatSettings(float amplitude, float speed)
        {
            floatAmplitude = amplitude;
            floatSpeed = speed;
        }
        
        public void ResetToOriginal()
        {
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = originalPosition;
                rectTransform.localEulerAngles = originalRotation;
                rectTransform.localScale = originalScale;
            }
        }
    }
}