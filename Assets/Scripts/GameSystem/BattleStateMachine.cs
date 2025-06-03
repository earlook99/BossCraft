using System;
using UnityEngine;
using GameSystem.Events;

namespace GameSystem
{
    public enum BattleState
    {
        Initializing,
        PlayerChoice,
        BossAction,
        ExecutingAction,
        CheckBattleEnd,
        BattleEnded
    }

    public class BattleStateMachine : MonoBehaviour
    {
        private BattleState _currentState;
        private BattleState _previousState;
        
        public BattleState CurrentState => _currentState;
        public BattleState PreviousState => _previousState;
        
        public event Action<BattleState, BattleState> OnStateChanged;
        
        private void Start()
        {
            _currentState = BattleState.Initializing;
            _previousState = BattleState.Initializing;
        }
        
        public void TransitionTo(BattleState newState)
        {
            if (_currentState == newState)
                return;
                
            _previousState = _currentState;
            _currentState = newState;
            
            Debug.Log($"Battle State: {_previousState} -> {_currentState}");
            
            OnStateChanged?.Invoke(_previousState, _currentState);
            
            ExecuteStateLogic(newState);
        }
        
        private void ExecuteStateLogic(BattleState state)
        {
            switch (state)
            {
                case BattleState.Initializing:
                    HandleInitializing();
                    break;
                case BattleState.PlayerChoice:
                    HandlePlayerChoice();
                    break;
                case BattleState.BossAction:
                    HandleBossAction();
                    break;
                case BattleState.ExecutingAction:
                    break;
                case BattleState.CheckBattleEnd:
                    HandleCheckBattleEnd();
                    break;
                case BattleState.BattleEnded:
                    HandleBattleEnded();
                    break;
            }
        }
        
        private void HandleInitializing()
        {
            TransitionTo(BattleState.PlayerChoice);
        }
        
        private void HandlePlayerChoice()
        {
        }
        
        private void HandleBossAction()
        {
        }
        
        private void HandleCheckBattleEnd()
        {
        }
        
        private void HandleBattleEnded()
        {
        }
        
        public bool CanTransitionTo(BattleState targetState)
        {
            switch (_currentState)
            {
                case BattleState.Initializing:
                    return targetState == BattleState.PlayerChoice;
                    
                case BattleState.PlayerChoice:
                    return targetState == BattleState.ExecutingAction || 
                           targetState == BattleState.BossAction ||
                           targetState == BattleState.CheckBattleEnd;
                           
                case BattleState.BossAction:
                    return targetState == BattleState.ExecutingAction ||
                           targetState == BattleState.CheckBattleEnd;
                           
                case BattleState.ExecutingAction:
                    return targetState == BattleState.PlayerChoice ||
                           targetState == BattleState.BossAction ||
                           targetState == BattleState.CheckBattleEnd;
                           
                case BattleState.CheckBattleEnd:
                    return targetState == BattleState.PlayerChoice ||
                           targetState == BattleState.BattleEnded;
                           
                case BattleState.BattleEnded:
                    return false;
                    
                default:
                    return false;
            }
        }
    }
}