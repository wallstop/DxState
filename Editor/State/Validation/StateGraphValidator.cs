#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Builder;

    internal static class StateGraphValidator
    {
        public static StateGraphValidationReport Validate(StateGraphAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            List<StateGraphValidationIssue> issues = new List<StateGraphValidationIssue>();
            IReadOnlyList<StateGraphAsset.StackDefinition> stacks = asset.Stacks;
            if (stacks == null || stacks.Count == 0)
            {
                issues.Add(
                    new StateGraphValidationIssue(
                        StateGraphValidationSeverity.Warning,
                        "State graph does not contain any stacks.",
                        null,
                        -1,
                        -1,
                        -1
                    )
                );
                return new StateGraphValidationReport(issues);
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                StateGraphAsset.StackDefinition stack = stacks[i];
                string stackName = !string.IsNullOrWhiteSpace(stack.Name)
                    ? stack.Name
                    : $"Stack {i}";

                ValidateStack(asset, stack, stackName, i, issues);
            }

            return new StateGraphValidationReport(issues);
        }

        public static void LogReport(StateGraphAsset asset, StateGraphValidationReport report)
        {
            if (asset == null || report == null)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Validation results for '{asset.name}':");
            builder.AppendLine(
                $"Errors: {report.ErrorCount}, Warnings: {report.WarningCount}, Info: {report.InfoCount}"
            );

            IReadOnlyList<StateGraphValidationIssue> issues = report.Issues;
            for (int i = 0; i < issues.Count; i++)
            {
                StateGraphValidationIssue issue = issues[i];
                builder.Append(" • ");
                builder.Append(issue.Severity);
                builder.Append(": ");
                if (!string.IsNullOrEmpty(issue.StackName))
                {
                    builder.Append('[');
                    builder.Append(issue.StackName);
                    builder.Append("] ");
                }
                builder.Append(issue.Message);
                if (issue.TargetsState)
                {
                    builder.Append(" (State Index: ");
                    builder.Append(issue.StateIndex);
                    builder.Append(')');
                }
                if (issue.TargetsTransition)
                {
                    builder.Append(" (Transition Index: ");
                    builder.Append(issue.TransitionIndex);
                    builder.Append(')');
                }
                builder.AppendLine();
            }

            if (report.HasErrors)
            {
                Debug.LogError(builder.ToString(), asset);
            }
            else if (report.HasWarnings)
            {
                Debug.LogWarning(builder.ToString(), asset);
            }
            else
            {
                Debug.Log(builder.ToString(), asset);
            }
        }

        private static void ValidateStack(
            StateGraphAsset asset,
            StateGraphAsset.StackDefinition stack,
            string stackName,
            int stackIndex,
            List<StateGraphValidationIssue> issues
        )
        {
            IReadOnlyList<StateGraphAsset.StateReference> states = stack.States;
            if (states == null || states.Count == 0)
            {
                issues.Add(
                    new StateGraphValidationIssue(
                        StateGraphValidationSeverity.Error,
                        "Stack has no state references.",
                        stackName,
                        stackIndex,
                        -1,
                        -1
                    )
                );
                return;
            }

            HashSet<UnityEngine.Object> uniqueStates = new HashSet<UnityEngine.Object>();
            int initialCount = 0;
            for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                StateGraphAsset.StateReference reference = states[stateIndex];
                if (reference == null)
                {
                    issues.Add(
                        new StateGraphValidationIssue(
                            StateGraphValidationSeverity.Error,
                            "State entry is null.",
                            stackName,
                            stackIndex,
                            stateIndex,
                            -1
                        )
                    );
                    continue;
                }

                UnityEngine.Object rawState = reference.RawState;
                if (rawState == null)
                {
                    issues.Add(
                        new StateGraphValidationIssue(
                            StateGraphValidationSeverity.Error,
                            "State reference is missing.",
                            stackName,
                            stackIndex,
                            stateIndex,
                            -1
                        )
                    );
                    continue;
                }

                if (rawState is not IState)
                {
                    issues.Add(
                        new StateGraphValidationIssue(
                            StateGraphValidationSeverity.Error,
                            $"State reference '{rawState.name}' does not implement IState.",
                            stackName,
                            stackIndex,
                            stateIndex,
                            -1
                        )
                    );
                }

                if (!uniqueStates.Add(rawState))
                {
                    issues.Add(
                        new StateGraphValidationIssue(
                            StateGraphValidationSeverity.Warning,
                            $"State '{rawState.name}' is referenced multiple times.",
                            stackName,
                            stackIndex,
                            stateIndex,
                            -1
                        )
                    );
                }

                if (reference.SetAsInitial)
                {
                    initialCount++;
                }
            }

            if (initialCount == 0)
            {
                issues.Add(
                    new StateGraphValidationIssue(
                        StateGraphValidationSeverity.Warning,
                        "Stack does not define an initial state.",
                        stackName,
                        stackIndex,
                        -1,
                        -1
                    )
                );
            }
            else if (initialCount > 1)
            {
                issues.Add(
                    new StateGraphValidationIssue(
                        StateGraphValidationSeverity.Error,
                        "Multiple states are marked as initial.",
                        stackName,
                        stackIndex,
                        -1,
                        -1
                    )
                );
            }

            IReadOnlyList<StateGraphAsset.StateTransitionMetadata> transitions = stack.Transitions;
            if (transitions != null)
            {
                for (int transitionIndex = 0; transitionIndex < transitions.Count; transitionIndex++)
                {
                    ValidateTransition(
                        transitions[transitionIndex],
                        states.Count,
                        stackName,
                        stackIndex,
                        transitionIndex,
                        issues
                    );
                }
            }

            try
            {
                StateStackConfiguration configuration = stack.BuildConfiguration();
                if (configuration == null)
                {
                    issues.Add(
                        new StateGraphValidationIssue(
                            StateGraphValidationSeverity.Error,
                            "Stack configuration could not be built.",
                            stackName,
                            stackIndex,
                            -1,
                            -1
                        )
                    );
                }
            }
            catch (Exception exception)
            {
                issues.Add(
                    new StateGraphValidationIssue(
                        StateGraphValidationSeverity.Error,
                        $"Configuration build failed: {exception.Message}",
                        stackName,
                        stackIndex,
                        -1,
                        -1
                    )
                );
            }
        }

        private static void ValidateTransition(
            StateGraphAsset.StateTransitionMetadata transition,
            int stateCount,
            string stackName,
            int stackIndex,
            int transitionIndex,
            List<StateGraphValidationIssue> issues
        )
        {
            int fromIndex = transition.FromIndex;
            int toIndex = transition.ToIndex;
            bool fromValid = 0 <= fromIndex && fromIndex < stateCount;
            bool toValid = 0 <= toIndex && toIndex < stateCount;

            if (!fromValid || !toValid)
            {
                string message = !fromValid && !toValid
                    ? "Transition uses invalid source and destination indices."
                    : (!fromValid
                        ? "Transition source index is invalid."
                        : "Transition destination index is invalid.");
                issues.Add(
                    new StateGraphValidationIssue(
                        StateGraphValidationSeverity.Error,
                        message,
                        stackName,
                        stackIndex,
                        -1,
                        transitionIndex
                    )
                );
                return;
            }

            if (fromIndex == toIndex)
            {
                issues.Add(
                    new StateGraphValidationIssue(
                        StateGraphValidationSeverity.Warning,
                        "Transition references the same state as source and destination.",
                        stackName,
                        stackIndex,
                        -1,
                        transitionIndex
                    )
                );
            }

            TransitionCause cause = transition.Cause;
            TransitionFlags flags = transition.Flags;
            if (cause == TransitionCause.Initialization && flags != TransitionFlags.None)
            {
                issues.Add(
                    new StateGraphValidationIssue(
                        StateGraphValidationSeverity.Warning,
                        "Initialization transitions should not use additional flags.",
                        stackName,
                        stackIndex,
                        -1,
                        transitionIndex
                    )
                );
            }

            if (cause == TransitionCause.Forced && flags == TransitionFlags.None)
            {
                issues.Add(
                    new StateGraphValidationIssue(
                        StateGraphValidationSeverity.Info,
                        "Forced transitions typically include the Forced flag.",
                        stackName,
                        stackIndex,
                        -1,
                        transitionIndex
                    )
                );
            }
        }
    }
}
#endif
