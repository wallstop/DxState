#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State.Serialization
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Builder;

    internal static class StateGraphJsonUtility
    {
        public static string ExportToJson(StateGraphAsset asset, bool prettyPrint = true)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            StateGraphJsonModel model = BuildModel(asset);
            return JsonUtility.ToJson(model, prettyPrint);
        }

        public static void ImportFromJson(StateGraphAsset asset, string json)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON content must be provided.", nameof(json));
            }

            StateGraphJsonModel model = JsonUtility.FromJson<StateGraphJsonModel>(json);
            if (model == null)
            {
                throw new InvalidOperationException("Unable to parse state graph JSON content.");
            }

            ApplyModel(asset, model);
        }

        private static StateGraphJsonModel BuildModel(StateGraphAsset asset)
        {
            StateGraphJsonModel model = new StateGraphJsonModel();
            model.stacks = new List<StateGraphJsonStack>();

            IReadOnlyList<StateGraphAsset.StackDefinition> stacks = asset.Stacks;
            if (stacks == null)
            {
                return model;
            }

            for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
            {
                StateGraphAsset.StackDefinition definition = stacks[stackIndex];
                StateGraphJsonStack stackModel = new StateGraphJsonStack();
                stackModel.name = definition.Name;
                stackModel.states = new List<StateGraphJsonState>();
                stackModel.transitions = new List<StateGraphJsonTransition>();

                IReadOnlyList<StateGraphAsset.StateReference> states = definition.States;
                if (states != null)
                {
                    for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
                    {
                        StateGraphAsset.StateReference reference = states[stateIndex];
                        StateGraphJsonState stateModel = BuildStateModel(reference);
                        stackModel.states.Add(stateModel);
                    }
                }

                IReadOnlyList<StateGraphAsset.StateTransitionMetadata> transitions =
                    definition.Transitions;
                if (transitions != null)
                {
                    for (int transitionIndex = 0; transitionIndex < transitions.Count; transitionIndex++)
                    {
                        StateGraphAsset.StateTransitionMetadata metadata = transitions[transitionIndex];
                        StateGraphJsonTransition transitionModel = BuildTransitionModel(metadata);
                        stackModel.transitions.Add(transitionModel);
                    }
                }

                model.stacks.Add(stackModel);
            }

            return model;
        }

        private static StateGraphJsonState BuildStateModel(StateGraphAsset.StateReference reference)
        {
            StateGraphJsonState stateModel = new StateGraphJsonState();
            UnityEngine.Object rawState = reference != null ? reference.RawState : null;
            stateModel.assetPath = rawState != null ? AssetDatabase.GetAssetPath(rawState) : string.Empty;
            stateModel.setAsInitial = reference != null && reference.SetAsInitial;
            stateModel.objectName = rawState != null ? rawState.name : string.Empty;
            stateModel.type = rawState != null ? rawState.GetType().AssemblyQualifiedName : string.Empty;

            if (rawState != null)
            {
                string guid;
                long localId;
                bool resolved = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(rawState, out guid, out localId);
                if (resolved)
                {
                    stateModel.guid = guid;
                    stateModel.localId = localId;
                }
            }

            return stateModel;
        }

        private static StateGraphJsonTransition BuildTransitionModel(
            StateGraphAsset.StateTransitionMetadata metadata
        )
        {
            StateGraphJsonTransition transition = new StateGraphJsonTransition();
            transition.fromIndex = metadata != null ? metadata.FromIndex : -1;
            transition.toIndex = metadata != null ? metadata.ToIndex : -1;
            transition.label = metadata != null ? metadata.Label : string.Empty;
            transition.tooltip = metadata != null ? metadata.Tooltip : string.Empty;
            transition.cause = metadata != null ? metadata.Cause : TransitionCause.Unspecified;
            transition.flags = metadata != null ? metadata.Flags : TransitionFlags.None;
            return transition;
        }

        private static void ApplyModel(StateGraphAsset asset, StateGraphJsonModel model)
        {
            SerializedObject serializedGraph = new SerializedObject(asset);
            SerializedProperty stacksProperty = serializedGraph.FindProperty("_stacks");
            if (stacksProperty == null)
            {
                throw new InvalidOperationException("StateGraph asset structure is incompatible.");
            }

            List<StateGraphJsonStack> stacks = model.stacks ?? new List<StateGraphJsonStack>();
            stacksProperty.arraySize = stacks.Count;

            for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
            {
                StateGraphJsonStack stackModel = stacks[stackIndex];
                SerializedProperty stackProperty = stacksProperty.GetArrayElementAtIndex(stackIndex);
                SerializedProperty nameProperty = stackProperty.FindPropertyRelative("_name");
                if (nameProperty != null)
                {
                    nameProperty.stringValue = stackModel.name ?? string.Empty;
                }

                SerializedProperty statesProperty = stackProperty.FindPropertyRelative("_states");
                List<StateGraphJsonState> stateModels = stackModel.states ?? new List<StateGraphJsonState>();
                statesProperty.arraySize = stateModels.Count;
                for (int stateIndex = 0; stateIndex < stateModels.Count; stateIndex++)
                {
                    SerializedProperty entry = statesProperty.GetArrayElementAtIndex(stateIndex);
                    SerializedProperty stateProperty = entry.FindPropertyRelative("_state");
                    SerializedProperty initialProperty = entry.FindPropertyRelative("_setAsInitial");
                    StateGraphJsonState stateModel = stateModels[stateIndex];
                    UnityEngine.Object resolvedState = ResolveStateObject(stateModel);
                    if (stateProperty != null)
                    {
                        stateProperty.objectReferenceValue = resolvedState;
                    }

                    if (initialProperty != null)
                    {
                        initialProperty.boolValue = stateModel.setAsInitial;
                    }
                }

                SerializedProperty transitionsProperty = stackProperty.FindPropertyRelative("_transitions");
                List<StateGraphJsonTransition> transitionModels =
                    stackModel.transitions ?? new List<StateGraphJsonTransition>();
                transitionsProperty.arraySize = transitionModels.Count;
                for (int transitionIndex = 0; transitionIndex < transitionModels.Count; transitionIndex++)
                {
                    SerializedProperty entry = transitionsProperty.GetArrayElementAtIndex(transitionIndex);
                    StateGraphJsonTransition transitionModel = transitionModels[transitionIndex];
                    SerializedProperty fromProperty = entry.FindPropertyRelative("_fromIndex");
                    SerializedProperty toProperty = entry.FindPropertyRelative("_toIndex");
                    SerializedProperty labelProperty = entry.FindPropertyRelative("_label");
                    SerializedProperty tooltipProperty = entry.FindPropertyRelative("_tooltip");
                    SerializedProperty causeProperty = entry.FindPropertyRelative("_cause");
                    SerializedProperty flagsProperty = entry.FindPropertyRelative("_flags");

                    if (fromProperty != null)
                    {
                        fromProperty.intValue = transitionModel.fromIndex;
                    }

                    if (toProperty != null)
                    {
                        toProperty.intValue = transitionModel.toIndex;
                    }

                    if (labelProperty != null)
                    {
                        labelProperty.stringValue = transitionModel.label ?? string.Empty;
                    }

                    if (tooltipProperty != null)
                    {
                        tooltipProperty.stringValue = transitionModel.tooltip ?? string.Empty;
                    }

                    if (causeProperty != null)
                    {
                        causeProperty.intValue = (int)transitionModel.cause;
                    }

                    if (flagsProperty != null)
                    {
                        flagsProperty.intValue = (int)transitionModel.flags;
                    }
                }
            }

            serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private static UnityEngine.Object ResolveStateObject(StateGraphJsonState stateModel)
        {
            if (stateModel == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(stateModel.guid))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(stateModel.guid);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    UnityEngine.Object resolved = ResolveObjectByLocalId(assetPath, stateModel.guid, stateModel.localId);
                    if (resolved != null)
                    {
                        return resolved;
                    }
                }
            }

            if (!string.IsNullOrEmpty(stateModel.assetPath))
            {
                UnityEngine.Object directAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    stateModel.assetPath
                );
                if (directAsset != null)
                {
                    return directAsset;
                }
            }

            return null;
        }

        private static UnityEngine.Object ResolveObjectByLocalId(
            string assetPath,
            string guid,
            long localId
        )
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            if (localId == 0)
            {
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                UnityEngine.Object candidate = assets[i];
                if (candidate == null)
                {
                    continue;
                }

                string candidateGuid;
                long candidateLocalId;
                bool resolved = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    candidate,
                    out candidateGuid,
                    out candidateLocalId
                );
                if (!resolved)
                {
                    continue;
                }

                if (string.Equals(candidateGuid, guid, StringComparison.Ordinal)
                    && candidateLocalId == localId)
                {
                    return candidate;
                }
            }

            return null;
        }

        [Serializable]
        private sealed class StateGraphJsonModel
        {
            public List<StateGraphJsonStack> stacks;
        }

        [Serializable]
        private sealed class StateGraphJsonStack
        {
            public string name;
            public List<StateGraphJsonState> states;
            public List<StateGraphJsonTransition> transitions;
        }

        [Serializable]
        private sealed class StateGraphJsonState
        {
            public string guid;
            public long localId;
            public string assetPath;
            public string objectName;
            public string type;
            public bool setAsInitial;
        }

        [Serializable]
        private sealed class StateGraphJsonTransition
        {
            public int fromIndex;
            public int toIndex;
            public string label;
            public string tooltip;
            public TransitionCause cause;
            public TransitionFlags flags;
        }
    }
}
#endif
