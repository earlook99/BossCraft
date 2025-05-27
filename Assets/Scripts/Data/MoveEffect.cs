using UnityEngine;

namespace Data
{
    /// <summary>
    /// Represents a single effect within a move.
    /// Multiple effects can be combined in one move via MoveData.Effects[].
    /// </summary>
    [System.Serializable]
    public class MoveEffect
    {
        public MoveEffectType EffectType;

        /// <summary>
        /// 예) Damage일 때 데미지 양,
        ///     Heal일 때 회복량,
        ///     Buff/Debuff일 때 ±값 등등.
        /// </summary>
        public int Power;

        /// <summary>
        /// 버프/디버프가 공격 스탯인지 방어 스탯인지 구분할 때 사용.
        /// </summary>
        public BuffsType BuffsType;

        /// <summary>
        /// 명중률 등 추가 세부 정보를 두고 싶으면 여기에 필드 추가 가능.
        /// 예) 0.8이면 80% 확률로 적용
        /// </summary>
        [Range(0f, 1f)]
        public float Accuracy = 1.0f;
        
        [Range(0f, 1f)]
        public float CritChance = 0.0f;

        // 필요하면 지속턴(Duration), 치명타확률, 상태이상확률 등등 추가.
    }
}