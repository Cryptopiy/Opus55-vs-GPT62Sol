using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class IslandPost
{
    readonly VolumeProfile profile;
    readonly Bloom bloom;
    readonly ColorAdjustments grade;
    readonly DepthOfField depth;
    public float Focus=9f, Aperture=4.6f, Exposure;
    public bool Photo;
    public IslandPost(Transform parent)
    {
        var volume=IslandGeometry.Root("Coastal grading · Global Volume",parent).gameObject.AddComponent<Volume>();
        volume.isGlobal=true;
        volume.priority=30;
        profile=ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name="Living Island day and night";
        volume.sharedProfile=profile;

        bloom=profile.Add<Bloom>(true);
        bloom.threshold.value=1.09f;
        bloom.intensity.value=.21f;
        bloom.scatter.value=.57f;
        bloom.highQualityFiltering.value=false;
        profile.Add<Tonemapping>(true).mode.value=TonemappingMode.ACES;
        grade=profile.Add<ColorAdjustments>(true);
        grade.contrast.value=7f;
        var vignette=profile.Add<Vignette>(true);
        vignette.intensity.value=.17f;
        vignette.smoothness.value=.64f;
        vignette.color.value=new Color(.046f,.060f,.085f);
        var grain=profile.Add<FilmGrain>(true);
        grain.intensity.value=.045f;
        grain.response.value=.65f;
        depth=profile.Add<DepthOfField>(true);
        depth.mode.value=DepthOfFieldMode.Bokeh;
        depth.focalLength.value=44f;
        depth.bladeCount.value=7;
        depth.active=false;
    }

    public void Update(float day)
    {
        grade.postExposure.value=Mathf.Lerp(-.15f,-.03f,day)+(Photo?Exposure:0);
        grade.colorFilter.value=Color.Lerp(new Color(.81f,.90f,1f),Color.white,day);
        grade.saturation.value=Mathf.Lerp(-7f,4f,day);
        bloom.intensity.value=Mathf.Lerp(.34f,.20f,day);
        depth.active=Photo;
        if(Photo)
        {
            depth.focusDistance.value=Focus;
            depth.aperture.value=Aperture;
        }
    }

    public void Dispose()
    {
        foreach(VolumeComponent component in profile.components) Object.Destroy(component);
        Object.Destroy(profile);
    }
}

