using System.Collections.Generic;
using Data;
using Entity;
using UnityEngine;
using Random = UnityEngine.Random;
using static GameSystem.GameConstants.AI;

namespace GameSystem
{
    public class UtilityAI
    {
        private readonly BattleEntity _boss;
        private readonly BattleEntity[] _party;
        private readonly BattleEntity[] _allEntities;
        private readonly AIWeights _weights;
        private readonly int _currentTurn;
        
        private readonly List<int> _validTargetIndices = new List<int>(4);

        public UtilityAI(BattleEntity boss, BattleEntity[] party, BattleEntity[] allEntities, AIWeights weights, int currentTurn = 0)
        {
            _boss = boss;
            _party = party;
            _allEntities = allEntities;
            _weights = weights;
            _currentTurn = currentTurn;
        }

        public MoveDecision Decide()
        {
            if (ShouldUseShield(out int shieldMoveIndex))
            {
                return new MoveDecision(shieldMoveIndex, EntityType.Boss, SHIELD_PRIORITY_SCORE);
            }
            
            MoveDecision best = default;
            MoveDecision second = default;
            MoveDecision third = default;

            EvaluateAllMoves(ref best, ref second, ref third);

            if (best.UtilityScore <= 0f)
            {
                return new MoveDecision(INVALID_MOVE_INDEX, EntityType.Boss, 0f);
            }

            return SelectFinalDecision(best, second, third);
        }

        private bool ShouldUseShield(out int shieldMoveIndex)
        {
            shieldMoveIndex = INVALID_MOVE_INDEX;
            
            if (!(_boss is BossEntity bossEntity) || bossEntity.HasShield)
                return false;

            var trigger = bossEntity.GetAvailableShieldTrigger(_currentTurn);

            if (trigger == null)
                return false;

            shieldMoveIndex = GetShieldMoveIndex();
            return shieldMoveIndex >= 0;
        }
        
        private int GetShieldMoveIndex()
        {
            MoveInstance[] moves = _boss.MoveInstances;
            
            for (int i = 0; i < moves.Length; i++)
            {
                if (moves[i].CooldownLeft > 0)
                    continue;
                    
                var effects = moves[i].Data.Effects;
                for (int j = 0; j < effects.Length; j++)
                {
                    if (effects[j].EffectType == MoveEffectType.Shield)
                    {
                        return i;
                    }
                }
            }
            return INVALID_MOVE_INDEX;
        }

        private void EvaluateAllMoves(ref MoveDecision best, ref MoveDecision second, ref MoveDecision third)
        {
            MoveInstance[] moves = _boss.MoveInstances;
    
            Debug.Log($"보스 스킬 평가 시작. 총 {moves.Length}개");
    
            for (int i = 0; i < moves.Length; i++)
            {
                MoveInstance move = moves[i];
        
                Debug.Log($"스킬 {i}: {move.Data.Name}, 쿨다운: {move.CooldownLeft}");
        
                if (move.CooldownLeft > 0)
                    continue;

                MoveDecision decision = EvaluateMove(i, move.Data);
                Debug.Log($"스킬 {i} 점수: {decision.UtilityScore}");
        
                UpdateTopDecisions(ref best, ref second, ref third, decision);
            }
    
            Debug.Log($"최종 베스트 점수: {best.UtilityScore}");
        }

        private MoveDecision SelectFinalDecision(MoveDecision best, MoveDecision second, MoveDecision third)
        {
            float randomRoll = Random.value;
            
            if (randomRoll < BEST_CHOICE_PROBABILITY) 
                return best;
            else if (randomRoll < BEST_CHOICE_PROBABILITY + SECOND_CHOICE_PROBABILITY)
                return second;
            else
                return third;
        }

        private MoveDecision EvaluateMove(int index, MoveData data)
        {
            float utility = 0f;
            EntityType target = EntityType.Boss;

            switch (data.Category)
            {
                case MoveCategory.Single:
                    utility = ScoreSingle(data, ref target);
                    break;
                case MoveCategory.AOE:
                    utility = ScoreAOE(data);
                    break;
                case MoveCategory.MultiRandom:
                    utility = ScoreMultiRandom(data);
                    break;
            }

            return new MoveDecision(index, target, utility);
        }

        private float ScoreSingle(MoveData data, ref EntityType targetOut)
        {
            for (int i = 0; i < _party.Length; i++)
            {
                if (_party[i] != null && _party[i].IsTaunting && _party[i].CurrentHP > 0)
                {
                    targetOut = (EntityType)FindEntityIndex(_party[i]);
                    float tauntScore = CalculateMoveUtility(_boss, _party[i], data) * _weights.SingleHit * 2f;
                    Debug.Log($"[ScoreSingle] Taunt target found: {_party[i].EntityName}, Score: {tauntScore}");
                    return tauntScore;
                }
            }
    
            float bestScore = -1f;
            EntityType bestTarget = EntityType.Boss;

            for (int i = 0; i < _party.Length; i++)
            {
                var player = _party[i];
                if (player == null || player.CurrentHP <= 0) continue;
                if (player.IsStealthed) continue;

                float localScore = CalculateMoveUtility(_boss, player, data) * _weights.SingleHit;
                Debug.Log($"[ScoreSingle] Evaluating {player.EntityName}: Base utility: {CalculateMoveUtility(_boss, player, data)}, SingleHit weight: {_weights.SingleHit}, Final: {localScore}");

                if (localScore > bestScore)
                {
                    bestScore = localScore;
                    bestTarget = (EntityType)FindEntityIndex(player);
                }
            }

            targetOut = bestTarget;
            Debug.Log($"[ScoreSingle] Best target: {bestTarget}, Best score: {bestScore}");
            return bestScore;
        }

        private int FindEntityIndex(BattleEntity target)
        {
            for (int i = 0; i < _allEntities.Length; i++)
            {
                if (_allEntities[i] == target)
                    return i;
            }
            
            Debug.LogError($"[UtilityAI] Could not find entity {target.EntityName} in allEntities array!");
            return 0; // 기본값으로 첫 번째 엔티티 반환
        }

        private float ScoreAOE(MoveData data)
        {
            float sumScore = 0f;
            
            for (int i = 0; i < _party.Length; i++)
            {
                var player = _party[i];
                if (player != null && player.CurrentHP > 0)
                {
                    sumScore += CalculateMoveUtility(_boss, player, data);
                }
            }

            return sumScore * _weights.AOE;
        }

        private float ScoreMultiRandom(MoveData data)
        {
            EntityType dummy = EntityType.Boss;
            float singleScore = ScoreSingle(data, ref dummy);
            return singleScore * _weights.MultiRandomHit;
        }

        private float CalculateMoveUtility(BattleEntity boss, BattleEntity target, MoveData data)
        {
            float total = 0f;

            foreach (var effect in data.Effects)
            {
                total += CalculateEffectUtility(boss, target, data, effect);
            }

            return total;
        }

        private float CalculateEffectUtility(BattleEntity boss, BattleEntity target, MoveData data, MoveEffect effect)
        {
            switch (effect.EffectType)
            {
                case MoveEffectType.Damage:
                    return CalculateDamageUtility(boss, target, data, effect);
                    
                case MoveEffectType.Stun:
                    return _weights.StunBase * effect.Accuracy;
                    
                case MoveEffectType.Buff:
                    return target == boss ? _weights.BuffBase : 0f;
                    
                case MoveEffectType.Debuff:
                    return _weights.DebuffBase;
                    
                case MoveEffectType.Heal:
                    return target == boss ? effect.Power * _weights.HealBase : 0f;
                
                case MoveEffectType.ClearOppBuff:
                    return target.HasBuffs ? _weights.ClearOppBuffBase : 0f;
                    
                default:
                    return 0f;
            }
        }

        private float CalculateDamageUtility(BattleEntity boss, BattleEntity target, MoveData data, MoveEffect effect)
        {
            float raw = DamageFormula.GetExpectedRawDamage(boss, data);
            float actual = target.PreviewMitigate(raw, data);
    
            Debug.Log($"[DamageUtility] Target: {target.EntityName}, Raw: {raw}, Actual: {actual}");
            Debug.Log($"[DamageUtility] Boss Attack: {boss.Attack}, AttackMultiplier: {boss.AttackMultiplier}");
            Debug.Log($"[DamageUtility] Target Defense: {target.Defense}, DefenseMultiplier: {target.DefenseMultiplier}");

            float killBonus = 0f;
            if (target.CurrentHP <= actual)
            {
                killBonus = _weights.KillBonus * effect.Accuracy;
            }

            float totalUtility = actual + killBonus;
            Debug.Log($"[DamageUtility] Kill bonus: {killBonus}, Total utility: {totalUtility}");
    
            return totalUtility;
        }

        private static void UpdateTopDecisions(ref MoveDecision best, ref MoveDecision second, 
                                              ref MoveDecision third, MoveDecision candidate)
        {
            if (candidate.UtilityScore > best.UtilityScore)
            {
                third = second;
                second = best;
                best = candidate;
            }
            else if (candidate.UtilityScore > second.UtilityScore)
            {
                third = second;
                second = candidate;
            }
            else if (candidate.UtilityScore > third.UtilityScore)
            {
                third = candidate;
            }
        }
    }
}