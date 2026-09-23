using UnityEditor;
using UnityEditor.SceneManagement;

namespace Ronriku.Editor
{
    [InitializeOnLoad]
    internal static class RonrikuEditorStartup
    {
        static RonrikuEditorStartup()
        {
            EditorApplication.update += OpenBootstrapWhenWorkspaceIsEmpty;
        }

        private static void OpenBootstrapWhenWorkspaceIsEmpty()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
            {
                EditorApplication.update -= OpenBootstrapWhenWorkspaceIsEmpty;
                return;
            }
            const string path = "Assets/Ronriku/Scenes/Bootstrap.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                EditorApplication.update -= OpenBootstrapWhenWorkspaceIsEmpty;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
        }
    }
}
