using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// Concept-derived dimensions, not an architectural survey. One Unity unit is one metre.
[InitializeOnLoad]
public static class PersianHouseBlockoutBuilder
{
    const string Dir = "Assets/PersianHouseBlockout";
    const string ScenePath = Dir + "/PersianHouse_Blockout.unity";
    const float Upper = 4.2f, Wall = .30f;
    static Transform root;
    static Material wallMat, floorMat, darkMat, stairMat;
    static int solids;
    static readonly List<string> checks = new List<string>();

    static PersianHouseBlockoutBuilder() { EditorApplication.delayCall += AutoBuild; }
    static void AutoBuild()
    {
        if ((File.Exists(ScenePath) && !File.Exists(Dir+"/REBUILD_REQUEST")) || SessionState.GetBool("PersianHouseBuilding", false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += AutoBuild; return; }
        if(File.Exists(Dir+"/REBUILD_REQUEST")) File.Delete(Dir+"/REBUILD_REQUEST");
        Build();
    }

    [MenuItem("Tools/Persian House/Open Blockout")]
    public static void Open()
    {
        if (!File.Exists(ScenePath)) { Build(); return; }
        // Prefab mode preserves any unsaved scene the user already has open.
        PrefabStageUtility.OpenPrefab(Dir + "/PersianHouse_Blockout.prefab");
        EditorApplication.delayCall += Frame;
    }

    static Material Mat(string name, Color c)
    {
        var path = Dir + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("HDRP/Lit")); m.name = name;
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", .08f);
        AssetDatabase.CreateAsset(m, path); return m;
    }
    static Transform Group(string name, Transform parent)
    { var o = new GameObject(name); o.transform.SetParent(parent, false); return o.transform; }
    static GameObject Box(Transform p, string name, float x, float y, float z, float sx, float sy, float sz, Material m = null)
    {
        if (Mathf.Min(sx, Mathf.Min(sy, sz)) <= 0) throw new Exception("Invalid solid: " + name);
        var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = name;
        o.transform.SetParent(p, false); o.transform.localPosition = new Vector3(x,y,z);
        o.transform.localScale = new Vector3(sx,sy,sz);
        o.GetComponent<Renderer>().sharedMaterial = m ?? wallMat; o.isStatic = true; solids++; return o;
    }
    static void Slab(Transform p, string name, float x0,float x1,float z0,float z1,float top, Material m = null, float thick=.22f)
    { Box(p,name,(x0+x1)/2,top-thick/2,(z0+z1)/2,x1-x0,thick,z1-z0,m ?? floorMat); }
    static void WX(Transform p,string n,float x0,float x1,float z,float y,float h)
    { Box(p,n,(x0+x1)/2,y+h/2,z,x1-x0,h,Wall); }
    static void WZ(Transform p,string n,float x,float z0,float z1,float y,float h)
    { Box(p,n,x,y+h/2,(z0+z1)/2,Wall,h,z1-z0); }
    static void DoorX(Transform p,string n,float x0,float x1,float z,float y,float h,float center,float width=1.2f,float height=2.4f)
    {
        WX(p,n+" left",x0,center-width/2,z,y,h); WX(p,n+" right",center+width/2,x1,z,y,h);
        WX(p,n+" lintel",center-width/2,center+width/2,z,y+height,h-height);
    }
    static void DoorZ(Transform p,string n,float x,float z0,float z1,float y,float h,float center,float width=1.2f,float height=2.4f)
    {
        WZ(p,n+" left",x,z0,center-width/2,y,h); WZ(p,n+" right",x,center+width/2,z1,y,h);
        WZ(p,n+" lintel",x,center-width/2,center+width/2,y+height,h-height);
    }
    static void Room(Transform p,string name,float x0,float x1,float z0,float z1,float y,string door)
    {
        var r=Group(name,p); float h=y<1?3.98f:3.65f;
        Slab(r,"Floor",x0,x1,z0,z1,y);
        if(door=="S") DoorX(r,"South doorway",x0,x1,z0,y,h,(x0+x1)/2); else WX(r,"South wall",x0,x1,z0,y,h);
        if(door=="N") DoorX(r,"North doorway",x0,x1,z1,y,h,(x0+x1)/2); else WX(r,"North wall",x0,x1,z1,y,h);
        if(door=="W") DoorZ(r,"West doorway",x0,z0,z1,y,h,(z0+z1)/2); else WZ(r,"West wall",x0,z0,z1,y,h);
        if(door=="E") DoorZ(r,"East doorway",x1,z0,z1,y,h,(z0+z1)/2); else WZ(r,"East wall",x1,z0,z1,y,h);
    }

    // A low-poly pointed opening: solid extruded arch band, with an actual empty aperture.
    static void Arch(Transform p,string name,Vector3 at,bool alongZ,float width,float spring,float peak,float top)
    {
        var g=Group(name,p); g.localPosition=at;
        if(alongZ) g.localRotation=Quaternion.Euler(0,90,0);
        float half=width/2, pier=.30f;
        Box(g,"Left pier",-half-pier/2,top/2,0,pier,top,.38f);
        Box(g,"Right pier",half+pier/2,top/2,0,pier,top,.38f);
        var verts=new List<Vector3>(); var tris=new List<int>();
        const int segments=12;
        for(int i=0;i<segments;i++)
        {
            float a=-half+width*i/segments,b=-half+width*(i+1)/segments;
            float ya=spring+(peak-spring)*(1-Mathf.Pow(Mathf.Abs(a/half),1.45f));
            float yb=spring+(peak-spring)*(1-Mathf.Pow(Mathf.Abs(b/half),1.45f));
            int k=verts.Count;
            verts.AddRange(new[]{new Vector3(a,ya,-.19f),new Vector3(b,yb,-.19f),new Vector3(b,top,-.19f),new Vector3(a,top,-.19f),new Vector3(a,ya,.19f),new Vector3(b,yb,.19f),new Vector3(b,top,.19f),new Vector3(a,top,.19f)});
            int[] faces={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
            foreach(int t in faces) tris.Add(k+t);
        }
        // Independent face vertices keep the blockout faces flat instead of smoothing
        // across the internal seams of adjacent arch segments.
        var flatVerts=new List<Vector3>(); var flatTris=new List<int>();
        foreach(int t in tris) { flatTris.Add(flatVerts.Count); flatVerts.Add(verts[t]); }
        var mesh=new Mesh {name=name}; mesh.SetVertices(flatVerts); mesh.SetTriangles(flatTris,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string meshPath=Dir+"/Meshes/Arch_"+Guid.NewGuid().ToString("N")+".asset"; AssetDatabase.CreateAsset(mesh,meshPath);
        var o=new GameObject("Arch solid",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider)); o.transform.SetParent(g,false);
        o.GetComponent<MeshFilter>().sharedMesh=mesh; o.GetComponent<MeshRenderer>().sharedMaterial=wallMat;
        o.GetComponent<MeshCollider>().sharedMesh=mesh; o.isStatic=true; solids++;
    }

    static void Galleries(Transform p,float y,bool rail)
    {
        var g=Group("Courtyard galleries - 1.8m wide",p); float top=y<1?3.98f:3.7f;
        Slab(g,"West gallery",-10.6f,-8.8f,-11.2f,11.2f,y);
        Slab(g,"East gallery",8.8f,10.6f,-11.2f,11.2f,y);
        Slab(g,"North gallery",-8.8f,8.8f,9.4f,11.2f,y);
        Slab(g,"South gallery",-8.8f,8.8f,-11.2f,-9.4f,y);
        for(int side=-1;side<=1;side+=2)
        for(int i=0;i<6;i++)
        {
            float z=-9.4f+(i+.5f)*(18.8f/6);
            Arch(g,"Side arcade "+side+" "+i,new Vector3(side*8.8f,y,z),true,18.8f/6-.3f,2.25f,3.30f,top);
            if(rail) Box(g,"Balcony parapet",side*8.8f,y+.5f,z,.18f,1,18.8f/6-.3f);
        }
        foreach(float z in new[]{-9.4f,9.4f})
        {
            Arch(g,"Central courtyard arch",new Vector3(0,y,z),false,5.6f,2.25f,3.5f,top);
            foreach(float x in new[]{-7.25f,-4.35f,4.35f,7.25f})
            {
                Arch(g,"End arcade",new Vector3(x,y,z),false,2.6f,2.25f,3.3f,top);
                if(rail) Box(g,"Balcony parapet",x,y+.5f,z,2.6f,1,.18f);
            }
            if(rail) Box(g,"Central balcony parapet",0,y+.5f,z,5.6f,1,.18f);
        }
    }
    static void Stair(Transform p,string name,float x0,float baseY,float rise)
    {
        var s=Group(name,p);
        // Two 1.15m clear flights, 24 risers. Landing/exit are on the north side.
        const int n=12; const float tread=.28f,w=1.15f,start=9.4f;
        float step=rise/(2*n),end=start-n*tread;
        for(int i=0;i<n;i++)
        {
            float h=(i+1)*step;
            Box(s,"Flight A tread "+(i+1),x0+w/2,baseY+h-.1f,start-(i+.5f)*tread,w,.2f,tread,stairMat);
            float h2=rise/2+(i+1)*step;
            Box(s,"Flight B tread "+(i+1),x0+w+0.15f+w/2,baseY+h2-.1f,end+(i+.5f)*tread,w,.2f,tread,stairMat);
        }
        Slab(s,"Mid landing",x0,x0+2*w+.15f,end-1.2f,end,baseY+rise/2,stairMat);
        Slab(s,"Arrival landing",x0+w+.15f,x0+2*w+.15f,start,start+1.8f,baseY+rise,stairMat);
        WZ(s,"Stair outer wall",x0-.15f,end-1.2f,start+1.8f,baseY,rise+1);
        // Thin central guard follows each flight, leaving full width on both sides.
        Box(s,"Central guard",x0+w+.075f,baseY+rise/2+.5f,(start+end)/2,.12f,rise+1,start-end);
        checks.Add(name+": 24 risers of "+(step*1000).ToString("F0")+" mm; tread 280 mm; clear width 1150 mm.");
    }

    static void Level(Transform p,float y,bool upper)
    {
        float h=upper?3.65f:3.98f;
        Galleries(p,y,upper);
        foreach(int side in new[]{-1,1})
        {
            float x0=side<0?-13.6f:10.6f,x1=side<0?-10.6f:13.6f;
            string door=side<0?"E":"W";
            Room(p,(side<0?"West":"East")+" side room A",x0,x1,-5.4f,.0f,y,door);
            Room(p,(side<0?"West":"East")+" side room B",x0,x1,-9.4f,-5.4f,y,door);
            Room(p,(side<0?"West":"East")+" side room C",x0,x1,0,4.5f,y,door);
            // Stairwell floor is intentionally absent above the flights.
            Slab(p,"Stair access landing",x0,x1,9.4f,11.2f,y);
            WZ(p,"Stairwell outside",side*13.6f,4.5f,11.2f,y,h);
            WZ(p,"Stairwell gallery side",side*10.6f,4.5f,9.4f,y,h);
            WX(p,"Stairwell end",x0,x1,4.5f,y,h);
            // Four northern side rooms flank the main hall.
            float a=side<0?-13.6f:4.6f,b=side<0?-4.6f:13.6f,mid=(a+b)/2;
            Room(p,"North side room outer "+side,a,mid,11.2f,16.5f,y,"S");
            Room(p,"North side room inner "+side,mid,b,11.2f,16.5f,y,"S");
        }
        var hall=Group("Main hall - Iwan - 9.2 x 8.8m",p);
        Slab(hall,"Hall floor",-4.6f,4.6f,11.2f,20,y);
        WX(hall,"North wall",-4.6f,4.6f,20,y,h);
        WZ(hall,"West wall",-4.6f,11.2f,20,y,h); WZ(hall,"East wall",4.6f,11.2f,20,y,h);
        WX(hall,"Front west",-4.6f,-2.95f,11.2f,y,h); WX(hall,"Front east",2.95f,4.6f,11.2f,y,h);
        Arch(hall,"Iwan opening",new Vector3(0,y,11.2f),false,5.6f,2.3f,3.5f,h);
        Room(p,"Southwest private room A",-10.6f,-6.3f,-17,-11.2f,y,"N");
        Room(p,"Southwest private room B",-6.3f,-2.1f,-17,-11.2f,y,"N");
        Room(p,upper?"Southeast private hall":"Kitchen and service hall",2.1f,13.6f,-18.5f,-11.2f,y,"N");
        var entry=Group(upper?"Upper entrance hall":"Hashti and street entrance",p);
        Slab(entry,"Hashti floor",-2.1f,2.1f,-17,-11.2f,y);
        WZ(entry,"Hashti west",-2.1f,-17,-11.2f,y,h); WZ(entry,"Hashti east",2.1f,-17,-11.2f,y,h);
        DoorX(entry,"Courtyard passage",-2.1f,2.1f,-11.2f,y,h,0,1.8f,2.7f);
        DoorX(entry,"Entrance passage",-2.1f,2.1f,-17,y,h,0,1.8f,2.7f);
        Slab(entry,"Entrance floor",-2.1f,2.1f,-20.8f,-17,y);
        WZ(entry,"Entrance west",-2.1f,-20.8f,-17,y,h); WZ(entry,"Entrance east",2.1f,-20.8f,-17,y,h);
        if(upper) { WX(entry,"Street parapet",-2.1f,2.1f,-20.8f,y,1.0f); }
        else DoorX(entry,"Street doorway",-2.1f,2.1f,-20.8f,y,h,0,1.8f,2.7f);
    }

    static void Courtyard(Transform p)
    {
        var c=Group("Courtyard - 17.6 x 18.8m",p);
        // Leave an actual 3.8 x 8m opening in the courtyard floor for the pool.
        Slab(c,"West paving",-8.8f,-1.9f,-9.4f,9.4f,0);
        Slab(c,"East paving",1.9f,8.8f,-9.4f,9.4f,0);
        Slab(c,"North paving",-1.9f,1.9f,4,9.4f,0);
        Slab(c,"South paving",-1.9f,1.9f,-9.4f,-4,0);
        Slab(c,"Pool bottom - 0.6m deep",-1.9f,1.9f,-4,4,-.6f,darkMat);
        Box(c,"Pool west edge",-1.9f,-.25f,0,.22f,.7f,8.22f,stairMat);
        Box(c,"Pool east edge",1.9f,-.25f,0,.22f,.7f,8.22f,stairMat);
        Box(c,"Pool north edge",0,-.25f,4,3.8f,.7f,.22f,stairMat);
        Box(c,"Pool south edge",0,-.25f,-4,3.8f,.7f,.22f,stairMat);
        foreach(float x in new[]{-5.7f,5.7f}) foreach(float z in new[]{-5.8f,5.8f})
            Slab(c,"Garden bed placeholder",x-1.25f,x+1.25f,z-1.5f,z+1.5f,.18f,darkMat);
    }
    static void Cellar(Transform p)
    {
        var c=Group("Cellar under north wing - floor -3.4m",p);
        Slab(c,"Cellar floor",-13.6f,10.6f,9.4f,20,-3.4f);
        WX(c,"North wall",-13.6f,10.6f,20,-3.4f,3.18f);
        WZ(c,"West wall",-13.6f,9.4f,20,-3.4f,3.18f);
        WZ(c,"East wall",10.6f,9.4f,20,-3.4f,3.18f);
        // Opening to the west stair at x=-13.15..-12.0; no wall across the stair exit.
        WX(c,"South wall",-11.7f,10.6f,9.4f,-3.4f,3.18f);
        foreach(float x in new[]{-7f,0,7f}) foreach(float z in new[]{12.4f,17.5f})
            Box(c,"Cellar pier",x,-1.81f,z,.55f,3.18f,.55f);
        Slab(c,"Cellar upper cover",-13.6f,10.6f,16.5f,20,0);
        Stair(c,"Basement to ground stairs",-13.3f,-3.4f,3.4f);
    }
    static void Roof(Transform p)
    {
        var r=Group("ROOF - enable for enclosed rooms",p); float y=8.1f;
        Slab(r,"West roof",-13.6f,-8.8f,-11.2f,16.5f,y);
        Slab(r,"East roof",8.8f,13.6f,-11.2f,16.5f,y);
        Slab(r,"North roof",-8.8f,8.8f,9.4f,16.5f,y);
        Slab(r,"Iwan roof",-4.6f,4.6f,16.5f,20,y);
        Slab(r,"South roof",-10.6f,13.6f,-17,-9.4f,y);
        Slab(r,"Service roof",2.1f,13.6f,-18.5f,-17,y);
        Slab(r,"Entrance roof",-2.1f,2.1f,-20.8f,-17,y);
        r.gameObject.SetActive(false);
    }
    static void Frame()
    {
        var sv=SceneView.lastActiveSceneView;
        if(sv!=null) { sv.sceneLighting=false; sv.LookAt(new Vector3(0,1,0),Quaternion.Euler(54,-35,0),32); sv.Repaint(); }
    }
    static void ValidateHeadroom()
    {
        Physics.SyncTransforms(); var colliders=root.GetComponentsInChildren<Collider>();
        foreach(var stair in new[]{new Vector3(-13.3f,0,4.2f),new Vector3(10.85f,0,4.2f),new Vector3(-13.3f,-3.4f,3.4f)})
        for(int flight=0;flight<2;flight++) for(int i=0;i<12;i++)
        {
            float x=stair.x+.575f+flight*1.3f;
            float z=flight==0?9.4f-(i+.5f)*.28f:6.04f+(i+.5f)*.28f;
            float y=stair.y+(flight*12+i+1)*stair.z/24;
            foreach(var col in colliders)
                if(col.Raycast(new Ray(new Vector3(x,y+.03f,z),Vector3.up),out RaycastHit hit,1.95f))
                    throw new Exception("Stair headroom blocked by "+col.name+" at "+new Vector3(x,y,z));
        }
        checks.Add("PASS: 72 stair treads have at least 1.95m overhead clearance.");
        foreach(float z in new[]{-20.8f,-17f,-11.2f,11.2f})
        foreach(var col in colliders)
            if(col.Raycast(new Ray(new Vector3(0,1.8f,z-.4f),Vector3.forward),out RaycastHit hit,.8f))
                throw new Exception("Central passage obstructed by "+col.name);
        checks.Add("PASS: entrance, Hashti, courtyard and Iwan central passages are open.");
    }
    static void RenderOverview(Camera camera)
    {
        var hidden=new List<GameObject>();
        // Temporarily isolate this scene for the preview, restoring the user's scene afterwards.
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var other=SceneManager.GetSceneAt(i); if(other==camera.gameObject.scene) continue;
            foreach(var o in other.GetRootGameObjects()) if(o.activeSelf) {hidden.Add(o);o.SetActive(false);}
        }
        var rt=new RenderTexture(1600,1200,24,RenderTextureFormat.ARGB32); rt.Create();
        var prior=RenderTexture.active;
        try
        {
            var request=new RenderPipeline.StandardRequest {destination=rt};
            RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=rt; var tex=new Texture2D(1600,1200,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,1600,1200),0,0); tex.Apply();
            File.WriteAllBytes(Dir+"/Blockout_Overview.png",tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
        }
        finally {RenderTexture.active=prior;rt.Release();UnityEngine.Object.DestroyImmediate(rt);foreach(var o in hidden) if(o!=null)o.SetActive(true);}
    }
    static void Build()
    {
        SessionState.SetBool("PersianHouseBuilding",true);
        if(PrefabStageUtility.GetCurrentPrefabStage()!=null) StageUtility.GoToMainStage();
        Scene previous=SceneManager.GetActiveScene(); Scene scene=default;
        try
        {
            Directory.CreateDirectory(Dir+"/Materials"); Directory.CreateDirectory(Dir+"/Meshes"); AssetDatabase.Refresh();
            solids=0; checks.Clear();
            wallMat=Mat("Blockout - walls",new Color(.64f,.65f,.66f));
            floorMat=Mat("Blockout - floors",new Color(.45f,.47f,.49f));
            darkMat=Mat("Blockout - recesses",new Color(.22f,.24f,.26f));
            stairMat=Mat("Blockout - stairs",new Color(.55f,.57f,.59f));
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            root=Group("Persian House | METRES | 27.2 x 40.8m",null);
            var ground=Group("01 GROUND FLOOR - 0.00m",root); Level(ground,0,false); Courtyard(ground);
            var upper=Group("02 FIRST FLOOR - +4.20m",root); Level(upper,Upper,true);
            var circulation=Group("03 STAIRS - 175mm risers",root);
            Stair(circulation,"West ground to first",-13.3f,0,Upper);
            Stair(circulation,"East ground to first",10.85f,0,Upper);
            Cellar(Group("00 BASEMENT - -3.40m",root)); Roof(root);
            // A human-height reference that does not block circulation.
            var scale=Group("04 SCALE REFERENCE - 1.8m human",root);
            var human=GameObject.CreatePrimitive(PrimitiveType.Capsule); human.name="1.80m tall x 0.50m diameter";
            human.transform.SetParent(scale,false); human.transform.localPosition=new Vector3(3.3f,.9f,-6.5f); human.transform.localScale=new Vector3(.5f,.9f,.5f);
            human.GetComponent<Renderer>().sharedMaterial=darkMat;
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,Dir+"/PersianHouse_Blockout.prefab");
            var sun=new GameObject("Blockout daylight",typeof(Light)); sun.transform.rotation=Quaternion.Euler(52,-30,0);
            var light=sun.GetComponent<Light>(); light.type=LightType.Directional; light.intensity=70000; light.shadows=LightShadows.Soft;
            sun.AddComponent<HDAdditionalLightData>();
            var volume=new GameObject("Fixed blockout exposure",typeof(Volume)).GetComponent<Volume>(); volume.isGlobal=true;
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Dir+"/BlockoutExposure.asset");
            if(profile==null) {profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Dir+"/BlockoutExposure.asset");}
            if(!profile.TryGet<Exposure>(out var exposure)) exposure=profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(12.5f);
            volume.sharedProfile=profile;
            var camera=new GameObject("Overview Camera",typeof(Camera)).GetComponent<Camera>(); camera.tag="MainCamera";
            camera.transform.position=new Vector3(-33,39,-46); camera.transform.LookAt(new Vector3(0,1,0)); camera.fieldOfView=48; camera.farClipPlane=250;
            var data=camera.gameObject.AddComponent<HDAdditionalCameraData>(); data.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color; data.backgroundColorHDR=new Color(.18f,.19f,.21f);
            // Verify basic geometry and navigation dimensions before saving.
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(float.IsNaN(t.position.x) || float.IsNaN(t.position.y) || float.IsNaN(t.position.z)) throw new Exception("Invalid transform");
            ValidateHeadroom();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath);
            RenderOverview(camera);
            File.WriteAllText(Dir+"/BUILD_REPORT.txt","SUCCESS\nScene: "+ScenePath+"\nOne Unity unit = one metre.\nReference bar: approx. 81 pixels / 10m.\nFootprint: 27.2 x 40.8m; courtyard: 17.6 x 18.8m.\nFloor elevations: -3.4 / 0 / +4.2m.\nDoorways: 1.2 x 2.4m; main passages 1.8 x 2.7m.\nWalls: 0.30m; floor slabs: 0.22m.\nPool: 3.8 x 8m, depth 0.6m.\nSolid objects: "+solids+"\n"+string.Join("\n",checks)+"\nConcept inferred: heights, precise partitions, cellar extent and stair connectivity.\nRoof disabled for blockout inspection. No textures or decorative assets.\n");
            AssetDatabase.SaveAssets(); SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true);
            Debug.Log("PERSIAN_HOUSE_BLOCKOUT_SUCCESS: "+solids+" solids. "+ScenePath);
            Open();
        }
        catch(Exception e) { Debug.LogException(e); File.WriteAllText(Dir+"/BUILD_ERROR.txt",e.ToString()); if(previous.IsValid()) SceneManager.SetActiveScene(previous); }
        finally { SessionState.SetBool("PersianHouseBuilding",false); }
    }
}
