using UnityEngine;

namespace UI
{
    public class QuitManager : MonoBehaviour
    {
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            // WebGL에서는 게임을 종료할 수 없음
            // 대신 메인 메뉴로 돌아가거나 페이지 새로고침
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
#else
            Application.Quit();
#endif
        }
    }
}
