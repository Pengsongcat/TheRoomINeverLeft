using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TRINL.Rendering.Editor
{
    public static class SpatialGhostCylinderValidation
    {
        [MenuItem("Tools/TRINL/Validate Cylinder Ghosts")]
        public static void Run()
        {
            var go = GameObject.Find("Field_Poppy_vmcobd0ja_Mid_vmcobd0ja_VarC_LOD0 (3)");
            var effect = go ? go.GetComponent<SpatialGhosts>() : null;
            if (!effect) throw new Exception("Cylinder test target missing.");
            string saved = EditorJsonUtility.ToJson(effect);
            var renderers = effect.Root.GetComponentsInChildren<Renderer>();
            var layers = new int[renderers.Length];
            for (int i=0; i<layers.Length; i++) layers[i]=renderers[i].gameObject.layer;
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var sourceMaterial = go.GetComponent<Renderer>().sharedMaterial;
            var playerObject = new GameObject("Cylinder test player") {hideFlags=HideFlags.HideAndDontSave};
            var observerObject = new GameObject("Cylinder test observer") {hideFlags=HideFlags.HideAndDontSave};
            var player = playerObject.AddComponent<Camera>(); player.enabled=false;
            var observer = observerObject.AddComponent<Camera>(); observer.enabled=false;
            var target = new RenderTexture(640,640,24);
            var pixels = new Texture2D(640,640,TextureFormat.RGB24,false);
            var oldActive = RenderTexture.active;
            const string folder="Library/SpatialGhostCylinderValidation";
            Directory.CreateDirectory(folder);
            try
            {
                foreach(var r in renderers) r.gameObject.layer=31;
                effect.directionSpace=SpatialGhosts.DirectionSpace.Cylinder;
                effect.directionCamera=player;
                effect.opacity=.7f; effect.opacityDecay=.7f; effect.edgeSoftness=.15f;
                effect.cylinderDistances=new Vector2(.6f,4);
                effect.cylinderRadii=new Vector2(.8f,12);
                effect.cylinderLength=1.2f;
                var center=effect.Root.TransformPoint(effect.pivotOffset);
                observer.clearFlags=CameraClearFlags.SolidColor; observer.backgroundColor=Color.black;
                observer.cullingMask=1<<31; observer.allowHDR=false; observer.allowMSAA=false;
                observer.orthographic=true; observer.orthographicSize=2.2f;
                observer.nearClipPlane=.01f; observer.farClipPlane=50;
                observer.transform.position=center+new Vector3(0,5,-3);
                observer.transform.LookAt(center);
                observer.targetTexture=target;
                player.transform.position=center+Vector3.back*.6f;
                float near=effect.GetCylinderRadius();
                if(Mathf.Abs(near-.8f)>.001f) throw new Exception("Near radius mismatch");
                effect.enabled=false;
                var baseline=Capture(observer,pixels,folder+"/original.png");
                effect.enabled=true;
                var nearPixels=Capture(observer,pixels,folder+"/near.png");
                player.transform.position=center+Vector3.back*4;
                float far=effect.GetCylinderRadius();
                if(Mathf.Abs(far-12)>.001f) throw new Exception("Far radius mismatch");
                var farPixels=Capture(observer,pixels,folder+"/far.png");
                int shapeChanged=Difference(nearPixels,farPixels), ghosts=Difference(baseline,nearPixels);
                if(shapeChanged<100 || ghosts<100) throw new Exception("Cylinder rendering missing: "+shapeChanged+" / "+ghosts);
                player.transform.position=center+Vector3.back*2.3f;
                if(Mathf.Abs(effect.GetCylinderRadius()-6.4f)>.001f) throw new Exception("Midpoint interpolation mismatch");
                foreach(var r in renderers) r.gameObject.layer=30;
                var excluded=Capture(observer,pixels,folder+"/excluded.png");
                foreach(var p in excluded) if(p.r>3||p.g>3||p.b>3) throw new Exception("Layer inheritance failed");
                if(go.GetComponent<MeshFilter>().sharedMesh!=mesh || go.GetComponent<Renderer>().sharedMaterial!=sourceMaterial)
                    throw new Exception("Original asset reference changed");
                string report=$"PASS: near radius={near}, far={far}, midpoint=6.4; near/far changed pixels={shapeChanged}; ghost pixels={ghosts}; layer exclusion passed; original assets preserved. Desktop rendering only; XR/device performance untested.";
                File.WriteAllText(folder+"/validation.txt",report); Debug.Log(report);
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(saved,effect);
                for(int i=0;i<layers.Length;i++) renderers[i].gameObject.layer=layers[i];
                observer.targetTexture=null; RenderTexture.active=oldActive;
                UnityEngine.Object.DestroyImmediate(playerObject); UnityEngine.Object.DestroyImmediate(observerObject);
                UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
                effect.RefreshSources(); SceneView.RepaintAll();
            }
        }
        static Color32[] Capture(Camera camera,Texture2D pixels,string path)
        {
            camera.Render(); RenderTexture.active=camera.targetTexture;
            pixels.ReadPixels(new Rect(0,0,pixels.width,pixels.height),0,0); pixels.Apply();
            File.WriteAllBytes(path,pixels.EncodeToPNG()); return pixels.GetPixels32();
        }
        static int Difference(Color32[] a,Color32[] b)
        {
            int count=0; for(int i=0;i<a.Length;i++)
                if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>6) count++;
            return count;
        }
    }
}
