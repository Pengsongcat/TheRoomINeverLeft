using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TRINL.Rendering.Editor
{
    [CustomEditor(typeof(MeshFog))]
    public sealed class MeshFogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var fog=(MeshFog)target;
            if(fog.source && fog.source.GetComponent<Renderer>())
                Undo.RecordObject(fog.source.GetComponent<Renderer>(),"编辑雾显示");
            DrawDefaultInspector();
            fog.Apply();
            EditorGUILayout.HelpBox("静态封闭 Mesh → SDF → 体积雾。Source 指定单个 MeshFilter。骨骼动画及相交雾体排序不在此版本范围内。",MessageType.Info);
            if(!fog.source && GUILayout.Button("识别本物体的 Mesh"))
            { Undo.RecordObject(fog,"识别 Mesh"); fog.source=fog.GetComponent<MeshFilter>(); EditorUtility.SetDirty(fog); }
            if(fog.shape && fog.source && fog.shape.sourceMesh!=fog.source.sharedMesh)
                EditorGUILayout.HelpBox("Source Mesh 与已烘焙形状不匹配，请重新烘焙。",MessageType.Warning);
            if(fog.shape && fog.edgeSoftness+fog.contourNoise>fog.shape.padding)
                EditorGUILayout.HelpBox("边缘柔度与扰动超过烘焙余量，可能出现边界裁切。请增加 Padding 后重新烘焙。",MessageType.Warning);
            if(!fog.shape || !fog.shape.sdf) EditorGUILayout.HelpBox("尚未烘焙形状。",MessageType.Warning);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if(GUILayout.Button("1. 烘焙形状 / 更新 SDF")) Run(()=> { MeshFogBaker.Bake(fog); if(fog.volumeRenderer) Generate(fog); });
                if(GUILayout.Button("2. 生成 / 更新雾效")) Run(()=>Generate(fog));
                if(GUILayout.Button("3. 保存为 Prefab…"))
                {
                    string path=EditorUtility.SaveFilePanelInProject("保存雾效 Prefab",fog.name,"prefab","选择保存位置");
                    if(!string.IsNullOrEmpty(path)) PrefabUtility.SaveAsPrefabAssetAndConnect(fog.gameObject,path,InteractionMode.UserAction);
                }
            }
            if(GUILayout.Button(fog.showFog?"切换为原模型":"切换为雾"))
            {
                Undo.RecordObject(fog,"切换雾显示"); fog.showFog=!fog.showFog; fog.Apply(); EditorUtility.SetDirty(fog);
            }
        }
        static void Run(Action action)
        {
            try { action(); }
            catch(OperationCanceledException e) { Debug.Log(e.Message); }
            catch(Exception e) { Debug.LogException(e); }
        }
        public static void Generate(MeshFog fog)
        {
            if(!fog.source || !fog.shape || !fog.shape.sdf) throw new InvalidOperationException("先指定 Source 并烘焙形状。");
            if(!fog.source.transform.IsChildOf(fog.transform) && fog.source.transform!=fog.transform)
                throw new InvalidOperationException("Source 必须属于此物体或其子物体，以便 Prefab 一起保存。");
            var shader=Shader.Find("TRINL/Mesh Fog Volume");
            if(!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("体积 Shader 缺失或有编译错误。");
            Undo.RecordObject(fog,"生成雾效");
            if(fog.source.GetComponent<Renderer>()) Undo.RecordObject(fog.source.GetComponent<Renderer>(),"替换原模型显示");
            if(!fog.volumeMaterial)
            {
                fog.volumeMaterial=new Material(shader) { name=fog.name+" Fog" };
                string path=AssetDatabase.GenerateUniqueAssetPath("Assets/MeshFog/Generated/"+fog.shape.name+"_Fog.mat");
                AssetDatabase.CreateAsset(fog.volumeMaterial,path);
            }
            if(!fog.volumeRenderer)
            {
                var proxy=GameObject.CreatePrimitive(PrimitiveType.Cube);
                proxy.name="FogVolume";
                Undo.RegisterCreatedObjectUndo(proxy,"创建雾代理盒");
                Undo.DestroyObjectImmediate(proxy.GetComponent<Collider>());
                Undo.SetTransformParent(proxy.transform,fog.source.transform,"绑定雾代理盒");
                fog.volumeRenderer=proxy.GetComponent<MeshRenderer>();
            }
            var renderer=fog.volumeRenderer;
            if(renderer.transform.parent!=fog.source.transform)
                Undo.SetTransformParent(renderer.transform,fog.source.transform,"更新雾代理父物体");
            Undo.RecordObjects(new UnityEngine.Object[]{renderer,renderer.transform,renderer.gameObject},"更新雾代理盒");
            renderer.gameObject.layer=0;
            renderer.transform.localPosition=fog.shape.localBounds.center;
            renderer.transform.localRotation=Quaternion.identity;
            renderer.transform.localScale=fog.shape.localBounds.size;
            renderer.sharedMaterial=fog.volumeMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            fog.Apply();
            EditorUtility.SetDirty(fog); EditorUtility.SetDirty(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fog);
            AssetDatabase.SaveAssets(); SceneView.RepaintAll();
        }
    }
}
