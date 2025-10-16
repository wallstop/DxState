#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State.Serialization
{
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack.Builder;

    internal static class StateGraphSerializationMenu
    {
        [MenuItem("Assets/Wallstop Studios/DxState/Export State Graph JSON", true)]
        private static bool ExportEnabled()
        {
            return Selection.activeObject is StateGraphAsset;
        }

        [MenuItem("Assets/Wallstop Studios/DxState/Export State Graph JSON", false, 2050)]
        private static void Export()
        {
            StateGraphAsset asset = Selection.activeObject as StateGraphAsset;
            if (asset == null)
            {
                return;
            }

            string defaultFileName = string.Concat(asset.name, "_Graph.json");
            string directory = Application.dataPath;
            string path = EditorUtility.SaveFilePanel(
                "Export State Graph",
                directory,
                defaultFileName,
                "json"
            );
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string json = StateGraphJsonUtility.ExportToJson(asset, true);
            File.WriteAllText(path, json);
            EditorUtility.DisplayDialog(
                "Export Complete",
                $"State graph exported to:\n{path}",
                "OK"
            );
        }

        [MenuItem("Assets/Wallstop Studios/DxState/Import State Graph JSON", true)]
        private static bool ImportEnabled()
        {
            return Selection.activeObject is StateGraphAsset;
        }

        [MenuItem("Assets/Wallstop Studios/DxState/Import State Graph JSON", false, 2051)]
        private static void Import()
        {
            StateGraphAsset asset = Selection.activeObject as StateGraphAsset;
            if (asset == null)
            {
                return;
            }

            string path = EditorUtility.OpenFilePanel(
                "Import State Graph",
                Application.dataPath,
                "json"
            );
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string json = File.ReadAllText(path);
            StateGraphJsonUtility.ImportFromJson(asset, json);
            EditorUtility.DisplayDialog(
                "Import Complete",
                $"State graph imported from:\n{path}",
                "OK"
            );
        }
    }
}
#endif
