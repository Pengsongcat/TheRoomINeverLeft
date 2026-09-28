using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(DoorExitDissolve))]
public sealed class DoorExitDissolveEditor : Editor
{
    DoorExitDissolve effect;
    bool preview;
    double last;
    void OnEnable(){effect=(DoorExitDissolve)target;AssemblyReloadEvents.beforeAssemblyReload+=Stop;EditorSceneManager.sceneSaving+=OnSaving;EditorApplication.playModeStateChanged+=OnPlay;}
    void OnDisable(){Stop();AssemblyReloadEvents.beforeAssemblyReload-=Stop;EditorSceneManager.sceneSaving-=OnSaving;EditorApplication.playModeStateChanged-=OnPlay;}
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if(GUILayout.Button("预览：门从上向下溶解")){Stop();effect.BeginDissolve();preview=effect.Triggered;last=EditorApplication.timeSinceStartup;if(preview)EditorApplication.update+=Tick;}
            if(GUILayout.Button("停止预览 / 恢复门"))Stop();
        }
        if(preview||Application.isPlaying)EditorGUILayout.LabelField("溶解进度",effect.Progress.ToString("P0"));
    }
    void Tick(){if(!effect||EditorApplication.isPlayingOrWillChangePlaymode){Stop();return;}double now=EditorApplication.timeSinceStartup;effect.Advance((float)(now-last));last=now;SceneView.RepaintAll();Repaint();}
    void Stop(){if(!preview)return;preview=false;EditorApplication.update-=Tick;if(effect)effect.Restore();SceneView.RepaintAll();}
    void OnSaving(Scene scene,string path){Stop();}
    void OnPlay(PlayModeStateChange state){Stop();}
}
