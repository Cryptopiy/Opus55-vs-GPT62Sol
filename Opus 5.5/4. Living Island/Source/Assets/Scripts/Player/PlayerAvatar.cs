using UnityEngine;

namespace LivingIsland
{
    /// <summary>
    /// Stylised humanoid built from engine primitives (capsules/spheres) with a fully procedural
    /// animation (I-25, I-26): walk/run cycles with swinging limbs, knee bend and body bob, a jump pose,
    /// a front-crawl swim stroke with flutter kick, and treading water when idle in deep water.
    /// </summary>
    public sealed class PlayerAvatar : MonoBehaviour
    {
        /// <summary>Raised when a foot touches the ground (footstep audio + FX).</summary>
        public System.Action<int> OnFootPlant;
        /// <summary>Raised when a hand enters the water during the swim stroke (splash FX).</summary>
        public System.Action<int> OnSwimStroke;

        PlayerController controller;
        Transform body, torso, head;
        Transform shoulderL, shoulderR, elbowL, elbowR, hipL, hipR, kneeL, kneeR;
        float phase, swimPhase, idleTime;
        float walkW, runW, airW, swimW, swimMoveW;
        int lastPlantHalf = -1, lastStrokeHalf = -1;

        public void Build(PlayerController owner, Material skin, Material shirt, Material shorts, Material hair)
        {
            controller = owner;
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            body.localPosition = new Vector3(0f, 0.95f, 0f);

            torso = Part(PrimitiveType.Capsule, body, new Vector3(0f, 0.3f, 0f), new Vector3(0.36f, 0.32f, 0.24f), shirt, "Torso");
            Part(PrimitiveType.Capsule, body, new Vector3(0f, 0.04f, 0f), new Vector3(0.32f, 0.14f, 0.22f), shorts, "Pelvis");
            head = Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.75f, 0f), new Vector3(0.24f, 0.27f, 0.25f), skin, "Head");
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.18f, -0.06f), new Vector3(1.02f, 0.72f, 1.02f), hair, "Hair");
            Part(PrimitiveType.Capsule, body, new Vector3(0f, 0.6f, 0f), new Vector3(0.09f, 0.06f, 0.09f), skin, "Neck");

            shoulderL = Joint(body, new Vector3(-0.22f, 0.52f, 0f));
            shoulderR = Joint(body, new Vector3(0.22f, 0.52f, 0f));
            elbowL = Limb(shoulderL, 0.29f, 0.11f, shirt, skin);
            elbowR = Limb(shoulderR, 0.29f, 0.11f, shirt, skin);
            Limb(elbowL, 0.27f, 0.09f, skin, skin, true);
            Limb(elbowR, 0.27f, 0.09f, skin, skin, true);

            hipL = Joint(body, new Vector3(-0.1f, 0f, 0f));
            hipR = Joint(body, new Vector3(0.1f, 0f, 0f));
            kneeL = Limb(hipL, 0.45f, 0.15f, shorts, skin);
            kneeR = Limb(hipR, 0.45f, 0.15f, shorts, skin);
            Foot(Limb(kneeL, 0.42f, 0.11f, skin, skin), skin);
            Foot(Limb(kneeR, 0.42f, 0.11f, skin, skin), skin);
        }

        static Transform Joint(Transform parent, Vector3 localPos)
        {
            var t = new GameObject("Joint").transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, string name)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // Visual only (the CharacterController collides): disable now so it never touches the controller.
            var col = go.GetComponent<Collider>();
            col.enabled = false;
            Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go.transform;
        }

        /// <summary>A limb segment hanging down from a joint; returns the joint at its far end.</summary>
        static Transform Limb(Transform joint, float length, float thickness, Material mat, Material endMat, bool hand = false)
        {
            Part(PrimitiveType.Capsule, joint, new Vector3(0f, -length * 0.5f, 0f), new Vector3(thickness, length * 0.5f + thickness * 0.25f, thickness), mat, "Segment");
            var end = Joint(joint, new Vector3(0f, -length, 0f));
            if (hand) Part(PrimitiveType.Sphere, end, new Vector3(0f, -0.04f, 0f), new Vector3(0.09f, 0.11f, 0.06f), endMat, "Hand");
            return end;
        }

        static void Foot(Transform ankle, Material mat)
        {
            Part(PrimitiveType.Capsule, ankle, new Vector3(0f, -0.03f, 0.06f), new Vector3(0.1f, 0.12f, 0.1f), mat, "Foot")
                .localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        void LateUpdate()
        {
            if (controller == null) return;
            float dt = Time.deltaTime;
            float speed = controller.HorizontalSpeed;
            bool swimming = controller.IsSwimming;
            bool airborne = !swimming && !controller.IsGrounded && Mathf.Abs(controller.Velocity.y) > 1.5f;

            // Smooth state weights so poses blend instead of snapping.
            float k = 1f - Mathf.Exp(-dt * 8f);
            walkW = Mathf.Lerp(walkW, !swimming && !airborne ? Mathf.Clamp01(speed / GameConfig.WalkSpeed) : 0f, k);
            runW = Mathf.Lerp(runW, !swimming && !airborne ? Mathf.Clamp01((speed - GameConfig.WalkSpeed) / (GameConfig.RunSpeed - GameConfig.WalkSpeed)) : 0f, k);
            airW = Mathf.Lerp(airW, airborne ? 1f : 0f, k);
            swimW = Mathf.Lerp(swimW, swimming ? 1f : 0f, 1f - Mathf.Exp(-dt * 4f));
            swimMoveW = Mathf.Lerp(swimMoveW, swimming ? Mathf.Clamp01(speed / GameConfig.SwimSpeed) : 0f, k);

            // Cycle phase advances with distance travelled (stride ~1.4 m walking, ~2.2 m running).
            float stride = Mathf.Lerp(1.4f, 2.2f, runW);
            phase = Mathf.Repeat(phase + speed * dt / stride, 1f);
            swimPhase = Mathf.Repeat(swimPhase + dt * Mathf.Lerp(0.45f, 0.75f, swimMoveW), 1f);
            idleTime += dt;

            float a = phase * Mathf.PI * 2f;
            float swing = Mathf.Sin(a);
            float legAmp = Mathf.Lerp(24f, 44f, runW) * walkW;
            float armAmp = Mathf.Lerp(20f, 38f, runW) * walkW;

            // ---- land locomotion
            float hipLAng = swing * legAmp - airW * 35f;
            float hipRAng = -swing * legAmp - airW * 20f;
            float kneeLAng = Mathf.Max(0f, -Mathf.Cos(a)) * Mathf.Lerp(35f, 85f, runW) * walkW + airW * 55f;
            float kneeRAng = Mathf.Max(0f, Mathf.Cos(a)) * Mathf.Lerp(35f, 85f, runW) * walkW + airW * 40f;
            float shLAng = -swing * armAmp - airW * 25f;
            float shRAng = swing * armAmp - airW * 25f;
            float elbowAng = Mathf.Lerp(10f, 75f, runW) * walkW + 8f;
            float bob = Mathf.Abs(Mathf.Cos(a)) * Mathf.Lerp(0.035f, 0.075f, runW) * walkW;
            float lean = runW * 9f + walkW * 2f;
            float breathe = Mathf.Sin(idleTime * 1.6f) * 0.006f * (1f - walkW);

            // ---- swimming: crawl stroke when moving, treading water when idle
            float s = swimPhase * Mathf.PI * 2f;
            float crawlL = swimPhase * 360f, crawlR = Mathf.Repeat(swimPhase + 0.5f, 1f) * 360f;
            float kick = Mathf.Sin(s * 4f) * 18f;
            float treadArm = Mathf.Sin(s * 2f) * 25f;

            float bodyPitch = Mathf.Lerp(lean, 72f * swimMoveW + 10f * (1f - swimMoveW), swimW);
            body.localPosition = new Vector3(0f, 0.95f + bob + breathe - swimW * 0.05f, 0f);
            body.localRotation = Quaternion.Euler(bodyPitch, 0f, Mathf.Sin(s) * 6f * swimW * swimMoveW);
            torso.localRotation = Quaternion.Euler(0f, swing * 6f * walkW * (1f - swimW), 0f);

            float swimArmL = Mathf.Lerp(treadArm + 40f, -crawlL, swimMoveW);
            float swimArmR = Mathf.Lerp(-treadArm + 40f, -crawlR, swimMoveW);
            shoulderL.localRotation = Quaternion.Euler(Mathf.Lerp(shLAng, swimArmL, swimW), 0f, Mathf.Lerp(-6f, -15f * (1f - swimMoveW), swimW));
            shoulderR.localRotation = Quaternion.Euler(Mathf.Lerp(shRAng, swimArmR, swimW), 0f, Mathf.Lerp(6f, 15f * (1f - swimMoveW), swimW));
            elbowL.localRotation = Quaternion.Euler(-Mathf.Lerp(elbowAng, 20f, swimW), 0f, 0f);
            elbowR.localRotation = Quaternion.Euler(-Mathf.Lerp(elbowAng, 20f, swimW), 0f, 0f);
            hipL.localRotation = Quaternion.Euler(Mathf.Lerp(-hipLAng, kick * (0.5f + swimMoveW), swimW), 0f, 0f);
            hipR.localRotation = Quaternion.Euler(Mathf.Lerp(-hipRAng, -kick * (0.5f + swimMoveW), swimW), 0f, 0f);
            kneeL.localRotation = Quaternion.Euler(Mathf.Lerp(kneeLAng, 12f + Mathf.Max(0f, kick) * 0.6f, swimW), 0f, 0f);
            kneeR.localRotation = Quaternion.Euler(Mathf.Lerp(kneeRAng, 12f + Mathf.Max(0f, -kick) * 0.6f, swimW), 0f, 0f);
            head.localRotation = Quaternion.Euler(-bodyPitch * 0.7f * swimW, Mathf.Sin(s) * 25f * swimW * swimMoveW, 0f);

            // ---- events: foot plants twice per cycle, hand entries twice per stroke cycle
            int half = phase < 0.5f ? 0 : 1;
            if (!swimming && controller.IsGrounded && speed > 0.4f && half != lastPlantHalf)
                OnFootPlant?.Invoke(half);
            lastPlantHalf = half;
            int strokeHalf = swimPhase < 0.5f ? 0 : 1;
            if (swimming && strokeHalf != lastStrokeHalf) OnSwimStroke?.Invoke(strokeHalf);
            lastStrokeHalf = strokeHalf;
        }
    }
}
