using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public class ButtonSceneLoader : MonoBehaviour
    {
        [SerializeField] private string sceneName; // Object 대신 string 사용
    
        void Start()
        {
            GetComponent<Button>().onClick.AddListener(() => {
                if (!string.IsNullOrEmpty(sceneName))
                    SceneManager.LoadScene(sceneName);
            });
        }
    }
}