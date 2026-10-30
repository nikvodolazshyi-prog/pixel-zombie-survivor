using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelZombieSurvivor.Editor
{
    /// <summary>
    /// First-open convenience: if the generated arena is missing, create the whole
    /// demo automatically once scripts have compiled.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectAutoSetup
    {
        static ProjectAutoSetup()
        {
            EditorApplication.delayCall += TryGenerate;
        }

        private static void TryGenerate()
        {
            if (Application.isBatchMode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (File.Exists(DemoProjectBuilder.ScenePath))
            {
                return;
            }

            if (EditorSceneManager.GetActiveScene().isDirty &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            try
            {
                DemoProjectBuilder.BuildAll();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Pixel Zombie Survivor",
                    "Не удалось автоматически собрать проект. Откройте Console и запустите Tools > Pixel Zombie Survivor > Build Demo Project.",
                    "Понятно");
            }
        }
    }
}
