using UnityEditor;
using UnityEngine;
namespace TRINL.Rendering.Editor
{
    [CustomEditor(typeof(GlobleBlurOverride)), CanEditMultipleObjects]
    public sealed class GlobleBlurOverrideEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("独立控制 GlobleBlur。物体仍需位于 GlobleBlur 图层。子物体上启用的组件优先；停用恢复父级或全局参数。Ground 遮挡层继承 Renderer 全局配置。", MessageType.Info);
            Draw("includeChildren", "包含子物体");
            if (GUILayout.Button("从 Renderer 全局复制参数"))
            {
                serializedObject.ApplyModifiedProperties();
                var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Settings/VR_Renderer.asset");
                var global = data ? data.rendererFeatures.Find(f => f is ScreenDiffusionRendererFeature) as ScreenDiffusionRendererFeature : null;
                if (global)
                {
                    Undo.RecordObjects(targets, "Copy global GlobleBlur settings");
                    foreach (GlobleBlurOverride o in targets) { o.CopyFrom(global); EditorUtility.SetDirty(o); }
                    serializedObject.Update();
                }
            }
            Draw("iterations", "模糊级数");
            Draw("spread", "扩散范围");
            Draw("strength", "合成强度", "0 隐藏本组；停用组件才会恢复父级或全局设置。");
            Draw("blendMode", "混合模式", "Screen 在 Layer 模式下按 Mix 处理。");
            Draw("maxPyramid", "保留峰值颜色");
            Draw("occlusionDepthBias", "遮挡容差（米）");
            EditorGUILayout.HelpBox("支持 MeshRenderer / SkinnedMeshRenderer。每个独立覆盖组增加一套捕获与模糊处理；可用包含子物体将多个物体合并控制。", MessageType.None);
            serializedObject.ApplyModifiedProperties();
        }
        void Draw(string property, string label, string tooltip = null) => EditorGUILayout.PropertyField(serializedObject.FindProperty(property), new GUIContent(label, tooltip));
    }
}
