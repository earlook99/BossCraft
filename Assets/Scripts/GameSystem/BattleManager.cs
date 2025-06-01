using System;
using System.Collections;
using System.Linq;
using AI;
using CameraSystem;
using Data;
using Entity;
using Effect;
using Unity.Cinemachine;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameSystem
{
    public enum BattleState
    {
        PlayerChoice,
        BossAction,
        CheckBattleEnd,
        BattleEnded
    }

    public class BattleManager : MonoBehaviour
    {
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private CameraManager _cameraManager;

        public AIWeights Weights;

        private BattleState _currentState;

        [SerializeField] private BattleEntity[] _entities = new BattleEntity[5];
        
        public BattleEntity[] Entities => _entities;

        private const int PlayerCount = 4; 
        private int _turnCount;
        public int TurnCount => _turnCount;

        private int _currentPlayerIndex;

        public event Action OnEntitiesInitialized;

        [SerializeField] private GameObject _testEffect;

        private void Start()
        {
            _currentPlayerIndex = 0;

            if (BossContainer.Instance != null && _entities[4] != null)
            {
                var boss = _entities[4];
        
                if (BossContainer.Instance.CurrentBossImageData != null)
                {
                    Texture2D bossTexture = new Texture2D(2, 2);
                    if (bossTexture.LoadImage(BossContainer.Instance.CurrentBossImageData))
                    {
                        var newSprite = Sprite.Create(
                            bossTexture,
                            new Rect(0, 0, bossTexture.width, bossTexture.height),
                            new Vector2(0.5f, 0.5f),
                            0.7f
                        );
                
                        var spriteRenderer = boss.GetComponentInChildren<SpriteRenderer>();
                        if (spriteRenderer != null)
                        {
                            spriteRenderer.sprite = newSprite;
                            spriteRenderer.enabled = true;
                            spriteRenderer.color = Color.white;
                        }
                    }
                }
        
                if (!string.IsNullOrEmpty(BossContainer.Instance.CurrentBossName))
                {
                    boss.EntityName = BossContainer.Instance.CurrentBossName;
                }
            }
    
            OnEntitiesInitialized?.Invoke();
            
            ChangeBattleState(BattleState.PlayerChoice);
        }

        private void ChangeBattleState(BattleState newState)
        {
            _currentState = newState;
            switch (_currentState)
            {
                case BattleState.PlayerChoice:
                    BeginPlayerTurn();
                    break;

                case BattleState.BossAction:
                    StartCoroutine(BeginBossTurn());
                    break;

                case BattleState.CheckBattleEnd:
                    if (!CheckBattleEnd())
                    {
                        _currentPlayerIndex = 0;
                        ChangeBattleState(BattleState.PlayerChoice);
                    }
                    break;

                case BattleState.BattleEnded:
                    HandleBattleEnd();
                    break;
            }
        }

        private void BeginPlayerTurn()
        {
            while (_currentPlayerIndex < PlayerCount && _entities[_currentPlayerIndex].CurrentHP <= 0)
            {
                _currentPlayerIndex++;
            }
            
            _turnCount++;
            
            _cameraManager.SwitchCameraTo((CineCamType)_currentPlayerIndex);
            SetSpriteAlphaExclusive(_currentPlayerIndex);

            var currentEntity = _entities[_currentPlayerIndex];
            
            if (currentEntity.IsStunned)
            {
                currentEntity.ReduceStunDuration();
                StartCoroutine(ShowStunnedAndContinue(currentEntity));
                return;
            }
            
            if (currentEntity.IsCharging)
            {
                ActionData autoChargeAction = new ActionData(
                    ActionType.Move,
                    currentEntity.ChargingMoveIndex,
                    (EntityType)_currentPlayerIndex,
                    currentEntity.ChargingMoveTarget
                );

                StartCoroutine(ExecutePlayerAction(autoChargeAction));
            }
            else
            {
                _uiManager.ShowActionMenuForCurrentPlayer(_currentPlayerIndex);
            }
        }

        public void ReceivePlayerChoice(ActionData newAction)
        {
            StartCoroutine(ExecutePlayerAction(newAction));
        }

        private IEnumerator ExecutePlayerAction(ActionData action)
        {
            var source = _entities[(int)action.Source];
    
            if (source.IsGuarding)
            {
                source.SetGuardState(false);
            }
    
            switch (action.Action)
            {
                case ActionType.Move:
                    yield return StartCoroutine(PerformMove(action));
                    break;
            
                case ActionType.Guard:
                    source.SetGuardState(true);
                    yield return _uiManager.ShowBattleMessage($"{source.EntityName} takes a defensive stance!", 1f);
                    break;
            
                case ActionType.Taunt:
                    yield return _uiManager.ShowBattleMessage($"{source.EntityName} taunts the enemy!", 1f);
                    break;
            }

            if (CheckBattleEnd()) 
                yield break; 

            _currentPlayerIndex++;
            if (_currentPlayerIndex >= PlayerCount)
            {
                _currentPlayerIndex = 0;
                _cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
                SetSpriteAlphaExclusive(-1);
                ChangeBattleState(BattleState.BossAction);
            }
            else
            {
                SetSpriteAlphaExclusive(_currentPlayerIndex);
                ChangeBattleState(BattleState.PlayerChoice);
            }
        }

        private IEnumerator BeginBossTurn()
        {
            var boss = _entities[4];

            if (boss.IsCharging)
            {
                ActionData autoChargeAction = new ActionData(
                    ActionType.Move,
                    boss.ChargingMoveIndex,
                    EntityType.Boss,
                    boss.ChargingMoveTarget
                );
                yield return StartCoroutine(ExecuteBossAction(autoChargeAction));
            }
            else
            {
                var players = new BattleEntity[4];
                for (int i = 0; i < 4; i++)
                {
                    players[i] = _entities[i];
                }

                UtilityAI bossAI = new UtilityAI(boss, players, Weights);
                MoveDecision decision = bossAI.Decide();

                if (decision.MoveIndex >= 0)
                {
                    ActionData bossAction = new ActionData(
                        ActionType.Move,
                        decision.MoveIndex,
                        EntityType.Boss,
                        decision.TargetEntity
                    );
                    yield return StartCoroutine(ExecuteBossAction(bossAction));
                }
            }

            _cameraManager.SwitchCameraTo(CineCamType.Player1);
            ChangeBattleState(BattleState.CheckBattleEnd);
        }

        private IEnumerator ExecuteBossAction(ActionData bossAction)
        {
            var boss = _entities[(int)EntityType.Boss];
            var move = boss.GetMoveInstance(bossAction.ActionIndex);
            if (move == null) yield break;
            
            move.CooldownLeft = move.Data.Cooldown;

            yield return StartCoroutine(PerformMove(bossAction));
            if (CheckBattleEnd()) yield break;
        }

        private IEnumerator PerformMove(ActionData action)
        {
            var source = _entities[(int)action.Source];
            var moveInst = source.GetMoveInstance(action.ActionIndex);
            if (moveInst == null) yield break;

            if (moveInst.Data.RequiresCharge && !source.IsCharging)
            {
                source.SetChargingState(true, action.ActionIndex, action.Target);
                yield return _uiManager.ShowBattleMessage($"{source.EntityName} is charging up!",1f);
                yield break;
            }

            yield return ShowActionMessage(source, moveInst.Data);

            switch (moveInst.Data.Category)
            {
                case MoveCategory.Single:
                    {
                        var targetEntity = _entities[(int)action.Target];
                        yield return ApplyEffectsToSingle(source, targetEntity, moveInst);
                    }
                    break;
                case MoveCategory.AOE:
                    {
                        if (source is BossEntity)
                        {
                            var targetIndices = new[] {0,1,2,3}
                                .Where(idx=>_entities[idx].CurrentHP>0).ToArray();
                            yield return ApplyEffectsToMultiple(source, targetIndices, moveInst);
                        }
                        else
                        {
                            var targetIndices = new[] {4};
                            yield return ApplyEffectsToMultiple(source, targetIndices, moveInst);
                        }
                    }
                    break;
                case MoveCategory.MultiRandom:
                    yield return ApplyEffectsMultiRandom(source, moveInst);
                    break;
            }

            if (source.IsCharging)
            {
                source.SetChargingState(false);
            }
        }
        
        private IEnumerator ShowStunnedAndContinue(BattleEntity entity)
        {
            yield return _uiManager.ShowBattleMessage($"{entity.EntityName} is stunned!", 1f);
    
            _currentPlayerIndex++;
            if (_currentPlayerIndex >= PlayerCount)
            {
                _currentPlayerIndex = 0;
                _cameraManager.SwitchCameraTo(CineCamType.ZoomOut);
                SetSpriteAlphaExclusive(-1);
                ChangeBattleState(BattleState.BossAction);
            }
            else
            {
                ChangeBattleState(BattleState.PlayerChoice);
            }
        }

        private IEnumerator ApplyEffectsToSingle(BattleEntity source, BattleEntity target, MoveInstance moveInst)
        {
            var effects = moveInst.Data.Effects;
            if (effects == null || effects.Length == 0) yield break;

            if (target.CurrentHP<=0) yield break;

            foreach (var eff in effects)
            {
                yield return ApplyEffect(source, target, moveInst, eff);
            }
        }

        private IEnumerator ApplyEffectsToMultiple(BattleEntity source, int[] targetIndices, MoveInstance moveInst)
        {
            var effects = moveInst.Data.Effects;
            if (effects == null || effects.Length == 0) yield break;

            foreach (int idx in targetIndices)
            {
                var t = _entities[idx];
                if (t.CurrentHP<=0) continue;

                foreach (var eff in effects)
                {
                    yield return ApplyEffect(source, t, moveInst, eff);
                }
            }
        }

        private IEnumerator ApplyEffectsMultiRandom(BattleEntity source, MoveInstance moveInst)
        {
            int repeat = Random.Range(2,5);
            for (int i=0;i<repeat;i++)
            {
                var validTargets = (source is BossEntity)
                    ? new[] {0,1,2,3}.Where(idx=>_entities[idx].CurrentHP>0).ToArray()
                    : new[] {4}.Where(idx=>_entities[idx].CurrentHP>0).ToArray();
                if (validTargets.Length==0) break;

                int pick = validTargets[Random.Range(0, validTargets.Length)];
                var target = _entities[pick];

                foreach (var eff in moveInst.Data.Effects)
                {
                    if (eff.EffectType == MoveEffectType.Damage)
                    {
                        yield return ApplyEffect(source, target, moveInst, eff);
                    }
                }

                yield return new WaitForSeconds(0.5f);
            }
        }

        private IEnumerator ApplyEffect(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect eff)
        {
            switch (eff.EffectType)
            {
                case MoveEffectType.Damage:
                    yield return ApplyDamageEffect(source, target, moveInst, eff);
                    break;
                case MoveEffectType.Heal:
                    yield return ApplyHealEffect(source, target, moveInst, eff);
                    break;
                case MoveEffectType.Buff:
                    yield return ApplyBuffEffect(source, target, moveInst, eff);
                    break;
                case MoveEffectType.Debuff:
                    yield return ApplyDebuffEffect(source, target, moveInst, eff);
                    break;
                case MoveEffectType.Shield:
                    yield return ApplyShieldEffect(source, target, moveInst, eff);
                    break;
            }
        }

        private IEnumerator ApplyDamageEffect(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect eff)
        {
            var raw = DamageFormula.GetRawHit(
                source,
                eff.Power,
                eff.Accuracy,
                eff.CritChance
            );

            yield return SpawnMoveVFX(source, target, moveInst.Data, 2f);

            if (!raw.IsHit)
            {
                yield return _uiManager.ShowBattleMessage("Miss!");
                yield break;
            }

            int prevHP = target.CurrentHP;
            target.TakeDamage(moveInst.Data.Type, raw.Damage); 
            yield return _uiManager.AnimateHPBarUpdate(Array.IndexOf(_entities, target), prevHP, target.CurrentHP);
        }

        private IEnumerator ApplyHealEffect(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect eff)
        {
            yield return SpawnMoveVFX(source, target, moveInst.Data, 2f);

            int prevHP = target.CurrentHP;
            target.CurrentHP += eff.Power;
            if (target.CurrentHP > target.MaxHP) target.CurrentHP = target.MaxHP;

            yield return _uiManager.AnimateHPBarUpdate(Array.IndexOf(_entities, target), prevHP, target.CurrentHP);
        }

        private IEnumerator ApplyBuffEffect(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect eff)
        {
            float addVal = eff.Power / 100f; 
            const float MIN_BUFF = 0.6f, MAX_BUFF = 1.4f;

            switch (eff.BuffsType)
            {
                case BuffsType.Attack:
                    target.AtkBuffMultiplier += addVal;
                    target.AtkBuffMultiplier = Mathf.Clamp(target.AtkBuffMultiplier, MIN_BUFF, MAX_BUFF);
                    break;
                
                case BuffsType.Defense:
                    target.DefBuffMultiplier += Mathf.Abs(addVal);
                    target.DefBuffMultiplier = Mathf.Clamp(target.DefBuffMultiplier, MIN_BUFF, MAX_BUFF);
                    break;
            }

            yield return SpawnMoveVFX(source, target, moveInst.Data, 2f);
        }

        private IEnumerator ApplyDebuffEffect(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect eff)
        {
            float minusVal = eff.Power / 100f;
            const float MIN_BUFF = 0.6f, MAX_BUFF = 1.4f;
            
            switch (eff.BuffsType)
            {
                case BuffsType.Attack:
                    target.AtkBuffMultiplier -= Mathf.Abs(minusVal);
                    target.AtkBuffMultiplier = Mathf.Clamp(target.AtkBuffMultiplier, MIN_BUFF, MAX_BUFF);
                    break;
                
                case BuffsType.Defense:
                    target.DefBuffMultiplier -= Mathf.Abs(minusVal);
                    target.DefBuffMultiplier = Mathf.Clamp(target.DefBuffMultiplier, MIN_BUFF, MAX_BUFF);
                    break;
            }

            yield return SpawnMoveVFX(source, target, moveInst.Data, 2f);
        }
        
        private IEnumerator ApplyShieldEffect(BattleEntity source, BattleEntity target, MoveInstance moveInst, MoveEffect eff)
        {
            if (target is BossEntity boss)
            {
                int previousHP = boss.CurrentHP;
                var trigger = boss.GetAvailableShieldTrigger(_turnCount);
        
                if (trigger != null)
                {
                    boss.ActivateShield(trigger);
            
                    yield return _uiManager.ShowBattleMessage(
                        $"{boss.EntityName} activates shield!", 
                        1.5f
                    );
            
                    _uiManager.AnimateShieldConversion(boss, previousHP);
                    yield return new WaitForSeconds(0.5f);
            
                    yield return SpawnMoveVFX(source, target, moveInst.Data, 2f);
                }
            }
        }

        private IEnumerator ShowActionMessage(BattleEntity source, MoveData data)
        {
            yield return _uiManager.ShowBattleMessage($"{source.EntityName} used {data.Name}!", 1f);
        }

        private IEnumerator SpawnMoveVFX(BattleEntity source, BattleEntity target, MoveData moveData, float duration = 1f)
        {
            GameObject vfxPrefab = moveData.VFXPrefab != null ? moveData.VFXPrefab : _testEffect;
            
            if (vfxPrefab == null) yield break;
            
            var fx = Instantiate(vfxPrefab, target.transform.position, Quaternion.identity);
            
            if (fx.GetComponent<AnimationEventHandler>() == null)
            {
                fx.AddComponent<AnimationEventHandler>();
            }
            
            if (source is BossEntity)
            {
                // boss layer
            }
            else
            {
                fx.layer = LayerMask.NameToLayer("BossEffect");
            }
            
            yield return new WaitForSeconds(duration);
            
            if (fx != null)
            {
                Destroy(fx);
            }
        }

        private bool CheckBattleEnd()
        {
            var boss = _entities[4];
            if (boss.CurrentHP <= 0)
            {
                ChangeBattleState(BattleState.BattleEnded);
                return true;
            }

            bool allDead = true;
            for (int i=0;i<PlayerCount;i++)
            {
                if (_entities[i].CurrentHP>0)
                {
                    allDead=false;
                    break;
                }
            }
            if (allDead)
            {
                ChangeBattleState(BattleState.BattleEnded);
                return true;
            }

            return false;
        }

        private void HandleBattleEnd()
        {
            bool playerWon = _entities[4].CurrentHP <= 0;
            _uiManager.ShowBattleEndScreen(playerWon);
        }

        private void SetSpriteAlphaExclusive(int activeIndex)
        {
            for (int i=0;i<PlayerCount;i++)
            {
                var sr = _entities[i].SpriteRenderer;
                if (!sr) continue;

                var c = sr.color;
                c.a = (activeIndex<0) ? 1f : (i==activeIndex ? 1f : 0.1f);
                sr.color = c;
            }
        }
    }
}