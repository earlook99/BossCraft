using GameSystem;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// ScriptableObject representing the static data for a battle move/skill.
    /// Can hold multiple effects (Damage, Buff, etc.)
    /// and also define how it targets (Single, AOE) and whom it targets (Allies, Enemies, etc.).
    /// </summary>
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
    }

    /// <summary>
    /// Represents an instance of a move during a battle, including cooldown usage, etc.
    /// This does NOT store target info; that should go in ActionData or similar runtime logic.
    /// </summary>
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
