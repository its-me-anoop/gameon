using UnityEngine;

namespace IdleClinic.Presentation
{
    /// <summary>Small shared meshes with the same dimensions as Unity primitives, without implicit physics components.</summary>
    internal static class ClinicPrimitives
    {
        internal static Mesh Cube()
        {
            var vertices=new Vector3[24];var normals=new Vector3[24];var triangles=new int[36];
            var directions=new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            for(int face=0;face<6;face++)
            {
                var normal=directions[face];var u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up;
                var v=Vector3.Cross(normal,u);var center=normal*.5f;int first=face*4;
                vertices[first]=center-(u+v)*.5f;vertices[first+1]=center+(u-v)*.5f;
                vertices[first+2]=center+(u+v)*.5f;vertices[first+3]=center+(-u+v)*.5f;
                for(int i=0;i<4;i++)normals[first+i]=normal;
                int index=face*6;triangles[index]=first;triangles[index+1]=first+1;triangles[index+2]=first+2;
                triangles[index+3]=first;triangles[index+4]=first+2;triangles[index+5]=first+3;
            }
            return Make("Clinic cube",vertices,normals,triangles);
        }
        internal static Mesh Sphere()
        {
            const int slices=12,rings=8,stride=slices+1;
            var vertices=new Vector3[(rings+1)*stride];var normals=new Vector3[vertices.Length];var triangles=new int[rings*slices*6];
            for(int ring=0;ring<=rings;ring++)for(int slice=0;slice<=slices;slice++)
            {
                float latitude=Mathf.PI*ring/rings,longitude=2*Mathf.PI*slice/slices;
                var normal=new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(longitude));
                int index=ring*stride+slice;normals[index]=normal;vertices[index]=normal*.5f;
            }
            int at=0;
            for(int ring=0;ring<rings;ring++)for(int slice=0;slice<slices;slice++)
            {
                int a=ring*stride+slice,b=a+stride;
                triangles[at++]=a;triangles[at++]=a+1;triangles[at++]=b;
                triangles[at++]=a+1;triangles[at++]=b+1;triangles[at++]=b;
            }
            return Make("Clinic sphere",vertices,normals,triangles);
        }
        internal static Mesh Cylinder()
        {
            const int slices=16;
            int sideCount=(slices+1)*2,capCount=slices+2;
            var vertices=new Vector3[sideCount+capCount*2];var normals=new Vector3[vertices.Length];var triangles=new int[slices*12];
            for(int slice=0;slice<=slices;slice++)
            {
                float angle=2*Mathf.PI*slice/slices;var normal=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                vertices[slice*2]=normal*.5f+Vector3.down;vertices[slice*2+1]=normal*.5f+Vector3.up;
                normals[slice*2]=normals[slice*2+1]=normal;
            }
            int at=0;
            for(int slice=0;slice<slices;slice++)
            {
                int a=slice*2;
                triangles[at++]=a;triangles[at++]=a+1;triangles[at++]=a+2;
                triangles[at++]=a+1;triangles[at++]=a+3;triangles[at++]=a+2;
            }
            for(int cap=0;cap<2;cap++)
            {
                var normal=cap==0?Vector3.down:Vector3.up;int center=sideCount+cap*capCount;
                vertices[center]=normal;normals[center]=normal;
                for(int slice=0;slice<=slices;slice++)
                {
                    float angle=2*Mathf.PI*slice/slices;
                    vertices[center+1+slice]=normal+new Vector3(Mathf.Cos(angle)*.5f,0,Mathf.Sin(angle)*.5f);normals[center+1+slice]=normal;
                }
                for(int slice=0;slice<slices;slice++)
                {
                    triangles[at++]=center;triangles[at++]=center+1+slice+(cap==0?0:1);triangles[at++]=center+1+slice+(cap==0?1:0);
                }
            }
            return Make("Clinic cylinder",vertices,normals,triangles);
        }
        private static Mesh Make(string name,Vector3[] vertices,Vector3[] normals,int[] triangles)
        {
            var mesh=new Mesh { name=name,vertices=vertices,normals=normals,triangles=triangles };
            mesh.RecalculateBounds();return mesh;
        }
    }
}
