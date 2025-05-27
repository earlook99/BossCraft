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

        /// <summary>
        /// 가장 높은 UtilityScore를 갖는 3가지 후보(best, second, third)를 골라
        /// 일정 확률로 그중 하나를 반환.
        /// </summary>
        public MoveDecision Decide()
        {
            MoveDecision best = default;
            MoveDecision second = default;
            MoveDecision third = default;

            // 보스의 MoveInstances를 순회하여, 쿨다운/사용횟수 체크 후 Evaluate
            ReadOnlySpan<MoveInstance> moves = _boss.MoveInstances;
            for (int i = 0; i < moves.Length; i++)
            {
                ref readonly MoveInstance move = ref moves[i];

                // 사용 불가 상태면 스킵
                if (move.CooldownLeft > 0 || move.UsageLeft == 0)
                    continue;

                // MoveData를 평가: (스킬 유틸리티 + 최적의 타겟)
                MoveDecision decision = EvaluateMove(i, move.Data);

                // Top3 갱신
                InsertTop3(ref best, ref second, ref third, decision);
            }

            // 가능한 Move가 없거나, 모든 Move의 유틸리티가 0 이하라면 -1 반환
            if (best.UtilityScore <= 0f)
            {
                return new MoveDecision(-1, EntityType.Boss, 0f);
            }

            // 65% 확률로 best, 20%로 second, 15%로 third
            float randomRoll = Random.value;
            if (randomRoll < 0.65f) 
                return best;
            else if (randomRoll < 0.85f)
                return second;
            else
                return third;
        }

        /// <summary>
        /// Move의 카테고리에 따라 타겟팅 방식 및 유틸리티를 계산한다.
        /// </summary>
        private MoveDecision EvaluateMove(int index, MoveData data)
        {
            float utility = 0f;
            EntityType target = EntityType.Boss; // 기본값

            // 카테고리는 "단일, 광역, 멀티랜덤" 등 타겟팅 범위만 구분
            switch (data.Category)
            {
                case MoveCategory.Single:
                    utility = ScoreSingle(data, ref target);
                    break;

                case MoveCategory.AOE:
                    utility = ScoreAOE(data);
                    // 광역은 특정 targetOut이 없으므로, 필요시 별도 처리
                    break;

                case MoveCategory.MultiRandom:
                    utility = ScoreMultiRandom(data);
                    // 마찬가지로 멀티랜덤도 명확한 단일 target은 없음
                    break;

                default:
                    // 혹시 추가 카테고리가 있다면 처리
                    break;
            }

            return new MoveDecision(index, target, utility);
        }

        /// <summary>
        /// 단일 타겟 스킬을 가정하고, 4명의 플레이어 중
        /// 어느 타겟이 가장 유틸리티가 높은지 탐색.
        /// </summary>
        private float ScoreSingle(MoveData data, ref EntityType targetOut)
        {
            float bestScore = -1f;
            EntityType bestTarget = EntityType.Boss;

            for (int i = 0; i < _party.Length; i++)
            {
                var player = _party[i];
                // 보스 -> player에게 data를 썼을 때 유틸리티
                float localScore = CalculateMoveUtility(_boss, player, data);

                // 단일타겟 가중치
                localScore *= _weights.SingleHit;

                if (localScore > bestScore)
                {
                    bestScore = localScore;
                    bestTarget = (EntityType)i; 
                }
            }

            targetOut = bestTarget;
            return bestScore;
        }

        /// <summary>
        /// 광역 스킬은 플레이어 전원에게 효과가 동시에 적용되므로, 각자에 대한 스코어를 합산.
        /// </summary>
        private float ScoreAOE(MoveData data)
        {
            float sumScore = 0f;
            for (int i = 0; i < _party.Length; i++)
            {
                var player = _party[i];
                sumScore += CalculateMoveUtility(_boss, player, data);
            }

            // 광역 가중치
            sumScore *= _weights.AOE;
            return sumScore;
        }

        /// <summary>
        /// 멀티랜덤(예: 여러 번 랜덤 타격) 스킬은
        /// 간단히 단일 스코어를 산출 후, 멀티랜덤 가중치를 곱해 대략 추정.
        /// </summary>
        private float ScoreMultiRandom(MoveData data)
        {
            // 우선 단일 스코어를 구해둔 뒤...
            EntityType dummy = EntityType.Boss;
            float singleScore = ScoreSingle(data, ref dummy);

            // 멀티랜덤 가중치
            return singleScore * _weights.MultiRandomHit;
        }

        /// <summary>
        /// (boss -> target)으로 MoveData의 모든 효과를 고려하여,
        /// 보스 입장에서의 유틸리티를 합산.
        /// </summary>
        private float CalculateMoveUtility(BattleEntity boss, BattleEntity target, MoveData data)
        {
            float total = 0f;

            foreach (var eff in data.Effects)
            {
                switch (eff.EffectType)
                {
                    case MoveEffectType.Damage:
                    {
                        // 기대 데미지
                        float raw = DamageFormula.GetExpectedRawDamage(boss, data);
                        // target의 방어/속성 반영
                        float actual = target.PreviewMitigate(raw, data);

                        // 이 데미지로 타겟이 사망할 가능성이 있다면 KillBonus
                        float killBonus = 0f;
                        if (target.CurrentHP <= actual)
                        {
                            killBonus = _weights.KillBonus * eff.Accuracy;
                        }

                        total += (actual + killBonus);
                    }
                    break;

                    case MoveEffectType.Stun:
                    {
                        // 예: 스턴은 확률(eff.Accuracy)에 따라 적용
                        float stunScore = _weights.StunBase * eff.Accuracy;
                        total += stunScore;
                    }
                    break;

                    case MoveEffectType.Buff:
                    {
                        // 만약 boss가 자기 자신에게 Buff를 건다면, 큰 이득
                        // target == boss 라면 가중치 적용, 
                        // 아니면 0 또는 다른 로직(적에게 버프?) 처리
                        if (target == boss)
                        {
                            total += _weights.BuffBase;
                        }
                        // (플레이어에게 Buff를 준다면 보스 입장에선 손해이므로 0, 혹은 음수)
                    }
                    break;

                    case MoveEffectType.Debuff:
                    {
                        // target(플레이어) 디버프 -> 보스에게 이득
                        // (가중치: DebuffBase)
                        total += _weights.DebuffBase;
                    }
                    break;

                    case MoveEffectType.Heal:
                    {
                        // boss->target 이라면, 타겟이 boss인 경우에만 스코어
                        if (target == boss)
                        {
                            total += eff.Power * _weights.HealBase;
                        }
                        // 플레이어를 힐하면 보스 입장에서는 손해 -> 0 or 음수
                    }
                    break;

                    // 필요 시 ClearOppBuff, ClearSelfDebuff 등도 추가
                }
            }

            return total;
        }

        /// <summary>
        /// 유틸리티 상위 3개를 유지해두는 헬퍼 메서드
        /// </summary>
        private static void InsertTop3(ref MoveDecision best,
                                       ref MoveDecision second,
                                       ref MoveDecision third,
                                       MoveDecision candidate)
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
