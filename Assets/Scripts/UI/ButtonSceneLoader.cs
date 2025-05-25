using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public class ButtonSceneLoader : MonoBehaviour
    {
        [SerializeField] private Object targetScene; // 씬 파일 드래그
    
        void Start()
        {
            GetComponent<Button>().onClick.AddListener(() => {
                if (targetScene != null)
                    SceneManager.LoadScene(targetScene.name);
            });
        }
    }
}