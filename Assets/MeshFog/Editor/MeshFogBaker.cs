using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TRINL.Rendering.Editor
{
    // Editor-only CPU baker. BVH accelerates exact triangle distance and ray parity queries.
    public static class MeshFogBaker
    {
        struct Triangle { public Vector3 a,b,c; public Bounds bounds; public Vector3 center; }
        sealed class Node { public Bounds bounds; public Node left,right; public int start,count; }
        sealed class Tree
        {
            readonly Triangle[] triangles;
            readonly Node root;
            readonly Vector3 rayDirection = new Vector3(1, 0.37139f, 0.52913f).normalized;
            readonly List<float> intersections = new List<float>();
            public Tree(Vector3[] vertices, int[] indices)
            {
                var valid = new List<Triangle>();
                for (int i=0; i<indices.Length; i+=3)
                {
                    var a=vertices[indices[i]]; var b=vertices[indices[i+1]]; var c=vertices[indices[i+2]];
                    if (Vector3.Cross(b-a,c-a).sqrMagnitude < 1e-18f) continue;
                    var bounds=new Bounds(a,Vector3.zero); bounds.Encapsulate(b); bounds.Encapsulate(c);
                    valid.Add(new Triangle { a=a,b=b,c=c,bounds=bounds,center=(a+b+c)/3 });
                }
                if (valid.Count==0) throw new InvalidOperationException("Mesh has no non-degenerate triangles.");
                triangles=valid.ToArray(); root=Build(0,triangles.Length);
            }
            Node Build(int start,int count)
            {
                var node=new Node { start=start,count=count,bounds=triangles[start].bounds };
                for(int i=start+1;i<start+count;i++) node.bounds.Encapsulate(triangles[i].bounds);
                if(count<=8) return node;
                var size=node.bounds.size;
                int axis=size.x>size.y ? (size.x>size.z?0:2) : (size.y>size.z?1:2);
                Array.Sort(triangles,start,count,Comparer<Triangle>.Create((a,b)=>a.center[axis].CompareTo(b.center[axis])));
                int half=count/2;
                node.left=Build(start,half); node.right=Build(start+half,count-half);
                return node;
            }
            public float SignedDistance(Vector3 point)
            {
                float squared=float.PositiveInfinity;
                Nearest(root,point,ref squared);
                intersections.Clear(); Cast(root,new Ray(point,rayDirection));
                intersections.Sort();
                int unique=0; float last=float.NegativeInfinity;
                float epsilon=Mathf.Max(root.bounds.size.magnitude*1e-6f,1e-7f);
                foreach(float t in intersections) if(t-last>epsilon) { unique++; last=t; }
                return Mathf.Sqrt(squared)*((unique&1)==1?-1:1);
            }
            void Nearest(Node n,Vector3 p,ref float best)
            {
                if(n.bounds.SqrDistance(p)>best) return;
                if(n.left==null)
                {
                    for(int i=n.start;i<n.start+n.count;i++) best=Mathf.Min(best,DistanceSquared(p,triangles[i]));
                    return;
                }
                if(n.left.bounds.SqrDistance(p)<n.right.bounds.SqrDistance(p))
                { Nearest(n.left,p,ref best); Nearest(n.right,p,ref best); }
                else { Nearest(n.right,p,ref best); Nearest(n.left,p,ref best); }
            }
            void Cast(Node n,Ray ray)
            {
                if(!n.bounds.IntersectRay(ray)) return;
                if(n.left!=null) { Cast(n.left,ray); Cast(n.right,ray); return; }
                for(int i=n.start;i<n.start+n.count;i++)
                {
                    var t=triangles[i]; var e1=t.b-t.a; var e2=t.c-t.a;
                    var h=Vector3.Cross(ray.direction,e2); float determinant=Vector3.Dot(e1,h);
                    if(Mathf.Abs(determinant)<1e-10f) continue;
                    float inv=1/determinant; var s=ray.origin-t.a;
                    float u=inv*Vector3.Dot(s,h); if(u<0 || u>1) continue;
                    var q=Vector3.Cross(s,e1); float v=inv*Vector3.Dot(ray.direction,q);
                    if(v<0 || u+v>1) continue;
                    float distance=inv*Vector3.Dot(e2,q);
                    if(distance>1e-7f) intersections.Add(distance);
                }
            }
            static float DistanceSquared(Vector3 p,Triangle t)
            {
                Vector3 ab=t.b-t.a, ac=t.c-t.a, ap=p-t.a;
                float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
                if(d1<=0 && d2<=0) return ap.sqrMagnitude;
                var bp=p-t.b; float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);
                if(d3>=0 && d4<=d3) return bp.sqrMagnitude;
                float vc=d1*d4-d3*d2;
                if(vc<=0 && d1>=0 && d3<=0) return (p-(t.a+ab*(d1/(d1-d3)))).sqrMagnitude;
                var cp=p-t.c; float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);
                if(d6>=0 && d5<=d6) return cp.sqrMagnitude;
                float vb=d5*d2-d1*d6;
                if(vb<=0 && d2>=0 && d6<=0) return (p-(t.a+ac*(d2/(d2-d6)))).sqrMagnitude;
                float va=d3*d6-d5*d4;
                if(va<=0 && d4-d3>=0 && d5-d6>=0)
                    return (p-(t.b+(t.c-t.b)*((d4-d3)/((d4-d3)+(d5-d6))))).sqrMagnitude;
                float inv=1/(va+vb+vc);
                return (p-(t.a+ab*(vb*inv)+ac*(vc*inv))).sqrMagnitude;
            }
        }

        public static MeshFogShape Bake(MeshFog fog)
        {
            if(!fog.source || !fog.source.sharedMesh) throw new InvalidOperationException("请指定带 Mesh 的 Source MeshFilter。");
            var mesh=fog.source.sharedMesh;
            Vector3[] vertices; int[] indices;
            try { vertices=mesh.vertices; indices=mesh.triangles; }
            catch(Exception e) { throw new InvalidOperationException("无法读取 Mesh。请在模型导入设置启用 Read/Write 后重试。",e); }
            ValidateClosed(vertices,indices,mesh.bounds.size.magnitude);
            var tree=new Tree(vertices,indices);
            int resolution=Mathf.Clamp(fog.bakeResolution,16,96);
            var bounds=mesh.bounds;
            bounds.Expand(Mathf.Max(0.01f,fog.bakePadding)*2);
            var data=new float[resolution*resolution*resolution];
            int inside=0;
            try
            {
                for(int z=0;z<resolution;z++)
                {
                    if(EditorUtility.DisplayCancelableProgressBar("烘焙模型 SDF",mesh.name+" — "+resolution+"³",(float)z/resolution))
                        throw new OperationCanceledException("烘焙已取消，原资源保持不变。");
                    for(int y=0;y<resolution;y++) for(int x=0;x<resolution;x++)
                    {
                        var uv=new Vector3((x+0.5f)/resolution,(y+0.5f)/resolution,(z+0.5f)/resolution);
                        float distance=tree.SignedDistance(bounds.min+Vector3.Scale(uv,bounds.size));
                        data[x+resolution*(y+resolution*z)]=distance;
                        if(distance<0) inside++;
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            if(inside==0) throw new InvalidOperationException("未检测到内部体积。请使用封闭 Mesh，或提高精度。");
            var texture=new Texture3D(resolution,resolution,resolution,TextureFormat.RFloat,false)
            { name=mesh.name+" SDF",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp };
            texture.SetPixelData(data,0); texture.Apply(false,false);
            Undo.RecordObject(fog,"绑定烘焙形状");
            var asset=fog.shape;
            if(!asset)
            {
                if(!AssetDatabase.IsValidFolder("Assets/MeshFog/Generated")) AssetDatabase.CreateFolder("Assets/MeshFog","Generated");
                asset=ScriptableObject.CreateInstance<MeshFogShape>();
                string safeName=string.Join("_",fog.name.Split(System.IO.Path.GetInvalidFileNameChars()));
                AssetDatabase.CreateAsset(asset,AssetDatabase.GenerateUniqueAssetPath("Assets/MeshFog/Generated/"+safeName+"_Shape.asset"));
                asset.sdf=texture; AssetDatabase.AddObjectToAsset(texture,asset);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(asset,"更新烘焙形状");
                if(asset.sdf)
                {
                    Undo.RegisterCompleteObjectUndo(asset.sdf,"更新 SDF 纹理");
                    EditorUtility.CopySerialized(texture,asset.sdf);
                    UnityEngine.Object.DestroyImmediate(texture);
                    EditorUtility.SetDirty(asset.sdf);
                }
                else { asset.sdf=texture; AssetDatabase.AddObjectToAsset(texture,asset); }
            }
            asset.sourceMesh=mesh; asset.localBounds=bounds; asset.resolution=resolution; asset.padding=fog.bakePadding;
            fog.shape=asset;
            EditorUtility.SetDirty(asset); EditorUtility.SetDirty(fog);
            AssetDatabase.SaveAssets();
            Debug.Log("MeshFog SDF baked: "+resolution+"³, inside voxels="+inside+", negative inside / local distance.",fog);
            return asset;
        }

        static void ValidateClosed(Vector3[] vertices,int[] indices,float size)
        {
            // Weld UV seams for topology checks; discard triangles collapsed at sphere poles.
            float epsilon=Mathf.Max(size*1e-6f,1e-7f);
            var ids=new int[vertices.Length];
            var welded=new Dictionary<Vector3Int,int>();
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i]/epsilon;
                var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
                if(!welded.TryGetValue(key,out int id)) { id=welded.Count; welded.Add(key,id); }
                ids[i]=id;
            }
            var edges=new Dictionary<ulong,int>();
            void Edge(int a,int b)
            {
                ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
                edges.TryGetValue(key,out int count); edges[key]=count+1;
            }
            for(int i=0;i<indices.Length;i+=3)
            {
                int a=ids[indices[i]],b=ids[indices[i+1]],c=ids[indices[i+2]];
                if(a==b || b==c || c==a) continue;
                Edge(a,b); Edge(b,c); Edge(c,a);
            }
            foreach(var edge in edges) if(edge.Value!=2)
                throw new InvalidOperationException("Mesh 存在开口或非流形边。第一版需要封闭模型，请修补 Mesh 后重新烘焙。");
        }
    }
}
