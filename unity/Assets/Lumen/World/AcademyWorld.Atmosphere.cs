using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lumen.World
{
    public static partial class AcademyWorld
    {
        private sealed partial class Builder
        {
            private static Mesh atlasRoof, atlasCloth, atlasFern, atlasWater;
            private static readonly Color[] AtlasGreens = {
                new Color(.25f,.40f,.30f), new Color(.40f,.43f,.29f), new Color(.20f,.35f,.30f),
                new Color(.32f,.43f,.40f), new Color(.33f,.41f,.35f), new Color(.38f,.43f,.30f)
            };

            /// <summary>Layered, original environment art. No gameplay geometry, colliders or update loops.</summary>
            private void AcademyAtmosphere()
            {
                if (headless) return;
                var atlas = MathGroup("Academy atlas / atmosphere and architectural depth", root, Vector3.zero);
                ApplyAtlasPalette();
                if (!atlasRoof) atlasRoof = AtlasRoofMesh();
                if (!atlasFern) atlasFern = AtlasFernMesh();
                if (!atlasWater) atlasWater = AtlasWaterMesh();
                for (int i = 0; i < SectionPositions.Length; i++)
                {
                    var sector = MathGroup("Atlas biome / " + SubjectNames[i], atlas, SectionPositions[i]);
                    Color green = AtlasGreens[i];
                    AtlasMesh("Patinated pavilion roof", sector, new Vector3(0,5.24f,-1.4f), Vector3.one,
                        atlasRoof, LumenMaterials.GetSurface("metal", Color.Lerp(teal,ink,.15f+i*.07f), 3, .35f, .48f));
                    Shape("Roof aged stone pediment", PrimitiveType.Cube, sector, new Vector3(0,5.52f,2.96f), new Vector3(10.5f,.14f,.22f), cream, false);
                    Vector3 outward = SectionPositions[i].normalized;
                    Vector3 waterPosition = outward * 16.15f + Vector3.up * -.45f;
                    for (int ribbon = 0; ribbon < 3; ribbon++)
                    {
                        var water = AtlasMesh("Mineral waterfall / opaque layered ribbon", sector, waterPosition,
                            new Vector3(ribbon == 1 ? 1.15f : .3f, ribbon == 1 ? 1 : .93f, 1), atlasWater,
                            LumenMaterials.Get("atlas-water-"+ribbon, ribbon == 1 ? new Color(.36f,.60f,.62f) : new Color(.65f,.76f,.72f), .05f, .85f));
                        water.localRotation = Quaternion.LookRotation(outward);
                        water.localPosition += Vector3.Cross(Vector3.up,outward) * ((ribbon-1)*.58f);
                    }
                    for (int mist = 0; mist < 2; mist++)
                    {
                        var vapour = Shape("Waterfall cloud at cliff foot", PrimitiveType.Sphere, sector,
                            waterPosition + Vector3.down * (14.5f + mist*.8f), new Vector3(4.5f+mist*2,1.4f,3.3f+mist),
                            new Color(.68f,.76f,.74f), false);
                        vapour.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    }
                    for (int side=-1; side<=1; side+=2)
                    {
                        var fern=AtlasMesh("Wild fern on island shoulder",sector,new Vector3(side*10.4f,.04f,-4.8f),new Vector3(1.6f,1.5f,1.6f),atlasFern,LumenMaterials.Get("atlas-green-"+i,green));
                        fern.localRotation=Quaternion.Euler(0,i*37+side*25,0);
                    }
                    StaticBatchingUtility.Combine(sector.gameObject);
                }
                AtlasPromenade(atlas);
                AtlasHorizon(atlas);
                // 90 new renderers before static batching; small decorations do not cast costly shadows.
                foreach (var renderer in atlas.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.gameObject.name.Contains("fern") || renderer.gameObject.name.Contains("ribbon") || renderer.gameObject.name.Contains("cloud"))
                        renderer.shadowCastingMode=ShadowCastingMode.Off;
            }

            private void ApplyAtlasPalette()
            {
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    string name=renderer.gameObject.name;
                    if(name=="Faceted canopy" || name=="Canopy highlight")
                        renderer.sharedMaterial=LumenMaterials.Get("atlas-cypress-"+name,name=="Faceted canopy" ? new Color(.24f,.37f,.28f) : new Color(.34f,.45f,.31f));
                    else if(name=="Cloud sea") renderer.sharedMaterial=LumenMaterials.Get("atlas-cloud-sea",new Color(.49f,.62f,.66f));
                    else if(name=="Cloudbank") renderer.sharedMaterial=LumenMaterials.Get("atlas-cloudbank",new Color(.76f,.80f,.77f));
                    else if(name=="Weathered cliff" || name=="Floating foundation" || name=="Cliff facets")
                        renderer.sharedMaterial=LumenMaterials.GetSurface("stone",name=="Weathered cliff" ? new Color(.30f,.38f,.40f) : new Color(.24f,.31f,.35f),3);
                    else if(name=="Meadow")
                    {
                        // The shared surface helper preserves the actual source PBR maps; this tint adds depth.
                        var material=renderer.sharedMaterial;
                        if(material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",new Color(.40f,.52f,.37f));
                        if(material.HasProperty("_Color")) material.SetColor("_Color",new Color(.40f,.52f,.37f));
                    }
                }
            }

            private void AtlasPromenade(Transform atlas)
            {
                var paving=MathGroup("Promenade / weathered inlaid stone",atlas,Vector3.zero);
                for(int i=0;i<6;i++)
                {
                    Vector3 direction=SectionPositions[i].normalized;
                    for(int inset=0;inset<2;inset++)
                    {
                        var tile=Shape("Promenade dark stone inlay",PrimitiveType.Cube,paving,direction*(7.2f+inset*1.4f)+Vector3.up*.075f,
                            new Vector3(1.25f,.025f,.85f),inset==0 ? new Color(.36f,.43f,.43f) : new Color(.53f,.55f,.49f),false);
                        tile.transform.localRotation=Quaternion.LookRotation(direction);
                    }
                    Shape("Quiet knowledge light",PrimitiveType.Sphere,paving,direction*10.95f+Vector3.up*.20f,Vector3.one*.12f,
                        new Color(.82f,.72f,.45f),false,0,.7f,.35f);
                }
                StaticBatchingUtility.Combine(paving.gameObject);
            }

            private void AtlasHorizon(Transform atlas)
            {
                // Silhouettes are beyond all playable islands: depth cues without increasing route complexity.
                var horizon=MathGroup("Horizon / distant ruined observatories",atlas,Vector3.zero);
                for(int i=0;i<3;i++)
                {
                    float angle=(i*120+22)*Mathf.Deg2Rad;
                    var ruin=MathGroup("Unreachable atlas silhouette",horizon,new Vector3(Mathf.Sin(angle)*154,-6,Mathf.Cos(angle)*154));
                    ruin.localRotation=Quaternion.Euler(0,i*120+22,0);
                    Shape("Horizon mineral stone shelf",PrimitiveType.Cube,ruin,new Vector3(0,-4,0),new Vector3(24,7,17),new Color(.36f,.48f,.53f),false);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Shape("Distant archive stone tower",PrimitiveType.Cube,ruin,new Vector3(side*7,7,0),new Vector3(4,18+side*3,5),new Color(.40f,.51f,.55f),false);
                        Shape("Distant archive cap",PrimitiveType.Cube,ruin,new Vector3(side*7,17+side*1.5f,0),new Vector3(5,.8f,6),new Color(.35f,.46f,.50f),false);
                    }
                    Shape("Distant archive bridge",PrimitiveType.Cube,ruin,new Vector3(0,12,0),new Vector3(15,1.7f,4),new Color(.40f,.51f,.55f),false);
                }
                foreach(var renderer in horizon.GetComponentsInChildren<MeshRenderer>()) renderer.shadowCastingMode=ShadowCastingMode.Off;
                StaticBatchingUtility.Combine(horizon.gameObject);
            }

            private Transform AtlasMesh(string name,Transform parent,Vector3 position,Vector3 scale,Mesh mesh,Material material)
            {
                var item=MathGroup(name,parent,position); item.localScale=scale;
                item.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                item.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
                return item;
            }

            private static Mesh AtlasRoofMesh()
            {
                var v=new[]{new Vector3(-5.45f,0,-4.3f),new Vector3(5.45f,0,-4.3f),new Vector3(5.45f,0,4.3f),new Vector3(-5.45f,0,4.3f),new Vector3(0,1.75f,0)};
                return AtlasFacetedMesh("Atlas / hipped copper roof",v,new[]{0,4,1,1,4,2,2,4,3,3,4,0});
            }

            private static Mesh AtlasClothMesh()
            {
                if(atlasCloth) return atlasCloth;
                var vertices=new List<Vector3>(); var indices=new List<int>();
                for(int i=0;i<6;i++)
                {
                    float x=i/5f-.5f, wave=Mathf.Sin(i*1.2f)*2.4f;
                    vertices.Add(new Vector3(x,.5f,wave)); vertices.Add(new Vector3(x,-.5f+(i%2)*.05f,wave));
                    if(i==0) continue;
                    int p=(i-1)*2; indices.AddRange(new[]{p,p+2,p+1,p+1,p+2,p+3,p+1,p+2,p,p+3,p+2,p+1});
                }
                atlasCloth=AtlasFacetedMesh("Atlas / sculpted cloth pennant",vertices.ToArray(),indices.ToArray()); return atlasCloth;
            }

            private static Mesh AtlasFernMesh()
            {
                var vertices=new List<Vector3>();var indices=new List<int>();
                for(int i=0;i<7;i++)
                {
                    float angle=i*Mathf.PI*2/7;
                    Vector3 radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)), across=Vector3.Cross(Vector3.up,radial)*.18f;
                    int p=vertices.Count;
                    vertices.Add(Vector3.zero);vertices.Add(radial*.53f+Vector3.up*.4f+across);vertices.Add(radial*.98f+Vector3.up*.46f);vertices.Add(radial*.53f+Vector3.up*.4f-across);
                    indices.AddRange(new[]{p,p+1,p+2,p,p+2,p+3,p+2,p+1,p,p+3,p+2,p});
                }
                return AtlasFacetedMesh("Atlas / seven-frond meadow fern",vertices.ToArray(),indices.ToArray());
            }

            private static Mesh AtlasWaterMesh()
            {
                var vertices=new List<Vector3>();var indices=new List<int>();
                for(int i=0;i<8;i++)
                {
                    float depth=i*2.15f, drift=Mathf.Sin(i*.83f)*.19f, width=.52f-i*.027f;
                    vertices.Add(new Vector3(drift-width,-depth,Mathf.Sin(i*.45f)*.42f));vertices.Add(new Vector3(drift+width,-depth,Mathf.Sin(i*.45f)*.42f));
                    if(i==0) continue;
                    int p=(i-1)*2;indices.AddRange(new[]{p,p+1,p+2,p+1,p+3,p+2,p+2,p+1,p,p+2,p+3,p+1});
                }
                return AtlasFacetedMesh("Atlas / cliff-water ribbon",vertices.ToArray(),indices.ToArray());
            }

            private static Mesh AtlasFacetedMesh(string name,Vector3[] points,int[] faces)
            {
                var vertices=new Vector3[faces.Length];var uv=new Vector2[faces.Length];var indices=new int[faces.Length];
                for(int i=0;i<faces.Length;i++){vertices[i]=points[faces[i]];uv[i]=new Vector2(vertices[i].x*.2f,vertices[i].z*.2f);indices[i]=i;}
                var mesh=new Mesh{name=name,vertices=vertices,uv=uv,triangles=indices};mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
