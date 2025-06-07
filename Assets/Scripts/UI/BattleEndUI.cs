using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UI
{
    public class BattleEndUI : MonoBehaviour
    {
        [SerializeField] private GameObject battleEndPanel;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI subText;
        [SerializeField] private GameObject contentPanel; // 버튼과 텍스트가 있는 내부 패널

        private readonly Color VICTORY_COLOR = new Color(1f, 0.84f, 0f);
        private readonly Color DEFEAT_COLOR = new Color(0.8f, 0.2f, 0.2f);
        private const string MAIN_MENU_SCENE = "MainMenu";
        
        private bool isEscapeMode = false;
        private GameSystem.UI.BattleUIController battleUIController;
        
        private void Awake()
        {
            // Ensure the panel is hidden at start
            if (battleEndPanel != null)
            {
                battleEndPanel.SetActive(false);
                
                // Add click handler to background
                Button bgButton = battleEndPanel.GetComponent<Button>();
                if (bgButton == null)
                {
                    bgButton = battleEndPanel.AddComponent<Button>();
                }
                bgButton.transition = Selectable.Transition.None;
                bgButton.onClick.RemoveAllListeners();
                bgButton.onClick.AddListener(OnBackgroundClicked);
            }
            
            battleUIController = FindAnyObjectByType<GameSystem.UI.BattleUIController>();
        }
        
        public void ShowBattleEnd(bool isVictory)
        {
            Debug.Log($"[BattleEndUI] ShowBattleEnd called. IsVictory: {isVictory}");
            
            if (battleEndPanel == null)
            {
                Debug.LogError("BattleEndPanel is not assigned in BattleEndUI!");
                return;
            }
            
            isEscapeMode = false;
            Debug.Log($"[BattleEndUI] Activating battle end panel");
            battleEndPanel.SetActive(true);
            
            if (isVictory)
            {
                ShowVictory();
            }
            else
            {
                ShowDefeat();
            }
        }
        
        public void ShowEscapeConfirmation()
        {
            if (battleEndPanel == null)
            {
                Debug.LogError("BattleEndPanel is not assigned in BattleEndUI!");
                return;
            }
            
            isEscapeMode = true;
            battleEndPanel.SetActive(true);
            
            if (resultText != null)
            {
                resultText.text = "Escape?";
                resultText.color = Color.white;
            }
            
            if (subText != null)
            {
                subText.text = "Do you really want to run away from battle?";
            }
        }

        private void ShowVictory()
        {
            if (resultText != null)
            {
                resultText.text = "Victory!";
                resultText.color = VICTORY_COLOR;
            }
            
            if (subText != null)
            {
                subText.text = "The boss has been defeated!";
            }
        }

        private void ShowDefeat()
        {
            if (resultText != null)
            {
                resultText.text = "Defeat";
                resultText.color = DEFEAT_COLOR;
            }
            
            if (subText != null)
            {
                subText.text = "Your party has fallen...";
            }
        }
        
        public void OnReturnToMainMenu()
        {
            SceneManager.LoadScene(MAIN_MENU_SCENE);
        }
        
        private void OnBackgroundClicked()
        {
            if (isEscapeMode)
            {
                CancelEscape();
            }
        }
        
        public void OnContentClicked()
        {
            // This prevents the background click from triggering
            // when clicking on the content panel
        }
        
        private void CancelEscape()
        {
            Debug.Log("[BattleEndUI] Escape cancelled");
            battleEndPanel.SetActive(false);
            isEscapeMode = false;
            
            // Return to action menu
            if (battleUIController != null)
            {
                battleUIController.ShowActionMenuForPlayer(battleUIController.GetCurrentPlayerIndex());
            }
        }
        
        private void Update()
        {
            // Alternative: ESC key to cancel
            if (isEscapeMode && Input.GetKeyDown(KeyCode.Escape))
            {
                CancelEscape();
            }
        }
    }
}