using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

/// <summary>
/// Editor script to automatically build a clean, layered Animator Controller
/// for humanoid characters with combat, spells, and locomotion.
/// 
/// Usage: Place in Editor folder, then Tools > NinjaGame > Build Humanoid Animator
/// </summary>
public class AnimatorControllerBuilder : EditorWindow
{
    private AnimatorController controller;
    private string controllerPath = "Assets/Animations/Controllers/HumanoidAnimator.controller";
    private Avatar humanoidAvatar;

    [MenuItem("Tools/NinjaGame/Build Humanoid Animator")]
    static void Init()
    {
        AnimatorControllerBuilder window = (AnimatorControllerBuilder)EditorWindow.GetWindow(typeof(AnimatorControllerBuilder));
        window.titleContent = new GUIContent("Animator Builder");
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Humanoid Animator Controller Builder", EditorStyles.boldLabel);
        GUILayout.Space(10);

        controllerPath = EditorGUILayout.TextField("Controller Path:", controllerPath);
        humanoidAvatar = (Avatar)EditorGUILayout.ObjectField("Humanoid Avatar:", humanoidAvatar, typeof(Avatar), false);

        GUILayout.Space(10);

        if (GUILayout.Button("Build Animator Controller", GUILayout.Height(40)))
        {
            BuildAnimatorController();
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "This will create a complete layered animator with:\n" +
            "- Layer 0: Locomotion (Free + Strafe)\n" +
            "- Layer 1: Full Body Combat\n" +
            "- Layer 2: Upper Body Spells\n" +
            "- Layer 3: Full Body Actions\n" +
            "- Layer 4: Reactions\n\n" +
            "Note: Animation clips must be added manually after creation.",
            MessageType.Info
        );
    }

    void BuildAnimatorController()
    {
        // Ensure directory exists
        string directory = System.IO.Path.GetDirectoryName(controllerPath);
        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
            Debug.Log($"Created directory: {directory}");
        }

        // Create controller
        controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        if (controller == null)
        {
            Debug.LogError("Failed to create Animator Controller!");
            return;
        }

        Debug.Log($"Creating Animator Controller at: {controllerPath}");

        // Setup parameters
        SetupParameters();

        // Remove default layer
        if (controller.layers.Length > 0)
        {
            controller.RemoveLayer(0);
        }

        // Build layers
        BuildLocomotionLayer();
        BuildCombatLayer();
        BuildSpellLayer();
        BuildActionsLayer();
        BuildReactionsLayer();

        // Save
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Animator Controller built successfully!");
        EditorGUIUtility.PingObject(controller);
    }

    void SetupParameters()
    {
        // Locomotion parameters (from LocomotionModule)
        AddParameter("MovementState", AnimatorControllerParameterType.Int);
        AddParameter("IsLockedOn", AnimatorControllerParameterType.Bool);
        AddParameter("StrafeX", AnimatorControllerParameterType.Float);
        AddParameter("StrafeY", AnimatorControllerParameterType.Float);
        AddParameter("MovementSpeed", AnimatorControllerParameterType.Float);
        AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);

        // Combat triggers
        AddParameter("BasicAttack1", AnimatorControllerParameterType.Trigger);
        AddParameter("BasicAttack2", AnimatorControllerParameterType.Trigger);
        AddParameter("BasicAttack3", AnimatorControllerParameterType.Trigger);
        AddParameter("Cleave", AnimatorControllerParameterType.Trigger);
        AddParameter("Whirlwind", AnimatorControllerParameterType.Trigger);
        AddParameter("Thrust", AnimatorControllerParameterType.Trigger);
        AddParameter("Slam", AnimatorControllerParameterType.Trigger);

        // Spell triggers
        AddParameter("CastProjectile", AnimatorControllerParameterType.Trigger);
        AddParameter("CastBeam", AnimatorControllerParameterType.Trigger);
        AddParameter("CastBreath", AnimatorControllerParameterType.Trigger);
        AddParameter("CastWave", AnimatorControllerParameterType.Trigger);

        // Action triggers
        AddParameter("JumpTrigger", AnimatorControllerParameterType.Trigger);
        AddParameter("DashTrigger", AnimatorControllerParameterType.Trigger);
        AddParameter("IsDashing", AnimatorControllerParameterType.Bool);

        // Reaction triggers
        AddParameter("HitLight", AnimatorControllerParameterType.Trigger);
        AddParameter("HitHeavy", AnimatorControllerParameterType.Trigger);
        AddParameter("Stagger", AnimatorControllerParameterType.Trigger);
        AddParameter("Death", AnimatorControllerParameterType.Trigger);
        AddParameter("IsDead", AnimatorControllerParameterType.Bool);

        Debug.Log($"Created {controller.parameters.Length} parameters");
    }

    void AddParameter(string name, AnimatorControllerParameterType type)
    {
        if (!HasParameter(name))
        {
            controller.AddParameter(name, type);
        }
    }

    bool HasParameter(string name)
    {
        foreach (var param in controller.parameters)
        {
            if (param.name == name) return true;
        }
        return false;
    }

    #region Layer 0: Locomotion

    void BuildLocomotionLayer()
    {
        var layer = CreateLayer("Locomotion", 1f);
        var stateMachine = layer.stateMachine;

        // Create sub-state machines
        var freeMovementSM = stateMachine.AddStateMachine("Free Movement", new Vector3(300, 0, 0));
        var strafeMovementSM = stateMachine.AddStateMachine("Strafe Movement", new Vector3(300, 150, 0));

        // Build Free Movement
        BuildFreeMovementStates(freeMovementSM);

        // Build Strafe Movement
        BuildStrafeMovementStates(strafeMovementSM);

        // Transitions between sub-state machines
        var toStrafe = stateMachine.AddStateMachineTransition(freeMovementSM, strafeMovementSM);
        toStrafe.AddCondition(AnimatorConditionMode.If, 0, "IsLockedOn");
        toStrafe.isExit = false;

        var toFree = stateMachine.AddStateMachineTransition(strafeMovementSM, freeMovementSM);
        toFree.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLockedOn");
        toFree.isExit = false;

        // Note: Default states are set within each sub-state machine

        Debug.Log("Built Locomotion Layer");
    }

    void BuildFreeMovementStates(AnimatorStateMachine sm)
    {
        // Create blend tree for free movement
        var blendTreeState = sm.AddState("Free Movement Blend", new Vector3(300, 100, 0));

        BlendTree blendTree = new BlendTree
        {
            name = "Free Movement",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "MovementSpeed",
            useAutomaticThresholds = false
        };

        // Add motion placeholders (user will replace with actual clips)
        blendTree.AddChild(null, 0.0f);   // Idle at 0.0
        blendTree.AddChild(null, 0.33f);  // Walk at 0.33
        blendTree.AddChild(null, 0.66f);  // Run at 0.66
        blendTree.AddChild(null, 1.0f);   // Sprint at 1.0

        // Assign blend tree to state
        blendTreeState.motion = blendTree;
        sm.defaultState = blendTreeState;

        AssetDatabase.AddObjectToAsset(blendTree, controller);
    }

    void BuildStrafeMovementStates(AnimatorStateMachine sm)
    {
        // Create 2D blend tree for strafe movement
        var blendTreeState = sm.AddState("Strafe Movement Blend", new Vector3(300, 100, 0));

        BlendTree blendTree = new BlendTree
        {
            name = "Strafe Movement",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = "StrafeX",
            blendParameterY = "StrafeY",
            useAutomaticThresholds = false
        };

        // Add 9 motion placeholders for 8-directional + idle
        // Center
        blendTree.AddChild(null, new Vector2(0f, 0f));    // Idle

        // Cardinal directions
        blendTree.AddChild(null, new Vector2(0f, 1f));    // Forward
        blendTree.AddChild(null, new Vector2(0f, -1f));   // Backward
        blendTree.AddChild(null, new Vector2(-1f, 0f));   // Left
        blendTree.AddChild(null, new Vector2(1f, 0f));    // Right

        // Diagonals
        blendTree.AddChild(null, new Vector2(-1f, 1f));   // Forward-Left
        blendTree.AddChild(null, new Vector2(1f, 1f));    // Forward-Right
        blendTree.AddChild(null, new Vector2(-1f, -1f));  // Backward-Left
        blendTree.AddChild(null, new Vector2(1f, -1f));   // Backward-Right

        blendTreeState.motion = blendTree;
        sm.defaultState = blendTreeState;

        AssetDatabase.AddObjectToAsset(blendTree, controller);
    }

    #endregion

    #region Layer 1: Full Body Combat

    void BuildCombatLayer()
    {
        var layer = CreateLayer("Full Body Combat", 1f, null, true); // Override
        var sm = layer.stateMachine;

        // Idle state (empty, returns to locomotion)
        var idle = sm.AddState("Combat Idle", new Vector3(300, 0, 0));
        sm.defaultState = idle;

        // Basic attacks
        var attack1 = CreateAttackState(sm, "BasicAttack1", new Vector3(300, 100, 0));
        var attack2 = CreateAttackState(sm, "BasicAttack2", new Vector3(500, 100, 0));
        var attack3 = CreateAttackState(sm, "BasicAttack3", new Vector3(700, 100, 0));

        // Heavy attacks
        var cleave = CreateAttackState(sm, "Cleave", new Vector3(300, 200, 0));
        var whirlwind = CreateAttackState(sm, "Whirlwind", new Vector3(500, 200, 0));
        var thrust = CreateAttackState(sm, "Thrust", new Vector3(700, 200, 0));
        var slam = CreateAttackState(sm, "Slam", new Vector3(900, 200, 0));

        // Create transitions from Any State (instant response)
        CreateInstantTransition(sm, attack1, "BasicAttack1");
        CreateInstantTransition(sm, attack2, "BasicAttack2");
        CreateInstantTransition(sm, attack3, "BasicAttack3");
        CreateInstantTransition(sm, cleave, "Cleave");
        CreateInstantTransition(sm, whirlwind, "Whirlwind");
        CreateInstantTransition(sm, thrust, "Thrust");
        CreateInstantTransition(sm, slam, "Slam");

        Debug.Log("Built Full Body Combat Layer");
    }

    AnimatorState CreateAttackState(AnimatorStateMachine sm, string name, Vector3 position)
    {
        var state = sm.AddState(name, position);
        // Motion will be added by user
        // Exit transition back to idle (has exit time)
        var exitTransition = state.AddExitTransition();
        exitTransition.hasExitTime = true;
        exitTransition.exitTime = 0.9f;
        exitTransition.duration = 0.1f;
        return state;
    }

    void CreateInstantTransition(AnimatorStateMachine sm, AnimatorState target, string triggerName)
    {
        var transition = sm.AddAnyStateTransition(target);
        transition.AddCondition(AnimatorConditionMode.If, 0, triggerName);
        transition.duration = 0f;
        transition.hasExitTime = false;
        transition.canTransitionToSelf = false;
    }

    #endregion

    #region Layer 2: Upper Body Spells

    void BuildSpellLayer()
    {
        // Create upper body mask
        AvatarMask upperBodyMask = CreateUpperBodyMask();

        var layer = CreateLayer("Upper Body Spells", 1f, upperBodyMask, false);
        var sm = layer.stateMachine;

        var idle = sm.AddState("Spell Idle", new Vector3(300, 0, 0));
        sm.defaultState = idle;

        // Spell states
        var projectile = CreateSpellState(sm, "CastProjectile", new Vector3(300, 100, 0));
        var beam = CreateSpellState(sm, "CastBeam", new Vector3(500, 100, 0));
        var breath = CreateSpellState(sm, "CastBreath", new Vector3(700, 100, 0));
        var wave = CreateSpellState(sm, "CastWave", new Vector3(900, 100, 0));

        // Instant transitions
        CreateInstantTransition(sm, projectile, "CastProjectile");
        CreateInstantTransition(sm, beam, "CastBeam");
        CreateInstantTransition(sm, breath, "CastBreath");
        CreateInstantTransition(sm, wave, "CastWave");

        Debug.Log("Built Upper Body Spells Layer");
    }

    AnimatorState CreateSpellState(AnimatorStateMachine sm, string name, Vector3 position)
    {
        var state = sm.AddState(name, position);
        var exitTransition = state.AddExitTransition();
        exitTransition.hasExitTime = true;
        exitTransition.exitTime = 0.9f;
        exitTransition.duration = 0.1f;
        return state;
    }

    AvatarMask CreateUpperBodyMask()
    {
        AvatarMask mask = new AvatarMask();
        mask.name = "UpperBodyMask";

        // Enable only upper body transforms
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            AvatarMaskBodyPart part = (AvatarMaskBodyPart)i;

            // Enable upper body parts
            if (part == AvatarMaskBodyPart.Head ||
                part == AvatarMaskBodyPart.Body ||
                part == AvatarMaskBodyPart.LeftArm ||
                part == AvatarMaskBodyPart.RightArm)
            {
                mask.SetHumanoidBodyPartActive(part, true);
            }
            else
            {
                mask.SetHumanoidBodyPartActive(part, false);
            }
        }

        // Save mask as asset
        string maskPath = System.IO.Path.GetDirectoryName(controllerPath) + "/UpperBodyMask.mask";
        AssetDatabase.CreateAsset(mask, maskPath);

        Debug.Log($"Created Upper Body Mask at: {maskPath}");
        return mask;
    }

    #endregion

    #region Layer 3: Full Body Actions

    void BuildActionsLayer()
    {
        var layer = CreateLayer("Full Body Actions", 1f, null, true); // Override
        var sm = layer.stateMachine;

        var idle = sm.AddState("Actions Idle", new Vector3(300, 0, 0));
        sm.defaultState = idle;

        // Jump states
        var jumpStart = sm.AddState("Jump Start", new Vector3(300, 100, 0));
        var jumpFall = sm.AddState("Jump Fall", new Vector3(500, 100, 0));
        var jumpLand = sm.AddState("Jump Land", new Vector3(700, 100, 0));

        // Dash state
        var dash = sm.AddState("Dash", new Vector3(300, 200, 0));

        // Jump trigger
        var toJump = sm.AddAnyStateTransition(jumpStart);
        toJump.AddCondition(AnimatorConditionMode.If, 0, "JumpTrigger");
        toJump.duration = 0f;
        toJump.hasExitTime = false;

        // Jump start -> fall (when leaving ground)
        var toFall = jumpStart.AddTransition(jumpFall);
        toFall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        toFall.AddCondition(AnimatorConditionMode.Less, 0, "VerticalVelocity");
        toFall.duration = 0.1f;
        toFall.hasExitTime = false;

        // Fall -> land (when touching ground)
        var toLand = jumpFall.AddTransition(jumpLand);
        toLand.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        toLand.duration = 0.05f;
        toLand.hasExitTime = false;

        // Land -> exit
        var exitLand = jumpLand.AddExitTransition();
        exitLand.hasExitTime = true;
        exitLand.exitTime = 0.8f;
        exitLand.duration = 0.1f;

        // Dash transition
        var toDash = sm.AddAnyStateTransition(dash);
        toDash.AddCondition(AnimatorConditionMode.If, 0, "IsDashing");
        toDash.duration = 0f;
        toDash.hasExitTime = false;

        var exitDash = dash.AddExitTransition();
        exitDash.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");
        exitDash.duration = 0.1f;
        exitDash.hasExitTime = false;

        Debug.Log("Built Full Body Actions Layer");
    }

    #endregion

    #region Layer 4: Reactions

    void BuildReactionsLayer()
    {
        var layer = CreateLayer("Reactions", 1f, null, true); // Override, highest priority
        var sm = layer.stateMachine;

        var idle = sm.AddState("Reaction Idle", new Vector3(300, 0, 0));
        sm.defaultState = idle;

        // Reaction states
        var hitLight = sm.AddState("Hit Light", new Vector3(300, 100, 0));
        var hitHeavy = sm.AddState("Hit Heavy", new Vector3(500, 100, 0));
        var stagger = sm.AddState("Stagger", new Vector3(700, 100, 0));
        var death = sm.AddState("Death", new Vector3(300, 200, 0));

        // Transitions
        CreateInstantTransition(sm, hitLight, "HitLight");
        CreateInstantTransition(sm, hitHeavy, "HitHeavy");
        CreateInstantTransition(sm, stagger, "Stagger");
        CreateInstantTransition(sm, death, "Death");

        // Hit reactions exit automatically
        var exitLight = hitLight.AddExitTransition();
        exitLight.hasExitTime = true;
        exitLight.exitTime = 0.9f;
        exitLight.duration = 0.05f;

        var exitHeavy = hitHeavy.AddExitTransition();
        exitHeavy.hasExitTime = true;
        exitHeavy.exitTime = 0.9f;
        exitHeavy.duration = 0.1f;

        var exitStagger = stagger.AddExitTransition();
        exitStagger.hasExitTime = true;
        exitStagger.exitTime = 0.95f;
        exitStagger.duration = 0.05f;

        // Death stays (controlled by IsDead bool)
        var exitDeath = death.AddExitTransition();
        exitDeath.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        exitDeath.duration = 0.2f;
        exitDeath.hasExitTime = false;

        Debug.Log("Built Reactions Layer");
    }

    #endregion

    AnimatorControllerLayer CreateLayer(string name, float weight, AvatarMask mask = null, bool isOverride = false)
    {
        var layer = new AnimatorControllerLayer
        {
            name = name,
            defaultWeight = weight,
            avatarMask = mask,
            blendingMode = isOverride ? AnimatorLayerBlendingMode.Override : AnimatorLayerBlendingMode.Additive,
            stateMachine = new AnimatorStateMachine
            {
                name = name,
                hideFlags = HideFlags.HideInHierarchy
            }
        };

        AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
        controller.AddLayer(layer);

        return layer;
    }
}