using UnityEngine;
using GameSystem;

namespace Data
{
    [CreateAssetMenu(fileName = "UITheme", menuName = "Scriptable Objects/UITheme")]
    public class UITheme : ScriptableObject
    {
        [Header("Action Button Sprites")]
        public Sprite fightSprite;
        public Sprite itemSprite;
        public Sprite guardSprite;
        public Sprite tauntSprite;
        
        [Header("Element Button Sprites")]
        public Sprite blazeSprite;
        public Sprite tideSprite;
        public Sprite mysticSprite;
        public Sprite terraSprite;
        public Sprite natureSprite;
        public Sprite darkSprite;
        public Sprite lightSprite;
        public Sprite stormSprite;
        
        [Header("Default Sprites")]
        public Sprite defaultButtonSprite;
        
        public Sprite GetActionSprite(ActionType actionType)
        {
            switch (actionType)
            {
                case ActionType.Move:
                    return fightSprite;
                case ActionType.Item:
                    return itemSprite;
                case ActionType.Guard:
                    return guardSprite;
                case ActionType.Taunt:
                    return tauntSprite;
                default:
                    return defaultButtonSprite;
            }
        }
        
        public Sprite GetElementSprite(ElementType elementType)
        {
            switch (elementType)
            {
                case ElementType.Blaze:
                    return blazeSprite;
                case ElementType.Tide:
                    return tideSprite;
                case ElementType.Mystic:
                    return mysticSprite;
                case ElementType.Terra:
                    return terraSprite;
                case ElementType.Nature:
                    return natureSprite;
                case ElementType.Dark:
                    return darkSprite;
                case ElementType.Light:
                    return lightSprite;
                case ElementType.Storm:
                    return stormSprite;
                default:
                    return defaultButtonSprite;
            }
        }
    }
}