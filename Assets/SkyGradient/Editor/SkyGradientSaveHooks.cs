using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace TRINL.Rendering.Editor
{
    // Keep temporary camera overrides and materials out of the saved scene.
    [InitializeOnLoad]
    static class SkyGradientSaveHooks
    {
        static SkyGradientSaveHooks()
        {
            EditorSceneManager.sceneSaving += (scene,path) => {
                foreach(var c in Resources.FindObjectsOfTypeAll<SkyGradientController>())
                    if(c.gameObject.scene==scene) c.SuspendPreview();
            };
            EditorSceneManager.sceneSaved += scene => {
                foreach(var c in Resources.FindObjectsOfTypeAll<SkyGradientController>())
                    if(c.gameObject.scene==scene && c.isActiveAndEnabled) c.Apply();
            };
        }
    }
}
