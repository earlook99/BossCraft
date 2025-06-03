using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class ButtonPopupLoader : MonoBehaviour
    {
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private Button popupCloseButton;

        void Start()
        {
            popupPanel.SetActive(false);

            GetComponent<Button>().onClick.AddListener(() =>
            {
                if (popupPanel != null)
                    popupPanel.SetActive(true);
            });

            popupCloseButton.onClick.AddListener(() =>
            {
                if (popupPanel != null)
                    popupPanel.SetActive(false);
            });
        }
    }
}