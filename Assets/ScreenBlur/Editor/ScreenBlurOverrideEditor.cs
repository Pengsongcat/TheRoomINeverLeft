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

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("表面噪波", EditorStyles.boldLabel);
            var overrideNoise = serializedObject.FindProperty("overrideNoise");
            EditorGUILayout.PropertyField(overrideNoise, new GUIContent("独立控制噪波", "不勾选时继承 Renderer 全局噪波。勾选后可独立开启或关闭。"));
            using (new EditorGUI.DisabledScope(!overrideNoise.hasMultipleDifferentValues && !overrideNoise.boolValue))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("enableNoise"), new GUIContent("启用噪波"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("noisePlacement"), new GUIContent("处理顺序", "Before Blur：噪波一起模糊；After Blur：模糊后叠加噪波。"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseTexture"), new GUIContent("噪波贴图", "留空使用内置三维噪波。"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseSize"), new GUIContent("噪波大小", "模型最长尺寸的比例；越大颗粒越大。"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseStrength"), new GUIContent("噪波强度"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("animateNoise"), new GUIContent("噪波流动"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseVelocity"), new GUIContent("流动速度 / 方向"));
            }
            if (!overrideNoise.hasMultipleDifferentValues && !overrideNoise.boolValue)
                EditorGUILayout.HelpBox("当前继承 Renderer 的全局噪波；下方灰色数值是本组件保存的独立参数，不代表全局实时值。", MessageType.None);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("相机颗粒 / 局部过曝", EditorStyles.boldLabel);
            var overrideGrain = serializedObject.FindProperty("overrideCameraGrain");
            EditorGUILayout.PropertyField(overrideGrain, new GUIContent("独立控制相机颗粒"));
            using (new EditorGUI.DisabledScope(!overrideGrain.hasMultipleDifferentValues && !overrideGrain.boolValue))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("enableCameraGrain"), new GUIContent("启用相机颗粒与曝光"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraGrainStrength"), new GUIContent("颗粒强度"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraGrainSize"), new GUIContent("颗粒大小（渲染像素）"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraGrainColor"), new GUIContent("彩色噪点比例"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraGrainSpeed"), new GUIContent("每秒变化次数", "0 为静止颗粒。"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraExposure"), new GUIContent("曝光增益（EV）", "+1 为两倍亮度。只处理本组的颜色。"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraClipHighlights"), new GUIContent("高光截白", "截断超过白点的颜色，模拟过曝细节丢失；关闭则保留 HDR 高光。"));
            }
            EditorGUILayout.HelpBox("颗粒在模糊后叠加，只限本组模糊覆盖范围（包含软边缘），不会给整个背景加噪点。未独立控制时继承 Renderer 全局设置。", MessageType.None);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
