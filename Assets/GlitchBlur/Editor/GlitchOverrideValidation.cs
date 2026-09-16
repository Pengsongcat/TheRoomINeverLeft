using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TRINL.Rendering.Editor
{
    public static class GlitchOverrideValidation
    {
        [MenuItem("Tools/TRINL/Validate Glitch Object Overrides")]
        public static void Run()
        {
            var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VR_Renderer.asset");
            var effect=data.rendererFeatures.Find(f=>f is GlitchBlurRendererFeature) as GlitchBlurRendererFeature;
            if(!effect) throw new Exception("GlitchBlur missing");
            string saved=EditorJsonUtility.ToJson(effect);
            var a=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var b=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cameraObject=new GameObject("Override validation camera");
            a.hideFlags=b.hideFlags=cameraObject.hideFlags=HideFlags.HideAndDontSave;
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")){hideFlags=HideFlags.HideAndDontSave};
            mat.SetColor("_BaseColor",Color.white);
            var rt=new RenderTexture(512,512,24);
            var texture=new Texture2D(512,512,TextureFormat.RGB24,false);
            var oldActive=RenderTexture.active;
            var mainCamera=Camera.main;
            Vector3 savedPlayerPosition=mainCamera ? mainCamera.transform.position : Vector3.zero;
            const string folder="Library/GlitchOverrideValidation";Directory.CreateDirectory(folder);
            try
            {
                a.layer=b.layer=LayerMask.NameToLayer("GlitchBlur");
                a.transform.position=new Vector3(1000,1000.8f,0);
                b.transform.position=new Vector3(1000,999.2f,0);
                a.transform.localScale=b.transform.localScale=new Vector3(.2f,.3f,.2f);
                a.GetComponent<Renderer>().sharedMaterial=b.GetComponent<Renderer>().sharedMaterial=mat;
                camera.transform.position=new Vector3(1000,1000,-5);
                camera.orthographic=true;camera.orthographicSize=2;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.cullingMask=1<<a.layer;camera.nearClipPlane=.1f;camera.farClipPlane=10;
                camera.allowHDR=false;camera.allowMSAA=false;camera.targetTexture=rt;
                effect.cylinderTrails=false;effect.horizontalLength=effect.outerHorizontalLength=30;
                effect.gaussianRadius=0;effect.gaussianStrength=1;
                effect.gaussianDistanceResponse=false;
                effect.originalWeight=effect.outerOriginalWeight=0;
                effect.verticalSoftness=effect.bandJitter=effect.driftSpeed=0;
                effect.downsample=2;effect.opacity=1;
                var baseline=Capture(camera,texture,folder+"/global.png");
                var local=a.AddComponent<GlitchBlurOverride>();local.CopyFrom(effect);
                local.horizontalLength=local.outerHorizontalLength=110;
                var changed=Capture(camera,texture,folder+"/local-long.png");
                int top=Difference(baseline,changed,true),bottom=Difference(baseline,changed,false);
                if(top<100||bottom!=0)throw new Exception($"Local independence failed: top={top}, bottom={bottom}");
                local.gaussianRadius=12; local.gaussianStrength=1;
                var gaussian=Capture(camera,texture,folder+"/local-gaussian.png");
                int gaussianChanged=Difference(changed,gaussian,true);
                if(gaussianChanged<100 || Difference(changed,gaussian,false)!=0)
                    throw new Exception("Local Gaussian isolation failed");
                if(!mainCamera)throw new Exception("Distance response validation requires Main Camera");
                local.gaussianDistanceResponse=true;
                local.gaussianResponseDistance=5;
                local.gaussianNearStrength=.6f;local.gaussianFarStrength=1;
                mainCamera.transform.position=a.transform.position+Vector3.back*10;
                var distant=Capture(camera,texture,folder+"/gaussian-distance-far.png");
                if(Difference(gaussian,distant,true)!=0 || Difference(gaussian,distant,false)!=0)
                    throw new Exception("Beyond 5m did not match fixed strength 1");
                mainCamera.transform.position=a.transform.position+Vector3.back*.1f;
                var close=Capture(camera,texture,folder+"/gaussian-distance-near.png");
                if(Difference(close,distant,true)<100 || Difference(close,distant,false)!=0)
                    throw new Exception("Approaching did not reduce local blur independently");
                local.gaussianResponseDistance=.01f;
                var shortRange=Capture(camera,texture,folder+"/gaussian-distance-short-range.png");
                if(Difference(shortRange,gaussian,true)>10)
                    throw new Exception("Public response distance did not control threshold");
                local.gaussianDistanceResponse=false;
                mainCamera.transform.position=savedPlayerPosition;
                local.gaussianStrength=0;
                var gaussianOff=Capture(camera,texture,folder+"/gaussian-strength-zero.png");
                if(Difference(changed,gaussianOff,true)!=0 || Difference(changed,gaussianOff,false)!=0)
                    throw new Exception("Gaussian strength zero changed original output");
                local.gaussianRadius=0;local.gaussianStrength=1;
                effect.gaussianRadius=12;
                var globalGaussian=Capture(camera,texture,folder+"/global-gaussian.png");
                if(Difference(changed,globalGaussian,true)!=0 || Difference(changed,globalGaussian,false)<100)
                    throw new Exception("Global Gaussian affected the local override or did not blur the global object");
                effect.gaussianRadius=0;
                local.enabled=false;
                var restored=Capture(camera,texture,folder+"/disabled.png");
                if(Difference(baseline,restored,true)>0||Difference(baseline,restored,false)>0)
                    throw new Exception("Disable did not restore global settings");
                local.enabled=true;
                local.cylinderTrails=true;local.cylinderLengths=new Vector2(.5f,1);
                local.cylinderResolution=256;local.cylinderSamples=17;
                var mixed=Capture(camera,texture,folder+"/mixed-modes.png");
                if(Difference(baseline,mixed,false)!=0||Difference(baseline,mixed,true)<100)
                    throw new Exception("Mixed cylinder/flat independence failed");
                var localB=b.AddComponent<GlitchBlurOverride>();localB.CopyFrom(effect);
                localB.horizontalLength=localB.outerHorizontalLength=95;
                var two=Capture(camera,texture,folder+"/two-overrides.png");
                if(Difference(mixed,two,true)!=0 || Difference(mixed,two,false)<100)
                    throw new Exception($"Two simultaneous overrides interfered: A={Difference(mixed,two,true)}, B={Difference(mixed,two,false)}");
                var ghosts=a.AddComponent<SpatialGhosts>();
                ghosts.directionSpace=SpatialGhosts.DirectionSpace.Cylinder;
                ghosts.cylinderMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/SpatialGhosts/SpatialGhostCylinder.mat");
                ghosts.opacity=.7f;ghosts.opacityDecay=.7f;
                local.cylinderTrails=false;local.opacity=0;
                var hidden=Capture(camera,texture,folder+"/ghosts-local-opacity-zero.png");
                int topLit=0;for(int i=256*512;i<hidden.Length;i++)if(hidden[i].r>3)topLit++;
                if(Difference(two,hidden,false)!=0)throw new Exception("Other local object changed when hiding A: "+Difference(two,hidden,false));
                if(topLit>20)throw new Exception("SpatialGhosts escaped local opacity: "+topLit+" A="+local.opacity+"/"+local.originalWeight+" B="+localB.originalWeight+" global="+effect.originalWeight);
                string report=$"PASS: local changed pixels={top}, untouched object changed={bottom}; disable restores global; local cylinder with global flat passed; original layer/material unchanged.";
                report += $" Two simultaneous overrides passed; SpatialGhosts opacity-zero pixels={topLit}.";
                report += $" Gaussian local changed pixels={gaussianChanged}; global/local Gaussian isolation and zero-strength bypass passed.";
                report += " Player-distance response: beyond 5m equals strength 1; approach reduces blur; editable threshold and local isolation passed.";
                File.WriteAllText(folder+"/validation.txt",report);Debug.Log(report);
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(saved,effect);camera.targetTexture=null;RenderTexture.active=oldActive;
                if(mainCamera)mainCamera.transform.position=savedPlayerPosition;
                UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);
                UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(mat);
                UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
                SceneView.RepaintAll();
            }
        }
        static Color32[] Capture(Camera c,Texture2D t,string path)
        {c.Render();RenderTexture.active=c.targetTexture;t.ReadPixels(new Rect(0,0,512,512),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());return t.GetPixels32();}
        static int Difference(Color32[] a,Color32[] b,bool top)
        {int n=0;for(int i=top?256*512:0;i<(top?a.Length:256*512);i++)if(Mathf.Abs(a[i].r-b[i].r)>2)n++;return n;}
    }
}
