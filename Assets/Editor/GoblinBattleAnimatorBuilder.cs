using System;
using System.Collections.Generic;
using System.IO;
using Game.Battle;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class GoblinBattleAnimatorBuilder
{
    private const string BattleFolder = "Assets/Animations/Battle";
    private const string BaseClipFolder = BattleFolder + "/Base";
    private const string OutputFolder = BattleFolder + "/Goblin";
    private const string BaseControllerPath = BattleFolder + "/Battle_Base.controller";
    private const string OverrideControllerPath = OutputFolder + "/Goblin_Battle.overrideController";
    private const string LegacyControllerPath = OutputFolder + "/Goblin_Battle.controller";
    private const string AppearancePath =
        "Assets/Resources/Battle/Appearances/GoblinBattleAppearance.asset";
    private const string SpriteRoot =
        "Assets/Art/Sprites/Enemies/Goblin/PixelLab";
    private const string RendererPath = "BodySprite";

    private static readonly float[] AttackDurations =
    {
        0.11f, 0.095f, 0.095f, 0.105f, 0.13f, 0.085f, 0.055f, 0.055f, 0.055f,
        0.055f, 0f, 0.09f, 0.09f, 0.09f, 0.09f, 0.1f, 0.14f
    };

    private static readonly float[] HurtDurations =
    {
        0.08f, 0.05f, 0.06f, 0.12f, 0.08f
    };

    private static readonly float[] DefeatDurations =
    {
        0.04f, 0.04f, 0.05f, 0.06f, 0.09f, 0.045f, 0.045f, 0.045f, 0.045f,
        0.045f, 0.07f, 0.1f, 0.12f, 0.14f, 0.09f, 0.06f, 0.35f
    };

    [InitializeOnLoadMethod]
    private static void BuildWhenMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath) == null ||
            AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverrideControllerPath) == null)
            EditorApplication.delayCall += Build;
    }

    [MenuItem("Tools/Battle/Rebuild Goblin Battle Animator")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Directory.CreateDirectory(BaseClipFolder);
        Directory.CreateDirectory(OutputFolder);
        AssetDatabase.Refresh();

        Sprite idle = LoadSprite(SpriteRoot + "/Goblin_West.png");
        Sprite[] attack = LoadNumberedSprites(
            SpriteRoot + "/Animations/ClubAttack_West/Goblin_ClubAttack_West_", 0, 16, 2);
        Sprite[] hurt = LoadNumberedSprites(
            SpriteRoot + "/Animations/Hurt/Goblin_Hurt", 1, 5, 0);
        Sprite[] defeat = LoadNumberedSprites(
            SpriteRoot + "/Animations/Death/Goblin_Death", 1, 17, 0);

        AnimationClip idleClip = CreateClip("Goblin_Idle", new[] { idle }, new[] { 0.1f });
        AnimationClip attackClip = CreateClip("Goblin_Attack", attack, AttackDurations,
            SumThrough(AttackDurations, 9));
        AnimationClip hurtClip = CreateClip("Goblin_Hurt", hurt, HurtDurations);
        AnimationClip defeatClip = CreateClip("Goblin_Defeat", defeat, DefeatDurations);

        AnimationClip baseIdle = CreatePlaceholderClip("Battle_Idle");
        AnimationClip baseAttack = CreatePlaceholderClip("Battle_Attack");
        AnimationClip baseHurt = CreatePlaceholderClip("Battle_Hurt");
        AnimationClip baseDefeat = CreatePlaceholderClip("Battle_Defeat");

        AnimatorController baseController = GetOrCreateBaseController();
        ConfigureBaseStates(baseController, baseIdle, baseAttack, baseHurt, baseDefeat);
        AnimatorOverrideController overrideController = GetOrCreateOverrideController(baseController);
        ApplyGoblinOverrides(overrideController, baseIdle, idleClip, baseAttack, attackClip,
            baseHurt, hurtClip, baseDefeat, defeatClip);

        BattleAppearance appearance = AssetDatabase.LoadAssetAtPath<BattleAppearance>(AppearancePath);
        if (appearance == null)
            throw new InvalidOperationException("Missing Goblin battle appearance: " + AppearancePath);

        appearance.battleAnimatorController = overrideController;
        appearance.attackSprites = Array.Empty<Sprite>();
        appearance.attackFrameSeconds = Array.Empty<float>();
        appearance.hurtSprites = Array.Empty<Sprite>();
        appearance.hurtFrameSeconds = Array.Empty<float>();
        appearance.defeatSprites = Array.Empty<Sprite>();
        appearance.defeatFrameSeconds = Array.Empty<float>();

        EditorUtility.SetDirty(baseController);
        EditorUtility.SetDirty(overrideController);
        EditorUtility.SetDirty(appearance);
        AssetDatabase.SaveAssets();
        Debug.Log("Goblin Battle Animator rebuilt: " + OverrideControllerPath);
    }

    private static AnimatorController GetOrCreateBaseController()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
        if (controller != null)
            return controller;

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(LegacyControllerPath) != null)
        {
            string error = AssetDatabase.MoveAsset(LegacyControllerPath, BaseControllerPath);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
        }

        return AnimatorController.CreateAnimatorControllerAtPath(BaseControllerPath);
    }

    private static void ConfigureBaseStates(AnimatorController controller, AnimationClip idle,
        AnimationClip attack, AnimationClip hurt, AnimationClip defeat)
    {
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = GetOrCreateState(stateMachine, "Idle");
        idleState.motion = idle;
        stateMachine.defaultState = idleState;
        GetOrCreateState(stateMachine, "Attack").motion = attack;
        GetOrCreateState(stateMachine, "Hurt").motion = hurt;
        GetOrCreateState(stateMachine, "Defeat").motion = defeat;
    }

    private static AnimatorState GetOrCreateState(AnimatorStateMachine stateMachine, string name)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
            if (child.state.name == name)
                return child.state;
        return stateMachine.AddState(name);
    }

    private static AnimatorOverrideController GetOrCreateOverrideController(
        AnimatorController baseController)
    {
        AnimatorOverrideController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverrideControllerPath);
        if (controller == null)
        {
            controller = new AnimatorOverrideController(baseController);
            AssetDatabase.CreateAsset(controller, OverrideControllerPath);
        }
        else
        {
            controller.runtimeAnimatorController = baseController;
        }
        return controller;
    }

    private static void ApplyGoblinOverrides(AnimatorOverrideController controller,
        AnimationClip baseIdle, AnimationClip idle, AnimationClip baseAttack, AnimationClip attack,
        AnimationClip baseHurt, AnimationClip hurt, AnimationClip baseDefeat, AnimationClip defeat)
    {
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>
        {
            new KeyValuePair<AnimationClip, AnimationClip>(baseIdle, idle),
            new KeyValuePair<AnimationClip, AnimationClip>(baseAttack, attack),
            new KeyValuePair<AnimationClip, AnimationClip>(baseHurt, hurt),
            new KeyValuePair<AnimationClip, AnimationClip>(baseDefeat, defeat)
        };
        controller.ApplyOverrides(overrides);
    }

    private static AnimationClip CreatePlaceholderClip(string name)
    {
        string path = BaseClipFolder + "/" + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.name = name;
        clip.frameRate = 60f;
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationClip CreateClip(string name, Sprite[] sprites, float[] durations,
        float impactTime = -1f)
    {
        if (sprites.Length != durations.Length)
            throw new InvalidOperationException(name + " sprite and duration counts do not match.");

        string path = OutputFolder + "/" + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.name = name;
        clip.frameRate = 60f;
        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = RendererPath,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };

        var keys = new List<ObjectReferenceKeyframe>();
        float time = 0f;
        Sprite lastVisibleSprite = null;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
                throw new InvalidOperationException(name + " is missing sprite " + i + ".");

            float duration = Mathf.Max(0f, durations[i]);
            if (duration > 0f || i == 0)
            {
                keys.Add(new ObjectReferenceKeyframe { time = time, value = sprites[i] });
                lastVisibleSprite = sprites[i];
            }
            time += duration;
        }

        keys.Add(new ObjectReferenceKeyframe { time = time, value = lastVisibleSprite });
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());

        var events = new List<AnimationEvent>();
        if (impactTime >= 0f)
        {
            events.Add(new AnimationEvent
            {
                time = Mathf.Min(impactTime, time),
                functionName = "OnBattleAnimationImpact"
            });
        }
        events.Add(new AnimationEvent
        {
            time = Mathf.Max(0f, time - 0.001f),
            functionName = "OnBattleAnimationComplete"
        });
        AnimationUtility.SetAnimationEvents(clip, events.ToArray());
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static Sprite[] LoadNumberedSprites(string prefix, int first, int last, int digits)
    {
        var sprites = new Sprite[last - first + 1];
        for (int number = first; number <= last; number++)
        {
            string suffix = digits > 0 ? number.ToString("D" + digits) : number.ToString();
            sprites[number - first] = LoadSprite(prefix + suffix + ".png");
        }
        return sprites;
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new InvalidOperationException("Missing sprite: " + path);
        return sprite;
    }

    private static float SumThrough(float[] durations, int lastIndex)
    {
        float total = 0f;
        for (int i = 0; i <= lastIndex && i < durations.Length; i++)
            total += Mathf.Max(0f, durations[i]);
        return total;
    }
}
