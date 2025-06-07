using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameSystem;

namespace AI
{
    public class BossHistoryItem : MonoBehaviour
    {
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private TextMeshProUGUI infoText;
        [SerializeField] private Button selectButton;

        private BossHistoryData bossData;
        private int historyIndex;
        private Action<BossHistoryData> onSelected;
        private Texture2D thumbnailTexture;

        private void Awake()
        {
            if (selectButton == null)
            {
                selectButton = GetComponent<Button>();
            }
            
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnSelectClicked);
            }

            // 고정 크기 설정 - 늘어나지 않게
            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = gameObject.AddComponent<LayoutElement>();
            }
            
            layoutElement.preferredWidth = 300f;  // 여백 포함 크기
            layoutElement.preferredHeight = 360f; // 512 + 텍스트 공간
            layoutElement.flexibleWidth = 0f;     // 늘어나지 않음
            layoutElement.flexibleHeight = 0f;    // 늘어나지 않음
        }

        public void Setup(BossHistoryData data, int index, Action<BossHistoryData> onSelected)
        {
            this.bossData = data;
            this.historyIndex = index;
            this.onSelected = onSelected;

            Debug.Log($"[BossHistoryItem] Setup - thumbnailImage: {(thumbnailImage != null ? "OK" : "NULL")}, infoText: {(infoText != null ? "OK" : "NULL")}");

            UpdateThumbnail();
            UpdateInfoText();
        }

        private void UpdateThumbnail()
        {
            if (thumbnailImage == null || bossData == null) 
            {
                Debug.LogError("[BossHistoryItem] thumbnailImage or bossData is null!");
                return;
            }

            if (thumbnailTexture != null)
            {
                UnityEngine.Object.Destroy(thumbnailTexture);
                thumbnailTexture = null;
            }

            thumbnailTexture = bossData.GetThumbnailTexture();
            
            if (thumbnailTexture != null)
            {
                // 고품질 설정
                thumbnailTexture.filterMode = FilterMode.Trilinear;
                thumbnailTexture.anisoLevel = 9;
                
                Debug.Log($"[BossHistoryItem] Thumbnail loaded: {thumbnailTexture.width}x{thumbnailTexture.height}");
                
                Sprite thumbnailSprite = Sprite.Create(
                    thumbnailTexture,
                    new Rect(0, 0, thumbnailTexture.width, thumbnailTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f // PixelsPerUnit을 100으로 설정 (더 선명함)
                );
                thumbnailImage.sprite = thumbnailSprite;
            }
            else
            {
                Debug.LogError("[BossHistoryItem] Failed to get thumbnail texture!");
                thumbnailImage.sprite = null;
            }
        }

        private void UpdateInfoText()
        {
            if (infoText == null || bossData == null) return;

            DateTime timestamp = DateTime.FromBinary(bossData.timestamp);
            string timeAgo = GetTimeAgoString(timestamp);
            string text = $"보스 #{historyIndex + 1}\n({timeAgo})";
            
            infoText.text = text;
        }

        private string GetTimeAgoString(DateTime timestamp)
        {
            var timeSpan = DateTime.Now - timestamp;
            
            if (timeSpan.TotalMinutes < 1)
                return "방금 전";
            else if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}분 전";
            else if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}시간 전";
            else
                return $"{(int)timeSpan.TotalDays}일 전";
        }

        private void OnSelectClicked()
        {
            onSelected?.Invoke(bossData);
        }

        private void OnDestroy()
        {
            if (thumbnailTexture != null)
            {
                UnityEngine.Object.Destroy(thumbnailTexture);
            }
        }
    }
}