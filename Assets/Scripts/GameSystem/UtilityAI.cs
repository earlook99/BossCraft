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

        public UtilityAI(BattleEntity boss, BattleEntity[] party, AIWeights weights)
        {
            _boss = boss;
            _party = party;
            _weights = weights;
        }

        public MoveDecision Decide()
        {
            MoveDecision best = default;
            MoveDecision second = default;
            MoveDecision third = default;

            ReadOnlySpan<MoveInstance> moves = _boss.MoveInstances;
            for (int i = 0; i < moves.Length; i++)
            {
                ref readonly MoveInstance move = ref moves[i];

                if (move.CooldownLeft > 0 || move.UsageLeft == 0)
                {
                    continue;
                }

                MoveDecision decision = EvaluateMove(i, move.Data);
                InsertTop3(ref best, ref second, ref third, decision);
            }

            if (best.UtilityScore <= 0f)
            {
                return new MoveDecision(-1, EntityType.Boss, 0f);
            }

            float randomRoll = Random.value;
            return randomRoll < 0.65f ? best : randomRoll < 0.85f ? second : third;
        }

        MoveDecision EvaluateMove(int index, MoveData data)
        {
            float utility;
            EntityType target = EntityType.Character1;

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
                
                case MoveCategory.Charge:
                    utility = ScoreCharge(data, ref target);
                    break;
                
                case MoveCategory.Buff:
                    utility = _weights.BuffBase;
                    break;
                
                case MoveCategory.Debuff:
                    utility = _weights.DebuffBase;
                    break;
                
                case MoveCategory.ClearOppBuff:
                    utility = _weights.ClearOppBuffBase;
                    break;
                
                case MoveCategory.ClearSelfDebuff:
                    utility = _weights.ClearSelfDebuffBase;
                    break;
                
                case MoveCategory.Stun:
                    utility = _weights.StunBase * data.Accuracy;
                    break;
                
                default:
                    utility = 0f;
                    break;
            }

            return new MoveDecision(index, target, utility);
        }

        float ScoreSingle(MoveData data, ref EntityType targetOut)
        {
            float best = -1f;
            for (int i = 0; i < _party.Length; i++)
            {
                var player = _party[i];
                float expectation = ExpectedDamage(player, data);
                float bonus = player.CurrentHP <= expectation ? _weights.KillBonus * data.Accuracy : 0f;
                float score = (expectation + bonus) * _weights.SingleHit;

                if (score > best)
                {
                    best = score;
                    targetOut = (EntityType)i;
                }
            }

            return best;
        }

        float ScoreAOE(MoveData data)
        {
            float sum = 0f;
            for (int i = 0; i < _party.Length; i++)
            {
                var player = _party[i];
                float expectation = ExpectedDamage(player, data);
                float bonus = player.CurrentHP <= expectation ? _weights.KillBonus * data.Accuracy : 0f;
                sum += expectation + bonus;
            }

            return sum * _weights.AOE;
        }

        float ScoreMultiRandom(MoveData data)
        {
            EntityType dummy = EntityType.Character1;
            return ScoreSingle(data, ref dummy) * _weights.MultiRandomHit;
        }
        
        float ScoreCharge(MoveData data, ref EntityType targetOut)
            => ScoreSingle(data, ref targetOut) * _weights.Charge;

        float ExpectedDamage(BattleEntity player, MoveData data)
        {
            float rawDamage = DamageFormula.GetExpectedRawDamage(_boss, data);
            return player.PreviewMitigate(rawDamage, data);
        }

        static void InsertTop3(ref MoveDecision best, ref MoveDecision second, ref MoveDecision third, MoveDecision candidate)
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
