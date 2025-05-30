using GameSystem;
using UnityEngine;

namespace AI
{
    public class BossContainer : MonoBehaviour
    {
        private static BossContainer _instance;
        public static BossContainer Instance => _instance;

        // 싱글턴이 들고 있을 런타임 데이터
        public Sprite CurrentBossSprite;
        public string CurrentBossName;
        public ElementType CurrentBossType;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
