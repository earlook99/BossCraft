using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AI
{
    public class CustomBossDropdown : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Button toggleButton;
        [SerializeField] private TextMeshProUGUI toggleButtonText;
        [SerializeField] private GameObject dropdownPanel;
        [SerializeField] private Transform contentParent;
        [SerializeField] private GameObject historyItemPrefab;
        [SerializeField] private ScrollRect scrollRect;

        [Header("Settings")]
        [SerializeField] private string defaultText = "최근 보스 선택";

        private List<GameObject> historyItems = new List<GameObject>();
        private bool isOpen = false;
        private Action<BossHistoryData> onBossSelected;

        public event Action<BossHistoryData> OnBossSelected;

        private void Awake()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(ToggleDropdown);
            }

            if (dropdownPanel != null)
            {
                dropdownPanel.SetActive(false);
            }

            UpdateToggleButtonText();
        }

        private void Start()
        {
            Canvas.ForceUpdateCanvases();
        }

        private void Update()
        {
            if (isOpen && Input.GetMouseButtonDown(0))
            {
                if (!IsPointerOverDropdown())
                {
                    CloseDropdown();
                }
            }
        }

        public void SetOnBossSelectedCallback(Action<BossHistoryData> callback)
        {
            onBossSelected = callback;
        }

        public void UpdateHistory()
        {
            Debug.Log("[CustomBossDropdown] UpdateHistory called");
            
            ClearHistoryItems();

            if (BossContainer.Instance == null) 
            {
                Debug.LogError("[CustomBossDropdown] BossContainer.Instance is null!");
                gameObject.SetActive(false);
                return;
            }

            var history = BossContainer.Instance.GetBossHistory();
            Debug.Log($"[CustomBossDropdown] History count: {history.Count}");
            
            if (history.Count == 0)
            {
                Debug.Log("[CustomBossDropdown] No history found, hiding dropdown");
                gameObject.SetActive(false);
                return;
            }

            Debug.Log("[CustomBossDropdown] Showing dropdown with history");
            gameObject.SetActive(true);
            CreateHistoryItems(history);
        }

        private void ClearHistoryItems()
        {
            foreach (var item in historyItems)
            {
                if (item != null)
                {
                    UnityEngine.Object.Destroy(item);
                }
            }
            historyItems.Clear();
        }

        private void CreateHistoryItems(List<BossHistoryData> history)
        {
            if (historyItemPrefab == null || contentParent == null) 
            {
                Debug.LogError("[CustomBossDropdown] historyItemPrefab or contentParent is null!");
                return;
            }

            Debug.Log($"[CustomBossDropdown] Content Parent: {contentParent.name}, Is Persistent: {contentParent.gameObject.scene.name == "DontDestroyOnLoad"}");

            for (int i = 0; i < history.Count; i++)
            {
                var historyData = history[i];
                GameObject itemObj = Instantiate(historyItemPrefab);
                itemObj.transform.SetParent(contentParent, false);
                
                var historyItem = itemObj.GetComponent<BossHistoryItem>();
                if (historyItem != null)
                {
                    historyItem.Setup(historyData, i, OnHistoryItemSelected);
                }

                historyItems.Add(itemObj);
            }

            // 스크롤 설정을 위한 강제 업데이트
            StartCoroutine(FixScrollAfterDelay());
        }

        private System.Collections.IEnumerator FixScrollAfterDelay()
        {
            // 한 프레임 기다린 후 강제 업데이트
            yield return null;
            
            Canvas.ForceUpdateCanvases();
            
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
                
                // Content Size 강제 재계산
                var contentSizeFitter = contentParent.GetComponent<ContentSizeFitter>();
                if (contentSizeFitter != null)
                {
                    contentSizeFitter.enabled = false;
                    contentSizeFitter.enabled = true;
                }
                
                // 레이아웃 강제 재계산
                var layoutGroup = contentParent.GetComponent<VerticalLayoutGroup>();
                if (layoutGroup != null)
                {
                    layoutGroup.enabled = false;
                    layoutGroup.enabled = true;
                }
            }
            
            // 한 번 더 업데이트
            yield return null;
            Canvas.ForceUpdateCanvases();
            
            Debug.Log($"[CustomBossDropdown] Content size after fix: {contentParent.GetComponent<RectTransform>().sizeDelta}");
        }

        private void OnHistoryItemSelected(BossHistoryData selectedBoss)
        {
            CloseDropdown();
            onBossSelected?.Invoke(selectedBoss);
            OnBossSelected?.Invoke(selectedBoss);
        }

        private void ToggleDropdown()
        {
            if (isOpen)
            {
                CloseDropdown();
            }
            else
            {
                OpenDropdown();
            }
        }

        private void OpenDropdown()
        {
            if (dropdownPanel != null)
            {
                dropdownPanel.SetActive(true);
                isOpen = true;
                
                Canvas.ForceUpdateCanvases();
                if (scrollRect != null)
                {
                    scrollRect.verticalNormalizedPosition = 1f;
                }
            }
        }

        private void CloseDropdown()
        {
            if (dropdownPanel != null)
            {
                dropdownPanel.SetActive(false);
                isOpen = false;
            }
        }

        private bool IsPointerOverDropdown()
        {
            if (!isOpen || dropdownPanel == null) return false;

            Vector2 mousePosition = Input.mousePosition;
            
            RectTransform toggleRect = toggleButton?.GetComponent<RectTransform>();
            if (toggleRect != null && RectTransformUtility.RectangleContainsScreenPoint(toggleRect, mousePosition))
            {
                return true;
            }

            RectTransform panelRect = dropdownPanel.GetComponent<RectTransform>();
            if (panelRect != null && RectTransformUtility.RectangleContainsScreenPoint(panelRect, mousePosition))
            {
                return true;
            }

            return false;
        }

        private void UpdateToggleButtonText()
        {
            if (toggleButtonText != null)
            {
                toggleButtonText.text = defaultText;
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (toggleButton != null)
            {
                toggleButton.interactable = interactable;
            }
        }

        private void OnDisable()
        {
            CloseDropdown();
        }
    }
}