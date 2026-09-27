using UnityEngine;

namespace Game.Battle
{
    [CreateAssetMenu(menuName = "Battle/Appearance", fileName = "BattleAppearance")]
    public class BattleAppearance : ScriptableObject
    {
        public Sprite sprite;
        public Vector2 footPoint = new Vector2(0.5f, 0f);
        [Min(0.01f)] public float scale = 1f;

#if UNITY_5_3_OR_NEWER
        public RuntimeAnimatorController battleAnimatorController;
#endif

        public Sprite[] attackSprites;
        public float[] attackFrameSeconds;
        [Min(0)] public int attackImpactFrame;

        public Sprite[] hurtSprites;
        public float[] hurtFrameSeconds;

        public Sprite[] defeatSprites;
        public float[] defeatFrameSeconds;

        public bool HasAttackAnimation
        {
            get { return attackSprites != null && attackSprites.Length > 0; }
        }

        public float GetAttackFrameSeconds(int frameIndex, float fallback)
        {
            if (attackFrameSeconds == null || frameIndex < 0 ||
                frameIndex >= attackFrameSeconds.Length)
                return fallback;
            return Mathf.Max(0f, attackFrameSeconds[frameIndex]);
        }

        public bool HasHurtAnimation
        {
            get { return hurtSprites != null && hurtSprites.Length > 0; }
        }

        public float GetHurtFrameSeconds(int frameIndex, float fallback)
        {
            if (hurtFrameSeconds == null || frameIndex < 0 ||
                frameIndex >= hurtFrameSeconds.Length)
                return fallback;
            return Mathf.Max(0f, hurtFrameSeconds[frameIndex]);
        }

        public bool HasDefeatAnimation
        {
            get { return defeatSprites != null && defeatSprites.Length > 0; }
        }

        public float GetDefeatFrameSeconds(int frameIndex, float fallback)
        {
            if (defeatFrameSeconds == null || frameIndex < 0 ||
                frameIndex >= defeatFrameSeconds.Length)
                return fallback;
            return Mathf.Max(0f, defeatFrameSeconds[frameIndex]);
        }
    }
}
