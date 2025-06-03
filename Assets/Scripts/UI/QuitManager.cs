using UnityEngine;

namespace UI
{
    public class QuitManager : MonoBehaviour
    {
        private const string MAIN_MENU_SCENE = "MainMenu";

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            UnityEngine.SceneManagement.SceneManager.LoadScene(MAIN_MENU_SCENE);
#else
            Application.Quit();
#endif
        }
    }
}