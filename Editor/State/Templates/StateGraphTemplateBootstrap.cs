#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Builder.Templates;

    [InitializeOnLoad]
    internal static class StateGraphTemplateBootstrap
    {
        private const string TemplateFolder =
            "Packages/com.wallstop-studios.dxstate/Editor/State/Templates/Generated";

        private const string HfsmTemplatePath =
            TemplateFolder + "/HFSMNode.asset";

        private const string TriggerTemplatePath =
            TemplateFolder + "/TriggerState.asset";

        static StateGraphTemplateBootstrap()
        {
            EnsureDefaultTemplates();
        }

        private static void EnsureDefaultTemplates()
        {
            if (!Directory.Exists(TemplateFolder))
            {
                Directory.CreateDirectory(TemplateFolder);
            }

            EnsureTemplate(HfsmTemplatePath, ConfigureHierarchicalTemplate);
            EnsureTemplate(TriggerTemplatePath, ConfigureTriggerTemplate);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureTemplate(
            string assetPath,
            Action<StateGraphTemplate> configure
        )
        {
            StateGraphTemplate template = AssetDatabase.LoadAssetAtPath<StateGraphTemplate>(assetPath);
            if (template == null)
            {
                template = ScriptableObject.CreateInstance<StateGraphTemplate>();
                configure(template);
                AssetDatabase.CreateAsset(template, assetPath);
                AssetDatabase.SaveAssets();
                return;
            }

            configure(template);
            EditorUtility.SetDirty(template);
        }

        private static void ConfigureHierarchicalTemplate(StateGraphTemplate template)
        {
            template.SetDisplayName("Hierarchical Node");
            template.SetDescription(
                "Creates a composite root with two child placeholders for hierarchical flows."
            );
            template.SetCategory(StateGraphTemplate.TemplateCategory.Hierarchical);

            StateGraphTemplate.StackDefinitionData stack = template.StackDefinition;
            stack.Clear();
            stack.SetName("Hierarchical Node");

            stack.AddState(
                "Composite Root",
                StateGraphTemplate.StateNodeKind.Hierarchical,
                setAsInitial: true,
                TickMode.Update,
                tickWhenInactive: false,
                "Acts as the parent aggregator for nested regions."
            );

            stack.AddState(
                "Primary Child",
                StateGraphTemplate.StateNodeKind.Default,
                setAsInitial: false,
                TickMode.Update,
                tickWhenInactive: false,
                "Entry child state for the composite."
            );

            stack.AddState(
                "Secondary Child",
                StateGraphTemplate.StateNodeKind.Default,
                setAsInitial: false,
                TickMode.Update,
                tickWhenInactive: false,
                "Fallback child state handled by the composite."
            );

            stack.AddTransition(
                0,
                1,
                "Enter",
                "Composite activates the primary child",
                TransitionCause.Initialization,
                TransitionFlags.None
            );

            stack.AddTransition(
                1,
                2,
                "Advance",
                "Flow to the secondary child",
                TransitionCause.RuleSatisfied,
                TransitionFlags.None
            );
        }

        private static void ConfigureTriggerTemplate(StateGraphTemplate template)
        {
            template.SetDisplayName("Trigger Pair");
            template.SetDescription(
                "Adds trigger-ready placeholder states for event driven state machines."
            );
            template.SetCategory(StateGraphTemplate.TemplateCategory.Trigger);

            StateGraphTemplate.StackDefinitionData stack = template.StackDefinition;
            stack.Clear();
            stack.SetName("Trigger Pair");

            stack.AddState(
                "Await Trigger",
                StateGraphTemplate.StateNodeKind.Trigger,
                setAsInitial: true,
                TickMode.Update,
                tickWhenInactive: false,
                "Monitors incoming triggers and dispatches to the active state."
            );

            stack.AddState(
                "Triggered",
                StateGraphTemplate.StateNodeKind.Default,
                setAsInitial: false,
                TickMode.None,
                tickWhenInactive: false,
                "Represents the post-trigger execution window."
            );

            stack.AddTransition(
                0,
                1,
                "On Trigger",
                "Switch when the awaiting state fires a trigger",
                TransitionCause.RuleSatisfied,
                TransitionFlags.None
            );
        }
    }
}
#endif
