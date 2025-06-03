using System;
using Data;
using Entity;
using Random = UnityEngine.Random;

namespace GameSystem
{
    public class UtilityAI
    {
        private readonly BattleEntity _boss;
        private readonly BattleEntity[] _party;
        private readonly AIWeights _weights;

        private const float BEST_CHOICE_PROBABILITY = 0.65f;
        private const float SECOND_CHOICE_PROBABILITY = 0.20f;
        private const float SHIELD_PRIORITY_SCORE = 1000f;
        private const int INVALID_MOVE_INDEX = -1;

        public UtilityAI(BattleEntity boss, BattleEntity[] party, AIWeights weights)
        {
            _boss = boss;
            _party = party;
            _weights = weights;
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

            var trigger = bossEntity.GetAvailableShieldTrigger(
                BattleContext.Instance?.GetCurrentTurn() ?? 0
            );

            if (trigger == null)
                return false;

            shieldMoveIndex = GetShieldMoveIndex();
            return shieldMoveIndex >= 0;
        }
        
        private int GetShieldMoveIndex()
        {
            ReadOnlySpan<MoveInstance> moves = _boss.MoveInstances;
            
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
            ReadOnlySpan<MoveInstance> moves = _boss.MoveInstances;
            
            for (int i = 0; i < moves.Length; i++)
            {
                ref readonly MoveInstance move = ref moves[i];

                if (move.CooldownLeft > 0)
                    continue;

                MoveDecision decision = EvaluateMove(i, move.Data);
                UpdateTopDecisions(ref best, ref second, ref third, decision);
            }
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
            float bestScore = -1f;
            EntityType bestTarget = EntityType.Boss;

            for (int i = 0; i < _party.Length; i++)
            {
                var player = _party[i];
                float localScore = CalculateMoveUtility(_boss, player, data) * _weights.SingleHit;

                if (localScore > bestScore)
                {
                    bestScore = localScore;
                    bestTarget = (EntityType)i; 
                }
            }

            targetOut = bestTarget;
            return bestScore;
        }

        private float ScoreAOE(MoveData data)
        {
            float sumScore = 0f;
            
            for (int i = 0; i < _party.Length; i++)
            {
                sumScore += CalculateMoveUtility(_boss, _party[i], data);
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
                    
                default:
                    return 0f;
            }
        }

        private float CalculateDamageUtility(BattleEntity boss, BattleEntity target, MoveData data, MoveEffect effect)
        {
            float raw = DamageFormula.GetExpectedRawDamage(boss, data);
            float actual = target.PreviewMitigate(raw, data);

            float killBonus = 0f;
            if (target.CurrentHP <= actual)
            {
                killBonus = _weights.KillBonus * effect.Accuracy;
            }

            return actual + killBonus;
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