using System.Collections.Generic;
using UnityEngine;

namespace GameSystem
{
    public enum ActionType
    {
        Move,
        Item
        // etc...
    }

    public enum EntityType
    {
        Character1,
        Character2,
        Character3,
        Character4,
        Boss
    }
    
    public struct ActionData
    {
        public ActionType Action { get; private set; }
        public int ActionIndex { get; private set; }
        public EntityType Source { get; private set; }
        public EntityType Target { get; private set; }
    }
    public class UIManager : MonoBehaviour
    {
        private List<ActionData> _playerChoices = new List<ActionData>();
        private int _currentPlayerIndex;
        
        // TODO: Have to initialize PlayerChoice's Target to Boss when player's case

        // public event System.Action<List<PlayerChoice> OnAllChoicesComplete;
    }
}
