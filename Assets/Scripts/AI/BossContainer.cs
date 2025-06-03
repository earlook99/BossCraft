using GameSystem;
using UnityEngine;

namespace AI
{
    public class BossContainer : MonoBehaviour
    {
        private static BossContainer _instance;
        public static BossContainer Instance => _instance;

        public byte[] CurrentBossImageData;
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