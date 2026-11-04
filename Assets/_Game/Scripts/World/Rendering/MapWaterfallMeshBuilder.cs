using System.Collections.Generic;
using System.IO;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    public static class MapWaterfallMeshBuilder
    {
        public static Mesh Build(MapWaterfallRouteData route, float travelOffset = 0,MapRiverPath receivingRiver=null,bool joinedUpstream=false,
            MapRiverPath upstreamRiver=null,System.Func<float,float,float> heightSampler=null)
        {
            int rows = route.sections.Count, columns = route.sections[0].points.Count, plane = rows * columns;
            int triangleCount = (rows - 1) * (columns - 1) * 4 + (rows - 1) * 4 + (columns - 1) * 4;
            if (triangleCount > 11200) throw new InvalidDataException("Контактная геометрия превышает бюджет M1 (11200 треугольников). Сузьте поток или сократите переход.");
            var vertices = new Vector3[plane * 2]; var uv = new Vector2[vertices.Length]; var colors = new Color[vertices.Length];
            var normalized = new List<Vector2>(vertices.Length); var sections = new List<Vector4>(vertices.Length);
            var lipData=new List<Vector2>(vertices.Length);
            float time = travelOffset;
            float length = 0;
            var distances = new float[rows];
            for (int row = 1; row < rows; row++)
                distances[row] = length += Vector3.Distance(route.sections[row].FrontCenter, route.sections[row - 1].FrontCenter);
            bool softLip=upstreamRiver?.record.layoutVersion==3;
            var upper=softLip?upstreamRiver.frames[upstreamRiver.frames.Count-1]:default;
            // End the calm transition before a detached/steep section. Old saved routes get
            // the same derived join, without changing their support samples or serialized data.
            float lipLength=Mathf.Min(.8f,length*.25f);
            if(softLip)for(int row=1;row<rows;row++)
            {
                var section=route.sections[row];var prior=route.sections[row-1];
                var delta=section.FrontCenter-prior.FrontCenter;
                if(section.kind==MapWaterfallSectionKind.FreeFall||section.points.Exists(p=>p.detached)||Mathf.Abs(delta.y)>new Vector2(delta.x,delta.z).magnitude*.35f)
                {lipLength=Mathf.Min(lipLength,distances[row]);break;}
            }
            int landingStart=rows-1;
            if(receivingRiver?.record.layoutVersion==3)
                for(int row=0;row<rows;row++)if((route.sections[row].points[columns/2].surfaceKey??"").StartsWith(MapWaterfallLanding.SurfacePrefix,System.StringComparison.Ordinal)){landingStart=row;break;}
            for (int side = 0; side < 2; side++)
            {
                time = travelOffset;
                for (int row = 0; row < rows; row++)
                {
                    var section = route.sections[row];
                    if (row > 0) time += Vector3.Distance(section.FrontCenter, route.sections[row - 1].FrontCenter)
                        * .5f * (1 / section.speed + 1 / route.sections[row - 1].speed);
                    for (int col = 0; col < columns; col++)
                    {
                        var p = section.points[col]; int index = side * plane + row * columns + col;
                        vertices[index] = side == 0 ? p.Front(section.thickness) : p.position;
                        uv[index] = new Vector2(p.across >= 0 ? p.across : col / (float)(columns - 1), time * 4.5f);
                        float lip=softLip?(row==0?1:1-Mathf.SmoothStep(0,1,Mathf.Clamp01(distances[row]/Mathf.Max(.0001f,lipLength)))):0;
                        float depth=0;
                        if(softLip)
                        {
                            float signed=upper.width*(uv[index].x-.5f);var at=upper.center+upper.Left*signed;
                            float bed=heightSampler!=null?heightSampler(at.x,at.y):MapRiverSection.Bed(upper,signed,upper.water);
                            depth=Mathf.Max(0,upper.water-bed);
                            if(row==0)vertices[index]=new Vector3(at.x,side==0?upper.water+.018f:Mathf.Min(upper.water+.018f,bed),at.y);
                            else if(side==1&&lip>0)vertices[index]=Vector3.Lerp(vertices[index],p.Front(section.thickness)-Vector3.up*(depth+.018f),lip);
                        }
                        lipData.Add(new Vector2(lip,depth));
                        normalized.Add(new Vector2(uv[index].x, length > .0001f ? distances[row] / length : 0));
                        // Contact geometry remains fixed; animation lives in the shared flow shader.
                        float thickness=p.thickness>0?p.thickness:section.thickness;
                        if(softLip)thickness=Mathf.Lerp(thickness,depth+.018f,lip);
                        sections.Add(new Vector4(p.normal.x, p.normal.y, p.normal.z,thickness));
                        float landing=landingStart<rows-1?Mathf.SmoothStep(0,1,Mathf.InverseLerp(distances[landingStart],length,distances[row])):0;
                        colors[index] = new Color(1, .17f, p.detached ? .9f : .3f, landing);
                    }
                }
            }
            var triangles = new List<int>(triangleCount * 3);
            void Tri(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
            for (int row = 0; row < rows - 1; row++) for (int col = 0; col < columns - 1; col++)
            {
                int a = row * columns + col, b = a + 1, c = a + columns, d = c + 1;
                Tri(a, b, c); Tri(b, d, c); Tri(a + plane, c + plane, b + plane); Tri(b + plane, c + plane, d + plane);
            }
            for (int row = 0; row < rows - 1; row++)
            {
                int a = row * columns, b = a + columns, c = a + columns - 1, d = b + columns - 1;
                Tri(a, b, a + plane); Tri(b, b + plane, a + plane);
                Tri(c, c + plane, d); Tri(d, c + plane, d + plane);
            }
            for (int col = 0; col < columns - 1; col++)
            {
                int a = col, b = col + 1, c = (rows - 1) * columns + col, d = c + 1;
                if(!joinedUpstream&&!softLip){Tri(a, a + plane, b); Tri(b, a + plane, b + plane);}
                if(receivingRiver?.record.layoutVersion!=3){Tri(c, d, c + plane); Tri(d, d + plane, c + plane);}
            }
            var mesh = new Mesh { name = "Saved contact waterfall volume", hideFlags = HideFlags.HideAndDontSave,
                indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.SetUVs(1, normalized); mesh.SetUVs(2, sections);
            mesh.SetUVs(3,lipData);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            if(softLip)
            {
                var normals=mesh.normals;var normal=upstreamRiver.SurfaceNormal(upstreamRiver.Length);
                for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
                {
                    int index=row*columns+col;float lip=lipData[index].x;
                    if(lip<=0)continue;
                    normals[index]=Vector3.Slerp(normals[index],normal,lip).normalized;
                    normals[plane+index]=Vector3.Slerp(normals[plane+index],-normal,lip).normalized;
                }
                mesh.normals=normals;
            }
            if(landingStart<rows-1)
            {
                var normals=mesh.normals;var normal=receivingRiver.SurfaceNormal(0);
                for(int col=0;col<columns;col++){normals[(rows-1)*columns+col]=normal;normals[plane+(rows-1)*columns+col]=-normal;}
                mesh.normals=normals;
            }
            mesh.RecalculateBounds(); return mesh;
        }

        public static Mesh Impact(MapWaterfallRouteData route,int row=-1,float scale=.65f,MapRiverPath receivingRiver=null,float flowTimeOffset=0)
        {
            var end = route.sections[row<0?route.sections.Count-1:row]; Vector3 center = end.FrontCenter;
            Vector3 side = (end.points[end.points.Count - 1].position - end.points[0].position).normalized;
            Vector3 forward = Vector3.Cross(Vector3.up, side).normalized;
            float channelWidth=Vector3.Distance(end.points[0].position,end.points[end.points.Count-1].position);
            float width=channelWidth*scale;
            const int rings = 5, around = 32;
            var vertices = new Vector3[(rings + 1) * (around + 1)]; var uv = new Vector2[vertices.Length]; var colors = new Color[vertices.Length];
            var triangles = new List<int>();
            for (int r = 0; r <= rings; r++) for (int c = 0; c <= around; c++)
            {
                float a = c * Mathf.PI * 2 / around, t = r / (float)rings;
                int i = r * (around + 1) + c;
                vertices[i] = center + side * (Mathf.Cos(a) * width * .45f * t) + forward * ((Mathf.Sin(a) * .6f + .6f) * width * t);
                vertices[i].y += .012f + Mathf.Sin(t * Mathf.PI) * .018f;
                var delta=vertices[i]-center;
                uv[i]=new Vector2(.5f+Vector3.Dot(delta,side)/Mathf.Max(.01f,channelWidth),(flowTimeOffset+Vector3.Dot(delta,forward)/Mathf.Max(.02f,end.speed))*4.5f);
                colors[i] = new Color(Mathf.Pow(1-t,1.3f), .17f, 1, 1);
                if(receivingRiver?.record.layoutVersion==3)
                {
                    var p=new Vector2(vertices[i].x,vertices[i].z);
                    if(receivingRiver.Nearest(p,out var frame,out float signed)&&Vector2.Distance(p,frame.center)<=frame.width*.5f)
                    {
                        vertices[i].y=frame.water+.022f;
                        uv[i]=new Vector2(.5f+signed/frame.width,(flowTimeOffset+receivingRiver.TravelTime(frame.station))*4.5f);
                    }
                    else colors[i].r=0;
                }
                if (r == rings || c == around) continue;
                int j = i + around + 1;
                triangles.Add(i); triangles.Add(j); triangles.Add(i + 1);
                triangles.Add(i + 1); triangles.Add(j); triangles.Add(j + 1);
            }
            var mesh = new Mesh { name = "Saved waterfall impact", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
