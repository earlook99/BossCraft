using System;
using System.Collections;
using System.Collections.Generic;
using Entity;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameSystem
{
    /// <summary>
    /// Manages the user interface for the battle system.
    /// This includes displaying character statuses, action menus, and battle messages.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleManager _battleManager;

        // ---- (Scene-placed) Boss Area, Party Area
        [Header("UI Containers")]
        [SerializeField] private RectTransform _bossArea; // Parent transform for boss status UI
        [SerializeField] private RectTransform _partyArea; // Parent transform for player party status UIs

        // ---- (Scene-placed) Panel (GameObject) + ActionMenuUI attached to it
        [Header("Action Menu")]
        [SerializeField] private GameObject _actionMenuPanel; // The panel GameObject holding the action menu
        [SerializeField] private ActionMenuUI _actionMenuUI; // The script component for the action menu

        [Header("Other UI")]
        [SerializeField] private GameObject _battleInfo; // GameObject used to display battle messages. Battle messages, etc.

        // ---- (Reference to Prefab made in Project) ----
        [Header("Prefabs")]
        [SerializeField] private CharacterStatusUI _characterStatusPrefab; // Prefab for character status UI elements

        private Dictionary<int, CharacterStatusUI> _entityStatusUIs = new Dictionary<int, CharacterStatusUI>(); // Cache for instantiated status UIs
        private BattleEntity[] _battleEntities; // Cache for battle entities
        private TextMeshProUGUI _battleMessageTextComponent; // Cached TextMeshProUGUI component for battle messages

        private int _currentPlayerIndex; // Index of the player character whose turn it is to act

        /// <summary>
        /// Called when the script instance is being loaded.
        /// Ensures BattleManager reference and initializes UI components.
        /// </summary>
        private void Awake()
        {
            if (_battleManager == null)
            {
                _battleManager = FindAnyObjectByType<BattleManager>();
            }
            // Deactivate action menu initially
            if (_actionMenuPanel != null)
            {
                _actionMenuPanel.SetActive(false);
            }

            if (_battleInfo != null) 
            { 
                _battleMessageTextComponent = _battleInfo.GetComponentInChildren<TextMeshProUGUI>(); 
            }
            
            // Initial setup for battle UI
            SetupBattleUI();
        }

        /// <summary>
        /// Called before the first frame update.
        /// Initializes the current player index.
        /// </summary>
        private void Start()
        {
            _currentPlayerIndex = 0;
        }

        /// <summary>
        /// Sets up the entire battle UI, including cleaning up existing elements and creating new ones.
        /// </summary>
        public void SetupBattleUI()
        {
            CleanupExistingUI();

            // Get entity array from BattleManager
            _battleEntities = _battleManager.Entities;
            
            // Create Boss UI
            CreateBossStatusUI();
            // Create Player Party UI
            CreatePartyStatusUIs();
        }

        /// <summary>
        /// Cleans up any existing character status UI elements.
        /// </summary>
        private void CleanupExistingUI()
        {
            foreach (var kv in _entityStatusUIs)
            {
                if (kv.Value != null && kv.Value.gameObject != null)
                {
                    Destroy(kv.Value.gameObject);
                }
            }
            _entityStatusUIs.Clear();
        }

        /// <summary>
        /// Creates and sets up the status UI for the boss entity.
        /// </summary>
        private void CreateBossStatusUI()
        {
            var bossEntity = _battleEntities[(int)EntityType.Boss];
            if (bossEntity == null || _bossArea == null || _characterStatusPrefab == null)
            {
                return;
            }

            // Instantiate Prefab -> Attach to Boss Area
            var statusUI = Instantiate(_characterStatusPrefab, _bossArea);
            statusUI.Setup(bossEntity, (int)EntityType.Boss);

            _entityStatusUIs.Add((int)EntityType.Boss, statusUI);
        }

        /// <summary>
        /// Creates and sets up the status UIs for all player characters in the party.
        /// </summary>
        private void CreatePartyStatusUIs()
        {
            if (_partyArea == null || _characterStatusPrefab == null)
            {
                return;
            }

            // 4 characters (Character1~4)
            for (int i = 0; i < 4; i++)
            {
                var entity = _battleEntities[i];
                if (entity == null) continue;

                var statusUI = Instantiate(_characterStatusPrefab, _partyArea);
                statusUI.Setup(entity, i);

                _entityStatusUIs.Add(i, statusUI);
            }
        }
        
        /// <summary>
        /// Shows the action menu for the specified player.
        /// </summary>
        /// <param name="currentPlayerIndex">The index of the player whose turn it is.</param>
        public void ShowActionMenuForCurrentPlayer(int currentPlayerIndex)
        {
            _currentPlayerIndex = currentPlayerIndex;
            
            if (_actionMenuPanel)
            {
                _actionMenuPanel.SetActive(true);
            }
            
            if (_actionMenuUI)
            {
                var playerEntity = _battleEntities[_currentPlayerIndex];
                
                // turnCount, etc., are examples
                _actionMenuUI.Setup(this, playerEntity, _currentPlayerIndex);
            }
        }

        /// <summary>
        /// Called when an action is selected from the action menu.
        /// Constructs an <see cref="ActionData"/> object and passes it to the <see cref="BattleManager"/>.
        /// </summary>
        /// <param name="actionType">The type of action selected.</param>
        /// <param name="actionIndex">The specific index of the action (e.g., move index, item index).</param>
        public void OnActionSelect(ActionType actionType, int actionIndex)
        {
            var newAction = new ActionData(actionType, actionIndex, (EntityType)_currentPlayerIndex, EntityType.Boss);
            _battleManager.ReceivePlayerChoice(newAction);
        }

        /// <summary>
        /// Called when it's the boss's turn, to hide the player action menu.
        /// </summary>
        private void StartBossTurn()
        {
            // Hide player action menu
            if (_actionMenuPanel != null)
            {
                _actionMenuPanel.SetActive(false);
            }
        }

        // ---- Display battle message ----
        /// <summary>
        /// Displays a battle message on the UI for a specified duration.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="duration">How long the message should be visible, in seconds.</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
        public IEnumerator ShowBattleMessage(string message, float duration = 2f)
        {
            if (_battleMessageTextComponent != null)
            {
                _battleMessageTextComponent.text = message;
                if (_battleInfo != null) _battleInfo.SetActive(true); // Show the parent GameObject
                yield return new WaitForSeconds(duration);
                // if (_battleInfo != null) _battleInfo.SetActive(false); // Hide after duration
            }
        }
        
        /// <summary>
        /// Animates the HP bar of a specified entity decreasing from a start HP to an end HP.
        /// </summary>
        /// <param name="entityIndex">The index of the entity whose HP bar to animate.</param>
        /// <param name="startHP">The HP value to start the animation from.</param>
        /// <param name="endHP">The HP value to end the animation at.</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
        public IEnumerator AnimateHPBarUpdate(int entityIndex, int startHP, int endHP)
        {
            float duration = 1.0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                int currentHP = (int)Mathf.Lerp(startHP, endHP, t);
                _entityStatusUIs[entityIndex].UpdateHP(currentHP);
                yield return null;
            }

            _entityStatusUIs[entityIndex].UpdateHP(endHP);
        }

    }
}
