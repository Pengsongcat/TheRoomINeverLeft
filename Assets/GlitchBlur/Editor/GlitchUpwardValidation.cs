using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TRINL.Rendering.Editor
{
    public static class GlitchUpwardValidation
    {
        [MenuItem("Tools/TRINL/Validate Glitch Upward Surfaces")]
        public static void Run()
        {
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VR_Renderer.asset");
            var effect=renderer.rendererFeatures.Find(x=>x is GlitchBlurRendererFeature) as GlitchBlurRendererFeature;
            if(!effect)throw new Exception("Glitch feature missing");
            string saved=EditorJsonUtility.ToJson(effect);
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.hideFlags=HideFlags.HideInHierarchy;
            var cameraObject=new GameObject("Upward validation camera"){hideFlags=HideFlags.HideAndDontSave};
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")){hideFlags=HideFlags.HideAndDontSave};
            mat.SetColor("_BaseColor",Color.white);cube.GetComponent<Renderer>().sharedMaterial=mat;
            var target=new RenderTexture(512,512,24);var pixels=new Texture2D(512,512,TextureFormat.RGB24,false);
            var old=RenderTexture.active;
            const string folder="Library/GlitchUpwardValidation";Directory.CreateDirectory(folder);
            try
            {
                cube.layer=LayerMask.NameToLayer("GlitchBlur");cube.transform.position=new Vector3(1000,1000,0);
                camera.transform.position=cube.transform.position+new Vector3(2,3,-4);camera.transform.LookAt(cube.transform.position);
                camera.orthographic=true;camera.orthographicSize=2;
                camera.nearClipPlane=.1f;camera.farClipPlane=20;
                camera.cullingMask=1<<cube.layer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.allowHDR=false;camera.allowMSAA=false;camera.targetTexture=target;
                effect.upwardResponse=true;effect.upwardPreview=true;effect.upwardTwoSided=true;
                effect.upwardThreshold=.35f;effect.upwardSoftness=.3f;
                effect.upwardLength=1.5f;effect.otherLength=.2f;
                effect.upwardContribution=1;effect.otherContribution=0;
                effect.cylinderTrails=false;effect.downsample=1;effect.opacity=1;
                effect.verticalSoftness=0;effect.bandJitter=0;effect.gaussianRadius=0;
                effect.horizontalLength=effect.outerHorizontalLength=50;
                effect.originalWeight=effect.outerOriginalWeight=0;
                var mask=Capture(camera,pixels,folder+"/mask.png");
                int top=Read(mask,camera,cube.transform.position+Vector3.up*.5f);
                int side=Read(mask,camera,cube.transform.position+Vector3.back*.5f);
                if(top<230||side>20)throw new Exception($"World-up mask failed: top={top}, side={side}");
                effect.upwardPreview=false;
                var flat=Capture(camera,pixels,folder+"/upward-flat.png");
                int preserved=Read(flat,camera,cube.transform.position+Vector3.back*.5f);
                if(preserved<200)throw new Exception("Non-upward surface disappeared instead of retaining its source");
                effect.upwardResponse=false;
                var uniform=Capture(camera,pixels,folder+"/uniform-flat.png");
                if(Difference(flat,uniform)<500)throw new Exception("Flat trail did not respond to normals");
                effect.upwardResponse=true;effect.cylinderTrails=true;
                effect.cylinderLengths=new Vector2(.8f,.8f);effect.cylinderSamples=17;effect.cylinderResolution=256;
                var cylindrical=Capture(camera,pixels,folder+"/upward-cylinder.png");
                effect.upwardResponse=false;
                var uniformCylinder=Capture(camera,pixels,folder+"/uniform-cylinder.png");
                if(Difference(cylindrical,uniformCylinder)<100)throw new Exception("Cylinder trail did not respond to normals");
                effect.upwardResponse=true;effect.upwardPreview=true;
                var local=cube.AddComponent<GlitchBlurOverride>();local.CopyFrom(effect);
                local.upwardResponse=false;local.cylinderTrails=false;local.originalWeight=local.outerOriginalWeight=1;
                var overridePixels=Capture(camera,pixels,folder+"/local-disabled.png");
                if(Read(overridePixels,camera,cube.transform.position+Vector3.back*.5f)<200)
                    throw new Exception("Local orientation toggle did not override global preview");
                local.enabled=false;
                var restored=Capture(camera,pixels,folder+"/global-restored.png");
                if(Read(restored,camera,cube.transform.position+Vector3.back*.5f)>20)
                    throw new Exception("Global preview not restored after local disabled");
                string report=$"PASS: top mask={top}, side mask={side}; side source retained={preserved}; flat and cylinder normals alter trails; local override and disable restore passed. Desktop only.";
                File.WriteAllText(folder+"/validation.txt",report);Debug.Log(report);
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(saved,effect);camera.targetTexture=null;RenderTexture.active=old;
                UnityEngine.Object.DestroyImmediate(cube);UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(mat);UnityEngine.Object.DestroyImmediate(pixels);
                target.Release();UnityEngine.Object.DestroyImmediate(target);SceneView.RepaintAll();
            }
        }
        static Color32[] Capture(Camera c,Texture2D t,string file)
        {c.Render();RenderTexture.active=c.targetTexture;t.ReadPixels(new Rect(0,0,512,512),0,0);t.Apply();File.WriteAllBytes(file,t.EncodeToPNG());return t.GetPixels32();}
        static int Read(Color32[] p,Camera c,Vector3 world)
        {var uv=c.WorldToViewportPoint(world);return p[Mathf.Clamp((int)(uv.y*512),0,511)*512+Mathf.Clamp((int)(uv.x*512),0,511)].r;}
        static int Difference(Color32[] a,Color32[] b)
        {int count=0;for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)>3)count++;return count;}
    }
}
