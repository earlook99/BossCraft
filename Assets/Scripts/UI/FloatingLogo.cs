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
        
        private float cachedTime;
        private float cachedFloatSin;
        private float cachedRotationSin;
        private float cachedScaleSin;
        
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
        }
        
        private void Update()
        {
            cachedTime = Time.time;
            
            cachedFloatSin = Mathf.Sin(cachedTime * floatSpeed);
            ApplyFloating();
            
            if (enableRotation)
            {
                cachedRotationSin = Mathf.Sin(cachedTime * rotationSpeed);
                ApplyRotation();
            }
            
            if (enableScale)
            {
                cachedScaleSin = Mathf.Sin(cachedTime * scaleSpeed);
                ApplyScaling();
            }
        }

        private void ApplyFloating()
        {
            float yOffset = cachedFloatSin * floatAmplitude;
            rectTransform.anchoredPosition = new Vector3(originalPosition.x, originalPosition.y + yOffset, originalPosition.z);
        }

        private void ApplyRotation()
        {
            float rotationOffset = cachedRotationSin * rotationAmplitude;
            rectTransform.localEulerAngles = new Vector3(originalRotation.x, originalRotation.y, originalRotation.z + rotationOffset);
        }

        private void ApplyScaling()
        {
            float scaleOffset = 1f + cachedScaleSin * scaleAmplitude;
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