using GameSystem;
using UnityEngine;

namespace Data
{
    public enum MoveCategory
    {
        Single,
        AOE,
        MultiRandom,
        Charge,
        Buff,
        Debuff,
        ClearOppBuff,
        ClearSelfDebuff,
        Stun
    }
    
    [CreateAssetMenu(fileName = "MoveData", menuName = "Scriptable Objects/MoveData")]
    public class MoveData : ScriptableObject
    {
        public string Name;
        public MoveCategory Category;
        public ElementType Type;
        
        public int Power;
        [SerializeField] private int _accuracy = 100;
        public float CriticalChance = 0.05f;

        public int Cooldown = 2;
        public int UsageLimit = 99;

        public float Accuracy
        {
            get => _accuracy / 100f;
            set => _accuracy = Mathf.Clamp(Mathf.RoundToInt(value), 0, 100);
        }
    }

    [System.Serializable]
    public class MoveInstance
    {
        public MoveData Data;
        public int CooldownLeft;
        public int UsageLeft;

        public MoveInstance(MoveData soData)
        {
            Data = soData;
            CooldownLeft = 0;
            UsageLeft = soData.UsageLimit;
        }
    }
}
