using UnityEngine;

namespace UI
{
    /// <summary>
    /// UI 요소에 부드러운 플로팅(떠다니는) 애니메이션을 적용하는 컴포넌트
    /// </summary>
    public class FloatingLogo : MonoBehaviour
    {
        [Header("Float Settings")]
        [SerializeField] private float floatAmplitude = 10f; // 상하 움직임 폭
        [SerializeField] private float floatSpeed = 1f; // 움직임 속도
        
        [Header("Rotation Settings")]
        [SerializeField] private bool enableRotation = true; // 회전 효과 활성화
        [SerializeField] private float rotationAmplitude = 5f; // 회전 각도 폭
        [SerializeField] private float rotationSpeed = 0.8f; // 회전 속도
        
        [Header("Scale Settings")]
        [SerializeField] private bool enableScale = false; // 크기 변화 활성화
        [SerializeField] private float scaleAmplitude = 0.1f; // 크기 변화 폭
        [SerializeField] private float scaleSpeed = 0.6f; // 크기 변화 속도

        private RectTransform rectTransform;
        private Vector3 originalPosition;
        private Vector3 originalRotation;
        private Vector3 originalScale;
        
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            
            // 원래 위치, 회전, 크기 저장
            originalPosition = rectTransform.anchoredPosition;
            originalRotation = rectTransform.localEulerAngles;
            originalScale = rectTransform.localScale;
        }
        
        private void Update()
        {
            float time = Time.time;
            
            // 상하 플로팅
            float yOffset = Mathf.Sin(time * floatSpeed) * floatAmplitude;
            rectTransform.anchoredPosition = originalPosition + new Vector3(0, yOffset, 0);
            
            // 회전 효과
            if (enableRotation)
            {
                float rotationOffset = Mathf.Sin(time * rotationSpeed) * rotationAmplitude;
                rectTransform.localEulerAngles = originalRotation + new Vector3(0, 0, rotationOffset);
            }
            
            // 크기 변화 효과
            if (enableScale)
            {
                float scaleOffset = 1f + Mathf.Sin(time * scaleSpeed) * scaleAmplitude;
                rectTransform.localScale = originalScale * scaleOffset;
            }
        }
        
        /// <summary>
        /// 애니메이션 설정을 런타임에 변경
        /// </summary>
        public void SetFloatSettings(float amplitude, float speed)
        {
            floatAmplitude = amplitude;
            floatSpeed = speed;
        }
        
        /// <summary>
        /// 애니메이션을 원래 위치로 리셋
        /// </summary>
        public void ResetToOriginal()
        {
            rectTransform.anchoredPosition = originalPosition;
            rectTransform.localEulerAngles = originalRotation;
            rectTransform.localScale = originalScale;
        }
    }
}