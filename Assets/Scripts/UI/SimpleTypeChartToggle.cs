using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class SimpleTypeChartToggle : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button toggleButton;      // 토글 버튼
        [SerializeField] private GameObject chartImage;    // 상성표 이미지
        
        private bool isShowing = false;
        
        private void Start()
        {
            // 시작할 때 차트는 숨김
            chartImage.SetActive(false);
            
            // 버튼 클릭 이벤트 연결
            toggleButton.onClick.AddListener(ToggleChart);
        }
        
        private void ToggleChart()
        {
            isShowing = !isShowing;
            chartImage.SetActive(isShowing);
        }
        
        // 다른 곳 클릭하면 닫히게 하고 싶다면 이 메서드 추가
        private void Update()
        {
            if (isShowing && Input.GetMouseButtonDown(0))
            {
                // 버튼이나 차트 영역이 아닌 곳을 클릭했는지 체크
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        toggleButton.GetComponent<RectTransform>(), 
                        Input.mousePosition) &&
                    !RectTransformUtility.RectangleContainsScreenPoint(
                        chartImage.GetComponent<RectTransform>(), 
                        Input.mousePosition))
                {
                    isShowing = false;
                    chartImage.SetActive(false);
                }
            }
        }
    }
}