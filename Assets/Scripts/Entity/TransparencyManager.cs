using System.Collections;
using UnityEngine;
using static GameSystem.GameConstants.UI;

namespace Entity
{
    public enum TransparencyState
    {
        Normal = 0,
        Inactive = 1,
        Charging = 2,
        Stunned = 3,
        Stealth = 4,
        TargetingInvalid = 5,
        TargetingHover = 6,
        Dead = 7
    }

    public class TransparencyManager : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private TransparencyState currentState = TransparencyState.Normal;
        private Coroutine transitionCoroutine;
        private Coroutine pulseCoroutine;
        
        private Color baseColor = Color.white;
        private Color originalColor = Color.white;
        private float targetAlpha = 1f;
        private bool isInDamageFlash = false;
        
        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
                originalColor = spriteRenderer.color;
            }
        }
        
        public void SetState(TransparencyState state, bool immediate = false)
        {
            if (currentState == state && !immediate) return;
            
            currentState = state;
            float newAlpha = GetAlphaForState(state);
            Color tintColor = GetTintForState(state);
            
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }
            
            if (state == TransparencyState.Charging)
            {
                pulseCoroutine = StartCoroutine(PulseAlpha());
            }
            else
            {
                SetAlpha(newAlpha, tintColor, immediate);
            }
        }
        
        private float GetAlphaForState(TransparencyState state)
        {
            switch (state)
            {
                case TransparencyState.Dead:
                    return DEAD_SPRITE_ALPHA;
                case TransparencyState.Stealth:
                    return STEALTH_ALPHA;
                case TransparencyState.Stunned:
                    return STUNNED_ALPHA;
                case TransparencyState.Inactive:
                    return INACTIVE_SPRITE_ALPHA;
                case TransparencyState.TargetingInvalid:
                    return TARGETING_INVALID_ALPHA;
                case TransparencyState.Charging:
                    return CHARGING_ALPHA_MIN;
                case TransparencyState.Normal:
                case TransparencyState.TargetingHover:
                default:
                    return ACTIVE_SPRITE_ALPHA;
            }
        }
        
        private Color GetTintForState(TransparencyState state)
        {
            switch (state)
            {
                case TransparencyState.TargetingHover:
                    return HOVER_TINT_COLOR;
                case TransparencyState.Stunned:
                    return new Color(0.7f, 0.7f, 0.7f, 1f); // Gray tint
                default:
                    return Color.white;
            }
        }
        
        private void SetAlpha(float alpha, Color tint, bool immediate)
        {
            if (spriteRenderer == null) return;
            
            targetAlpha = alpha;
            Color targetColor = baseColor * tint;
            targetColor.a = alpha;
            
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            
            if (immediate || isInDamageFlash)
            {
                spriteRenderer.color = targetColor;
                originalColor = targetColor;
            }
            else
            {
                transitionCoroutine = StartCoroutine(TransitionAlpha(targetColor));
            }
        }
        
        private IEnumerator TransitionAlpha(Color targetColor)
        {
            Color startColor = spriteRenderer.color;
            float elapsed = 0f;
            
            while (elapsed < ALPHA_TRANSITION_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / ALPHA_TRANSITION_DURATION;
                spriteRenderer.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }
            
            spriteRenderer.color = targetColor;
            originalColor = targetColor;
            transitionCoroutine = null;
        }
        
        private IEnumerator PulseAlpha()
        {
            while (currentState == TransparencyState.Charging)
            {
                float t = Mathf.PingPong(Time.time * 2f, 1f);
                float alpha = Mathf.Lerp(CHARGING_ALPHA_MIN, CHARGING_ALPHA_MAX, t);
                
                Color color = spriteRenderer.color;
                color.a = alpha;
                spriteRenderer.color = color;
                originalColor = color;
                
                yield return null;
            }
        }
        
        public void StartDamageFlash()
        {
            isInDamageFlash = true;
        }
        
        public void EndDamageFlash()
        {
            isInDamageFlash = false;
            // Restore the correct state after damage flash
            SetState(currentState, true);
        }
        
        public TransparencyState GetCurrentState()
        {
            return currentState;
        }
        
        public static TransparencyState DetermineStateForEntity(BattleEntity entity, bool isActive, bool isTargetingMode = false, bool isValidTarget = false, bool isHovered = false)
        {
            if (!entity.IsAlive) return TransparencyState.Dead;
            if (isHovered && isValidTarget) return TransparencyState.TargetingHover;
            if (isTargetingMode && !isValidTarget) return TransparencyState.TargetingInvalid;
            if (entity.IsStealthed) return TransparencyState.Stealth;
            if (entity.IsStunned) return TransparencyState.Stunned;
            if (entity.IsCharging) return TransparencyState.Charging;
            if (!isActive && entity is not BossEntity) return TransparencyState.Inactive;
            return TransparencyState.Normal;
        }
    }
}