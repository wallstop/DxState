#if UNITY_2021_2_OR_NEWER
namespace WallstopStudios.DxState.Editor.State
{
    using System;
    using System.Collections;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEditor.Overlays;
    using UnityEngine;
    using UnityEngine.UIElements;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;
    using Object = UnityEngine.Object;

    [Overlay(typeof(SceneView), "DxState/Stack Controls", true)]
    public sealed class StateStackToolbarOverlay : Overlay
    {
        private TextField _stateNameField;
        private Label _statusLabel;

        public override VisualElement CreatePanelContent()
        {
            VisualElement root = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 4,
                    paddingRight = 4,
                },
            };

            _stateNameField = new TextField
            {
                style = { width = 140, marginRight = 4 },
                label = "State",
            };
            root.Add(_stateNameField);

            root.Add(CreateButton("Push", PushState));
            root.Add(CreateButton("Flatten", FlattenState));
            root.Add(CreateButton("Pop", PopState));
            root.Add(CreateButton("Clear", ClearStack));

            _statusLabel = new Label
            {
                style = { marginLeft = 6, unityFontStyleAndWeight = FontStyle.Italic },
                text = "Idle",
            };
            root.Add(_statusLabel);

            return root;
        }

        private Button CreateButton(string text, System.Action action)
        {
            Button button = new Button(() => Execute(action))
            {
                text = text,
                style = { marginRight = 4 },
            };
            return button;
        }

        private void Execute(System.Action action)
        {
            if (!EditorApplication.isPlaying)
            {
                _statusLabel.text = "Enter Play Mode to control the stack";
                return;
            }

            StateStackManager manager = Object.FindObjectOfType<StateStackManager>();
            if (manager == null)
            {
                _statusLabel.text = "No StateStackManager found";
                return;
            }

            action?.Invoke();
            _statusLabel.text = manager.IsTransitioning ? "Transitioning..." : "Command sent";
        }

        private void PushState()
        {
            string stateName = _stateNameField?.value;
            if (string.IsNullOrWhiteSpace(stateName))
            {
                _statusLabel.text = "Enter a state name";
                return;
            }

            StateStackManager manager = Object.FindObjectOfType<StateStackManager>();
            if (manager == null)
            {
                _statusLabel.text = "No StateStackManager found";
                return;
            }

            manager.StartCoroutine(PushCoroutine(manager, stateName));
        }

        private IEnumerator PushCoroutine(StateStackManager manager, string stateName)
        {
            yield return manager.PushAsync(stateName).AsIEnumerator();
        }

        private void FlattenState()
        {
            string stateName = _stateNameField?.value;
            if (string.IsNullOrWhiteSpace(stateName))
            {
                _statusLabel.text = "Enter a state name";
                return;
            }

            StateStackManager manager = Object.FindObjectOfType<StateStackManager>();
            if (manager == null)
            {
                _statusLabel.text = "No StateStackManager found";
                return;
            }

            manager.StartCoroutine(FlattenCoroutine(manager, stateName));
        }

        private IEnumerator FlattenCoroutine(StateStackManager manager, string stateName)
        {
            yield return manager.FlattenAsync(stateName).AsIEnumerator();
        }

        private void PopState()
        {
            StateStackManager manager = Object.FindObjectOfType<StateStackManager>();
            if (manager == null)
            {
                _statusLabel.text = "No StateStackManager found";
                return;
            }

            manager.StartCoroutine(PopCoroutine(manager));
        }

        private IEnumerator PopCoroutine(StateStackManager manager)
        {
            yield return manager.PopAsync().AsIEnumerator();
        }

        private void ClearStack()
        {
            StateStackManager manager = Object.FindObjectOfType<StateStackManager>();
            if (manager == null)
            {
                _statusLabel.text = "No StateStackManager found";
                return;
            }

            manager.StartCoroutine(manager.ClearAsync().AsIEnumerator());
        }
    }

    internal static class ValueTaskExtensions
    {
        public static IEnumerator AsIEnumerator(this ValueTask task)
        {
            Task inner = task.AsTask();
            while (!inner.IsCompleted)
            {
                yield return null;
            }

            if (inner.IsFaulted)
            {
                throw inner.Exception ?? new Exception("Task faulted");
            }
        }

        public static IEnumerator AsIEnumerator<T>(this ValueTask<T> task)
        {
            Task inner = task.AsTask();
            while (!inner.IsCompleted)
            {
                yield return null;
            }

            if (inner.IsFaulted)
            {
                throw inner.Exception ?? new Exception("Task faulted");
            }
        }
    }
}
#endif
