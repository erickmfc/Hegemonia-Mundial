using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EditorSelectionRuntimeGuard
{
    static readonly Object[] EmptySelection = new Object[0];

    static EditorSelectionRuntimeGuard()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        AssemblyReloadEvents.beforeAssemblyReload -= ClearSelection;
        AssemblyReloadEvents.beforeAssemblyReload += ClearSelection;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
        {
            ClearSelection();
        }
    }

    static void OnEditorUpdate()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Object[] selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            return;
        }

        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i] == null)
            {
                ClearSelection();
                return;
            }
        }
    }

    static void ClearSelection()
    {
        Selection.activeObject = null;
        Selection.objects = EmptySelection;
    }
}
