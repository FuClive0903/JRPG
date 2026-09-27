using System;
using System.Collections;
using UnityEngine;

namespace Game.Battle
{
    public class BattleActorView : MonoBehaviour
    {
        private Combatant combatant;
        [SerializeField] private BattleAppearance appearance;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer selectionMarker;
        [SerializeField, Min(0f)] private float actionSeconds = 0.25f;
        [SerializeField, Min(0f)] private float reactionSeconds = 0.2f;
        [SerializeField, Min(0f)] private float defeatSeconds = 0.35f;
        private BattleAppearance runtimeAppearance;
        private SpriteRenderer[] sprites;
        private Color[] originalColors;
        private Sprite idleSprite;
        private BattleAppearance playingAttackAppearance;
#if UNITY_5_3_OR_NEWER
        private const string IdleState = "Idle";
        private const string AttackState = "Attack";
        private const string HurtState = "Hurt";
        private const string DefeatState = "Defeat";
        private Animator battleAnimator;
        private bool animatorImpactReached;
        private bool animatorComplete;
        private bool animatorAttackPlaying;
#endif

        public void Setup(Combatant combatant, Action<Combatant> onClicked)
        {
            SetAppearance(Color.white, 1f);
            this.combatant = combatant;
            runtimeAppearance = null;
            if (combatant != null && !string.IsNullOrEmpty(combatant.BattleAppearanceResource))
            {
                runtimeAppearance = Resources.Load<BattleAppearance>(combatant.BattleAppearanceResource);
                if (runtimeAppearance == null)
                    Debug.LogWarning("Missing battle appearance: " +
                        combatant.BattleAppearanceResource, this);
            }
            ApplyAppearance();
#if UNITY_5_3_OR_NEWER
            ConfigureBattleAnimator(runtimeAppearance != null ? runtimeAppearance : appearance);
#endif
            sprites = bodyRenderer != null ? new[] { bodyRenderer }
                : GetComponentsInChildren<SpriteRenderer>(true);
            originalColors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                originalColors[i] = sprites[i].color;
            ResetPresentation();
            SetSelected(false);

            if (this.combatant == null)
            {
                return;
            }
        }

        [ContextMenu("Apply Appearance")]
        public void ApplyAppearance()
        {
            if (bodyRenderer == null)
                return;
            BattleAppearance activeAppearance = runtimeAppearance != null
                ? runtimeAppearance : appearance;
            if (activeAppearance != null)
            {
                bodyRenderer.sprite = activeAppearance.sprite;
                bodyRenderer.transform.localScale = Vector3.one *
                    Mathf.Max(0.01f, activeAppearance.scale);
            }
            Sprite sprite = bodyRenderer.sprite;
            if (sprite == null)
                return;

            Vector2 point = activeAppearance != null ? activeAppearance.footPoint
                : new Vector2(0.5f, 0f);
            Vector3 foot = new Vector3((sprite.rect.width * point.x - sprite.pivot.x) / sprite.pixelsPerUnit,
                (sprite.rect.height * point.y - sprite.pivot.y) / sprite.pixelsPerUnit, 0f);
            if (bodyRenderer.flipX) foot.x = -foot.x;
            if (bodyRenderer.flipY) foot.y = -foot.y;
            // BodySprite is a direct, unrotated child. Only its visual offset changes.
            Vector3 offset = -Vector3.Scale(foot, bodyRenderer.transform.localScale);
            offset.z = bodyRenderer.transform.localPosition.z;
            bodyRenderer.transform.localPosition = offset;
            idleSprite = bodyRenderer.sprite;
        }

        private void OnValidate()
        {
            ApplyAppearance();
        }

        public void SetSelected(bool selected)
        {
            if (selectionMarker != null)
                selectionMarker.enabled = selected;
        }

        // Replace these awaited routines with animation playback when art is ready.
        // They only present results; combat values are changed by BattleController.
        public virtual IEnumerator PlayAction(BattleCommandType command,
            BattleActionDefinition action)
        {
            BattleAppearance activeAppearance = runtimeAppearance != null
                ? runtimeAppearance : appearance;
#if UNITY_5_3_OR_NEWER
            if (command == BattleCommandType.Attack && HasBattleAnimator(activeAppearance))
            {
                animatorImpactReached = false;
                animatorComplete = false;
                animatorAttackPlaying = true;
                battleAnimator.Play(AttackState, 0, 0f);
                yield return null;
                while (animatorAttackPlaying && !animatorImpactReached && !animatorComplete &&
                    IsAnimatorStatePlaying(AttackState))
                    yield return null;
                yield break;
            }
#endif
            if (command == BattleCommandType.Attack && bodyRenderer != null &&
                activeAppearance != null && activeAppearance.HasAttackAnimation)
            {
                playingAttackAppearance = activeAppearance;
                int impactFrame = Math.Min(Math.Max(activeAppearance.attackImpactFrame, 0),
                    activeAppearance.attackSprites.Length - 1);
                yield return PlayAttackFrames(activeAppearance, 0, impactFrame);
                yield break;
            }

            yield return Flash(new Color(1f, 0.85f, 0.4f), actionSeconds);
        }

        public virtual IEnumerator PlayActionRecovery()
        {
#if UNITY_5_3_OR_NEWER
            if (animatorAttackPlaying)
            {
                while (!animatorComplete && IsAnimatorStatePlaying(AttackState))
                    yield return null;
                animatorAttackPlaying = false;
                PlayAnimatorState(IdleState);
                yield break;
            }
#endif
            if (playingAttackAppearance == null || bodyRenderer == null)
                yield break;

            BattleAppearance attackAppearance = playingAttackAppearance;
            int impactFrame = Math.Min(Math.Max(attackAppearance.attackImpactFrame, 0),
                attackAppearance.attackSprites.Length - 1);
            yield return PlayAttackFrames(attackAppearance, impactFrame + 1,
                attackAppearance.attackSprites.Length - 1);
            bodyRenderer.sprite = idleSprite;
            playingAttackAppearance = null;
        }

        private IEnumerator PlayAttackFrames(BattleAppearance attackAppearance,
            int firstFrame, int lastFrame)
        {
            float fallback = attackAppearance.attackSprites.Length > 0
                ? actionSeconds / attackAppearance.attackSprites.Length : 0f;
            for (int i = firstFrame; i <= lastFrame; i++)
            {
                Sprite frame = attackAppearance.attackSprites[i];
                if (frame != null)
                    bodyRenderer.sprite = frame;

                float seconds = attackAppearance.GetAttackFrameSeconds(i, fallback);
                float elapsed = 0f;
                while (elapsed < seconds)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }

        public virtual IEnumerator PlayReaction(float healthChange, BattleNumberStyle numberStyle)
        {
            BattleAppearance activeAppearance = runtimeAppearance != null
                ? runtimeAppearance : appearance;
#if UNITY_5_3_OR_NEWER
            if (healthChange < 0f && numberStyle != BattleNumberStyle.Poison &&
                HasBattleAnimator(activeAppearance))
            {
                animatorComplete = false;
                battleAnimator.Play(HurtState, 0, 0f);
                yield return null;
                while (!animatorComplete && IsAnimatorStatePlaying(HurtState))
                    yield return null;
                PlayAnimatorState(IdleState);
                yield break;
            }
#endif
            if (healthChange < 0f && numberStyle != BattleNumberStyle.Poison &&
                bodyRenderer != null && activeAppearance != null &&
                activeAppearance.HasHurtAnimation)
            {
                float fallback = reactionSeconds / activeAppearance.hurtSprites.Length;
                for (int i = 0; i < activeAppearance.hurtSprites.Length; i++)
                {
                    Sprite frame = activeAppearance.hurtSprites[i];
                    if (frame != null)
                        bodyRenderer.sprite = frame;

                    float seconds = activeAppearance.GetHurtFrameSeconds(i, fallback);
                    float frameElapsed = 0f;
                    while (frameElapsed < seconds)
                    {
                        frameElapsed += Time.deltaTime;
                        yield return null;
                    }
                }

                bodyRenderer.sprite = idleSprite;
                yield break;
            }

            yield return Flash(healthChange < 0f ? new Color(1f, 0.35f, 0.35f)
                : healthChange > 0f ? new Color(0.4f, 1f, 0.5f)
                : new Color(0.5f, 0.8f, 1f), reactionSeconds);
        }

        public virtual IEnumerator PlayDefeat()
        {
            BattleAppearance activeAppearance = runtimeAppearance != null
                ? runtimeAppearance : appearance;
#if UNITY_5_3_OR_NEWER
            if (HasBattleAnimator(activeAppearance))
            {
                animatorComplete = false;
                animatorAttackPlaying = false;
                battleAnimator.Play(DefeatState, 0, 0f);
                yield return null;
                while (!animatorComplete && IsAnimatorStatePlaying(DefeatState))
                    yield return null;
                yield break;
            }
#endif
            if (bodyRenderer != null && activeAppearance != null &&
                activeAppearance.HasDefeatAnimation)
            {
                SetAppearance(Color.white, 1f);
                float fallback = defeatSeconds / activeAppearance.defeatSprites.Length;
                for (int i = 0; i < activeAppearance.defeatSprites.Length; i++)
                {
                    Sprite frame = activeAppearance.defeatSprites[i];
                    if (frame != null)
                        bodyRenderer.sprite = frame;

                    float seconds = activeAppearance.GetDefeatFrameSeconds(i, fallback);
                    float frameElapsed = 0f;
                    while (frameElapsed < seconds)
                    {
                        frameElapsed += Time.deltaTime;
                        yield return null;
                    }
                }

                // Keep the corpse visible on the final frame until the battle scene ends.
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < defeatSeconds)
            {
                SetAppearance(Color.white, 1f - elapsed / defeatSeconds);
                elapsed += Time.deltaTime;
                yield return null;
            }
            SetAppearance(Color.white, 0f);
        }

        private IEnumerator Flash(Color tint, float seconds)
        {
            SetAppearance(tint, 1f);
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            SetAppearance(Color.white, 1f);
        }

        private void SetAppearance(Color tint, float alpha)
        {
            if (sprites == null)
                return;

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                    continue;
                Color color = originalColors[i] * tint;
                color.a = originalColors[i].a * alpha;
                sprites[i].color = color;
            }
        }

        public virtual void ResetPresentation()
        {
            if (bodyRenderer != null && idleSprite != null)
                bodyRenderer.sprite = idleSprite;
            playingAttackAppearance = null;
#if UNITY_5_3_OR_NEWER
            animatorAttackPlaying = false;
            animatorImpactReached = false;
            animatorComplete = false;
            PlayAnimatorState(IdleState);
#endif
            SetAppearance(Color.white, combatant == null || combatant.IsAlive() ? 1f : 0f);
        }

#if UNITY_5_3_OR_NEWER
        // Called by Animation Events stored in Battle Animator clips.
        public void OnBattleAnimationImpact()
        {
            animatorImpactReached = true;
        }

        // Called by Attack, Hurt and Defeat clips on their final frame.
        public void OnBattleAnimationComplete()
        {
            animatorComplete = true;
        }

        private void ConfigureBattleAnimator(BattleAppearance activeAppearance)
        {
            RuntimeAnimatorController controller = activeAppearance != null
                ? activeAppearance.battleAnimatorController : null;
            if (controller == null)
            {
                if (battleAnimator != null)
                    battleAnimator.enabled = false;
                return;
            }

            if (battleAnimator == null)
                battleAnimator = GetComponent<Animator>();
            if (battleAnimator == null)
                battleAnimator = gameObject.AddComponent<Animator>();

            battleAnimator.enabled = true;
            battleAnimator.applyRootMotion = false;
            battleAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (battleAnimator.runtimeAnimatorController != controller)
            {
                battleAnimator.runtimeAnimatorController = controller;
                battleAnimator.Rebind();
            }
            battleAnimator.Play(IdleState, 0, 0f);
            battleAnimator.Update(0f);
        }

        private bool HasBattleAnimator(BattleAppearance activeAppearance)
        {
            return bodyRenderer != null && battleAnimator != null && battleAnimator.enabled &&
                activeAppearance != null && activeAppearance.battleAnimatorController != null;
        }

        private bool IsAnimatorStatePlaying(string stateName)
        {
            if (battleAnimator == null || !battleAnimator.enabled ||
                !battleAnimator.gameObject.activeInHierarchy)
                return false;
            AnimatorStateInfo state = battleAnimator.GetCurrentAnimatorStateInfo(0);
            return state.IsName(stateName) && state.normalizedTime < 1f;
        }

        private void PlayAnimatorState(string stateName)
        {
            if (battleAnimator != null && battleAnimator.enabled &&
                battleAnimator.gameObject.activeInHierarchy)
                battleAnimator.Play(stateName, 0, 0f);
        }
#endif

        private void OnDisable()
        {
            ResetPresentation();
            SetSelected(false);
        }

    }
}
