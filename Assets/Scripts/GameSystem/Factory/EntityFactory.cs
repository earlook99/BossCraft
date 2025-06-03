using System.Collections.Generic;
using UnityEngine;
using Entity;
using Data;
using AI;

namespace GameSystem.Factory
{
    public class EntityFactory : MonoBehaviour
    {
        [System.Serializable]
        public class EntityPrefabConfig
        {
            public string EntityName;
            public GameObject Prefab;
            public List<MoveData> DefaultMoves;
            public EntityStats DefaultStats;
        }
        
        [System.Serializable]
        public struct EntityStats
        {
            public int MaxHP;
            public int Attack;
            public int Defense;
            public ElementType ElementType;
        }
        
        [Header("Entity Prefabs")]
        [SerializeField] private List<EntityPrefabConfig> _playerPrefabs;
        [SerializeField] private GameObject _bossPrefab;
        [SerializeField] private Transform _playerContainer;
        [SerializeField] private Transform _bossContainer;
        
        [Header("Boss Configuration")]
        [SerializeField] private ShieldPattern _defaultShieldPattern;
        [SerializeField] private List<MoveData> _defaultBossMoves;
        
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
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        public BattleEntity CreatePlayerEntity(string entityName, Vector3 position, int index)
        {
            var config = _playerPrefabs.Find(c => c.EntityName == entityName);
            if (config == null)
            {
                Debug.LogError($"Player entity config not found for: {entityName}");
                return null;
            }
            
            GameObject entityObj = Instantiate(config.Prefab, position, Quaternion.identity, _playerContainer);
            entityObj.name = $"Player_{index}_{entityName}";
            
            BattleEntity entity = entityObj.GetComponent<BattleEntity>();
            if (entity == null)
            {
                entity = entityObj.AddComponent<BattleEntity>();
            }
            
            ConfigureEntity(entity, config);
            
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
            bossObj.name = "Boss";
            
            BossEntity boss = bossObj.GetComponent<BossEntity>();
            if (boss == null)
            {
                boss = bossObj.AddComponent<BossEntity>();
            }
            
            ConfigureBossFromContainer(boss);
            
            return boss;
        }
        
        private void ConfigureEntity(BattleEntity entity, EntityPrefabConfig config)
        {
            entity.EntityName = config.EntityName;
            entity.MaxHP = config.DefaultStats.MaxHP;
            entity.Attack = config.DefaultStats.Attack;
            entity.Defense = config.DefaultStats.Defense;
            entity.ElementType = config.DefaultStats.ElementType;
            
            if (config.DefaultMoves != null && config.DefaultMoves.Count > 0)
            {
                entity.MoveSet = new List<MoveData>(config.DefaultMoves);
            }
            
            entity.Initialize();
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
                else
                {
                    boss.EntityName = "Unknown Boss";
                }
                
                boss.ElementType = container.CurrentBossType;
                
                if (container.CurrentBossImageData != null)
                {
                    SetBossSprite(boss, container.CurrentBossImageData);
                }
            }
            else
            {
                boss.EntityName = "Default Boss";
                boss.ElementType = ElementType.Dark;
            }
            
            if (_defaultShieldPattern != null)
            {
                SetBossShieldPattern(boss, _defaultShieldPattern);
            }
            
            if (_defaultBossMoves != null && _defaultBossMoves.Count > 0)
            {
                boss.MoveSet = new List<MoveData>(_defaultBossMoves);
            }
            
            boss.MaxHP = 500;
            boss.Attack = 80;
            boss.Defense = 60;
            
            boss.Initialize();
        }
        
        private void SetBossSprite(BossEntity boss, byte[] imageData)
        {
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(imageData)) return;
            
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            
            var spriteRenderer = boss.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
                spriteRenderer.enabled = true;
                spriteRenderer.color = Color.white;
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
        
        public BattleEntity[] CreateBattleEntities(string[] playerNames, Vector3[] playerPositions, Vector3 bossPosition)
        {
            BattleEntity[] entities = new BattleEntity[GameConstants.Battle.TOTAL_ENTITIES];
            
            for (int i = 0; i < GameConstants.Battle.PLAYER_COUNT; i++)
            {
                if (i < playerNames.Length && i < playerPositions.Length)
                {
                    entities[i] = CreatePlayerEntity(playerNames[i], playerPositions[i], i);
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