using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class IslandPlayer
{
    public readonly Transform Body;
    public readonly CharacterController Controller;
    public readonly Camera Camera;
    readonly Transform figure, leftLeg, rightLeg, leftArm, rightArm, pack;
    readonly Material[] outfit;
    float yaw = IslandConfig.StartHeading, pitch = 7.5f, vertical, gait, steps, swimSplash;
    float photoYaw, photoPitch;
    public bool Swimming { get; private set; }
    public float Footstep { get; private set; }
    public bool Splash { get; private set; }
    public Vector3 Position => Body.position;
    public float Yaw => yaw;
    public Vector3 Velocity => Controller.velocity;

    public IslandPlayer(Transform parent, Vector3 spawn)
    {
        Body = IslandGeometry.Root("Island explorer",parent);
        Body.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        Body.position = spawn + Vector3.up*.045f;
        Controller = Body.gameObject.AddComponent<CharacterController>();
        Controller.height = IslandConfig.PlayerHeight;
        Controller.radius = .29f;
        Controller.center = Vector3.up * (Controller.height*.5f);
        Controller.stepOffset = .27f;
        Controller.minMoveDistance = 0f;
        Controller.slopeLimit = 53f;
        Controller.skinWidth = .035f;

        var cloth = IslandGeometry.Lit("Sea-green field jacket",new Color(.16f,.44f,.41f));
        var canvas = IslandGeometry.Lit("Linen",new Color(.86f,.77f,.57f));
        var pants = IslandGeometry.Lit("Indigo trousers",new Color(.12f,.19f,.23f));
        var leather = IslandGeometry.Lit("Dark leather",new Color(.21f,.12f,.085f));
        var bag = IslandGeometry.Lit("Ochre rucksack",new Color(.73f,.34f,.135f));
        var skin = IslandGeometry.Lit("Warm skin",new Color(.67f,.40f,.25f));
        outfit = new[]{cloth,canvas,pants,leather,bag,skin};
        figure = IslandGeometry.Root("Jointed explorer",Body);
        figure.rotation = Quaternion.Euler(0f,yaw,0f);
        Part("Jacket",figure,cloth,new(0,.99f,0),new(.45f,.31f,.31f));
        Part("Collar",figure,canvas,new(0,1.244f,.01f),new(.21f,.065f,.22f));
        Part("Head",figure,skin,new(0,1.43f,.025f),new(.257f,.17f,.255f));
        var brim = IslandGeometry.Part("Sun hat brim",figure,PrimitiveType.Cylinder,canvas,new(0,1.555f,.02f),new(.47f,.019f,.42f));
        brim.localRotation = Quaternion.Euler(-6,0,0);
        Part("Hat crown",figure,canvas,new(0,1.653f,.017f),new(.284f,.104f,.277f));
        Part("Hat band",figure,cloth,new(0,1.591f,.02f),new(.305f,.026f,.286f));
        pack = Part("Rucksack",figure,bag,new(0,1.03f,-.209f),new(.34f,.23f,.22f));
        Part("Bedroll",figure,canvas,new(0,1.24f,-.236f),new(.137f,.24f,.139f)).localRotation=Quaternion.Euler(0,0,90);
        foreach(float x in new[]{-.127f,.127f})
            Part("Rucksack strap",figure,leather,new(x,1.085f,.143f),new(.05f,.20f,.052f));
        leftLeg = Limb("Left leg",-.118f,.721f,.37f,.155f,pants,leather,true);
        rightLeg = Limb("Right leg",.118f,.721f,.37f,.155f,pants,leather,true);
        leftArm = Limb("Left arm",-.249f,1.188f,.34f,.126f,cloth,skin,false);
        rightArm = Limb("Right arm",.249f,1.188f,.34f,.126f,cloth,skin,false);

        var t = IslandGeometry.Root("Third-person camera",parent);
        Camera = t.gameObject.AddComponent<Camera>();
        Camera.tag = "MainCamera";
        Camera.clearFlags = CameraClearFlags.Skybox;
        Camera.fieldOfView = IslandConfig.DefaultFov;
        Camera.nearClipPlane = .075f;
        Camera.farClipPlane = 1600f;
        Camera.allowHDR = true;
        var data = Camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        t.gameObject.AddComponent<AudioListener>();
        Follow(0f,true);
    }

    static Transform Part(string name, Transform parent, Material mat, Vector3 pos, Vector3 scale) =>
        IslandGeometry.Part(name,parent,PrimitiveType.Capsule,mat,pos,scale);

    Transform Limb(string name,float x,float y,float length,float width,Material upper,Material end,bool leg)
    {
        var joint = IslandGeometry.Root(name,figure);
        joint.localPosition=new Vector3(x,y,0);
        Part("Limb",joint,upper,new(0,-length*.64f,0),new(width,length*(leg?.81f:.70f),width));
        Part(leg ? "Boot" : "Hand",joint,end,new(0,-length*(leg?1.68f:1.38f),leg?.041f:0),new(width,leg?.093f:.082f,leg?.234f:.093f));
        return joint;
    }

    public void Teleport(Vector3 point, float heading)
    {
        Controller.enabled = false;
        Body.position = point;
        Controller.enabled = true;
        yaw = heading;
        vertical = 0f;
        figure.rotation = Quaternion.Euler(0f,yaw,0f);
        Follow(0f,true);
    }

    public void Step(float dt, Vector2 input, Vector2 look, bool sprint, bool jump, IslandTerrain terrain, float wave)
    {
        Footstep = 0f;
        Splash = false;
        yaw += look.x*.15f;
        pitch = Mathf.Clamp(pitch-look.y*.14f,-40f,73f);
        Vector3 p = Body.position;
        float height = terrain.Height(p.x,p.z);
        Swimming = IslandConfig.SeaLevel - height > IslandConfig.SwimDepth ;
        Vector3 move = Quaternion.Euler(0,yaw,0) * Vector3.ClampMagnitude(new Vector3(input.x,0,input.y),1f);
        float speed = Swimming ? IslandConfig.Swim : sprint ? IslandConfig.Run : IslandConfig.Walk;
        if (Swimming)
        {
            // Critically damped attraction keeps the swimmer near the moving surface.
            vertical = Mathf.Lerp(vertical,(-.63f + wave*.48f + Mathf.Sin(Time.time*3f)*.035f - p.y)*5f,1f-Mathf.Exp(-7f*dt));
        }
        else
        {
            if (Controller.isGrounded && vertical < 0f)
                vertical = jump ? Mathf.Sqrt(2f * 19.6f * IslandConfig.JumpHeight) : -2f;
            vertical -= 19.6f*dt;
        }

        Vector3 next = p + (move*speed + Vector3.up*vertical)*dt;
        float border = IslandConfig.WorldSize*.5f - Controller.radius;
        next.x = Mathf.Clamp(next.x,-border,border);
        next.z = Mathf.Clamp(next.z,-border,border);
        Controller.Move(next - p);

        float stride = move.magnitude;
        if (stride > .01f)
        {
            var heading = Quaternion.LookRotation(move);
            figure.rotation = Quaternion.Slerp(figure.rotation,heading,1f-Mathf.Exp(-12f*dt));
        }
        float rate = Swimming ? 1.2f : Mathf.Lerp(1.55f,2.32f,Mathf.InverseLerp(IslandConfig.Walk,IslandConfig.Run,speed));
        gait += dt*rate * (Swimming ? Mathf.Max(.24f,stride) : stride);
        float cycle = Mathf.Sin(gait*Mathf.PI*2f);
        float strength = Swimming ? Mathf.Max(.42f,stride) : Controller.isGrounded ? stride : .28f;
        float swing = cycle*strength;
        figure.localPosition = Vector3.up * (Swimming ? .36f : Mathf.Abs(swing)*.023f-.022f);
        Vector3 forward = figure.forward;
        float headingYaw = Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
        figure.rotation = Quaternion.Euler(Swimming ? 62f : 0f,headingYaw,0f);
        leftArm.localRotation = Quaternion.Euler(Swimming?-48f+swing*89f:swing*38f,0,-7f);
        rightArm.localRotation = Quaternion.Euler(Swimming?-48f-swing*89f:-swing*38f,0,7f);
        leftLeg.localRotation = Quaternion.Euler(-swing*(Swimming?21f:34f),0,0);
        rightLeg.localRotation = Quaternion.Euler(swing*(Swimming?21f:34f),0,0);
        pack.localRotation = Quaternion.Euler(swing*4f,0,0);

        if (Swimming)
        {
            swimSplash += dt * Mathf.Max(.4f,stride) * 6f;
            if (swimSplash >= 1f) { swimSplash -= 1f; Splash = true; }
        }
        else if (Controller.isGrounded)
        {
            steps += dt*speed*stride;
            if (steps >= (sprint ? 1.7f : 1.12f)) { steps = 0f; Footstep = speed; }
        }
    }

    public void Follow(float dt, bool immediate = false)
    {
        Vector3 pivot = Body.position + Vector3.up*(Swimming ? 1.18f : 1.57f);
        Quaternion orbit = Quaternion.Euler(pitch,yaw,0);
        Vector3 back = -(orbit * Vector3.forward);
        float arm = IslandConfig.CameraArm;
        if (Physics.SphereCast(pivot,.22f,back,out RaycastHit hit,arm,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            arm = Mathf.Max(.19f,hit.distance-.10f);
        Vector3 goal = pivot + back * arm;
        float blend = immediate ? 1f : 1f-Mathf.Exp(-15f*dt);
        Vector3 smooth = Vector3.Lerp(Camera.transform.position,goal,blend);
        // Check the smoothed position too so a fast orbit cannot cross the terrain.
        Vector3 line = smooth-pivot;
        if (line.sqrMagnitude > .04f && Physics.SphereCast(pivot,.18f,line.normalized,out hit,line.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            smooth = pivot + line.normalized*Mathf.Max(.15f,hit.distance-.09f);
        Camera.transform.SetPositionAndRotation(smooth,orbit);
    }

    public void BeginPhoto()
    {
        photoYaw = Camera.transform.eulerAngles.y;
        photoPitch = Mathf.DeltaAngle(0f,Camera.transform.eulerAngles.x);
    }

    public void Dispose()
    {
        foreach(Material material in outfit) Object.Destroy(material);
    }

    public void Fly(float dt, Vector2 movement, Vector2 look, float lift, bool fast)
    {
        photoYaw += look.x*.15f;
        photoPitch = Mathf.Clamp(photoPitch-look.y*.14f,-88f,88f);
        Camera.transform.rotation = Quaternion.Euler(photoPitch,photoYaw,0f);
        Vector3 move = Camera.transform.rotation * new Vector3(movement.x,0f,movement.y) + Vector3.up*lift;
        Camera.transform.position += Vector3.ClampMagnitude(move,1f) * (fast ? 18f : 6f) * dt;
    }
}

