#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using WallstopStudios.DxState.State.Stack.Builder.Templates;

    internal static class StateGraphTemplateCache
    {
        private static List<StateGraphTemplate> _templates;

        internal static IReadOnlyList<StateGraphTemplate> GetTemplates()
        {
            if (_templates == null)
            {
                Refresh();
            }

            return _templates;
        }

        internal static void Refresh()
        {
            string[] guids = AssetDatabase.FindAssets("t:StateGraphTemplate");
            List<StateGraphTemplate> templates = new List<StateGraphTemplate>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                StateGraphTemplate template = AssetDatabase.LoadAssetAtPath<StateGraphTemplate>(path);
                if (template == null)
                {
                    continue;
                }

                templates.Add(template);
            }

            templates.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal));
            _templates = templates;
        }
    }
}
#endif
