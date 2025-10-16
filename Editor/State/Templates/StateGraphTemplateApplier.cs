#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.DxState.State.Stack.Builder;
    using WallstopStudios.DxState.State.Stack.Builder.Templates;

    internal static class StateGraphTemplateApplier
    {
        private static readonly FieldInfo _stacksField = typeof(StateGraphAsset).GetField(
            "_stacks",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static readonly FieldInfo _stackNameField = typeof(StateGraphAsset.StackDefinition).GetField(
            "_name",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static readonly FieldInfo _stackStatesField = typeof(StateGraphAsset.StackDefinition).GetField(
            "_states",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static readonly FieldInfo _stackTransitionsField = typeof(StateGraphAsset.StackDefinition).GetField(
            "_transitions",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static readonly FieldInfo _referenceStateField = typeof(StateGraphAsset.StateReference).GetField(
            "_state",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static readonly FieldInfo _referenceInitialField = typeof(StateGraphAsset.StateReference).GetField(
            "_setAsInitial",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static readonly FieldInfo _transitionFromField = typeof(StateGraphAsset.StateTransitionMetadata).GetField(
            "_fromIndex",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static readonly FieldInfo _transitionToField = typeof(StateGraphAsset.StateTransitionMetadata).GetField(
            "_toIndex",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static readonly FieldInfo _transitionLabelField = typeof(StateGraphAsset.StateTransitionMetadata).GetField(
            "_label",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static readonly FieldInfo _transitionTooltipField = typeof(StateGraphAsset.StateTransitionMetadata).GetField(
            "_tooltip",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static readonly FieldInfo _transitionCauseField = typeof(StateGraphAsset.StateTransitionMetadata).GetField(
            "_cause",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static readonly FieldInfo _transitionFlagsField = typeof(StateGraphAsset.StateTransitionMetadata).GetField(
            "_flags",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        internal static TemplateApplicationResult ApplyTemplate(
            StateGraphTemplate template,
            StateGraphAsset targetGraph
        )
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (targetGraph == null)
            {
                throw new ArgumentNullException(nameof(targetGraph));
            }

            if (_stacksField == null)
            {
                throw new InvalidOperationException(
                    "Unable to access StateGraphAsset internals for template application."
                );
            }

            List<StateGraphAsset.StackDefinition> stacks =
                _stacksField.GetValue(targetGraph) as List<StateGraphAsset.StackDefinition>;
            if (stacks == null)
            {
                throw new InvalidOperationException("StateGraph asset is not initialised correctly.");
            }

            Undo.RegisterCompleteObjectUndo(targetGraph, "Apply State Graph Template");

            string baseStackName = template.StackDefinition.Name;
            string resolvedStackName = ResolveUniqueStackName(stacks, baseStackName);

            StateGraphAsset.StackDefinition stackDefinition = new StateGraphAsset.StackDefinition();
            _stackNameField?.SetValue(stackDefinition, resolvedStackName);

            List<StateGraphAsset.StateReference> stateReferences =
                _stackStatesField?.GetValue(stackDefinition)
                    as List<StateGraphAsset.StateReference>;
            List<StateGraphAsset.StateTransitionMetadata> transitionMetadata =
                _stackTransitionsField?.GetValue(stackDefinition)
                    as List<StateGraphAsset.StateTransitionMetadata>;

            if (stateReferences == null || transitionMetadata == null)
            {
                throw new InvalidOperationException(
                    "Unable to access stack internals while applying template."
                );
            }

            List<StateGraphPlaceholderState> createdStates = new List<StateGraphPlaceholderState>();

            IReadOnlyList<StateGraphTemplate.StateDefinition> stateDefinitions =
                template.StackDefinition.States;

            bool initialAssigned = false;
            for (int i = 0; i < stateDefinitions.Count; i++)
            {
                StateGraphTemplate.StateDefinition definition = stateDefinitions[i];
                StateGraphPlaceholderState placeholder = ScriptableObject
                    .CreateInstance<StateGraphPlaceholderState>();
                placeholder.name = definition.Name;
                Color accent = DetermineAccentColor(definition.Kind);
                placeholder.Configure(
                    definition.Name,
                    definition.Kind,
                    definition.TickMode,
                    definition.TickWhenInactive,
                    accent,
                    definition.Notes
                );

                Undo.RegisterCreatedObjectUndo(placeholder, "Create Placeholder State");
                AssetDatabase.AddObjectToAsset(placeholder, targetGraph);
                EditorUtility.SetDirty(placeholder);
                createdStates.Add(placeholder);

                StateGraphAsset.StateReference reference = new StateGraphAsset.StateReference();
                _referenceStateField?.SetValue(reference, placeholder);

                bool shouldBeInitial = definition.SetAsInitial && !initialAssigned;
                if (shouldBeInitial)
                {
                    initialAssigned = true;
                }

                _referenceInitialField?.SetValue(reference, shouldBeInitial);
                stateReferences.Add(reference);
            }

            if (!initialAssigned && stateReferences.Count > 0)
            {
                _referenceInitialField?.SetValue(stateReferences[0], true);
            }

            IReadOnlyList<StateGraphTemplate.TransitionDefinition> transitions =
                template.StackDefinition.Transitions;
            for (int i = 0; i < transitions.Count; i++)
            {
                StateGraphTemplate.TransitionDefinition definition = transitions[i];
                if (
                    definition.FromIndex < 0
                    || definition.FromIndex >= stateReferences.Count
                    || definition.ToIndex < 0
                    || definition.ToIndex >= stateReferences.Count
                )
                {
                    continue;
                }

                StateGraphAsset.StateTransitionMetadata metadata =
                    new StateGraphAsset.StateTransitionMetadata();
                _transitionFromField?.SetValue(metadata, definition.FromIndex);
                _transitionToField?.SetValue(metadata, definition.ToIndex);
                _transitionLabelField?.SetValue(metadata, definition.Label);
                _transitionTooltipField?.SetValue(metadata, definition.Tooltip);
                _transitionCauseField?.SetValue(metadata, definition.Cause);
                _transitionFlagsField?.SetValue(metadata, definition.Flags);
                transitionMetadata.Add(metadata);
            }

            stacks.Add(stackDefinition);

            EditorUtility.SetDirty(targetGraph);
            AssetDatabase.SaveAssets();

            TemplateApplicationResult result = new TemplateApplicationResult(
                resolvedStackName,
                stacks.Count - 1,
                createdStates
            );
            return result;
        }

        private static string ResolveUniqueStackName(
            List<StateGraphAsset.StackDefinition> stacks,
            string templateName
        )
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                templateName = "Stack";
            }

            HashSet<string> existing = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < stacks.Count; i++)
            {
                string name = _stackNameField?.GetValue(stacks[i]) as string;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    existing.Add(name);
                }
            }

            if (!existing.Contains(templateName))
            {
                return templateName;
            }

            int suffix = 1;
            while (suffix < 512)
            {
                string candidate = $"{templateName} {suffix}";
                if (!existing.Contains(candidate))
                {
                    return candidate;
                }

                suffix++;
            }

            return Guid.NewGuid().ToString("N");
        }

        private static Color DetermineAccentColor(StateGraphTemplate.StateNodeKind kind)
        {
            switch (kind)
            {
                case StateGraphTemplate.StateNodeKind.Hierarchical:
                    return new Color(0.31f, 0.58f, 0.86f, 1f);
                case StateGraphTemplate.StateNodeKind.Trigger:
                    return new Color(0.80f, 0.45f, 0.25f, 1f);
                default:
                    return new Color(0.18f, 0.46f, 0.86f, 1f);
            }
        }

        internal readonly struct TemplateApplicationResult
        {
            public TemplateApplicationResult(
                string stackName,
                int stackIndex,
                IReadOnlyList<StateGraphPlaceholderState> states
            )
            {
                StackName = stackName;
                StackIndex = stackIndex;
                CreatedStates = states;
            }

            public string StackName { get; }

            public int StackIndex { get; }

            public IReadOnlyList<StateGraphPlaceholderState> CreatedStates { get; }
        }
    }
}
#endif
