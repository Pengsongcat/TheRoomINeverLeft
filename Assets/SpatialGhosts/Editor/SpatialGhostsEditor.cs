using UnityEditor;
using UnityEngine;

namespace TRINL.Rendering.Editor
{
    [CustomEditor(typeof(SpatialGhosts)), CanEditMultipleObjects]
    public sealed class SpatialGhostsEditor : UnityEditor.Editor
    {
        const string MaterialPath = "Assets/SpatialGhosts/SpatialGhost.mat";

        void Field(string property, string label, string tooltip = null)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(property), new GUIContent(label, tooltip), true);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("三维几何残影：左右各 N 份，原模型保持原样。间距单位为世界米。", MessageType.Info);
            Field("sourceRoot", "目标根物体（可留空）");
            Field("copiesPerSide", "左右各几份");
            Field("directionSpace", "方向坐标系", "Local：跟随目标转动；World：固定世界方向；Camera：每份残影整体面向相机位置（Billboard）。");
            var mode = serializedObject.FindProperty("directionSpace");
            bool cylinder = !mode.hasMultipleDifferentValues && mode.enumValueIndex == (int)SpatialGhosts.DirectionSpace.Cylinder;
            if (cylinder)
            {
                EditorGUILayout.LabelField("Cylinder Settings · 圆柱拉伸", EditorStyles.boldLabel);
                Field("directionCamera", "玩家相机（自动 Main）");
                Pair("cylinderDistances", "响应距离（米）", "近", "远");
                Pair("cylinderRadii", "圆柱半径（米）", "近", "远");
                Field("cylinderLength", "每侧展开长度（米）");
                Field("cylinderTowardPlayer", "两端朝玩家弯曲");
                Field("cylinderMaterial", "圆柱形变材质");
                EditorGUILayout.HelpBox("按玩家水平距离自动平滑插值。半径越小弯曲越强；统一绕世界竖直轴弯曲，主模型保持原样。展开长度为最外层残影中心的弧长。", MessageType.None);
                if (targets.Length == 1)
                    EditorGUILayout.LabelField("当前半径", ((SpatialGhosts)target).GetCylinderRadius().ToString("F2") + " 米");
            }
            else Field("spacing", "重影间距（米）");
            if (mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)SpatialGhosts.DirectionSpace.Camera)
            {
                Field("directionCamera", "面向相机（自动 Main）", "留空会自动赋值 Camera.main，也支持运行时生成的主相机。手动指定的相机会保留。");
                Field("billboardRotationOffset", "模型正面校正（角度）", "默认根物体 +Z 面向相机；若模型正面相反，Y 设为 180。");
                EditorGUILayout.HelpBox("Camera 模式让每份残影整体朝向相机。相机绕到侧面时残影也会转向，原物体保持原样。", MessageType.None);
            }
            if (!cylinder) Field("direction", "展开方向", "例如 (1,0,0) 表示 X 轴。零向量自动按 X 轴处理。");
            Field("stretchPerStep", "每层增加的拉伸", "0.35：近影 1.35 倍、第二层 1.70 倍。");
            Field("opacity", "近侧残影透明度");
            Field("opacityDecay", "透明度衰减倍率", "每远一层乘一次此值；越小消失越快。");
            Field("tint", "残影颜色");
            Field("edgeSoftness", "轮廓淡化");
            Field("pivotOffset", "拉伸支点（目标局部坐标）");
            if (!cylinder) Field("ghostMaterial", "Shader Graph 材质");
            EditorGUILayout.HelpBox("残影自动继承每个源 Renderer 所在物体的 Layer；修改源物体 Layer 后立即跟随。", MessageType.None);
            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button(cylinder ? "使用附带圆柱形变材质" : "使用附带 Shader Graph 材质"))
            {
                Undo.RecordObjects(targets, "Assign spatial ghost material");
                foreach (SpatialGhosts effect in targets)
                {
                    if (cylinder) effect.cylinderMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/SpatialGhosts/SpatialGhostCylinder.mat");
                    else effect.ghostMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                    EditorUtility.SetDirty(effect);
                }
            }
            if (GUILayout.Button("刷新模型 / 材质列表"))
                foreach (SpatialGhosts effect in targets) effect.RefreshSources();
            if (GUI.changed) SceneView.RepaintAll();
        }

        void Pair(string name, string label, string left, string right)
        {
            var p = serializedObject.FindProperty(name);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            float previousWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 22;
            EditorGUILayout.PropertyField(p.FindPropertyRelative("x"), new GUIContent(left));
            EditorGUILayout.PropertyField(p.FindPropertyRelative("y"), new GUIContent(right));
            EditorGUIUtility.labelWidth = previousWidth;
            EditorGUILayout.EndHorizontal();
        }

        [MenuItem("GameObject/TRINL/Add Spatial Ghosts", false, 20)]
        static void AddToSelection()
        {
            foreach (var go in Selection.gameObjects)
            {
                var effect = go.GetComponent<SpatialGhosts>() ?? Undo.AddComponent<SpatialGhosts>(go);
                Undo.RecordObject(effect, "Configure spatial ghosts");
                effect.ghostMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                EditorUtility.SetDirty(effect);
            }
            SceneView.RepaintAll();
        }

        [MenuItem("GameObject/TRINL/Add Spatial Ghosts", true)]
        static bool CanAdd() => Selection.activeGameObject != null && !EditorUtility.IsPersistent(Selection.activeGameObject);
    }
}
