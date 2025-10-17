#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State
{
    using System;
    using System.IO;
    using System.Reflection;
    using UnityEditor;
    using Object = UnityEngine.Object;

    internal static class CreateGameStateTemplate
    {
        private const string DefaultScriptName = "NewGameState.cs";

        private const string TemplateContent =
            "using System;\n"
            + "using System.Threading.Tasks;\n"
            + "using UnityEngine;\n"
            + "using WallstopStudios.DxState.State.Stack;\n\n"
            + "public sealed class NewGameState : GameState\n"
            + "{\n"
            + "    public override string Name => string.IsNullOrWhiteSpace(_name) ? name : _name;\n\n"
            + "    public override TickMode TickMode => TickMode.None;\n\n"
            + "    public override ValueTask Enter<TProgress>(\n"
            + "        IState previousState,\n"
            + "        TProgress progress,\n"
            + "        StateDirection direction\n"
            + "    )\n"
            + "        where TProgress : IProgress<float>\n"
            + "    {\n"
            + "        progress.Report(1f);\n"
            + "        return default;\n"
            + "    }\n\n"
            + "    public override ValueTask Exit<TProgress>(\n"
            + "        IState nextState,\n"
            + "        TProgress progress,\n"
            + "        StateDirection direction\n"
            + "    )\n"
            + "        where TProgress : IProgress<float>\n"
            + "    {\n"
            + "        progress.Report(1f);\n"
            + "        return default;\n"
            + "    }\n\n"
            + "    protected override void OnValidate()\n"
            + "    {\n"
            + "        base.OnValidate();\n"
            + "        if (string.IsNullOrWhiteSpace(_name))\n"
            + "        {\n"
            + "            _name = name;\n"
            + "        }\n"
            + "    }\n"
            + "}\n";

        private static readonly MethodInfo GetActiveFolderPathMethod =
            typeof(ProjectWindowUtil).GetMethod(
                "GetActiveFolderPath",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            );

        [MenuItem("Assets/Create/Wallstop Studios/DxState/Game State", priority = 30)]
        private static void CreateGameState()
        {
            string targetFolder = ResolveTargetFolder();

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(targetFolder, DefaultScriptName)
            );
            string absolutePath = Path.GetFullPath(assetPath);

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(absolutePath, TemplateContent);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            Object createdAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (createdAsset != null)
            {
                ProjectWindowUtil.ShowCreatedAsset(createdAsset);
            }
        }

        private static string ResolveTargetFolder()
        {
            string defaultFolder = "Assets";

            if (GetActiveFolderPathMethod != null)
            {
                try
                {
                    object result = GetActiveFolderPathMethod.Invoke(null, null);
                    if (result is string path && !string.IsNullOrWhiteSpace(path))
                    {
                        return path;
                    }
                }
                catch
                {
                    // Fall back to selection-based resolution
                }
            }

            string selectionPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrWhiteSpace(selectionPath))
            {
                return defaultFolder;
            }

            if (AssetDatabase.IsValidFolder(selectionPath))
            {
                return selectionPath;
            }

            string parent = Path.GetDirectoryName(selectionPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                return parent.Replace('\\', '/');
            }

            return defaultFolder;
        }
    }
}
#endif
