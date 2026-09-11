using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TRINL.Rendering.Editor
{
    // Deterministic desktop rendering checks, isolated far from the working scene.
    public static class MeshFogValidation
    {
        public static string Run(MeshFog example)
        {
            string directory=Path.GetFullPath("Artifacts/MeshFog"); Directory.CreateDirectory(directory);
            var report=new StringBuilder();
            GameObject clone=null,cameraObject=null,block=null;
            Material blockMaterial=null;
            RenderTexture target=null;
            var previous=RenderTexture.active;
            try
            {
                clone=UnityEngine.Object.Instantiate(example.gameObject);
                clone.name="MeshFog validation (temporary)";
                clone.transform.SetPositionAndRotation(new Vector3(1000,1000,1000),Quaternion.identity);
                clone.transform.localScale=Vector3.one;
                var fog=clone.GetComponent<MeshFog>();
                fog.flowSpeed=Vector3.zero; fog.showFog=true; fog.steps=64; fog.Apply();
                var center=fog.source.transform.TransformPoint(fog.shape.localBounds.center);
                cameraObject=new GameObject("MeshFog validation camera (temporary)");
                var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false;
                camera.transform.position=center+Vector3.back*3.2f;
                camera.transform.rotation=Quaternion.identity;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
                camera.fieldOfView=44; camera.nearClipPlane=0.02f; camera.farClipPlane=10;
                camera.allowHDR=true; camera.allowMSAA=true;
                var additional=camera.GetUniversalAdditionalCameraData();
                additional.requiresDepthTexture=true; additional.renderPostProcessing=false;
                target=new RenderTexture(384,384,24,RenderTextureFormat.ARGBHalf) { antiAliasing=4 };
                target.Create(); camera.targetTexture=target;

                Color[] Capture(string name)
                {
                    fog.Apply(); camera.Render(); RenderTexture.active=target;
                    var texture=new Texture2D(384,384,TextureFormat.RGBAFloat,false,true);
                    texture.ReadPixels(new Rect(0,0,384,384),0,0); texture.Apply();
                    var pixels=texture.GetPixels();
                    File.WriteAllBytes(Path.Combine(directory,name+".png"),texture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(texture);
                    return pixels;
                }
                float MeanDifference(Color[] a,Color[] b)
                {
                    float total=0;
                    for(int i=0;i<a.Length;i++) total+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);
                    return total/(a.Length*3);
                }
                var baseline=Capture("01-fog-64-steps");
                fog.steps=128; var high=Capture("02-fog-128-steps");
                float difference=MeanDifference(baseline,high);
                if(difference>0.02f) throw new Exception("Step-count stability failed: "+difference);
                report.AppendLine("PASS 64 vs 128 steps mean RGB difference: "+difference.ToString("F6"));

                fog.showFog=false; fog.Apply();
                if(!fog.source.GetComponent<Renderer>().enabled || fog.volumeRenderer.enabled) throw new Exception("Original switch failed");
                Capture("03-original-mesh"); fog.showFog=true; fog.Apply();
                fog.enabled=false;
                if(!fog.source.GetComponent<Renderer>().enabled || fog.volumeRenderer.enabled) throw new Exception("Disable restore failed");
                fog.enabled=true; fog.Apply();
                report.AppendLine("PASS Original/fog switch and component disable restore original Renderer.");

                block=GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.transform.position=center+Vector3.back*0.8f;
                block.transform.localScale=new Vector3(0.2f,2.4f,0.08f);
                blockMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                blockMaterial.SetColor("_BaseColor",new Color(0.8f,0.12f,0.03f,1));
                block.GetComponent<Renderer>().sharedMaterial=blockMaterial;
                var occluded=Capture("04-foreground-occlusion");
                fog.showFog=false; fog.Apply(); fog.source.GetComponent<Renderer>().enabled=false;
                var onlyBlock=Capture("05-occluder-reference");
                int middle=192+384*192;
                float occlusionError=Mathf.Abs(occluded[middle].b-onlyBlock[middle].b);
                if(occlusionError>0.01f) throw new Exception("Opaque occlusion failed: "+occlusionError);
                report.AppendLine("PASS Foreground occlusion: center blue error="+occlusionError.ToString("F6"));
                // Restore intended visible source state before re-entering fog mode.
                fog.source.GetComponent<Renderer>().enabled=true; fog.showFog=true; fog.Apply();
                block.SetActive(false);

                camera.transform.position=center;
                var inside=Capture("06-inside-volume");
                if(inside[middle].b<0.005f) throw new Exception("Inside-volume rendering returned black.");
                report.AppendLine("PASS Camera inside volume: center blue="+inside[middle].b.ToString("F6"));

                camera.transform.position=center+Vector3.back*3.2f;
                clone.transform.rotation=Quaternion.Euler(10,35,8);
                clone.transform.localScale=new Vector3(1.3f,0.8f,0.7f);
                Capture("07-rotated-nonuniform-scale");
                report.AppendLine("Captured rotated / non-uniformly scaled example for visual inspection.");

                report.AppendLine("Desktop camera: 384x384, HDR, 4x MSAA, depth enabled, no post-processing.");
                report.AppendLine("Not a headset, Android build, or GPU performance test.");
                File.WriteAllText(Path.Combine(directory,"validation.txt"),report.ToString());
                return report.ToString();
            }
            finally
            {
                RenderTexture.active=previous;
                if(cameraObject) UnityEngine.Object.DestroyImmediate(cameraObject);
                if(target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if(clone) UnityEngine.Object.DestroyImmediate(clone);
                if(block) UnityEngine.Object.DestroyImmediate(block);
                if(blockMaterial) UnityEngine.Object.DestroyImmediate(blockMaterial);
            }
        }
    }
}
