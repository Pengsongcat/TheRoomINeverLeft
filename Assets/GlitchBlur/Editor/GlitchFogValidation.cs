using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TRINL.Rendering.Editor
{
    public static class GlitchFogValidation
    {
        [MenuItem("Tools/TRINL/Validate GlitchBlur Fog")]
        public static void Run()
        {
            var example = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<MeshFog>() : null;
            if (!example) throw new Exception("Select the fog or its parent.");
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VR_Renderer.asset");
            var effect = data.rendererFeatures.Find(f => f is GlitchBlurRendererFeature) as GlitchBlurRendererFeature;
            string settings = EditorJsonUtility.ToJson(effect);
            var oldRT = RenderTexture.active;
            var clone = UnityEngine.Object.Instantiate(example.gameObject);
            clone.name = "Fog compatibility validation";
            clone.transform.position = new Vector3(1000,1000,1000);
            var fog = clone.GetComponent<MeshFog>();
            fog.flowSpeed = Vector3.zero; fog.showFog = true; fog.Apply();
            var camObject = new GameObject("Fog compatibility camera");
            var cam = camObject.AddComponent<Camera>(); cam.enabled = false;
            var bounds = fog.volumeRenderer.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            cam.transform.position = bounds.center + Vector3.back * (radius * 3 + 1);
            cam.orthographic = true; cam.orthographicSize = radius * 1.15f;
            cam.nearClipPlane = 0.02f; cam.farClipPlane = radius * 8 + 10;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            cam.allowMSAA = false; cam.allowHDR = true;
            var rt = new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf);
            var tex = new Texture2D(512,512,TextureFormat.RGBAFloat,false,true);
            cam.targetTexture = rt;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = bounds.center + Vector3.back * (radius + 0.5f);
            blocker.transform.localScale = new Vector3(radius * 4, radius * 4, 0.1f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor",Color.black); blocker.GetComponent<Renderer>().sharedMaterial = mat;
            blocker.SetActive(false);
            string folder = "Library/GlitchFogValidation"; Directory.CreateDirectory(folder);
            Color[] Capture(string name)
            {
                fog.Apply(); cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0,0,512,512),0,0); tex.Apply();
                File.WriteAllBytes(folder+"/"+name+".png",tex.EncodeToPNG());
                return tex.GetPixels();
            }
            float Energy(Color[] pixels)
            {
                float sum=0; foreach(var p in pixels) sum+=p.r+p.g+p.b; return sum;
            }
            try
            {
                fog.volumeRenderer.gameObject.layer = 0;
                var normal = Capture("normal-fog");
                effect.horizontalLength = effect.outerHorizontalLength = 0;
                effect.originalWeight = effect.outerOriginalWeight = 1;
                effect.verticalSoftness = effect.bandJitter = effect.driftSpeed = 0;
                effect.opacity = 1;
                fog.volumeRenderer.gameObject.layer = LayerMask.NameToLayer("GlitchBlur");
                effect.downsample = 1; var full = Capture("glitch-1x");
                effect.downsample = 4; var quarter = Capture("glitch-4x");
                float n=Energy(normal), a=Energy(full), b=Energy(quarter);
                if (n<1 || a<n*0.7f || a>n*1.3f || b<a*0.65f || b>a*1.35f)
                    throw new Exception($"Fog visibility mismatch normal={n} capture1={a} capture4={b}");
                effect.horizontalLength=40; effect.outerHorizontalLength=100;
                effect.originalWeight=0.5f; effect.outerOriginalWeight=0.05f;
                var smear=Capture("fog-with-trails");
                if(Energy(smear)<1)throw new Exception("Fog trails are empty");
                blocker.SetActive(true); var hidden=Capture("foreground-occluded");
                if(Energy(hidden)>1)throw new Exception("Fog leaked through foreground: "+Energy(hidden));
                // A wall inside the proxy must retain fog in front of it. Proxy-back-face
                // depth would incorrectly reject all of that partially visible fog.
                effect.horizontalLength=effect.outerHorizontalLength=0;
                effect.originalWeight=effect.outerOriginalWeight=1;
                blocker.transform.position=bounds.center+Vector3.forward*radius*0.3f;
                fog.volumeRenderer.gameObject.layer=0;
                float partialReference=Energy(Capture("partial-default"));
                fog.volumeRenderer.gameObject.layer=LayerMask.NameToLayer("GlitchBlur");
                float partialCapture=Energy(Capture("partial-glitch"));
                if(partialReference<1 || Mathf.Abs(partialCapture-partialReference)/partialReference>0.35f)
                    throw new Exception($"Partial occlusion mismatch {partialReference} -> {partialCapture}");
                blocker.SetActive(false); cam.orthographic=false; cam.fieldOfView=50;
                fog.volumeRenderer.gameObject.layer=0;
                float perspectiveReference=Energy(Capture("perspective-default"));
                fog.volumeRenderer.gameObject.layer=LayerMask.NameToLayer("GlitchBlur");
                float perspectiveCapture=Energy(Capture("perspective-glitch"));
                if(perspectiveReference<1 || Mathf.Abs(perspectiveCapture-perspectiveReference)/perspectiveReference>0.35f)
                    throw new Exception($"Perspective mismatch {perspectiveReference} -> {perspectiveCapture}");
                string report=$"PASS fog energy: Default={n:F3}, Glitch 1x={a:F3}, Glitch 4x={b:F3}; trails={Energy(smear):F3}; full foreground occlusion={Energy(hidden):F6}; partial occlusion={partialReference:F3} -> {partialCapture:F3}; perspective={perspectiveReference:F3} -> {perspectiveCapture:F3}.\nDesktop orthographic and perspective verification. Headset/Android not tested.\n";
                File.WriteAllText(folder+"/validation.txt",report); Debug.Log(report);
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(settings,effect);
                cam.targetTexture=null; RenderTexture.active=oldRT;
                UnityEngine.Object.DestroyImmediate(clone); UnityEngine.Object.DestroyImmediate(camObject);
                UnityEngine.Object.DestroyImmediate(blocker); UnityEngine.Object.DestroyImmediate(mat);
                UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                SceneView.RepaintAll();
            }
        }
    }
}
