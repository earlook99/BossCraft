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
    public enum ActionType
    {
        Move,
        Item,
        Guard,
        Taunt
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
        public ActionType Action;
        public int ActionIndex;
        public EntityType Source;
        public EntityType Target;

        public ActionData(ActionType actionType, int actionIndex, EntityType source, EntityType target)
        {
            Action = actionType;
            ActionIndex = actionIndex;
            Source = source;
            Target = target;
        }
    }

    public class UIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleManager _battleManager;

        // ---- (씬에 직접 배치된) 보스 영역, 파티 영역
        [Header("UI Containers")]
        [SerializeField] private RectTransform _bossArea;
        [SerializeField] private RectTransform _partyArea;

        // ---- (씬에 직접 배치된) 패널(게임오브젝트) + 거기에 붙어있는 ActionMenuUI
        [Header("Action Menu")]
        [SerializeField] private GameObject _actionMenuPanel; 
        [SerializeField] private ActionMenuUI _actionMenuUI;

        [Header("Other UI")]
        [SerializeField] private GameObject _battleInfo; // 전투 메시지 등

        // ---- (Project에서 만든 Prefab 참조) ----
        [Header("Prefabs")]
        [SerializeField] private CharacterStatusUI _characterStatusPrefab; 

        private Dictionary<int, CharacterStatusUI> _entityStatusUIs = new Dictionary<int, CharacterStatusUI>();
        private BattleEntity[] _battleEntities;

        private int _currentPlayerIndex;

        // public event Action OnPlayerTurnEnd;

        private void Awake()
        {
            if (_battleManager == null)
            {
                _battleManager = FindAnyObjectByType<BattleManager>();
            }
            // 액션메뉴 초기 비활성화
            if (_actionMenuPanel != null)
            {
                _actionMenuPanel.SetActive(false);
            }
            
            // 전투 UI 초기 세팅
            SetupBattleUI();
        }

        private void Start()
        {
            _currentPlayerIndex = 0;
        }

        public void SetupBattleUI()
        {
            CleanupExistingUI();

            // BattleManager로부터 엔티티 배열 가져오기
            _battleEntities = _battleManager.Entities;
            
            // 보스 UI 생성
            CreateBossStatusUI();
            // 플레이어 파티 UI 생성
            CreatePartyStatusUIs();
        }

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

        private void CreateBossStatusUI()
        {
            var bossEntity = _battleEntities[(int)EntityType.Boss];
            if (bossEntity == null || _bossArea == null || _characterStatusPrefab == null)
            {
                return;
            }

            // Prefab Instantiate → 보스 영역에 붙이기
            var statusUI = Instantiate(_characterStatusPrefab, _bossArea);
            statusUI.Setup(bossEntity, (int)EntityType.Boss);

            _entityStatusUIs.Add((int)EntityType.Boss, statusUI);
        }

        private void CreatePartyStatusUIs()
        {
            if (_partyArea == null || _characterStatusPrefab == null)
            {
                return;
            }

            // 4명 (Character1~4)
            for (int i = 0; i < 4; i++)
            {
                var entity = _battleEntities[i];
                if (entity == null) continue;

                var statusUI = Instantiate(_characterStatusPrefab, _partyArea);
                statusUI.Setup(entity, i);

                _entityStatusUIs.Add(i, statusUI);
            }
        }
        
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
                
                // turnCount 등은 예시
                _actionMenuUI.Setup(this, playerEntity, _currentPlayerIndex);
            }
        }

        public void OnActionSelect(ActionType actionType, int actionIndex)
        {
            var newAction = new ActionData(actionType, actionIndex, (EntityType)_currentPlayerIndex, EntityType.Boss);
            _battleManager.OnActionChoice(newAction);
        }

        private void StartBossTurn()
        {
            // 플레이어 액션메뉴 숨기기
            if (_actionMenuPanel != null)
            {
                _actionMenuPanel.SetActive(false);
            }
        }

        // ---- 배틀 메시지 표시 ----
        public IEnumerator ShowBattleMessage(string message, float duration = 2f)
        {
            var messageText = _battleInfo.GetComponentInChildren<TextMeshProUGUI>();
            if (messageText != null)
            {
                messageText.text = message;
                _battleInfo.SetActive(true);
                yield return new WaitForSeconds(duration);
            }
        }
        
        public IEnumerator AnimateHPBarDecrease(int entityIndex, int startHP, int endHP)
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
