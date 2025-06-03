using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace UI
{
    public class BattleEndUI : MonoBehaviour
    {
        [SerializeField] private GameObject battleEndPanel;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI subText;

        private readonly Color VICTORY_COLOR = new Color(1f, 0.84f, 0f);
        private readonly Color DEFEAT_COLOR = new Color(0.8f, 0.2f, 0.2f);
        private const string MAIN_MENU_SCENE = "MainMenu";
        
        public void ShowBattleEnd(bool isVictory)
        {
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

        private void ShowVictory()
        {
            resultText.text = "Victory!";
            resultText.color = VICTORY_COLOR;
            subText.text = "The boss has been defeated!";
        }

        private void ShowDefeat()
        {
            resultText.text = "Defeat";
            resultText.color = DEFEAT_COLOR;
            subText.text = "Your party has fallen...";
        }
        
        public void OnReturnToMainMenu()
        {
            SceneManager.LoadScene(MAIN_MENU_SCENE);
        }
    }
}