#if WALLSTOPSTUDIOS_DXSTATE_CINEMACHINE
namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using Cinemachine;

    public sealed class CinemachineVirtualCameraController : IVirtualCameraController
    {
        private readonly CinemachineVirtualCamera _camera;
        private readonly int _activePriority;
        private readonly int _inactivePriority;

        public CinemachineVirtualCameraController(
            CinemachineVirtualCamera camera,
            int activePriority = 20,
            int inactivePriority = 0
        )
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _activePriority = activePriority;
            _inactivePriority = inactivePriority;
        }

        public void Activate()
        {
            _camera.Priority = _activePriority;
        }

        public void Deactivate()
        {
            _camera.Priority = _inactivePriority;
        }
    }
}
#endif

