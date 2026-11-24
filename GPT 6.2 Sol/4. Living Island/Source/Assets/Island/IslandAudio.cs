using System;
using UnityEngine;

public sealed class IslandAudio
{
    const int Rate=24000;
    readonly AudioSource ocean, wind, birds, crickets, rain, fire, step, thunder;
    readonly AudioClip oceanClip,windClip,birdClip,cricketClip,rainClip,fireClip,thunderClip;
    readonly AudioClip[] steps=new AudioClip[4];
    uint noise=260077;
    float low, body, flutter;
    float stepPitch, coastSampleDue, coastDistance;

    public IslandAudio(Transform root,Vector3 camp)
    {
        oceanClip=Make("Ocean wash",8,0);windClip=Make("Coastal air",7,1);
        birdClip=Make("Day birds",11,2);cricketClip=Make("Night insects",6,3);
        rainClip=Make("Rain bed",5,4);fireClip=Make("Wood crackle",5,5);
        thunderClip=Make("Thunder",5,6);
        steps[0]=Make("Sand footfall",.26f,7); steps[1]=Make("Grass footfall",.27f,8);
        steps[2]=Make("Rock footfall",.23f,9); steps[3]=Make("Water footfall",.33f,10);
        ocean=Source("Ocean wash",root,oceanClip);
        wind=Source("Coastal wind",root,windClip);
        birds=Source("Day birds",root,birdClip);
        crickets=Source("Night insects",root,cricketClip);
        rain=Source("Rain sound",root,rainClip);
        fire=Source("Positional campfire",root,fireClip);
        fire.spatialBlend=1;fire.minDistance=1.2f;fire.maxDistance=25f;fire.rolloffMode=AudioRolloffMode.Linear;
        fire.transform.position=camp;
        fire.dopplerLevel=0;
        step=Source("Footsteps",root,null);
        thunder=Source("Delayed thunder",root,null);
        thunder.priority=92;
    }

    static AudioSource Source(string name,Transform root,AudioClip clip)
    {
        var source=IslandGeometry.Root(name,root).gameObject.AddComponent<AudioSource>();
        source.playOnAwake=false;source.volume=0;source.loop=clip!=null;source.clip=clip;
        if(source.loop) source.Play();
        return source;
    }

    float Noise()
    {
        noise^=noise<<13;noise^=noise>>17;noise^=noise<<5;
        return (noise&0x00ffffff)/8388607.5f-1f;
    }

    AudioClip Make(string name,float seconds,int kind)
    {
        const float tau=2f*Mathf.PI;
        var data=new float[Mathf.RoundToInt(seconds*Rate)];
        low=body=flutter=0f;
        float crack=0f;
        for(int i=0;i<data.Length;i++)
        {
            float t=i/(float)Rate,u=t/seconds,raw=Noise();
            low+=(raw-low)*.0084f;
            body+=(raw-body)*.071f;
            flutter+=(body-flutter)*.034f;
            float sample;
            switch(kind)
            {
                case 0:
                    float tide=.38f+.62f*Mathf.Pow(.5f+.5f*Mathf.Sin(tau*(u*2+.14f)),2);
                    sample=(low*.61f+flutter*.40f+(raw-body)*.063f)*tide;
                    break;
                case 1:
                    float gust=.57f+.23f*Mathf.Sin(tau*u)+.16f*Mathf.Sin(tau*u*3f+1.7f);
                    sample=(low*1.11f+flutter*.22f)*gust;
                    break;
                case 2:
                    float location=Mathf.Repeat(t+3f,4.12f);
                    float trill=Mathf.Repeat(location,.23f);
                    float mask=Mathf.Pow(Mathf.Max(0f,Mathf.Sin(Mathf.PI*trill/.23f)),2f)*Mathf.Clamp01((.91f-location)*21f);
                    float freq=1740f+Mathf.Sin(location*22f)*270f;
                    sample=mask*(Mathf.Sin(tau*(trill*freq+trill*trill*1300))*.106f+body*.047f);
                    break;
                case 3:
                    float throb=.25f+.75f*Mathf.Pow(.5f+.5f*Mathf.Sin(t*tau*4.67f),4f);
                    sample=throb*(Mathf.Sin(tau*t*3642f)+.46f*Mathf.Sin(tau*t*4217f))*.036f*
                        (.46f+.54f*Mathf.Pow(.5f+.5f*Mathf.Sin(t*tau*1.19f),6f));
                    break;
                case 4:
                    if(raw>.9973f) crack=.17f;
                    crack*=.947f;
                    sample=(raw-body)*.095f+body*.35f+flutter*.51f+crack*raw;
                    break;
                case 5:
                    if(raw>.9992f) crack=.5f;
                    crack*=.944f;
                    sample=(raw*.026f+body*.088f)+crack*(.3f+raw*.7f);
                    break;
                case 6:
                    if(raw>.99972f) crack=.41f;
                    crack*=.9971f;
                    float shock=(low*1.28f+flutter*.98f+body*.41f+crack*raw)*
                        Mathf.Min(1,t*19f)*Mathf.Exp(-t*.63f);
                    sample=shock+Mathf.Sin(tau*(32f*t-1.71f*t*t))*.17f*Mathf.Min(1,t*18f)*Mathf.Exp(-t*1.18f);
                    break;
                default:
                    float envelope=Mathf.Min(1, t*95f)*Mathf.Exp(-u*(kind==10?3.8f:5.3f));
                    sample=kind switch {
                        7 => (raw*.25f+body*.63f+flutter*.82f)*envelope,
                        8 => (raw*.38f+(raw-body)*.16f*Mathf.Sin(t*169f)+flutter*.58f)*envelope,
                        9 => (body*.49f+Mathf.Sin(tau*(183*t-240*t*t))*.28f)*envelope,
                        _ => (flutter*1.20f+body*.73f+.25f*Mathf.Sin(tau*(461*t-520*t*t)))*envelope
                    };
                    break;
            }
            // Fade the seam on loops and suppress clicks on one-shots.
            float edge=Mathf.Min(1f,Mathf.Min(t,seconds-t)*48f);
            data[i]=Mathf.Clamp(sample*edge,-1,1);
        }
        var clip=AudioClip.Create(name,data.Length,1,Rate,false);
        clip.SetData(data,0);
        return clip;
    }

    public void SetCamp(Vector3 p) => fire.transform.position=p;

    public void Advance(float dt,IslandPlayer player,IslandTerrain land,IslandSkyWeather sky,bool underwater)
    {
        Vector3 p=player.Position;
        float y=land.Height(p.x,p.z);
        coastSampleDue-=dt;
        if(coastSampleDue<=0)
        {
            coastSampleDue=.14f;
            coastDistance=land.DistanceToCoast(p);
        }
        float coast=1-IslandTerrain.Smooth(2.2f,57f,coastDistance);
        float f=1-Mathf.Exp(-3.5f*dt);
        ocean.volume=Mathf.Lerp(ocean.volume,(.17f+.59f*coast)*(underwater?.45f:1f),f);
        wind.volume=Mathf.Lerp(wind.volume,(.24f+sky.Wind*.12f)*(underwater?.10f:1f),f);
        wind.pitch=1+sky.Wind*.105f;
        birds.volume=Mathf.Lerp(birds.volume,.59f*sky.Daylight*(1-sky.Rain)*(underwater?0:1),f);
        crickets.volume=Mathf.Lerp(crickets.volume,.61f*(1-sky.Daylight)*(1-sky.Rain)*(y>.2f?1:0),f);
        rain.volume=Mathf.Lerp(rain.volume,sky.RainAudio*(underwater?.25f:.78f),f);
        fire.volume=.84f;
        if(player.Footstep>0 || player.Splash)
        {
            int surface=y<.08f?3:y<=IslandConfig.BeachHeight?0:land.Slope(p.x,p.z)>.58f?2:1;
            stepPitch=Mathf.Repeat(stepPitch+.381966f,1f);
            step.pitch=.91f+stepPitch*.23f;
            step.volume=player.Splash?.31f:.62f;
            step.PlayOneShot(steps[surface]);
        }
        if(sky.Thunder>0)
        {
            thunder.volume=sky.Thunder;
            thunder.PlayOneShot(thunderClip);
        }
    }

    public void Pause(bool value)
    {
        foreach(var source in new[]{ocean,wind,birds,crickets,rain,fire,step,thunder})
        {
            if(value) source.Pause();
            else source.UnPause();
        }
    }

    public void Dispose()
    {
        foreach(var clip in steps) UnityEngine.Object.Destroy(clip);
        foreach(var clip in new[]{oceanClip,windClip,birdClip,cricketClip,rainClip,fireClip,thunderClip})
            UnityEngine.Object.Destroy(clip);
    }
}

