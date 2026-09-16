using UnityEditor;
using UnityEngine;

namespace TRINL.Rendering.Editor
{
    [CustomEditor(typeof(ScreenBlurOverride)), CanEditMultipleObjects]
    public sealed class ScreenBlurOverrideEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "本组件独立控制所属 Renderer 的 ScreenBlur 参数。物体仍需处于 Feature 配置的 ScreenBlur Layer；" +
                "停用组件恢复父级覆盖或全局参数。子物体上的启用组件优先。",
                MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("includeChildren"), new GUIContent("包含子物体"));

            if (GUILayout.Button("从 Renderer 全局复制参数"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Settings/VR_Renderer.asset");
                var global = data ? data.rendererFeatures.Find(f => f is ScreenBlurRendererFeature) as ScreenBlurRendererFeature : null;
                if (global)
                {
                    Undo.RecordObjects(targets, "Copy global ScreenBlur settings");
                    foreach (ScreenBlurOverride o in targets) { o.CopyFrom(global); EditorUtility.SetDirty(o); }
                    serializedObject.Update();
                }
                else EditorUtility.DisplayDialog("ScreenBlur",
                    "在 Assets/Settings/VR_Renderer.asset 上没有找到 ScreenBlur Renderer Feature。", "好");
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("模糊", EditorStyles.boldLabel);
            var radius = serializedObject.FindProperty("radius");
            EditorGUILayout.PropertyField(radius, new GUIContent("模糊范围（像素）", "高斯 sigma，单位为相机渲染像素；0 为无模糊。核覆盖正负 3 sigma。"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("opacity"), new GUIContent("合成不透明度", "1 完全显示，0 隐藏该组（不等于停用组件）。"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("depthBias"), new GUIContent("遮挡容差（米）", "深度比较容差，单位为相机空间距离。"));

            if (!radius.hasMultipleDifferentValues && radius.floatValue > 24f)
                EditorGUILayout.HelpBox(
                    $"当前范围 {radius.floatValue:0.#}，每个方向约 {Mathf.CeilToInt(radius.floatValue * 3f) * 2 + 1} 个采样位置。" +
                    "尚未实现降采样优化，VR 双眼下开销较高，请在头显实测 GPU 帧时间。",
                    MessageType.Warning);

            EditorGUILayout.HelpBox(
                "每个独立覆盖组会额外重跑一遍捕获与合成，组数越多开销越高。未挂本组件的物体使用 Feature 的全局参数，无需额外 Layer。",
                MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
