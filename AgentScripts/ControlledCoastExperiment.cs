using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;

// Experiment only. Outside Assets: compiled in memory by Unity Pipeline.
// No assets, scene, prefab, source method, or global bake settings are saved.
public static class ControlledCoastExperiment
{
    const string Dir = "C:/Users/Mathe/Desktop/Hegemonia-Mundial-main/ValidationResults/ControlledFix-20261001";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const float Width = 32f;
    static GlobalTerrainStreamer streamer;
    static GlobalWorldDiagnostics diagnostics;
    static GlobalWorldDefinition world;
    static NavMeshSurface surface;
    static NavMeshData originalNav;
    static bool originalVoxelOverride;
    static float originalVoxel;
    static bool originalCameraEnabled;
    static CameraController cameraController;
    static readonly List<NavMeshData> temporaryNav = new List<NavMeshData>();
    static readonly List<Patch> patches = new List<Patch>();
    static readonly List<Probe> probes = new List<Probe>();
    static readonly List<GameObject> units = new List<GameObject>();
    static readonly List<object> stages = new List<object>();
    static readonly List<object> bakes = new List<object>();
    static readonly List<object> profilerSpikes = new List<object>();
    static readonly List<Vector2> seeds = new List<Vector2>();
    static readonly List<Vector2> clicks = new List<Vector2>();
    static EditorApplication.CallbackFunction tick;
    static AsyncOperation operation;
    static NavMeshData nextNav;
    static double phaseStarted;
    static double bakeStarted;
    static double bakeDispatchMs;
    static long memoryBefore;
    static int phase;
    static string label;
    static bool pathsCaptured;
    static bool frozen;
    static bool restored;
    static int lastProfilerFrame = -1;
    static float worstBakeFrame;
    static float previousFrameTime;
    static MethodInfo benchmarkStart;
    static MethodInfo benchmarkStop;

    sealed class Patch
    {
        public Terrain terrain;
        public TerrainData data;
        public int x, z;
        public float[,] original;
    }

    sealed class Probe
    {
        public string name;
        public float[] coast, requested, sampled, start, edge, destination, finalPosition;
        public float edgeToWater, slope, stoppingDistance, agentRadius, waterHeight;
        public bool navStopsBeforeCoast, sampleFound, accepted, waterTest;
        public string rayHit, layer, classification, initialPathStatus, finalPathStatus;
        public float remainingDistance;
        public float[][] initialCorners, finalCorners;
        [JsonIgnore] public NavMeshAgent agent;
    }

    static float[] V(Vector3 p) => new[] { p.x, p.y, p.z };
    static void Save(string file, object result) => File.WriteAllText(Path.Combine(Dir, file), JsonConvert.SerializeObject(result, Formatting.Indented));
    static Vector3 Normal(Vector2 q) => new Vector3(
        world.SignedCoastDistanceAtWorld(q.x + 10, q.y) - world.SignedCoastDistanceAtWorld(q.x - 10, q.y), 0,
        world.SignedCoastDistanceAtWorld(q.x, q.y + 10) - world.SignedCoastDistanceAtWorld(q.x, q.y - 10)).normalized;

    static Vector3 Ground(Vector3 p, out RaycastHit hit)
    {
        if (!Physics.Raycast(new Vector3(p.x, 1000f, p.z), Vector3.down, out hit, 2000f, 1 << LayerMask.NameToLayer("Chao"), QueryTriggerInteraction.Ignore))
            throw new InvalidOperationException("No existing TerrainCollider at " + p);
        p.y = hit.point.y;
        return p;
    }

    static Vector3 Coast(Vector2 q, Vector3 n)
    {
        float lo = -150f, hi = 150f;
        Vector3 center = new Vector3(q.x, 0, q.y);
        for (int k = 0; k < 18; k++)
        {
            float mid = (lo + hi) / 2;
            if (Ground(center + n * mid, out _).y > world.seaLevel) hi = mid;
            else lo = mid;
        }
        return Ground(center + n * hi, out _);
    }

    static float Candidate(float x, float z)
    {
        float d = world.SignedCoastDistanceAtWorld(x, z);
        float old = world.HeightAtWorld(x, z);
        return world.seaLevel + (old - world.seaLevel) * Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Abs(d) / Width));
    }

    public static object Main()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
        Directory.CreateDirectory(Dir);
        streamer = Object.FindFirstObjectByType<GlobalTerrainStreamer>();
        diagnostics = Object.FindFirstObjectByType<GlobalWorldDiagnostics>();
        world = streamer.world;
        surface = streamer.GetComponent<NavMeshSurface>();
        originalNav = surface.navMeshData;
        if (originalNav == null || streamer.ActiveTileCount != streamer.DesiredTileCount) throw new InvalidOperationException("Wait for existing streaming/NavMesh stabilization.");
        if (Object.FindObjectsByType<ControleUnidade>(FindObjectsSortMode.None).Length != 0) throw new InvalidOperationException("Baseline must have no units.");
        cameraController = Object.FindFirstObjectByType<CameraController>();
        originalCameraEnabled = cameraController.enabled;
        cameraController.enabled = false;
        originalVoxelOverride = surface.overrideVoxelSize;
        originalVoxel = surface.voxelSize;
        var loop = (Coroutine)typeof(GlobalTerrainStreamer).GetField("streamingLoop", Private).GetValue(streamer);
        if (loop != null) streamer.StopCoroutine(loop);
        frozen = true;
        benchmarkStart = typeof(GlobalWorldDiagnostics).GetMethod("StartBenchmark", Private);
        benchmarkStop = typeof(GlobalWorldDiagnostics).GetMethod("StopBenchmark", Private);
        seeds.AddRange(new[] {new Vector2(-200000, 77000), new Vector2(-199000, 77500), new Vector2(-187000, 81500)});
        clicks.AddRange(new[] {new Vector2(-199985.3f, 76980.88f), new Vector2(-199005.7f, 77506.77f), new Vector2(-186998.6f, 81498.25f)});
        // Search an additional truly gentle *original* physical shore, not merely flat inland.
        Vector2? gentle = null;
        float minSlope = float.MaxValue;
        for (int k = -24; k <= 24; k++)
        {
            var q = seeds[2] + new Vector2(k * 32f, 0);
            var center = new Vector3(q.x, 0, q.y);
            var direction = Normal(q);
            if (Ground(center-direction*150f,out _).y>=world.seaLevel || Ground(center+direction*150f,out _).y<=world.seaLevel) continue;
            Vector3 n = Normal(q), coast = Coast(q, n);
            Ground(coast + n * 0.5f, out var hit);
            float slope = Vector3.Angle(hit.normal, Vector3.up);
            minSlope = Mathf.Min(minSlope, slope);
            if (slope < 20f) { gentle = q; break; }
        }
        if (gentle.HasValue)
        {
            seeds.Add(gentle.Value);
            var p = Coast(gentle.Value, Normal(gentle.Value)) + Normal(gentle.Value) * 0.5f;
            clicks.Add(new Vector2(p.x, p.z));
        }
        Save("coast-experiment-config.json", new {Width, totalBand=Width*2, seaLevel=world.seaLevel, world.minimumLandClearance, world.coastalElevationBlendWidth,
            gridSpacing=world.terrainTileSize/(world.localHeightResolution-1), surfaceSettings=surface.GetBuildSettings(), fourthGentleFound=gentle.HasValue, minOriginalShoreSlope=minSlope,
            rules="Same fixed clicked XZ coordinates. Patch existing runtime heights only. Freeze existing streaming coroutine. Existing surface.UpdateNavMesh; no source/asset save."});
        phase = 0;
        BeginProbe("coast-A-original-voxel4");
        tick = Update;
        EditorApplication.update += tick;
        return new {started=true, points=seeds.Count, band=Width*2};
    }

    static void BeginProbe(string nextLabel)
    {
        RemoveUnits();
        probes.Clear();
        label = nextLabel;
        for (int i = 0; i < seeds.Count; i++) AddProbe(i, false);
        // Independent test: is an order into water accepted and now connected?
        AddProbe(1, true);
        pathsCaptured = false;
        phaseStarted = EditorApplication.timeSinceStartup;
        diagnostics.benchmarkLabel = label;
        benchmarkStart.Invoke(diagnostics, null);
    }

    static void AddProbe(int i, bool water)
    {
        Vector3 n = Normal(seeds[i]);
        Vector3 coast = Coast(seeds[i], n);
        Vector3 requested = water ? Ground(coast - n * 20f, out _) : Ground(new Vector3(clicks[i].x, 0, clicks[i].y), out _);
        Ground(requested, out var ray);
        Vector3 tangent = new Vector3(n.z,0,-n.x);
        Vector3 start = Ground(coast + n * (water?70f:60f) + (water?tangent*16f:Vector3.zero), out _);
        if (!NavMesh.SamplePosition(start, out var startHit, 8f, NavMesh.AllAreas)) throw new InvalidOperationException("Start lost navigation: " + start);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/teste/Tanque.prefab");
        var go = Object.Instantiate(prefab, startHit.position + (water ? n * 10f : Vector3.zero), Quaternion.identity);
        go.name = "ControlledCoast_" + (water ? "water" : i.ToString());
        units.Add(go);
        var agent = go.GetComponent<NavMeshAgent>();
        agent.Warp(startHit.position);
        bool blocked = NavMesh.Raycast(startHit.position, coast, out var edge, agent.areaMask);
        bool found = NavMesh.SamplePosition(requested, out var sample, 8f, agent.areaMask);
        // Water regression tests deliberately keep the requested water point.
        Vector3 target = (!water && found) ? sample.position : requested;
        RegistroSuperficieMapa.TryClassify(requested, out var classification, out _);
        bool accepted = go.GetComponent<ControleUnidade>().EmitirOrdemMover(target);
        probes.Add(new Probe {name=go.name, coast=V(coast), requested=V(requested), sampled=found?V(sample.position):null,
            start=V(startHit.position), edge=blocked?V(edge.position):null, navStopsBeforeCoast=blocked,
            edgeToWater=blocked?Vector2.Distance(new Vector2(edge.position.x,edge.position.z),new Vector2(coast.x,coast.z)):0f,
            slope=Vector3.Angle(ray.normal,Vector3.up), sampleFound=found, accepted=accepted, agentRadius=agent.radius, stoppingDistance=agent.stoppingDistance,
            classification=classification.ToString(), rayHit=ray.collider.name, layer=LayerMask.LayerToName(ray.collider.gameObject.layer), agent=agent, waterTest=water, waterHeight=requested.y});
    }

    static void CapturePaths(bool final)
    {
        foreach (var p in probes)
        {
            var a = p.agent;
            if (a == null || !a.enabled) continue;
            if (final)
            {
                p.finalPosition=V(a.transform.position); p.destination=V(a.destination); p.finalPathStatus=a.pathStatus.ToString();
                p.remainingDistance=a.remainingDistance; p.finalCorners=a.path.corners.Select(V).ToArray();
            }
            else { p.initialPathStatus=a.pathStatus.ToString(); p.initialCorners=a.path.corners.Select(V).ToArray(); }
        }
    }

    static void FinishProbe()
    {
        CapturePaths(true);
        benchmarkStop.Invoke(diagnostics, null);
        Save(label+".json", probes.ToArray());
        stages.Add(new {label,results=probes.ToArray(),voxel=surface.voxelSize});
    }

    static void ApplyPatch()
    {
        // Keep all distant heights/vegetation/assets untouched. Full candidate in
        // a 100 m radius around each tested coast; fade to old heights by 200 m.
        foreach (var q in seeds)
        foreach (var t in Terrain.activeTerrains.Where(t=>t.name.StartsWith("TerrainTile_")))
        {
            TerrainData data=t.terrainData;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(data))) throw new InvalidOperationException("Refusing to modify persistent TerrainData.");
            Vector3 origin=t.transform.position, size=data.size;
            const float radius=200f;
            if (q.x+radius<origin.x||q.x-radius>origin.x+size.x||q.y+radius<origin.z||q.y-radius>origin.z+size.z)continue;
            int resolution=data.heightmapResolution;
            int x0=Mathf.Clamp(Mathf.FloorToInt((q.x-radius-origin.x)/size.x*(resolution-1)),0,resolution-1);
            int x1=Mathf.Clamp(Mathf.CeilToInt((q.x+radius-origin.x)/size.x*(resolution-1)),0,resolution-1);
            int z0=Mathf.Clamp(Mathf.FloorToInt((q.y-radius-origin.z)/size.z*(resolution-1)),0,resolution-1);
            int z1=Mathf.Clamp(Mathf.CeilToInt((q.y+radius-origin.z)/size.z*(resolution-1)),0,resolution-1);
            var old=data.GetHeights(x0,z0,x1-x0+1,z1-z0+1);
            var next=(float[,])old.Clone();
            int changed=0;
            for(int z=0;z<next.GetLength(0);z++)for(int x=0;x<next.GetLength(1);x++)
            {
                float wx=origin.x+(x0+x)*size.x/(resolution-1),wz=origin.z+(z0+z)*size.z/(resolution-1);
                float distance=world.SignedCoastDistanceAtWorld(wx,wz);
                float r=Vector2.Distance(q,new Vector2(wx,wz));
                if(Mathf.Abs(distance)>=Width||r>=radius)continue;
                float fade=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(100f,radius,r));
                float candidate=(Candidate(wx,wz)-origin.y)/size.y;
                next[z,x]=Mathf.Lerp(old[z,x],candidate,fade);
                changed++;
            }
            if(changed==0)continue;
            patches.Add(new Patch {terrain=t,data=data,x=x0,z=z0,original=old});
            data.SetHeightsDelayLOD(x0,z0,next);
            data.SyncHeightmap();
            t.Flush();
        }
        Physics.SyncTransforms();
        Save("patches.json", patches.Select(p=>new {terrain=p.terrain.name,p.x,p.z,width=p.original.GetLength(1),height=p.original.GetLength(0),assetPath=AssetDatabase.GetAssetPath(p.data)}).ToArray());
    }

    static void BeginBake(float voxel)
    {
        RemoveUnits();
        surface.overrideVoxelSize=true;
        surface.voxelSize=voxel;
        nextNav=new NavMeshData(surface.agentTypeID);
        temporaryNav.Add(nextNav);
        memoryBefore=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        bakeStarted=EditorApplication.timeSinceStartup;
        var sw=Stopwatch.StartNew();
        operation=surface.UpdateNavMesh(nextNav);
        sw.Stop();
        bakeDispatchMs=sw.Elapsed.TotalMilliseconds;
        worstBakeFrame=0f;
        diagnostics.benchmarkLabel="coast-bake-voxel"+voxel;
        benchmarkStart.Invoke(diagnostics,null);
    }

    static void CommitBake()
    {
        surface.RemoveData();
        surface.navMeshData=nextNav;
        surface.AddData();
        var result=new {voxel=surface.voxelSize,elapsedMs=(EditorApplication.timeSinceStartup-bakeStarted)*1000,dispatchMainMs=bakeDispatchMs,
            unityMemoryBefore=memoryBefore,unityMemoryAfter=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),worstObservedFrameMs=worstBakeFrame,
            tiles=streamer.ActiveTileCount,area="existing loaded local surface; no world rebuild"};
        bakes.Add(result);
        benchmarkStop.Invoke(diagnostics,null);
        Save("bakes.json",bakes);
    }

    static void Update()
    {
        try
        {
            if(!Application.isPlaying){Restore();return;}
            previousFrameTime=Time.unscaledDeltaTime*1000f;
            int f=UnityEditorInternal.ProfilerDriver.lastFrameIndex;
            if(f>=0&&f!=lastProfilerFrame)
            {
                lastProfilerFrame=f;
                using(var v=UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(f,0))
                    if(v.valid&&v.frameTimeMs>100)profilerSpikes.Add(new {phase,frame=f,ms=v.frameTimeMs,
                        top=Enumerable.Range(0,v.sampleCount).Where(i=>v.GetSampleTimeMs(i)>5).OrderByDescending(i=>v.GetSampleTimeMs(i)).Take(25).Select(i=>new {name=v.GetSampleName(i),ms=v.GetSampleTimeMs(i)}).ToArray()});
            }
            if(phase==1||phase==3)
            {
                worstBakeFrame=Mathf.Max(worstBakeFrame,previousFrameTime);
                if(!operation.isDone)return;
                CommitBake();
                phase=phase==1?2:4;
                BeginProbe(phase==2?"coast-B-smooth-voxel4":"coast-C-smooth-voxel2");
                return;
            }
            if(!pathsCaptured&&EditorApplication.timeSinceStartup-phaseStarted>0.8){CapturePaths(false);pathsCaptured=true;}
            // Allow agents to reach the endpoint even after bake/render stalls.
            // A bounded 45 s window avoids labelling a 14 s intermediate pose final.
            if(EditorApplication.timeSinceStartup-phaseStarted<45)return;
            FinishProbe();
            if(phase==0){ApplyPatch();phase=1;BeginBake(4f);return;}
            bool submerged=probes.Any(p=>p.waterTest&&p.accepted&&p.initialPathStatus=="PathComplete");
            bool marginLarge=probes.Any(p=>!p.waterTest&&p.edgeToWater>3f);
            if(phase==2&&marginLarge&&!submerged){phase=3;BeginBake(2f);return;}
            Save("coast-conclusion.json",new {geometryOnlyCandidateAccepted=!submerged,waterRegression=submerged,remainingMarginRequiresVoxel=marginLarge,
                voxel2Tested=phase==4,reason=submerged?"Rejected: requested underwater destination accepted with complete path after removing coastal wall.":marginLarge?"Compare voxel results before any adoption.":"No exaggerated shore margin remaining; voxel2 unnecessary."});
            Save("coast-profiler-spikes.json",profilerSpikes);
            UnityEditorInternal.ProfilerDriver.SaveProfile(Path.Combine(Dir,"coast-profiler.data"));
            Restore();
        }
        catch(Exception ex)
        {
            Save("coast-error.json",new {exception=ex.ToString(),phase});
            Restore();
        }
    }

    static void RemoveUnits()
    {
        foreach(var go in units)if(go!=null){var agent=go.GetComponent<NavMeshAgent>();if(agent!=null)agent.enabled=false;Object.Destroy(go);}
        units.Clear();
    }

    public static object Restore()
    {
        if(restored)return "Already restored";
        restored=true;
        if(tick!=null)EditorApplication.update-=tick;
        if(diagnostics!=null&&benchmarkStop!=null)benchmarkStop.Invoke(diagnostics,null);
        RemoveUnits();
        for(int i=patches.Count-1;i>=0;i--){var p=patches[i];if(p.data!=null){p.data.SetHeightsDelayLOD(p.x,p.z,p.original);p.data.SyncHeightmap();if(p.terrain!=null)p.terrain.Flush();}}
        if(surface!=null){surface.RemoveData();surface.overrideVoxelSize=originalVoxelOverride;surface.voxelSize=originalVoxel;surface.navMeshData=originalNav;if(originalNav!=null)surface.AddData();}
        foreach(var data in temporaryNav)if(data!=null)Object.Destroy(data);
        if(cameraController!=null)cameraController.enabled=originalCameraEnabled;
        if(frozen&&streamer!=null&&Application.isPlaying)
        {
            var loop=(System.Collections.IEnumerator)typeof(GlobalTerrainStreamer).GetMethod("StreamContinuously",Private).Invoke(streamer,null);
            typeof(GlobalTerrainStreamer).GetField("streamingLoop",Private).SetValue(streamer,streamer.StartCoroutine(loop));
        }
        Save("coast-restored.json",new {restored=true,patches=patches.Count,navRestored=surface!=null&&surface.navMeshData==originalNav,voxelRestored=surface!=null&&surface.voxelSize==originalVoxel});
        return new {restored=true,patches=patches.Count};
    }
}
