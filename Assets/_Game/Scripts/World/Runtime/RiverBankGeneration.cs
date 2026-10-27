using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>Versioned, deterministic bank shape. Authoritative river inputs remain outside presentation.</summary>
    public static class RiverBankGeneration
    {
        public const int Version=2;
        public const int ProfileVertices=8;
        public static float Noise(int seed,float x,float y)
        {
            int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);float u=x-ix,v=y-iy;u=u*u*(3-2*u);v=v*v*(3-2*v);
            return Mathf.Lerp(Mathf.Lerp(Hash(seed,ix,iy),Hash(seed,ix+1,iy),u),Mathf.Lerp(Hash(seed,ix,iy+1),Hash(seed,ix+1,iy+1),u),v)*2-1;
        }
        static float Hash(int seed,int x,int y)
        {unchecked{uint h=(uint)seed^(uint)x*0x8da6b343u^(uint)y*0xd8163841u;h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;return (h&0xffffffu)/16777216f;}}
        public static float WidthVariation(int seed,float station)=>Noise(seed,station*.035f,7)*.8f+Noise(seed+31,station*.12f,19)*.24f;
        public static float CrestOffset(MapRiverFrame f,int side)=>.48f+Noise(f.seed+side*73,f.station*.13f,0)*.12f;
        public static float SampleBed(MapRiverFrame f,float signedDistance,float ground)
        {
            float d=Mathf.Abs(signedDistance),half=f.width*.5f,sand=signedDistance>=0?f.leftSand:f.rightSand;
            float transition=signedDistance>=0?f.leftTransition:f.rightTransition;
            float depth=Mathf.Lerp(f.depth,f.shallowDepth,f.ford),bed=f.water-depth;
            if(d<half)
            {
                float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(half*.45f,half,d));
                float shelf=f.water-Mathf.Lerp(.58f,.13f,sand);return Mathf.Lerp(Mathf.Lerp(bed,shelf,edge),f.water-f.shallowDepth,f.ford);
            }
            // Cliff earth keeps its original plateau until a narrow band hidden inside the rock mass.
            float cliff=Mathf.SmoothStep(0,1,Mathf.InverseLerp(half-.04f,half+CrestOffset(f,signedDistance>=0?1:-1),d));
            float ramp=Mathf.SmoothStep(0,1,Mathf.Clamp01((d-half)/transition));
            return Mathf.Lerp(f.water-Mathf.Lerp(.58f,.13f,sand),ground,Mathf.Lerp(cliff,ramp,sand));
        }
        public static Vector3 ProfilePoint(MapRiverFrame f,int side,int vertex,Func<float,float,float> baseHeight,Func<float,float,float> composedHeight)
        {
            float half=f.width*.5f,station=f.station;
            float broad=Noise(f.seed+side*37,station*.045f,3),medium=Noise(f.seed+side*53,station*.19f,9),fine=Noise(f.seed+side*89,station*.53f,vertex*.71f);
            float buttress=.35f+.65f*(broad*.5f+.5f);
            float sand=side>0?f.leftSand:f.rightSand,stepped=side>0?f.leftSteps:f.rightSteps,rocky=side>0?f.leftRocky:f.rightRocky;
            float crest=CrestOffset(f,side);
            float offset=vertex==0?-.68f:vertex==1?-.2f:vertex==2?.02f+buttress*.35f:vertex==3?.10f+buttress*.55f:vertex==4?.22f+stepped*.85f:vertex==5?crest:vertex==6?1.45f:3;
            float d=half+offset;
            if(vertex>0&&vertex<5)d+=medium*.16f+fine*.06f;
            if(sand>.01f)d=Mathf.Lerp(d,half+(vertex/7f)*(side>0?f.leftTransition:f.rightTransition),sand);
            var p=f.center+f.Left*(d*side);var crestP=f.center+f.Left*((half+crest)*side);
            float top=baseHeight(crestP.x,crestP.y),bottom=f.water-.62f;
            float level=vertex==0?0:vertex==1?.12f:vertex==2?.28f:vertex==3?.5f:vertex==4?.71f:1;
            float y=Mathf.Lerp(bottom,top,level);
            if(vertex>0&&vertex<5)y+=medium*.12f+fine*.06f;
            if(vertex==5)y=top+.025f;
            if(vertex>=6)y=composedHeight(p.x,p.y)+.025f;
            float terrain=composedHeight(p.x,p.y)+.025f;
            y=Mathf.Lerp(y,terrain,Mathf.Max(sand,rocky*.68f));
            return new Vector3(p.x,y,p.y);
        }
        /// <summary>R=rock G=soil B=grass; wet moss reuses the grass layer with rock mixed beneath.</summary>
        public static Color Weights(MapRiverFrame f,int side,int vertex)
        {
            float sand=side>0?f.leftSand:f.rightSand;
            float moss=(Noise(f.seed+side*79,f.station*.27f,vertex*.53f)*.5f+.5f)*.30f;
            float grass=vertex>=5?Mathf.Lerp(.55f,1,(vertex-5)/2f):vertex==2||vertex==4?moss:0;
            float soil=vertex>=5?.10f:.03f;soil=Mathf.Lerp(soil,.98f,sand);grass*=1-sand;
            float stone=Mathf.Max(0,1-grass-soil);float sum=stone+soil+grass;
            return new Color(stone/sum,soil/sum,grass/sum,moss);
        }
    }
}
