using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lumen.World
{
    public static partial class AcademyWorld
    {
        private sealed partial class Builder
        {
            private static Mesh polishChamfer, polishCube, polishVent, polishFolio, polishTideTicks, polishGeometricRelief, polishStarDial;

            /// <summary>Contained v0.9 art pass. No anchors, gameplay, animation or collision changes.</summary>
            private void AcademyPolish()
            {
                if (headless) return;
                if (!polishChamfer) polishChamfer = PolishChamferMesh();
                if (!polishCube) polishCube = PolishCubeMesh();
                Material limestone = LumenMaterials.GetSurface("stone", new Color(.60f,.59f,.54f), 2, 0, .34f);
                Material patina = LumenMaterials.GetSurface("metal", new Color(.27f,.36f,.35f), 2, .38f, .48f);
                Material darkStone = LumenMaterials.Get("numerion-polish-shadow-slate", new Color(.13f,.19f,.21f), .03f, .32f);
                Material oldBrass = LumenMaterials.Get("numerion-polish-worn-brass", new Color(.62f,.48f,.29f), .52f, .52f);
                PolishExistingArchitecture(root, limestone, patina, darkStone, oldBrass);

                var detail = MathGroup("Numerion / restrained architectural craft", root, Vector3.zero);
                PolishObservatoryDetails(detail, darkStone, oldBrass);
                if (!polishVent) polishVent = PolishStripBundle("Numerion / four copper ventilation fins", new[] {
                    new Vector3(0,-.18f,0), new Vector3(0,-.06f,0), new Vector3(0,.06f,0), new Vector3(0,.18f,0)
                }, new[] {new Vector3(1.65f,.045f,.13f),new Vector3(1.65f,.045f,.13f),new Vector3(1.65f,.045f,.13f),new Vector3(1.65f,.045f,.13f)});
                for (int i = 0; i < SectionPositions.Length; i++)
                {
                    var department = MathGroup("Department craft / " + i, detail, SectionPositions[i]);
                    PolishMesh("Pavilion lower cornice shadow", department, new Vector3(0,5.32f,2.99f), new Vector3(10.65f,.10f,.13f), polishChamfer, darkStone);
                    for (int side = -1; side <= 1; side += 2)
                        PolishMesh("Pavilion copper ventilation grille", department, new Vector3(side*3.2f,5.82f,2.70f), Vector3.one, polishVent, oldBrass);
                }
                PolishExpeditionDetails(detail, oldBrass, darkStone);
                // This group contains only rigid trim. Labels and the existing moving exhibits remain separate.
                foreach (var renderer in detail.GetComponentsInChildren<MeshRenderer>())
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                StaticBatchingUtility.Combine(detail.gameObject);
            }

            private void PolishPrebatchGeometry(Transform target)
            {
                if (headless) return;
                if (!polishChamfer) polishChamfer = PolishChamferMesh();
                PolishExistingArchitecture(target,
                    LumenMaterials.GetSurface("stone", new Color(.60f,.59f,.54f), 2, 0, .34f),
                    LumenMaterials.GetSurface("metal", new Color(.27f,.36f,.35f), 2, .38f, .48f),
                    LumenMaterials.Get("numerion-polish-shadow-slate", new Color(.13f,.19f,.21f), .03f, .32f),
                    LumenMaterials.Get("numerion-polish-worn-brass", new Color(.62f,.48f,.29f), .52f, .52f));
            }

            private void PolishExistingArchitecture(Transform target, Material limestone, Material patina, Material darkStone, Material oldBrass)
            {
                foreach (var renderer in target.GetComponentsInChildren<MeshRenderer>())
                {
                    // Island geometry is dressed before its existing batch is built. Never rewrite
                    // a mesh already packed into a static batch by another environment layer.
                    if (renderer.isPartOfStaticBatch) continue;
                    string name = renderer.gameObject.name;
                    bool stoneBox = name == "Observatory foundation" || name == "Observatory stone column"
                        || name == "Observatory capital" || name == "Observatory stone lintel"
                        || name == "Pavilion stone column base" || name == "Pavilion roof stone edge"
                        || name == "Lesson terminal limestone plinth" || name == "Surveyed ruin stone column"
                        || name == "Ruin capital" || name == "Broken geometrical arch lintel"
                        || name == "Algebra archive stone pier" || name == "Observatory roof lintel"
                        || name == "Tide archive stone column";
                    if (stoneBox)
                    {
                        // The shared chamfer stays entirely within the existing box. Physics remains exact.
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter) filter.sharedMesh = polishChamfer;
                        renderer.sharedMaterial = limestone;
                    }
                    else if (name == "Algebra instrument cabinet")
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter) filter.sharedMesh = polishChamfer;
                        renderer.sharedMaterial = darkStone;
                    }
                    else if (name == "Pavilion colored roof" || name == "Patinated pavilion roof"
                        || name == "Observatory glazed dome" || name == "Tide archive roof")
                        renderer.sharedMaterial = patina;
                    else if (name == "Pavilion subject frieze" || name == "Observatory inset")
                        renderer.sharedMaterial = darkStone;
                    else if (name == "Observatory window")
                        renderer.sharedMaterial = LumenMaterials.Get("numerion-polish-oxidised-glazing", new Color(.29f,.43f,.44f), .26f, .78f);
                    else if (name == "Observatory gold sill" || name == "Pavilion gold cornice"
                        || name == "Dome brass meridian")
                        renderer.sharedMaterial = oldBrass;
                }
            }

            private void PolishObservatoryDetails(Transform parent, Material slate, Material copper)
            {
                var hall = MathGroup("Observatory / window joinery and optical instrument", parent, new Vector3(0,0,-13.4f));
                for (int side = -1; side <= 1; side += 2)
                {
                    PolishMesh("Foundation recessed shadow course", hall, new Vector3(side*3.4f,.47f,0), new Vector3(2.13f,.075f,3.65f), polishChamfer, slate);
                    for (int bar = -1; bar <= 1; bar++)
                        PolishMesh("Glazing slender copper mullion", hall, new Vector3(side*3.4f+bar*.18f,4.3f,1.55f), new Vector3(.035f,2.45f,.055f), polishCube, copper);
                    for (int bar = -1; bar <= 1; bar += 2)
                        PolishMesh("Glazing cross transom", hall, new Vector3(side*3.4f,4.3f+bar*.62f,1.55f), new Vector3(.52f,.035f,.055f), polishCube, copper);
                }
                // A recognisable telescope silhouette, mounted high on the existing observatory dome.
                var instrument = MathGroup("Roof telescope assembly", hall, new Vector3(0,11.1f,2.35f));
                instrument.localRotation = Quaternion.Euler(-18,0,0);
                PolishMesh("Telescope shadow saddle", instrument, new Vector3(0,-.37f,-.55f), new Vector3(.75f,.38f,.85f), polishChamfer, slate);
                var tube = Shape("Telescope copper optical barrel", PrimitiveType.Cylinder, instrument, Vector3.zero, new Vector3(.68f,1.55f,.68f), agedCopper, false, .4f, .55f);
                tube.transform.localRotation = Quaternion.Euler(90,0,0);
                tube.GetComponent<MeshRenderer>().sharedMaterial = copper;
                var lens = Shape("Telescope dark objective", PrimitiveType.Cylinder, instrument, new Vector3(0,0,1.56f), new Vector3(.55f,.04f,.55f), ink, false);
                lens.transform.localRotation = Quaternion.Euler(90,0,0);
                lens.GetComponent<MeshRenderer>().sharedMaterial = slate;
            }

            private void PolishExpeditionDetails(Transform parent, Material copper, Material slate)
            {
                if (!polishFolio) polishFolio = PolishStripBundle("Numerion / field folio binding", new[] {
                    new Vector3(-.69f,0,0),new Vector3(.69f,0,0),new Vector3(0,0,-.30f),new Vector3(0,0,.30f)
                }, new[] {new Vector3(.025f,.025f,.62f),new Vector3(.025f,.025f,.62f),new Vector3(1.40f,.025f,.025f),new Vector3(1.40f,.025f,.025f)});
                for (int i = 0; i < ExpeditionCenters.Length; i++)
                {
                    var craft = MathGroup("Field archive craft / " + i, parent, ExpeditionCenters[i]);
                    float facing = i == 2 ? -1 : 1;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var binding = PolishMesh("Field study folio copper edge", craft, new Vector3(side*4*facing,1.23f,-.56f), Vector3.one, polishFolio, copper);
                        binding.localRotation = Quaternion.Euler(18,0,0);
                    }
                    if (i == 0)
                    {
                        if (!polishTideTicks) polishTideTicks = PolishRadialTicks("Numerion / eight major and eight minor tide graduations", 16, 3.02f, .22f);
                        PolishMesh("Tidal basin calibrated stone rim", craft, new Vector3(0,.145f,4), Vector3.one, polishTideTicks, copper);
                    }
                    else if (i == 1)
                    {
                        if (!polishGeometricRelief) polishGeometricRelief = PolishTriangleRelief();
                        PolishMesh("Ruin lintel triangular survey relief", craft, new Vector3(-1.8f,5.7f,4.83f), Vector3.one, polishGeometricRelief, copper);
                    }
                    else
                    {
                        if (!polishStarDial) polishStarDial = PolishRadialTicks("Numerion / observatory dial", 12, .52f, .15f);
                        var dial = PolishMesh("Equatorial instrument calibrated face", craft, new Vector3(0,5.25f,-2.54f), Vector3.one, polishStarDial, copper);
                        dial.localRotation = Quaternion.Euler(90,0,0);
                    }
                }
            }

            private Transform PolishMesh(string name, Transform parent, Vector3 position, Vector3 size, Mesh mesh, Material material)
            {
                var item = MathGroup(name,parent,position); item.localScale = size;
                item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                item.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                return item;
            }

            private static Mesh PolishStripBundle(string name, Vector3[] centers, Vector3[] sizes)
            {
                var combine = new CombineInstance[centers.Length];
                for (int i=0;i<combine.Length;i++)
                    combine[i] = new CombineInstance { mesh=polishCube, transform=Matrix4x4.TRS(centers[i],Quaternion.identity,sizes[i]) };
                var mesh = new Mesh { name=name }; mesh.CombineMeshes(combine,true,true); mesh.RecalculateBounds(); return mesh;
            }

            private static Mesh PolishRadialTicks(string name, int count, float radius, float length)
            {
                var combine = new CombineInstance[count];
                for (int i=0;i<count;i++)
                {
                    float a=i*Mathf.PI*2/count;
                    combine[i]=new CombineInstance { mesh=polishCube, transform=Matrix4x4.TRS(
                        new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius),Quaternion.Euler(0,a*Mathf.Rad2Deg,0),
                        new Vector3(i%2==0?.045f:.025f,.018f,i%2==0?length:length*.60f)) };
                }
                var mesh=new Mesh{name=name};mesh.CombineMeshes(combine,true,true);mesh.RecalculateBounds();return mesh;
            }

            private static Mesh PolishTriangleRelief()
            {
                var points=new List<Vector3>();var indices=new List<int>();
                for(int i=0;i<5;i++)
                {
                    int p=points.Count;float x=(i-2)*.62f;
                    points.Add(new Vector3(x-.18f,-.16f,0));points.Add(new Vector3(x+.18f,-.16f,0));points.Add(new Vector3(x,.17f,0));
                    indices.AddRange(new[]{p,p+2,p+1,p,p+1,p+2});
                }
                return PolishFacetedMesh("Numerion / five inlaid survey triangles",points,indices);
            }

            private static Mesh PolishChamferMesh()
            {
                var points=new List<Vector3>();var triangles=new List<int>();
                float[] ys={-.5f,-.44f,.44f,.5f};
                for(int ring=0;ring<4;ring++)
                {
                    float r=ring==0||ring==3?.44f:.5f,b=.065f;
                    points.AddRange(new[]{new Vector3(r,ys[ring],-r+b),new Vector3(r-b,ys[ring],-r),new Vector3(-r+b,ys[ring],-r),new Vector3(-r,ys[ring],-r+b),
                        new Vector3(-r,ys[ring],r-b),new Vector3(-r+b,ys[ring],r),new Vector3(r-b,ys[ring],r),new Vector3(r,ys[ring],r-b)});
                }
                for(int ring=0;ring<3;ring++)
                    for(int i=0;i<8;i++)
                    { int a=ring*8+i,b=ring*8+(i+1)%8,c=a+8,d=b+8;triangles.AddRange(new[]{a,b,c,b,d,c}); }
                points.Add(new Vector3(0,-.5f,0));points.Add(new Vector3(0,.5f,0));
                for(int i=0;i<8;i++)triangles.AddRange(new[]{32,(i+1)%8,i,33,24+i,24+(i+1)%8});
                return PolishFacetedMesh("Numerion / reusable sixty-four triangle dressed stone",points,triangles);
            }

            private static Mesh PolishCubeMesh()
            {
                var p=new List<Vector3>{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
                    new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
                return PolishFacetedMesh("Numerion / twelve triangle fine trim",p,new List<int>{0,3,1,1,3,2,5,6,4,4,6,7,4,7,0,0,7,3,1,2,5,5,2,6,3,7,2,2,7,6,4,0,5,5,0,1});
            }

            private static Mesh PolishFacetedMesh(string name,List<Vector3> points,List<int> faces)
            {
                var vertices=new Vector3[faces.Count];var normals=new Vector3[faces.Count];var uv=new Vector2[faces.Count];var indices=new int[faces.Count];
                for(int i=0;i<faces.Count;i+=3)
                {
                    Vector3 n=Vector3.Cross(points[faces[i+1]]-points[faces[i]],points[faces[i+2]]-points[faces[i]]).normalized;
                    for(int j=0;j<3;j++)
                    {
                        int k=i+j;Vector3 v=points[faces[k]];vertices[k]=v;normals[k]=n;indices[k]=k;
                        uv[k]=Mathf.Abs(n.y)>.6f?new Vector2(v.x+.5f,v.z+.5f):Mathf.Abs(n.x)>.6f?new Vector2(v.z+.5f,v.y+.5f):new Vector2(v.x+.5f,v.y+.5f);
                    }
                }
                var mesh=new Mesh{name=name,vertices=vertices,normals=normals,uv=uv,triangles=indices};mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
            }
        }
    }
}
