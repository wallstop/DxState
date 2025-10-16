#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State.Validation
{
    using UnityEditor;
    using WallstopStudios.DxState.State.Stack.Builder;

    internal static class StateGraphValidationMenu
    {
        [MenuItem("Assets/Wallstop Studios/DxState/Validate State Graph", true)]
        private static bool ValidateStateGraphEnabled()
        {
            return Selection.activeObject is StateGraphAsset;
        }

        [MenuItem("Assets/Wallstop Studios/DxState/Validate State Graph", false, 2000)]
        private static void ValidateStateGraph()
        {
            StateGraphAsset asset = Selection.activeObject as StateGraphAsset;
            if (asset == null)
            {
                return;
            }

            StateGraphValidationReport report = StateGraphValidator.Validate(asset);
            StateGraphValidator.LogReport(asset, report);
        }
    }
}
#endif
