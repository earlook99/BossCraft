using System.Collections.Generic;
using UnityEngine;
using Entity;
using Data;
using AI;

namespace GameSystem.Factory
{
    public class EntityFactory : MonoBehaviour
    {
        [Header("Entity Prefabs")]
        [SerializeField] private GameObject[] _playerPrefabs = new GameObject[4];
        [SerializeField] private GameObject _bossPrefab;
        
        [Header("Spawn Containers")]
        [SerializeField] private Transform _playerContainer;
        [SerializeField] private Transform _bossContainer;
        
        [Header("Boss Configuration")]
        [SerializeField] private ShieldPattern _defaultShieldPattern;
        [SerializeField] private MoveData _bossShieldMove;
        
        private static EntityFactory _instance;
        public static EntityFactory Instance => _instance;
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
            
            CreateContainers();
        }
        
        private void CreateContainers()
        {
            if (_playerContainer == null)
            {
                GameObject playerContainerObj = new GameObject("PlayerContainer");
                playerContainerObj.transform.SetParent(transform);
                _playerContainer = playerContainerObj.transform;
            }
            
            if (_bossContainer == null)
            {
                GameObject bossContainerObj = new GameObject("BossContainer");
                bossContainerObj.transform.SetParent(transform);
                _bossContainer = bossContainerObj.transform;
            }
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        public BattleEntity CreatePlayerEntity(int index, Vector3 position)
        {
            if (index < 0 || index >= _playerPrefabs.Length || _playerPrefabs[index] == null)
            {
                Debug.LogError($"Player prefab at index {index} not found");
                return null;
            }
            
            GameObject entityObj = Instantiate(_playerPrefabs[index], position, Quaternion.identity, _playerContainer);
            
            BattleEntity entity = entityObj.GetComponent<BattleEntity>();
            if (entity == null)
            {
                Debug.LogError($"BattleEntity component not found on prefab at index {index}");
                Destroy(entityObj);
                return null;
            }
            
            entity.Initialize();
            
            return entity;
        }
        
        public BossEntity CreateBossEntity(Vector3 position)
        {
            if (_bossPrefab == null)
            {
                Debug.LogError("Boss prefab not assigned");
                return null;
            }
            
            GameObject bossObj = Instantiate(_bossPrefab, position, Quaternion.identity, _bossContainer);
            
            BossEntity boss = bossObj.GetComponent<BossEntity>();
            if (boss == null)
            {
                Debug.LogError("BossEntity component not found on boss prefab");
                Destroy(bossObj);
                return null;
            }
            
            ConfigureBossFromContainer(boss);
            
            if (_defaultShieldPattern != null)
            {
                SetBossShieldPattern(boss, _defaultShieldPattern);
            }
            
            if (_bossShieldMove != null)
            {
                EnsureBossHasShieldMove(boss);
            }
            
            boss.Initialize();
            
            return boss;
        }
        
        private void ConfigureBossFromContainer(BossEntity boss)
        {
            if (BossContainer.Instance != null)
            {
                var container = BossContainer.Instance;
                
                if (!string.IsNullOrEmpty(container.CurrentBossName))
                {
                    boss.EntityName = container.CurrentBossName;
                }
                
                boss.ElementType = container.CurrentBossType;
                
                if (container.CurrentBossImageData != null)
                {
                    SetBossSprite(boss, container.CurrentBossImageData);
                }
            }
        }
        
        private void SetBossSprite(BossEntity boss, byte[] imageData)
        {
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(imageData)) return;
            
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                0.7f
            );
            
            var spriteRenderer = boss.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }
        
        private void SetBossShieldPattern(BossEntity boss, ShieldPattern pattern)
        {
            var shieldPatternField = typeof(BossEntity).GetField("_shieldPattern", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
            if (shieldPatternField != null)
            {
                shieldPatternField.SetValue(boss, pattern);
            }
        }
        
        private void EnsureBossHasShieldMove(BossEntity boss)
        {
            bool hasShieldMove = false;
            
            foreach (var move in boss.MoveSet)
            {
                if (move == null) continue;
                
                foreach (var effect in move.Effects)
                {
                    if (effect.EffectType == MoveEffectType.Shield)
                    {
                        hasShieldMove = true;
                        break;
                    }
                }
                
                if (hasShieldMove) break;
            }
            
            if (!hasShieldMove && _bossShieldMove != null)
            {
                boss.MoveSet.Add(_bossShieldMove);
            }
        }
        
        public BattleEntity[] CreateBattleEntities(Vector3[] playerPositions, Vector3 bossPosition)
        {
            BattleEntity[] entities = new BattleEntity[GameConstants.Battle.TOTAL_ENTITIES];
            
            for (int i = 0; i < GameConstants.Battle.PLAYER_COUNT; i++)
            {
                if (i < playerPositions.Length)
                {
                    entities[i] = CreatePlayerEntity(i, playerPositions[i]);
                }
            }
            
            entities[GameConstants.Battle.BOSS_INDEX] = CreateBossEntity(bossPosition);
            
            return entities;
        }
        
        public void DestroyAllEntities()
        {
            if (_playerContainer != null)
            {
                foreach (Transform child in _playerContainer)
                {
                    Destroy(child.gameObject);
                }
            }
            
            if (_bossContainer != null)
            {
                foreach (Transform child in _bossContainer)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }
}