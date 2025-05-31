using UnityEngine;

namespace Data
{
    [System.Serializable]
    public class ShieldTrigger
    {
        [Header("Trigger Condition")]
        [Range(0f, 1f)]
        public float HPThreshold = 0.5f;
        
        [Header("Shield Settings")]
        [Range(0.05f, 0.5f)]
        public float HPConversionRatio = 0.2f;
        
        [Range(1, 5)]
        public int StackCount = 3;
        
        [HideInInspector]
        public bool HasBeenUsed = false;
    }
    
    [CreateAssetMenu(fileName = "ShieldPattern", menuName = "Scriptable Objects/ShieldPattern")]
    public class ShieldPattern : ScriptableObject
    {
        [Header("Shield Configuration")]
        [Range(2f, 6f)]
        public float DefenseMultiplier = 4f;
        
        [Range(0.1f, 0.5f)]
        public float MinimumHPRatio = 0.2f;
        
        [Header("Shield Triggers")]
        public ShieldTrigger[] Triggers = new ShieldTrigger[]
        {
            new ShieldTrigger { HPThreshold = 0.8f, HPConversionRatio = 0.25f, StackCount = 3 },
            new ShieldTrigger { HPThreshold = 0.5f, HPConversionRatio = 0.20f, StackCount = 3 },
            new ShieldTrigger { HPThreshold = 0.3f, HPConversionRatio = 0.15f, StackCount = 2 }
        };
        
        public void ResetTriggers()
        {
            foreach (var trigger in Triggers)
            {
                trigger.HasBeenUsed = false;
            }
        }
    }
}