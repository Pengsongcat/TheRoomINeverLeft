using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TRINL.Rendering.Editor
{
    public static class GlitchCylinderValidation
    {
        [MenuItem("Tools/TRINL/Validate Glitch Cylinder")]
        public static void Run()
        {
            var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VR_Renderer.asset");
            var effect=data.rendererFeatures.Find(f=>f is GlitchBlurRendererFeature) as GlitchBlurRendererFeature;
            if(!effect) throw new Exception("GlitchBlur missing");
            string saved=EditorJsonUtility.ToJson(effect);
            var cameraObject=new GameObject("Glitch cylinder test camera"){hideFlags=HideFlags.HideAndDontSave};
            var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false;
            var probe=GameObject.CreatePrimitive(PrimitiveType.Cube); probe.hideFlags=HideFlags.HideAndDontSave;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")){hideFlags=HideFlags.HideAndDontSave};
            mat.SetColor("_BaseColor",new Color(1,.2f,.03f)); probe.GetComponent<Renderer>().sharedMaterial=mat;
            var rt=new RenderTexture(512,512,24); var pixels=new Texture2D(512,512,TextureFormat.RGB24,false);
            var oldActive=RenderTexture.active;
            const string folder="Library/GlitchCylinderValidation"; Directory.CreateDirectory(folder);
            GameObject blocker=null;
            try
            {
                var center=new Vector3(1000,1000,0); probe.transform.position=center;
                probe.transform.localScale=new Vector3(.22f,.8f,.22f);
                probe.layer=LayerMask.NameToLayer("GlitchBlur");
                var toward=Camera.main ? Camera.main.transform.position-center : Vector3.back;
                toward.y=0; float distance=toward.magnitude; toward.Normalize();
                camera.transform.position=center+toward*5+Vector3.up*3;
                camera.transform.LookAt(center); camera.orthographic=true; camera.orthographicSize=2;
                camera.nearClipPlane=.1f; camera.farClipPlane=20; camera.allowHDR=false; camera.allowMSAA=false;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
                camera.cullingMask=(1<<probe.layer)|1; camera.targetTexture=rt;
                effect.cylinderTrails=true; effect.cylinderSamples=33; effect.cylinderResolution=256;
                effect.gaussianRadius=0;effect.gaussianStrength=1;
                effect.gaussianDistanceResponse=false;
                effect.upwardResponse=false;
                effect.cylinderLengths=new Vector2(1.2f,1.2f); effect.cylinderRadii=new Vector2(.8f,12);
                effect.originalWeight=0; effect.outerOriginalWeight=0; effect.opacity=1;
                effect.bandJitter=0; effect.verticalSoftness=0; effect.downsample=2;
                effect.cylinderDistances=new Vector2(distance+1,distance+2);
                var near=Capture(camera,pixels,folder+"/near.png");
                effect.gaussianRadius=10;effect.gaussianStrength=1;
                var blurred=Capture(camera,pixels,folder+"/near-gaussian.png");
                if(Difference(near,blurred)<100)throw new Exception("Cylinder Gaussian did not soften the trail");
                effect.gaussianRadius=0;
                effect.cylinderDistances=new Vector2(0,Mathf.Max(.01f,distance-1));
                var far=Capture(camera,pixels,folder+"/far.png");
                int different=Difference(near,far), lit=Lit(near);
                if(lit<100||different<100) throw new Exception($"Cylinder missing: lit={lit}, changed={different}");
                effect.cylinderLengths=Vector2.zero;
                var zero=Capture(camera,pixels,folder+"/zero.png");
                if(Lit(far)<Lit(zero)*2) throw new Exception("World length failed to extend trail");
                effect.cylinderLengths=Vector2.one*1.2f;
                camera.orthographic=false; camera.fieldOfView=45;
                var perspective=Capture(camera,pixels,folder+"/perspective.png");
                if(Lit(perspective)<100) throw new Exception("Perspective reprojection missing");
                camera.orthographic=true;
                blocker=GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.hideFlags=HideFlags.HideAndDontSave;
                blocker.transform.position=center+(camera.transform.position-center).normalized*2.5f;
                blocker.transform.rotation=camera.transform.rotation; blocker.transform.localScale=new Vector3(6,6,.2f);
                var bm=new Material(mat){hideFlags=HideFlags.HideAndDontSave}; bm.SetColor("_BaseColor",Color.black);
                blocker.GetComponent<Renderer>().sharedMaterial=bm;
                var occluded=Capture(camera,pixels,folder+"/occluded.png");
                effect.gaussianRadius=10;
                var occludedBlur=Capture(camera,pixels,folder+"/occluded-gaussian.png");
                effect.gaussianRadius=0;
                if(Lit(occludedBlur)>0)throw new Exception("Gaussian leaked through foreground occlusion");
                UnityEngine.Object.DestroyImmediate(bm);
                if(Lit(occluded)>0) throw new Exception("Occluded trail leaked");
                UnityEngine.Object.DestroyImmediate(blocker); blocker=null;
                probe.layer=LayerMask.NameToLayer("ScreenBlur"); camera.cullingMask=1<<probe.layer;
                var screenA=Capture(camera,pixels,folder+"/screen-a.png");
                effect.cylinderTrails=false;
                var screenB=Capture(camera,pixels,folder+"/screen-b.png");
                if(Difference(screenA,screenB)>0) throw new Exception("ScreenBlur changed");
                string report=$"PASS: cylinder pixels={lit}, near/far changed={different}, zero/far coverage={Lit(zero)}/{Lit(far)}, foreground occlusion and ScreenBlur isolation passed. Desktop only; XR and GPU budget untested.";
                File.WriteAllText(folder+"/validation.txt",report); Debug.Log(report);
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(saved,effect);
                camera.targetTexture=null; RenderTexture.active=oldActive;
                if(blocker) UnityEngine.Object.DestroyImmediate(blocker);
                UnityEngine.Object.DestroyImmediate(probe); UnityEngine.Object.DestroyImmediate(mat);
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(pixels);
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt); SceneView.RepaintAll();
            }
        }
        static Color32[] Capture(Camera c,Texture2D t,string file)
        {c.Render(); RenderTexture.active=c.targetTexture; t.ReadPixels(new Rect(0,0,512,512),0,0);t.Apply();File.WriteAllBytes(file,t.EncodeToPNG());return t.GetPixels32();}
        static int Lit(Color32[] p) {int n=0;foreach(var c in p)if(c.r>3)n++;return n;}
        static int Difference(Color32[] a,Color32[] b)
        {int n=0;for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)>3)n++;return n;}
    }
}
