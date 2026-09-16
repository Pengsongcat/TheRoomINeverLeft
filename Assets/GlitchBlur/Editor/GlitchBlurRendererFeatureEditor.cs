using UnityEditor;
using UnityEngine;

namespace TRINL.Rendering.Editor
{
    [CustomEditor(typeof(GlitchBlurRendererFeature)), CanEditMultipleObjects]
    public sealed class GlitchBlurRendererFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("layerMask"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cylinderTrails"), new GUIContent("三维圆柱拖影"));
            bool cylindrical = serializedObject.FindProperty("cylinderTrails").boolValue;
            if (cylindrical)
            {
                EditorGUILayout.LabelField("Cylinder Settings", EditorStyles.boldLabel);
                var lines=serializedObject.FindProperty("continuousLines");
                EditorGUI.showMixedValue=lines.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                bool next=GUILayout.Toggle(lines.boolValue,lines.boolValue ? "连续拉伸成线：开启" : "连续拉伸成线：关闭（点状残影）","Button");
                if(EditorGUI.EndChangeCheck())lines.boolValue=next;
                EditorGUI.showMixedValue=false;
                if(lines.boolValue)EditorGUILayout.HelpBox("把相邻采样连接成连续弧线。拖影采样数控制弧线分段精度；关闭可恢复原点状效果。",MessageType.None);
                if(lines.boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("lineLengthMultiplier"),new GUIContent("线条长度倍率"));
                    Pair("lineRandomRange","随机长度倍率：最小 / 最大");
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("lineRandomCellSize"),new GUIContent("随机区域大小（米）"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("lineRandomSeed"),new GUIContent("随机种子"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("overlayPoints"),new GUIContent("叠加点状残影"));
                    if(serializedObject.FindProperty("overlayPoints").boolValue)
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("overlayPointWeight"),new GUIContent("点状叠加强度"));
                    EditorGUILayout.HelpBox("线长 = 原拖影长度 × 线条倍率 × 随机倍率。随机范围设为 1 / 1 可关闭随机；点状残影保留原拖影长度。随机分布固定在世界空间，物体移动跨区域时会变化。",MessageType.None);
                }
                Pair("cylinderDistances", "响应距离（米）：近 / 远");
                Pair("cylinderRadii", "圆柱半径（米）：近 / 远");
                Pair("cylinderLengths", "拖影长度（米）：内 / 外");
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cylinderTowardPlayer"), new GUIContent("朝玩家弯曲"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cylinderSamples"), new GUIContent("拖影采样数"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cylinderResolution"), new GUIContent("捕获宽度上限"));
                EditorGUILayout.HelpBox("自动使用 Main Camera。按可见表面的水平距离控制曲率；拖影拥有重新投影的三维位置和遮挡。仅包含捕获到的表面。提高采样数和捕获宽度会增加 GPU 开销。", MessageType.Info);
            }
            EditorGUILayout.HelpBox("左侧数值 = 内圈，右侧数值 = 外圈；中间自动平滑过渡。拖影影响越大，效果越强。", MessageType.Info);
            if (!cylindrical) Endpoints("拖影长度（像素）", "horizontalLength", "outerHorizontalLength", 256, false);
            Endpoints("拖影影响（%）", "originalWeight", "outerOriginalWeight", 100, true);
            EditorGUILayout.Space(5);
            GlitchUpwardInspector.Draw(serializedObject);
            EditorGUILayout.LabelField("Gaussian Blur · 高斯模糊", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gaussianRadius"), new GUIContent("模糊范围（像素）", "控制左右和上下的高斯柔化范围；0 为关闭。像素以相机输出分辨率计算。"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gaussianDistanceResponse"), new GUIContent("强度随玩家距离变化"));
            if (serializedObject.FindProperty("gaussianDistanceResponse").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gaussianResponseDistance"), new GUIContent("响应距离（米）", "达到或超过该距离时使用远处强度。"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gaussianNearStrength"), new GUIContent("近处强度", "距离趋近 0 时的模糊混合强度。"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gaussianFarStrength"), new GUIContent("远处强度"));
                EditorGUILayout.HelpBox("自动使用 Main Camera。根据可见拖影表面到玩家的三维距离平滑插值，超过响应距离保持远处强度。", MessageType.None);
            }
            else EditorGUILayout.PropertyField(serializedObject.FindProperty("gaussianStrength"), new GUIContent("模糊混合强度", "0 保留原拖影，1 完全使用高斯模糊结果。"));
            EditorGUILayout.Space(5);
            foreach (string name in new[] { "verticalSoftness", "bandJitter", "bandCount", "driftSpeed", "downsample", "opacity", "depthBias", "blurShader" })
                EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            if (serializedObject.ApplyModifiedProperties()) SceneView.RepaintAll();
        }

        void Pair(string name, string label)
        {
            var property = serializedObject.FindProperty(name);
            EditorGUILayout.LabelField(label);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(property.FindPropertyRelative("x"), GUIContent.none);
            EditorGUILayout.LabelField("→", GUILayout.Width(16));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("y"), GUIContent.none);
            EditorGUILayout.EndHorizontal();
        }

        void Endpoints(string title, string innerName, string outerName, float maximum, bool invertWeight)
        {
            var inner = serializedObject.FindProperty(innerName);
            var outer = serializedObject.FindProperty(outerName);
            float a = invertWeight ? (1 - inner.floatValue) * 100 : inner.floatValue;
            float b = invertWeight ? (1 - outer.floatValue) * 100 : outer.floatValue;
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect rect = EditorGUILayout.GetControlRect(false, 32);
            const float fieldWidth = 55;
            Rect left = new Rect(rect.x, rect.y + 7, fieldWidth, EditorGUIUtility.singleLineHeight);
            Rect right = new Rect(rect.xMax - fieldWidth, left.y, fieldWidth, left.height);
            Rect track = new Rect(left.xMax + 7, rect.y + 2, Mathf.Max(1, rect.width - 2 * fieldWidth - 14), 28);

            EditorGUI.showMixedValue = inner.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            float nextA = EditorGUI.FloatField(left, a);
            if (EditorGUI.EndChangeCheck())
            {
                a = Mathf.Clamp(nextA, 0, maximum);
                inner.floatValue = invertWeight ? 1 - a / 100 : a;
            }
            EditorGUI.showMixedValue = outer.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            float nextB = EditorGUI.FloatField(right, b);
            if (EditorGUI.EndChangeCheck())
            {
                b = Mathf.Clamp(nextB, 0, maximum);
                outer.floatValue = invertWeight ? 1 - b / 100 : b;
            }
            EditorGUI.showMixedValue = false;

            // The preview retains endpoint identity, including decreasing gradients.
            // A MinMaxSlider would reorder inner/outer values and lose that meaning.
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(track, new Color(0.1f, 0.1f, 0.1f, 0.55f));
                Handles.BeginGUI();
                Color previous = Handles.color;
                Handles.color = new Color(0.35f, 0.7f, 1, 1);
                Vector3 last = Vector3.zero;
                for (int i = 0; i <= 32; i++)
                {
                    float t = i / 32f;
                    float value = Mathf.Lerp(a, b, Mathf.SmoothStep(0, 1, t)) / maximum;
                    var point = new Vector3(track.x + t * track.width, track.yMax - 3 - value * (track.height - 6));
                    if (i > 0) Handles.DrawAAPolyLine(2, last, point);
                    last = point;
                }
                Handles.color = previous;
                Handles.EndGUI();
            }
            GUI.Label(track, new GUIContent("", "内圈 → 外圈，自动平滑插值。可直接编辑左右数值，支持内圈大于外圈。"));
        }
    }
}
