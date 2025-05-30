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
        
        public void ShowBattleEnd(bool isVictory)
        {
            battleEndPanel.SetActive(true);
            
            if (isVictory)
            {
                resultText.text = "Victory!";
                resultText.color = new Color(1f, 0.84f, 0f); // Gold
                subText.text = "The boss has been defeated!";
            }
            else
            {
                resultText.text = "Defeat";
                resultText.color = new Color(0.8f, 0.2f, 0.2f); // Red
                subText.text = "Your party has fallen...";
            }
        }
        
        public void OnReturnToMainMenu()
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}