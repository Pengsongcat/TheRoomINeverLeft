using UnityEngine;

namespace TRINL.Rendering
{
    public sealed class MeshFogShape : ScriptableObject
    {
        public Mesh sourceMesh;
        public Texture3D sdf;
        public Bounds localBounds;
        public int resolution;
        public float padding;
        public string encoding = "RFloat: negative inside, distance in source mesh local units, voxel centers";
    }
}
