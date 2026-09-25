using UnityEditor;
using UnityEngine;

namespace TRINL.Rendering.Editor
{
    [CustomEditor(typeof(TreeShadowCookie)), CanEditMultipleObjects]
    public sealed class TreeShadowCookieEditor : UnityEditor.Editor
    {
        static bool showTree = true, showCanopy = true, showWind = true, showSun = true, showOutput = true;

        // Not BeginFoldoutHeaderGroup: array fields draw their own foldout header, and Unity
        // refuses to nest those, which left the light / beam lists undrawable.
        static bool Section(bool open, string label)
        {
            EditorGUILayout.Space(2);
            return EditorGUILayout.Foldout(open, label, true, EditorStyles.foldoutHeader);
        }

        void Field(string name, string label, string tooltip = null)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label, tooltip));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var cookie = (TreeShadowCookie)target;

            showSun = Section(showSun, "阳光");
            if (showSun)
            {
                Field("sunIntensity", "太阳光强度", "光斑亮度。超过 0.8 后柔和压缩，重叠的光斑会平滑地饱和成白。");
                Field("shadowBrightness", "阴影亮度", "树荫处保留的天光，0 为全黑。场景里现有的树影灯是按接近全黑的视频阴影（约 0.06）调的强度，调高这里会让整片光池发白。");
                Field("contrast", "对比度");
                Field("blur", "光斑模糊度", "远景树冠叶隙成像出的太阳圆斑大小。越大光斑越圆越糊。");
                Field("nearBlur", "近处树枝模糊", "贴近墙面的树枝和叶子的影子柔化，越小越清晰。");
                Field("branchBlur", "远处树枝模糊", "离墙最远的树枝和叶子的影子柔化。中间的树枝在这两者之间渐变。");
                Field("halation", "晕散度", "亮斑向外扩散的光晕强度。");
                Field("halationRadius", "晕散范围", "光晕金字塔级数，每加一级范围翻倍。");
                Field("edgeFade", "边缘圆形衰减", "给聚光灯用的圆形渐隐，0 为关闭。");
            }

            showTree = Section(showTree, "树枝与树叶");
            if (showTree)
            {
                Field("leafCount", "树叶数量", "长在细枝上的叶子总数，越多越密。");
                Field("leafShape", "叶形", "Maple 枫叶（五裂带锯齿）/ Ovate 卵形叶 / Mixed 混合。");
                Field("treeLeafSize", "叶片大小");
                Field("limbCount", "主枝数量", "从画面边缘伸进来的主枝数，0 为不画树，只保留远景光斑。");
                Field("branchWidth", "枝条粗细");
                Field("treeDirection", "树的方向（度）", "主枝伸进画面的方向。0 = 从左往右，90 = 从下往上。");
                Field("treeDistance", "树离墙的距离", "0 = 整棵树贴着墙（影子清晰），1 = 离墙很远（影子很糊）。");
                Field("depthSpread", "远近分布", "各根树枝离墙远近的差异。0 = 所有树枝一样远，1 = 有近有远，模糊程度差别最大。");
            }

            showWind = Section(showWind, "风");
            if (showWind)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("起风")) foreach (TreeShadowCookie c in targets) { Undo.RecordObject(c, "Start wind"); c.StartWind(1f, c.windRamp); }
                    if (GUILayout.Button("停风")) foreach (TreeShadowCookie c in targets) { Undo.RecordObject(c, "Stop wind"); c.StopWind(c.windRamp); }
                }
                Field("windStrength", "风力（目标）", "实际风力会按下面的过渡时间平滑趋近这个值。脚本调用 SetWind / StartWind / StopWind，Timeline 直接 K 这个值。");
                if (!serializedObject.isEditingMultipleObjects)
                    EditorGUILayout.LabelField("当前实际风力", cookie.CurrentWind.ToString("0.00"));
                Field("windRamp", "过渡时间（秒）", "0 为立即生效。");
                Field("windSpeed", "摇摆速度");
                Field("windDirection", "风向（度）");
                Field("gustiness", "阵风", "阵风沿风向成片推过树冠，0 为均匀摇摆。");
                Field("branchSway", "枝条摆动", "主枝慢慢晃，细枝晃得更多，叶子跟着所在的枝一起动。");
                Field("flutter", "叶片翻转", "叶片抖动和侧转漏光，光斑会闪烁。");
                Field("windLean", "顺风倾斜", "起风时整棵树被吹向下风方向，细枝弯得最多。");
                Field("windBlurJitter", "风中模糊扰动", "起风时每处的模糊程度随风随机变化，光斑一会儿清晰一会儿糊。");
                Field("windMotionBlur", "风中拖影", "起风时影子沿风向轻微拖长，模拟运动模糊。");
                Field("idleSway", "无风微动", "风力为 0 时保留的轻微晃动，0 为完全静止。");
            }

            showCanopy = Section(showCanopy, "远景树冠（圆形光斑）");
            if (showCanopy)
            {
                Field("seed", "随机种子", "同时决定树的形态和远景树冠。");
                Field("leafSize", "远景叶片尺寸");
                Field("canopyDensity", "远景树冠密度", "越高叶隙越少，光斑越少越暗。");
                Field("clumping", "疏密变化", "大尺度的稀疏 / 茂密分布，0 为均匀。");
            }

            showOutput = Section(showOutput, "输出");
            if (showOutput)
            {
                Field("resolution", "分辨率");
                Field("updateRate", "更新频率（次/秒）", "0 为每帧。无风且无微动时不会重复渲染。");
                Field("targetTexture", "输出到 RenderTexture");
                Field("lights", "灯光 Cookie");
                Field("volumetricCookies", "VLB 体积 Cookie");
                Field("animateInEditMode", "编辑模式下播放");
                Field("shader", "Shader");
            }

            serializedObject.ApplyModifiedProperties();

            if (!serializedObject.isEditingMultipleObjects && cookie.Output != null)
            {
                EditorGUILayout.Space(4);
                float w = Mathf.Min(EditorGUIUtility.currentViewWidth - 40f, 256f);
                var rect = GUILayoutUtility.GetRect(w, w, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(rect, cookie.Output);
                if (cookie.animateInEditMode) Repaint();
            }
        }
    }
}
