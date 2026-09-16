using UnityEditor;
using UnityEngine;

namespace TRINL.Rendering.Editor
{
    internal static class GlitchUpwardInspector
    {
        internal static void Draw(SerializedObject obj)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("朝上表面响应 · 世界 Y 轴",EditorStyles.boldLabel);
            Field(obj,"upwardResponse","启用朝向权重");
            if(!obj.FindProperty("upwardResponse").boolValue)return;
            Field(obj,"upwardThreshold","朝上识别阈值","1 是水平朝上，0 是竖直侧面，-1 是朝下。");
            Field(obj,"upwardSoftness","过渡柔和度");
            Field(obj,"upwardLength","朝上区域拉伸倍率");
            Field(obj,"otherLength","其他区域拉伸倍率");
            Field(obj,"upwardContribution","朝上区域拖影贡献","0 保留原像，1 完整应用拖影设置。");
            Field(obj,"otherContribution","其他区域拖影贡献","减少贡献时保留更多原像，不会直接删除模型。");
            Field(obj,"upwardTwoSided","双面法线修正","查看薄片背面时翻转法线；若资产已自定义正反面法线，可关闭比较。");
            Field(obj,"upwardPreview","预览朝向遮罩","白色影响大，黑色影响小。预览时跳过拖影与高斯柔化。");
            EditorGUILayout.HelpBox("依据表面朝向，不依据物体高度或实际灯光。调好遮罩后关闭预览即可查看拖影。",MessageType.None);
        }
        static void Field(SerializedObject obj,string name,string label,string tip=null)
        {EditorGUILayout.PropertyField(obj.FindProperty(name),new GUIContent(label,tip));}
    }
}
