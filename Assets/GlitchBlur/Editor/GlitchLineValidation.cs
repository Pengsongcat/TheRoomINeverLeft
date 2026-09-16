using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TRINL.Rendering.Editor
{
    public static class GlitchLineValidation
    {
        [MenuItem("Tools/TRINL/Validate Glitch Continuous Lines")]
        public static void Run()
        {
            var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VR_Renderer.asset");
            var effect=data.rendererFeatures.Find(f=>f is GlitchBlurRendererFeature) as GlitchBlurRendererFeature;
            if(!effect)throw new Exception("Glitch feature missing");
            string saved=EditorJsonUtility.ToJson(effect);
            var probe=GameObject.CreatePrimitive(PrimitiveType.Cube);probe.hideFlags=HideFlags.HideAndDontSave;
            var camObject=new GameObject("Continuous lines validation camera"){hideFlags=HideFlags.HideAndDontSave};
            var camera=camObject.AddComponent<Camera>();camera.enabled=false;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")){hideFlags=HideFlags.HideAndDontSave};
            mat.SetColor("_BaseColor",new Color(1,.25f,.05f));probe.GetComponent<Renderer>().sharedMaterial=mat;
            var rt=new RenderTexture(512,512,24);var pixels=new Texture2D(512,512,TextureFormat.RGB24,false);
            var old=RenderTexture.active;
            const string folder="Library/GlitchLineValidation";Directory.CreateDirectory(folder);
            try
            {
                probe.layer=LayerMask.NameToLayer("GlitchBlur");probe.transform.position=new Vector3(1000,1000,0);
                probe.transform.localScale=new Vector3(.04f,.25f,.04f);
                var toward=Camera.main ? Camera.main.transform.position-probe.transform.position : Vector3.back;
                toward.y=0;toward.Normalize();
                camera.transform.position=probe.transform.position+toward*5+Vector3.up*2;
                camera.transform.LookAt(probe.transform.position);camera.orthographic=true;camera.orthographicSize=1.6f;
                camera.nearClipPlane=.1f;camera.farClipPlane=20;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=Color.black;camera.cullingMask=1<<probe.layer;
                camera.allowHDR=false;camera.allowMSAA=false;camera.targetTexture=rt;
                effect.cylinderTrails=true;effect.continuousLines=false;effect.upwardResponse=false;
                effect.lineLengthMultiplier=1;effect.lineRandomRange=Vector2.one;effect.lineRandomCellSize=.1f;
                effect.lineRandomSeed=0;effect.overlayPoints=false;effect.overlayPointWeight=.5f;
                effect.gaussianRadius=0;effect.verticalSoftness=0;effect.bandJitter=0;effect.driftSpeed=0;
                effect.cylinderSamples=9;effect.cylinderResolution=256;effect.cylinderRadii=Vector2.one*1.2f;
                effect.cylinderLengths=Vector2.one*1.1f;effect.originalWeight=effect.outerOriginalWeight=0;
                effect.opacity=1;effect.downsample=2;
                var points=Capture(camera,pixels,folder+"/points.png");
                effect.continuousLines=true;
                var lines=Capture(camera,pixels,folder+"/lines.png");
                int pointGaps=Gaps(points),lineGaps=Gaps(lines);
                if(pointGaps<40 || lineGaps>pointGaps*.1f)throw new Exception($"Line continuity failed: point gaps={pointGaps}, line gaps={lineGaps}");
                effect.continuousLines=false;
                var restored=Capture(camera,pixels,folder+"/restored-points.png");
                if(Difference(points,restored)!=0)throw new Exception("Disabling lines did not restore original points");
                effect.continuousLines=true;
                var local=probe.AddComponent<GlitchBlurOverride>();local.CopyFrom(effect);local.continuousLines=false;
                var localPoints=Capture(camera,pixels,folder+"/local-points.png");
                if(Difference(points,localPoints)>10)throw new Exception("Local line toggle did not override global mode");
                local.continuousLines=true;
                var localLines=Capture(camera,pixels,folder+"/local-lines.png");
                if(Difference(lines,localLines)>10)throw new Exception("Local line mode does not match global mode");
                local.enabled=false;
                effect.lineLengthMultiplier=.4f;
                var shortLines=Capture(camera,pixels,folder+"/short-lines.png");
                if(Width(shortLines)>=Width(lines)*.75f)throw new Exception("Length control did not shorten trail");
                effect.lineLengthMultiplier=1;effect.lineRandomRange=new Vector2(.2f,1);effect.lineRandomCellSize=.025f;
                var random=Capture(camera,pixels,folder+"/random-lines.png");
                if(Difference(random,lines)<100)throw new Exception("Random length had no effect");
                var stable=Capture(camera,pixels,folder+"/random-repeat.png");
                if(Difference(random,stable)!=0)throw new Exception("Random lengths are not stable");
                local.CopyFrom(effect);local.enabled=true;
                var localRandom=Capture(camera,pixels,folder+"/local-random.png");
                if(Difference(random,localRandom)>10)throw new Exception("Local random controls mismatch");
                local.enabled=false;effect.lineRandomRange=Vector2.one;effect.lineLengthMultiplier=.4f;
                effect.overlayPoints=true;
                var combined=Capture(camera,pixels,folder+"/combined.png");
                if(Width(combined)<=Width(shortLines)*1.2f)throw new Exception("Point overlay missing beyond shortened lines");
                local.CopyFrom(effect);local.enabled=true;
                if(Difference(combined,Capture(camera,pixels,folder+"/local-combined.png"))>10)throw new Exception("Local overlay mismatch");
                string report=$"PASS: disconnected empty columns={pointGaps}, continuous empty columns={lineGaps}; toggle restores original; local/global mode switches match. Desktop only.";
                report+=" Length control, stable random range, point/line overlay and local copies passed.";
                File.WriteAllText(folder+"/validation.txt",report);Debug.Log(report);
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(saved,effect);camera.targetTexture=null;RenderTexture.active=old;
                UnityEngine.Object.DestroyImmediate(probe);UnityEngine.Object.DestroyImmediate(camObject);
                UnityEngine.Object.DestroyImmediate(mat);UnityEngine.Object.DestroyImmediate(pixels);
                rt.Release();UnityEngine.Object.DestroyImmediate(rt);SceneView.RepaintAll();
            }
        }
        static Color32[] Capture(Camera c,Texture2D t,string file)
        {c.Render();RenderTexture.active=c.targetTexture;t.ReadPixels(new Rect(0,0,512,512),0,0);t.Apply();File.WriteAllBytes(file,t.EncodeToPNG());return t.GetPixels32();}
        static int Difference(Color32[] a,Color32[] b)
        {int n=0;for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)>2)n++;return n;}
        static int Gaps(Color32[] pixels)
        {
            var occupied=new bool[512];int min=512,max=-1;
            for(int i=0;i<pixels.Length;i++)if(pixels[i].r>2){int x=i%512;occupied[x]=true;min=Mathf.Min(min,x);max=Mathf.Max(max,x);}
            if(max<min)throw new Exception("No trail rendered");
            int gaps=0;for(int x=min;x<=max;x++)if(!occupied[x])gaps++;return gaps;
        }
        static int Width(Color32[] pixels)
        {int min=512,max=-1;for(int i=0;i<pixels.Length;i++)if(pixels[i].r>2){min=Mathf.Min(min,i%512);max=Mathf.Max(max,i%512);}return max-min+1;}
    }
}
