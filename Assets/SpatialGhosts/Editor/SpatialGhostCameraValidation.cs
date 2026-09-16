using System;
using UnityEditor;
using UnityEngine;
namespace TRINL.Rendering.Editor
{
    public static class SpatialGhostCameraValidation
    {
        [MenuItem("Tools/TRINL/Validate Spatial Ghost Camera Direction")]
        public static void Run()
        {
            var root=new GameObject("Billboard test"){hideFlags=HideFlags.HideAndDontSave};
            var cameraObject=new GameObject("Billboard test camera"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var effect=root.AddComponent<SpatialGhosts>();
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
                root.transform.position=new Vector3(3,2,1);
                root.transform.rotation=Quaternion.Euler(15,35,5);
                effect.directionSpace=SpatialGhosts.DirectionSpace.Camera;
                effect.directionCamera=camera;effect.spacing=0.35f;effect.stretchPerStep=0.25f;
                var originalRotation=root.transform.rotation;
                // Hold camera rotation fixed: orbiting position alone must reorient each ghost.
                foreach(var offset in new[]{new Vector3(0,0,-5),new Vector3(5,1,0),new Vector3(-3,2,4)})
                {
                    camera.transform.position=root.transform.position+offset;
                    foreach(int step in new[]{-2,-1,1,2})
                    {
                        var matrix=effect.GetGhostMatrix(root.transform.localToWorldMatrix,step,camera);
                        var center=matrix.MultiplyPoint3x4(Vector3.zero);
                        var normal=matrix.inverse.transpose.MultiplyVector(Vector3.forward).normalized;
                        var toward=(camera.transform.position-center).normalized;
                        if(Vector3.Dot(normal,toward)<0.9999f)throw new Exception("Ghost is not facing the camera position.");
                    }
                }
                if(root.transform.rotation!=originalRotation)throw new Exception("Source rotation was modified.");
                effect.AssignMainCamera();
                if(effect.directionCamera!=camera)throw new Exception("Explicit camera was replaced.");
                effect.directionCamera=null;effect.AssignMainCamera();
                if(effect.directionCamera!=Camera.main)throw new Exception("Main camera was not assigned.");
                Debug.Log("PASS billboard: 3 camera positions with fixed rotation, all four copies face camera; source unchanged; explicit reference preserved; MainCamera auto-assigned. No headset run.");
            }
            finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);}
        }
    }
}
