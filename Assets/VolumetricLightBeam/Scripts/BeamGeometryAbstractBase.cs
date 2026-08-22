using System.Collections.Generic;
using UnityEngine;

namespace VLB
{
    public abstract class BeamGeometryAbstractBase : MonoBehaviour
    {
        public MeshRenderer meshRenderer { get; protected set; }

        public MeshFilter meshFilter { get; protected set; }
        public Mesh coneMesh { get; protected set; }

        protected Matrix4x4 m_ColorGradientMatrix;
        protected Material m_CustomMaterial = null;

        protected abstract VolumetricLightBeamAbstractBase GetMaster();
        
        // Need to store camera in a stack, to support rendering a camera withing another camera, like it can be the case for Crest water reflection
        Stack<Camera> m_CurrentCameraRenderingSRP = new Stack<Camera>();

        void OnDisable()
        {
            SRPHelper.UnregisterCameraRenderingCallbacks(OnBeginCameraRenderingSRP, OnEndCameraRenderingSRP);
            m_CurrentCameraRenderingSRP.Clear();
        }

        protected virtual void OnEnable()
        {
            SRPHelper.RegisterCameraRenderingCallbacks(OnBeginCameraRenderingSRP, OnEndCameraRenderingSRP);
        }

        void Start()
        {
            DestroyOrphanBeamGeom(); // Handle copy / paste the LightBeam in Editor
        }


        void OnDestroy()
        {
            if (m_CustomMaterial)
            {
                DestroyImmediate(m_CustomMaterial);
                m_CustomMaterial = null;
            }
        }

        void DestroyOrphanBeamGeom()
        {
            var master = GetMaster();
            if(master)
            {
                var beamGeom = master.GetBeamGeometry();
                if(beamGeom == this)
                {
                    // do not destroy me only if I have a master, and this master knows me as its beam geom
                    return;
                }
            }

            DestroyBeamGeometryGameObject(this);
        }

        public static void DestroyBeamGeometryGameObject(BeamGeometryAbstractBase beamGeom)
        {
            if (beamGeom)
                DestroyImmediate(beamGeom.gameObject);
        }

        protected abstract void OnWillCameraRenderThisBeam(Camera cam);
        
#if UNITY_2019_1_OR_NEWER
        void OnBeginCameraRenderingSRP(UnityEngine.Rendering.ScriptableRenderContext context, Camera cam)
#else
        void OnBeginCameraRenderingSRP(Camera cam)
#endif
        {
            m_CurrentCameraRenderingSRP.Push(cam);
        }
        
#if UNITY_2019_1_OR_NEWER
        void OnEndCameraRenderingSRP(UnityEngine.Rendering.ScriptableRenderContext context, Camera cam)
#else
        void OnEndCameraRenderingSRP(Camera cam)
#endif
        {
            if (m_CurrentCameraRenderingSRP.Count > 0)
            {
                m_CurrentCameraRenderingSRP.Pop();
            }
        }

        // With Builtin RP, this callback is called with Camera.current properly set
        // With URP, this callback is called without Camera.current set, so we have to retrieve the current camera from the stack we manage
        void OnWillRenderObject()
        {
            Camera currentCam = null;
            
            if (SRPHelper.IsUsingCustomRenderPipeline())
            {
                if (m_CurrentCameraRenderingSRP.Count > 0)
                {
                    currentCam = m_CurrentCameraRenderingSRP.Peek();
                }
            }
            else
            {
                currentCam = Camera.current;
            }

            if (currentCam && GetMaster())
            {
                if (
                    Utils.IsEditorCamera(currentCam) || // make sure to call UpdateCameraRelatedProperties for editor scene camera 
                    currentCam.enabled)    // prevent from doing stuff when we render from a previous DynamicOcclusionDepthBuffer's DepthCamera, because the DepthCamera are disabled 
                {
                    OnWillCameraRenderThisBeam(currentCam);
                }
            }
            
        }

#if UNITY_EDITOR
        void Update()
        {
            if (!Application.isPlaying)
            {
                DestroyOrphanBeamGeom();
            }
        }

        public bool _EDITOR_IsUsingCustomMaterial { get { return m_CustomMaterial != null; } }
#endif
    }
}
