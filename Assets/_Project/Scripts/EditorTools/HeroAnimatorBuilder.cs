using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ChibiRift.Data;
using ChibiRift.Gameplay;

namespace ChibiRift.EditorTools
{
    /// <summary>
    /// Builds the ten hero clips and the Animator Controller that plays them (P2 slice 1: art
    /// pipeline, A3). Runs only once <c>Hero_0</c> through <c>Hero_44</c> exist — before that, one
    /// warning and no-op, leaving the placeholder hero exactly as it was.
    /// </summary>
    /// <remarks>
    /// <para><b>The frame table is fixed, the timing is not.</b> Which 45 sprites become which clip
    /// is a constant here (the brief's table). How long the three attack clips take is read from
    /// <c>ATK_KnightBasic.asset</c> at build time — each attack clip's sample rate is set to exactly
    /// its own frame count divided by that step's <c>TotalDuration</c>, so the clip always matches
    /// the combo's real timing without the number living in two places. That is also why Attack1/2
    /// (6 frames / 0.30s = 20fps) and Attack3 (8 frames / 0.45s ≈ 17.78fps) do not share one rate:
    /// forcing a single rate across all three could only satisfy one exactly (see OI-18, COM-004).
    /// Idle/Run/Jump/Fall/Dash/Hurt/Death have no such constraint and stay at README's declared
    /// 12fps.</para>
    ///
    /// <para><b>Animation Events, not code, carry the hitbox window</b> (COM-004): each attack
    /// clip's <c>OnAttackActiveStart</c>/<c>OnAttackActiveEnd</c> events are placed at exactly
    /// <c>ActiveStartTime</c>/<c>ActiveEndTime</c> from the same asset — OI-18's migration plan
    /// completed without the seconds ever moving into a script.</para>
    /// </remarks>
    public static class HeroAnimatorBuilder
    {
        private const string SheetPath = "Assets/_Project/Art/Characters/Hero/Hero_Spritesheet.png";
        private const string AnimFolder = "Assets/_Project/Art/Characters/Hero/Animations";
        private const string ControllerPath = AnimFolder + "/Hero.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Hero.prefab";
        private const string DataRoot = "Assets/_Project/Data";

        private const int TotalFrames = 45; // Hero_0 .. Hero_44
        private const float DefaultSampleRate = 12f; // README section 2.
        private const float SpeedDeadZone = 0.05f;

        private static bool s_warnedMissing;

        [MenuItem("ChibiRift/Setup/8. Build Hero Animator")]
        public static void Build()
        {
            Sprite[] frames = LoadFrames();
            if (frames == null) return; // already warned

            var attack = AssetDatabase.LoadAssetAtPath<AttackData>($"{DataRoot}/ATK_KnightBasic.asset");
            if (attack == null || attack.StepCount < 3)
            {
                WarnOnce($"[Setup] {DataRoot}/ATK_KnightBasic.asset is missing or has fewer than 3 " +
                         "steps; Hero Animator not built.");
                return;
            }

            Directory.CreateDirectory(AnimFolder);
            AssetDatabase.Refresh();

            AnimationClip idle = BuildClip("Idle", frames, 0, 3, true, DefaultSampleRate);
            AnimationClip run = BuildClip("Run", frames, 4, 9, true, DefaultSampleRate);
            AnimationClip jump = BuildClip("Jump", frames, 10, 11, false, DefaultSampleRate);
            AnimationClip fall = BuildClip("Fall", frames, 12, 13, true, DefaultSampleRate);
            AnimationClip dash = BuildClip("Dash", frames, 14, 16, false, DefaultSampleRate);
            AnimationClip attack1 = BuildAttackClip("Attack1", frames, 17, 22, attack.GetStep(0));
            AnimationClip attack2 = BuildAttackClip("Attack2", frames, 23, 28, attack.GetStep(1));
            AnimationClip attack3 = BuildAttackClip("Attack3", frames, 29, 36, attack.GetStep(2));
            AnimationClip hurt = BuildClip("Hurt", frames, 37, 38, false, DefaultSampleRate);
            AnimationClip death = BuildClip("Death", frames, 39, 44, false, DefaultSampleRate);

            AnimatorController controller = BuildController(
                idle, run, jump, fall, dash, attack1, attack2, attack3, hurt, death);

            WireHeroData(controller);
            WireHeroPrefab(controller, attack1, attack2, attack3);

            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Hero Animator built.");
        }

        private static Sprite[] LoadFrames()
        {
            if (!File.Exists(SheetPath))
            {
                WarnOnce($"[Setup] {SheetPath} not found; Hero Animator not built (keeping the " +
                         "placeholder sprite).");
                return null;
            }

            var byName = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
            {
                if (asset is Sprite sprite) byName[sprite.name] = sprite;
            }

            var frames = new Sprite[TotalFrames];
            for (int i = 0; i < TotalFrames; i++)
            {
                if (!byName.TryGetValue($"Hero_{i}", out Sprite sprite))
                {
                    WarnOnce($"[Setup] {SheetPath} has no sprite named 'Hero_{i}' (expected " +
                             $"{TotalFrames} frames, Hero_0..Hero_{TotalFrames - 1}); Hero Animator not built.");
                    return null;
                }

                frames[i] = sprite;
            }

            return frames;
        }

        private static void WarnOnce(string message)
        {
            if (s_warnedMissing) return;
            s_warnedMissing = true;
            Debug.LogWarning(message);
        }

        private static AnimationClip BuildClip(
            string name, Sprite[] frames, int startFrame, int endFrameInclusive, bool loop, float sampleRate)
        {
            var slice = new Sprite[endFrameInclusive - startFrame + 1];
            System.Array.Copy(frames, startFrame, slice, 0, slice.Length);

            var clip = new AnimationClip { frameRate = sampleRate };
            SetSpriteCurve(clip, slice, sampleRate);
            SetLoop(clip, loop);

            string path = $"{AnimFolder}/{name}.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static AnimationClip BuildAttackClip(
            string name, Sprite[] frames, int startFrame, int endFrameInclusive, AttackStep step)
        {
            int frameCount = endFrameInclusive - startFrame + 1;
            float sampleRate = frameCount / step.TotalDuration;

            AnimationClip clip = BuildClip(name, frames, startFrame, endFrameInclusive, false, sampleRate);

            AnimationUtility.SetAnimationEvents(clip, new[]
            {
                new AnimationEvent { time = step.ActiveStartTime, functionName = "OnAttackActiveStart" },
                new AnimationEvent { time = step.ActiveEndTime, functionName = "OnAttackActiveEnd" }
            });

            return clip;
        }

        private static void SetSpriteCurve(AnimationClip clip, Sprite[] frames, float sampleRate)
        {
            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            var keyframes = new ObjectReferenceKeyframe[frames.Length];

            for (int i = 0; i < frames.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe { time = i / sampleRate, value = frames[i] };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        /// <summary>
        /// Gameplay decides state; this machine only ever reacts to the parameters
        /// <see cref="PlayerAnimatorDriver"/> sets — no condition here can change what the hero
        /// does, only how it looks (A3's "must not drive gameplay" rule).
        /// </summary>
        private static AnimatorController BuildController(
            AnimationClip idle, AnimationClip run, AnimationClip jump, AnimationClip fall, AnimationClip dash,
            AnimationClip attack1, AnimationClip attack2, AnimationClip attack3,
            AnimationClip hurt, AnimationClip death)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDashing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("ComboStep", AnimatorControllerParameterType.Int);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState idleState = AddState(sm, "Idle", idle);
            sm.defaultState = idleState;
            AnimatorState runState = AddState(sm, "Run", run);
            AnimatorState jumpState = AddState(sm, "Jump", jump);
            AnimatorState fallState = AddState(sm, "Fall", fall);
            AnimatorState dashState = AddState(sm, "Dash", dash);
            AnimatorState attack1State = AddState(sm, "Attack1", attack1);
            AnimatorState attack2State = AddState(sm, "Attack2", attack2);
            AnimatorState attack3State = AddState(sm, "Attack3", attack3);
            AnimatorState hurtState = AddState(sm, "Hurt", hurt);
            AnimatorState deathState = AddState(sm, "Death", death);

            // Grounded locomotion.
            OneCondition(idleState.AddTransition(runState), AnimatorConditionMode.Greater, SpeedDeadZone, "Speed");
            OneCondition(runState.AddTransition(idleState), AnimatorConditionMode.Less, SpeedDeadZone, "Speed");

            // Walking off a ledge with no jump: straight to Fall.
            OneCondition(idleState.AddTransition(fallState), AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            OneCondition(runState.AddTransition(fallState), AnimatorConditionMode.IfNot, 0f, "IsGrounded");

            TwoConditions(fallState.AddTransition(idleState),
                AnimatorConditionMode.If, 0f, "IsGrounded", AnimatorConditionMode.Less, SpeedDeadZone, "Speed");
            TwoConditions(fallState.AddTransition(runState),
                AnimatorConditionMode.If, 0f, "IsGrounded", AnimatorConditionMode.Greater, SpeedDeadZone, "Speed");

            // Jump plays out, then falls — Jump's own length governs when Fall takes over, not a
            // second parameter guessing at apex.
            ExitTime(jumpState.AddTransition(fallState));

            // Any State, in priority order (Unity checks a state's — and Any State's — outgoing
            // transitions top to bottom): Death first, since nothing may interrupt it once it fires.
            OneCondition(sm.AddAnyStateTransition(deathState), AnimatorConditionMode.If, 0f, "Death");
            OneCondition(sm.AddAnyStateTransition(hurtState), AnimatorConditionMode.If, 0f, "Hurt");
            OneCondition(sm.AddAnyStateTransition(jumpState), AnimatorConditionMode.If, 0f, "Jump");
            OneCondition(sm.AddAnyStateTransition(dashState), AnimatorConditionMode.If, 0f, "IsDashing");

            TwoConditions(sm.AddAnyStateTransition(attack1State),
                AnimatorConditionMode.If, 0f, "Attack", AnimatorConditionMode.Equals, 1f, "ComboStep");
            TwoConditions(sm.AddAnyStateTransition(attack2State),
                AnimatorConditionMode.If, 0f, "Attack", AnimatorConditionMode.Equals, 2f, "ComboStep");
            TwoConditions(sm.AddAnyStateTransition(attack3State),
                AnimatorConditionMode.If, 0f, "Attack", AnimatorConditionMode.Equals, 3f, "ComboStep");

            OneCondition(dashState.AddTransition(idleState), AnimatorConditionMode.IfNot, 0f, "IsDashing");

            ExitTime(attack1State.AddTransition(idleState));
            ExitTime(attack2State.AddTransition(idleState));
            ExitTime(attack3State.AddTransition(idleState));
            ExitTime(hurtState.AddTransition(idleState));

            return controller;
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            AnimatorState state = sm.AddState(name);
            state.motion = clip;
            return state;
        }

        private static void OneCondition(
            AnimatorStateTransition transition, AnimatorConditionMode mode, float threshold, string parameter)
        {
            transition.AddCondition(mode, threshold, parameter);
            NoBlend(transition);
        }

        private static void TwoConditions(
            AnimatorStateTransition transition,
            AnimatorConditionMode modeA, float thresholdA, string parameterA,
            AnimatorConditionMode modeB, float thresholdB, string parameterB)
        {
            transition.AddCondition(modeA, thresholdA, parameterA);
            transition.AddCondition(modeB, thresholdB, parameterB);
            NoBlend(transition);
        }

        /// <summary>No cross-fade: gameplay already dictates the exact frame these snap on.</summary>
        private static void NoBlend(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.duration = 0f;
        }

        private static void ExitTime(AnimatorStateTransition transition)
        {
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
        }

        private static void WireHeroData(AnimatorController controller)
        {
            var hero = AssetDatabase.LoadAssetAtPath<HeroData>($"{DataRoot}/HERO_Knight.asset");
            if (hero == null) return;

            var so = new SerializedObject(hero);
            so.FindProperty("_animatorController").objectReferenceValue = controller;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireHeroPrefab(
            AnimatorController controller, AnimationClip attack1, AnimationClip attack2, AnimationClip attack3)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (contents == null)
            {
                WarnOnce($"[Setup] {PrefabPath} not found; Animator not attached to the hero prefab.");
                return;
            }

            var animator = contents.GetComponent<Animator>();
            if (animator == null) animator = contents.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            if (contents.GetComponent<PlayerAnimatorDriver>() == null)
                contents.AddComponent<PlayerAnimatorDriver>();

            // PlayerCombat only reads these to check for hitbox events (COM-004) — never plays
            // them — so it needs the clips directly rather than a RuntimeAnimatorController, which
            // Gameplay code has no other reason to reference.
            var combat = contents.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                var combatSo = new SerializedObject(combat);
                SerializedProperty clips = combatSo.FindProperty("_attackClips");
                clips.arraySize = 3;
                clips.GetArrayElementAtIndex(0).objectReferenceValue = attack1;
                clips.GetArrayElementAtIndex(1).objectReferenceValue = attack2;
                clips.GetArrayElementAtIndex(2).objectReferenceValue = attack3;
                combatSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
