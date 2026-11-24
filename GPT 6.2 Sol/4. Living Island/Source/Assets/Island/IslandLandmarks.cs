using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class IslandLandmarks
{
    readonly Transform root, boat, beacon;
    readonly Light fire, lamp;
    readonly CapsuleCollider towerCollision;
    readonly Vector3 boatAnchor;
    readonly float boatKeel;
    readonly Mesh cone;
    readonly Material beam;
    readonly MaterialPropertyBlock beamProperties=new();
    readonly MeshRenderer beamRenderer;
    public Vector3 FirePosition { get; }
    public int PhysicsBodies => towerCollision.enabled && towerCollision.gameObject.activeInHierarchy ? 1 : 0;

    public IslandLandmarks(Transform parent,IslandTerrain ground,IslandModels models)
    {
        root=IslandGeometry.Root("Island landmarks",parent);
        FirePosition=ground.Camp;
        var tower=IslandModels.Create(models.lighthouse,"Clifftop lighthouse",root,ground.Lighthouse-Vector3.up*.035f,Quaternion.Euler(0,-32,0),Vector3.one);
        towerCollision=tower.gameObject.AddComponent<CapsuleCollider>();
        towerCollision.center=new Vector3(0,7.06f,0); towerCollision.radius=1.52f; towerCollision.height=14.12f;
        beacon=IslandGeometry.Root("Rotating lantern",tower);
        beacon.localPosition=new Vector3(0,IslandConfig.LighthouseHeight*.855f,0);
        lamp=beacon.gameObject.AddComponent<Light>();
        lamp.type=LightType.Spot; lamp.color=new Color(1f,.72f,.36f); lamp.spotAngle=12.5f; lamp.innerSpotAngle=5.6f;
        lamp.range=160f;lamp.shadows=LightShadows.Soft;
        lamp.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier=UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
        cone=BeamMesh();
        beam=IslandGeometry.Custom("Beacon");
        beamRenderer=IslandGeometry.Render("Coastal beam",beacon,cone,beam);
        beamRenderer.shadowCastingMode=ShadowCastingMode.Off;
        beamRenderer.receiveShadows=false;

        boatAnchor=ground.Boat;
        var boatRoot=IslandGeometry.Root("Tidal boat pivot",root);
        boat=IslandModels.Create(models.rowboat,"Rowboat with two oars",boatRoot,boatAnchor+Vector3.up*.21f,Quaternion.Euler(0,31,0),Vector3.one);
        boatKeel=boat.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.min.y;

        var logs=IslandModels.Create(models.driftwood,"Campfire firewood",root,ground.Camp,Quaternion.identity,Vector3.one);
        IslandModels.Part(logs.gameObject,"SM_Driftwood").SetActive(false);
        for(int i=0;i<IslandConfig.LogSeats;i++)
        {
            float angle=(i*2*Mathf.PI/IslandConfig.LogSeats)+.33f;
            Vector3 p=ground.Camp+new Vector3(Mathf.Cos(angle)*2.02f,0,Mathf.Sin(angle)*2.02f);
            p.y=ground.Height(p.x,p.z)-.025f;
            var seat=IslandModels.Create(models.driftwood,"Driftwood seat",root,p,Quaternion.Euler(0,90-angle*Mathf.Rad2Deg,0),Vector3.one*.77f);
            IslandModels.Part(seat.gameObject,"SM_CampfireLogs").SetActive(false);
        }
        for(int i=0;i<12;i++)
        {
            float a=i*2*Mathf.PI/12;
            Vector3 p=ground.Camp+new Vector3(Mathf.Cos(a)*.71f,0,Mathf.Sin(a)*.71f);
            p.y=ground.Height(p.x,p.z)-.051f;
            IslandModels.Create(models.rocks[i%models.rocks.Length],"Hearth stone",root,p,Quaternion.Euler(0,i*23f,0),
                new Vector3(.23f+(i%3)*.045f,.19f,.28f));
        }

        fire=IslandGeometry.Root("Shadowed firelight",root).gameObject.AddComponent<Light>();
        fire.transform.position=ground.Camp+Vector3.up*1.06f;
        fire.type=LightType.Point;fire.color=new Color(1,.39f,.102f);fire.range=16f;
        fire.shadows=LightShadows.Soft;fire.shadowStrength=.64f;fire.shadowBias=.35f;
        fire.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier=UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
    }

    public void Advance(IslandSkyWeather sky)
    {
        float dark=1-IslandTerrain.Smooth(.026f,.39f,sky.Daylight);
        float fog=IslandTerrain.Smooth(.007f,.046f,sky.Density);
        float visible=Mathf.Max(dark,fog*.86f);
        float angle=Mathf.Repeat(sky.Clock/IslandConfig.BeaconPeriod,1f)*360f;
        beacon.localRotation=Quaternion.Euler(1.4f,angle,0);
        beamRenderer.enabled=visible>.015f;
        lamp.enabled=beamRenderer.enabled;
        lamp.intensity=visible*7.1f;
        beamProperties.SetFloat("_Strength",visible*(.17f+fog*.24f));
        beamRenderer.SetPropertyBlock(beamProperties);
        float t=sky.Clock;
        fire.intensity=(2.4f+Mathf.PerlinNoise(t*5.8f,.73f)*2.6f+Mathf.Sin(t*14.9f)*.43f)*(1+dark*.75f);
        float wave=IslandOcean.Elevation(boatAnchor,t,sky.Wave);
        float resting=boatAnchor.y-boatKeel+.03f;
        float afloat=wave-boatKeel-.12f;
        float afloatBlend=IslandTerrain.Smooth(0,.19f,afloat-resting);
        boat.position=new Vector3(boatAnchor.x,Mathf.Max(resting,afloat),boatAnchor.z);
        boat.rotation=Quaternion.Euler(afloatBlend*Mathf.Sin(t*1.58f)*1.8f,31f,afloatBlend*Mathf.Cos(t*1.29f)*2.16f);
    }

    static Mesh BeamMesh()
    {
        const int side=32;
        var v=new List<Vector3>(); var tri=new List<int>(); var uv=new List<Vector2>();
        for(int ring=0;ring<2;ring++)
        for(int i=0;i<=side;i++)
        {
            float a=2*Mathf.PI*i/side,r=ring==0?.25f:12.5f;
            v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,ring==0?.43f:144f));
            uv.Add(new Vector2(i/(float)side,ring));
        }
        for(int i=0;i<side;i++)
        {
            int j=i+side+1;
            tri.Add(i);tri.Add(i+1);tri.Add(j);
            tri.Add(i+1);tri.Add(j+1);tri.Add(j);
        }
        return IslandGeometry.Mesh("Open beacon cone",v,tri,uv);
    }

    public void Dispose()
    {
        Object.Destroy(root.gameObject);Object.Destroy(cone);Object.Destroy(beam);
    }
}

