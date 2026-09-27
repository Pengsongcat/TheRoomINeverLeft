using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(DoorOpeningController))]
public sealed class DoorOpeningControllerEditor : Editor
{
    DoorOpeningController controller;
    Transform previewLeaf;
    bool previewing;
    double lastTime;

    void OnEnable()
    {
        controller = (DoorOpeningController)target;
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        Undo.undoRedoPerformed += StopPreview;
    }

    void OnDisable()
    {
        StopPreview();
        AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        Undo.undoRedoPerformed -= StopPreview;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (Application.isPlaying)
        {
            var camera = Camera.main;
            EditorGUILayout.HelpBox(camera
                ? "Main Camera: " + camera.name + " | 触发距离: " + controller.GetTriggerDistance(camera.transform.position).ToString("F2") + " m / " + controller.triggerDistance.ToString("F2") + " m\n" + controller.EditorPreviewStatus
                : "未找到启用且标记为 MainCamera 的摄像机。", MessageType.Info);
            Repaint();
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Edit Mode Preview / 编辑模式预览", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("无需 Play。完整流程模拟摄像机已就绪：延迟 → 虚掩 → 风吹。预览忽略真实摄像机距离；可随时点击完全打开测试打断。停止、切换选择、保存场景或进入 Play 会恢复原姿态。", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(controller)))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("完整流程（含延迟）")) PlayPreview(0);
                if (GUILayout.Button("虚掩开启")) PlayPreview(1);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("风吹晃动")) PlayPreview(3);
                if (GUILayout.Button("完全打开 + 柔和回弹")) PlayPreview(2);
            }
            using (new EditorGUI.DisabledScope(!previewing))
                if (GUILayout.Button("停止预览 / 恢复原姿态")) StopPreview();
        }
        if (previewing) EditorGUILayout.LabelField(controller.EditorPreviewStatus);
    }

    public void PlayPreview(int mode)
    {
        if (!controller || EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(controller)) return;
        Transform requestedLeaf = controller.doorLeaf ? controller.doorLeaf.transform : controller.transform;
        if (previewing && requestedLeaf != previewLeaf) StopPreview();
        if (!previewing)
        {
            if (AnimationMode.InAnimationMode())
            {
                Debug.LogWarning("请先停止其他 Animation 窗口或 Inspector 的动画预览。", controller);
                return;
            }
            previewLeaf = requestedLeaf;
            AnimationMode.StartAnimationMode();
            // Register all quaternion channels so Unity restores the exact original pose.
            var serialized = new SerializedObject(previewLeaf);
            foreach (string axis in new[] { "x", "y", "z", "w" })
            {
                string path = "m_LocalRotation." + axis;
                var binding = EditorCurveBinding.FloatCurve("", typeof(Transform), path);
                var modification = new PropertyModification {
                    target = previewLeaf, propertyPath = path,
                    value = serialized.FindProperty(path).floatValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                };
                AnimationMode.AddPropertyModification(binding, modification, true);
            }
            controller.BeginEditorPreview();
            previewing = true;
            EditorApplication.update += UpdatePreview;
        }
        var audio = controller.GetComponent<DoorAudioController>();
        if (audio) audio.BeginAudioPreview();
        controller.PlayEditorPreview(mode);
        lastTime = EditorApplication.timeSinceStartup;
        SceneView.RepaintAll();
        Repaint();
    }

    void UpdatePreview()
    {
        if (!controller || !previewLeaf || EditorApplication.isPlayingOrWillChangePlaymode ||
            !AnimationMode.InAnimationMode() ||
            (controller.doorLeaf ? controller.doorLeaf.transform : controller.transform) != previewLeaf)
        { StopPreview(); return; }
        double now = EditorApplication.timeSinceStartup;
        controller.TickEditorPreview(Mathf.Min((float)(now - lastTime), 0.1f));
        lastTime = now;
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
        Repaint();
    }

    public void StopPreview()
    {
        if (!previewing) return;
        var audio = controller ? controller.GetComponent<DoorAudioController>() : null;
        if (audio) audio.EndAudioPreview();
        previewing = false;
        EditorApplication.update -= UpdatePreview;
        AnimationMode.StopAnimationMode();
        previewLeaf = null;
        SceneView.RepaintAll();
        Repaint();
    }

    void OnPlayModeChanged(PlayModeStateChange state) { StopPreview(); }
    void OnSceneSaving(Scene scene, string path) { StopPreview(); }
}



