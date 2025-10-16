#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State
{
    using UnityEditor;

    internal static class CreateGameStateTemplate
    {
        private const string DefaultScriptName = "NewGameState.cs";

        private const string TemplateContent =
"using System;\n" +
"using System.Threading.Tasks;\n" +
"using UnityEngine;\n" +
"using WallstopStudios.DxState.State.Stack;\n\n" +
"public sealed class NewGameState : GameState\n" +
"{\n" +
"    public override string Name => string.IsNullOrWhiteSpace(_name) ? name : _name;\n\n" +
"    public override TickMode TickMode => TickMode.None;\n\n" +
"    public override ValueTask Enter<TProgress>(\n" +
"        IState previousState,\n" +
"        TProgress progress,\n" +
"        StateDirection direction\n" +
"    )\n" +
"        where TProgress : IProgress<float>\n" +
"    {\n" +
"        progress.Report(1f);\n" +
"        return default;\n" +
"    }\n\n" +
"    public override ValueTask Exit<TProgress>(\n" +
"        IState nextState,\n" +
"        TProgress progress,\n" +
"        StateDirection direction\n" +
"    )\n" +
"        where TProgress : IProgress<float>\n" +
"    {\n" +
"        progress.Report(1f);\n" +
"        return default;\n" +
"    }\n\n" +
"    protected override void OnValidate()\n" +
"    {\n" +
"        base.OnValidate();\n" +
"        if (string.IsNullOrWhiteSpace(_name))\n" +
"        {\n" +
"            _name = name;\n" +
"        }\n" +
"    }\n" +
"}\n";

        [MenuItem("Assets/Create/Wallstop Studios/DxState/Game State", priority = 30)]
        private static void CreateGameState()
        {
            ProjectWindowUtil.CreateScriptAssetWithContent(DefaultScriptName, TemplateContent);
        }
    }
}
#endif
