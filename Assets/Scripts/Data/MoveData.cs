using GameSystem;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "MoveData", menuName = "Scriptable Objects/MoveData")]
    public class MoveData : ScriptableObject
    {
        public string Name;
        public MoveCategory Category;
        public TargetSide AllowedTargetSide;
        public ElementType Type;
        public int Cooldown = 2;
        public bool RequiresCharge = false;
        public MoveEffect[] Effects;
        public GameObject VFXPrefab;
        
        [TextArea(2, 4)]
        public string Description;
    }

    [System.Serializable]
    public class MoveInstance
    {
        public MoveData Data;
        public int CooldownLeft;

        public MoveInstance(MoveData soData)
        {
            Data = soData;
            CooldownLeft = 0;
        }
    }
}