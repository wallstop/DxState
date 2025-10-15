namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using WallstopStudios.DxState.Extensions;

    public interface IVirtualCameraController
    {
        void Activate();

        void Deactivate();
    }

    public sealed class VirtualCameraState : IState
    {
        private readonly IVirtualCameraController _controller;

        public VirtualCameraState(string name, IVirtualCameraController controller)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("VirtualCameraState requires a name.", nameof(name));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            Name = name;
            _controller = controller;
        }

        public string Name { get; }

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState => null;

        public ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _controller.Activate();
            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }

        public void Tick(TickMode mode, float delta)
        {
        }

        public ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _controller.Deactivate();
            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            _controller.Deactivate();
            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }
    }
}

