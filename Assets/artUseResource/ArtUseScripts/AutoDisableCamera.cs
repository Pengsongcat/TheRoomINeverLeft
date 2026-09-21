using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif

public class AutoDisableCamera : MonoBehaviour
{
    [SerializeField] private bool disableOnAwake = true;
    [SerializeField] private int targetDisplay = 0; // 0 = Display 1

    void Awake()
    {
        if (disableOnAwake)
        {
            Camera[] cameras = GetComponentsInChildren<Camera>(true);
            foreach (Camera cam in cameras)
            {
                cam.enabled = false;
            }
        }

#if UNITY_EDITOR
        EnsureGameViewDisplay(targetDisplay);
#endif
    }

#if UNITY_EDITOR
    private static void EnsureGameViewDisplay(int displayIndex)
    {
        var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null)
        {
            Debug.LogWarning("[AutoDisableCamera] Could not find UnityEditor.GameView type.");
            return;
        }

        EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
        if (gameView == null)
        {
            Debug.LogWarning("[AutoDisableCamera] Could not get GameView window.");
            return;
        }

        // m_TargetDisplay is declared on GameView's base class (PlayModeView) since Unity 2020.1,
        // so it must be looked up by walking up the type hierarchy.
        FieldInfo targetDisplayField = FindField(gameViewType, "m_TargetDisplay");
        if (targetDisplayField == null)
        {
            Debug.LogWarning("[AutoDisableCamera] Could not find m_TargetDisplay field on GameView.");
            return;
        }

        int currentDisplay = (int)targetDisplayField.GetValue(gameView);
        if (currentDisplay != displayIndex)
        {
            targetDisplayField.SetValue(gameView, displayIndex);
            gameView.Repaint();
        }
    }

    private static FieldInfo FindField(System.Type type, string fieldName)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        for (System.Type t = type; t != null; t = t.BaseType)
        {
            FieldInfo field = t.GetField(fieldName, flags);
            if (field != null)
            {
                return field;
            }
        }
        return null;
    }
#endif
}
