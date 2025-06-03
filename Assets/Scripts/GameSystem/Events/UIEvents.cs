using System;
using Entity;
using Data;
using UI;

namespace GameSystem.Events
{
    public static class UIEvents
    {
        public static event Action<ActionSelectedEventArgs> OnActionSelected;
        public static event Action<TargetSelectedEventArgs> OnTargetSelected;
        public static event Action OnTargetSelectionCancelled;
        
        public static event Action<ShowMessageEventArgs> OnShowMessage;
        public static event Action<UpdateHPBarEventArgs> OnUpdateHPBar;
        public static event Action<UpdateShieldEventArgs> OnUpdateShield;
        
        public static event Action<MenuStateChangedEventArgs> OnMenuStateChanged;

        public static void RaiseActionSelected(ActionType actionType, int actionIndex, int playerIndex)
        {
            OnActionSelected?.Invoke(new ActionSelectedEventArgs 
            { 
                ActionType = actionType, 
                ActionIndex = actionIndex,
                PlayerIndex = playerIndex
            });
        }

        public static void RaiseTargetSelected(EntityType target)
        {
            OnTargetSelected?.Invoke(new TargetSelectedEventArgs { Target = target });
        }

        public static void RaiseTargetSelectionCancelled()
        {
            OnTargetSelectionCancelled?.Invoke();
        }

        public static void RaiseShowMessage(string message, float duration)
        {
            OnShowMessage?.Invoke(new ShowMessageEventArgs 
            { 
                Message = message, 
                Duration = duration 
            });
        }

        public static void RaiseUpdateHPBar(int entityIndex, int currentHP, int maxHP)
        {
            OnUpdateHPBar?.Invoke(new UpdateHPBarEventArgs 
            { 
                EntityIndex = entityIndex, 
                CurrentHP = currentHP, 
                MaxHP = maxHP 
            });
        }

        public static void RaiseUpdateShield(BossEntity boss)
        {
            OnUpdateShield?.Invoke(new UpdateShieldEventArgs { Boss = boss });
        }

        public static void RaiseMenuStateChanged(MenuState newState, int playerIndex)
        {
            OnMenuStateChanged?.Invoke(new MenuStateChangedEventArgs 
            { 
                NewState = newState,
                PlayerIndex = playerIndex
            });
        }

        public static void ClearAllListeners()
        {
            OnActionSelected = null;
            OnTargetSelected = null;
            OnTargetSelectionCancelled = null;
            OnShowMessage = null;
            OnUpdateHPBar = null;
            OnUpdateShield = null;
            OnMenuStateChanged = null;
        }
    }

    public class ActionSelectedEventArgs : EventArgs
    {
        public ActionType ActionType { get; set; }
        public int ActionIndex { get; set; }
        public int PlayerIndex { get; set; }
    }

    public class TargetSelectedEventArgs : EventArgs
    {
        public EntityType Target { get; set; }
    }

    public class ShowMessageEventArgs : EventArgs
    {
        public string Message { get; set; }
        public float Duration { get; set; }
    }

    public class UpdateHPBarEventArgs : EventArgs
    {
        public int EntityIndex { get; set; }
        public int CurrentHP { get; set; }
        public int MaxHP { get; set; }
    }

    public class UpdateShieldEventArgs : EventArgs
    {
        public BossEntity Boss { get; set; }
    }

    public class MenuStateChangedEventArgs : EventArgs
    {
        public MenuState NewState { get; set; }
        public int PlayerIndex { get; set; }
    }
}