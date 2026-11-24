using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WaveSurvival
{
    /// <summary>
    /// Primitive-built humanoid rig. Joints are unscaled transforms that carry the hit colliders (so decals parented
    /// to them never skew); visuals are scaled child meshes. Keeps bind poses for pooling resets.
    /// </summary>
    public sealed class EnemyBody
    {
        public Transform Model, Hips, Spine, Head, ArmL, ForeL, ArmR, ForeR, LegL, ShinL, LegR, ShinR;
        public Transform Muzzle;            // projectile origin (shooter rifle / boss arm cannon)
        public EnemyMaterials Mats;
        public float HeadHeight;            // model-space height of the head centre (before Model scale)
        public float Scale = 1f;

        public readonly List<Renderer> Renderers = new List<Renderer>();
        public readonly List<Material> BaseMaterials = new List<Material>();
        public readonly List<Color> BaseColors = new List<Color>();
        public readonly List<Color> BaseEmission = new List<Color>();
        public readonly List<bool> TintMask = new List<bool>();   // renderers that receive the red charge/enrage glow
        public readonly List<Collider> Hitboxes = new List<Collider>();
        readonly List<Transform> joints = new List<Transform>();
        readonly List<Vector3> bindPos = new List<Vector3>();
        readonly List<Quaternion> bindRot = new List<Quaternion>();

        public void CaptureBindPose()
        {
            joints.Clear(); bindPos.Clear(); bindRot.Clear();
            Transform[] list = { Model, Hips, Spine, Head, ArmL, ForeL, ArmR, ForeR, LegL, ShinL, LegR, ShinR };
            foreach (Transform t in list)
            {
                joints.Add(t);
                bindPos.Add(t.localPosition);
                bindRot.Add(t.localRotation);
            }
        }

        public void ResetPose()
        {
            for (int i = 0; i < joints.Count; i++)
            {
                joints[i].localPosition = bindPos[i];
                joints[i].localRotation = bindRot[i];
            }
        }

        public Quaternion Bind(Transform t)
        {
            int i = joints.IndexOf(t);
            return i >= 0 ? bindRot[i] : Quaternion.identity;
        }

        public Vector3 BindPosition(Transform t)
        {
            int i = joints.IndexOf(t);
            return i >= 0 ? bindPos[i] : Vector3.zero;
        }

        public void SetHitboxesEnabled(bool on)
        {
            for (int i = 0; i < Hitboxes.Count; i++) Hitboxes[i].enabled = on;
        }

        public void RestoreMaterials()
        {
            for (int i = 0; i < Renderers.Count; i++)
            {
                Renderers[i].sharedMaterial = BaseMaterials[i];
                Renderers[i].SetPropertyBlock(null);
                Renderers[i].shadowCastingMode = ShadowCastingMode.On;
            }
        }
    }

    /// <summary>Helpers to assemble an <see cref="EnemyBody"/> from primitives.</summary>
    public sealed class BodyBuilder
    {
        readonly EnemyBody body;
        readonly EnemyBase owner;

        public BodyBuilder(EnemyBody b, EnemyBase o)
        {
            body = b;
            owner = o;
        }

        public Transform Joint(Transform parent, string name, Vector3 pos, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.layer = GameConfig.Layers.Hitbox;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go.transform;
        }

        /// <summary>Visual-only scaled primitive under a joint.</summary>
        public Renderer Mesh(Transform joint, Mesh mesh, Material mat, Vector3 pos, Vector3 size, Vector3 euler = default, bool tint = true)
        {
            var go = new GameObject(mesh.name);
            go.layer = GameConfig.Layers.Hitbox;
            go.transform.SetParent(joint, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            body.Renderers.Add(mr);
            body.BaseMaterials.Add(mat);
            body.BaseColors.Add(mat.GetColor(MaterialFactory.BaseColor));
            body.BaseEmission.Add(mat.GetColor(MaterialFactory.EmissionColor));
            body.TintMask.Add(tint && mat != body.Mats.Glow);
            return mr;
        }

        public Renderer Box(Transform j, Material m, Vector3 pos, Vector3 size, Vector3 euler = default) => Mesh(j, GameAssets.Cube, m, pos, size, euler);
        public Renderer Ball(Transform j, Material m, Vector3 pos, float d) => Mesh(j, GameAssets.Sphere, m, pos, Vector3.one * d);
        /// <summary>Capsule hanging down from the joint (limb segment).</summary>
        public Renderer Limb(Transform j, Material m, float length, float thickness) =>
            Mesh(j, GameAssets.Capsule, m, new Vector3(0f, -length * 0.5f, 0f), new Vector3(thickness, length * 0.5f, thickness));

        public void HitBox(Transform joint, HitZone zone, Vector3 center, Vector3 size, SurfaceKind surface = SurfaceKind.Flesh)
        {
            var c = joint.gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
            Register(joint, c, zone, surface);
        }

        public void HitSphere(Transform joint, HitZone zone, Vector3 center, float radius)
        {
            var c = joint.gameObject.AddComponent<SphereCollider>();
            c.center = center;
            c.radius = radius;
            Register(joint, c, zone, SurfaceKind.Flesh);
        }

        public void HitLimb(Transform joint, float length, float radius)
        {
            var c = joint.gameObject.AddComponent<CapsuleCollider>();
            c.direction = 1;
            c.center = new Vector3(0f, -length * 0.5f, 0f);
            c.height = length + radius * 2f;
            c.radius = radius;
            Register(joint, c, HitZone.Limb, SurfaceKind.Flesh);
        }

        void Register(Transform joint, Collider c, HitZone zone, SurfaceKind surface)
        {
            // One Hitbox component per joint describes all its colliders; the first registration wins.
            if (!joint.TryGetComponent(out Hitbox hb))
            {
                hb = joint.gameObject.AddComponent<Hitbox>();
                hb.Owner = owner;
                hb.Zone = zone;
                hb.Surface = surface;
            }
            body.Hitboxes.Add(c);
        }

        public struct Spec
        {
            public float HipHeight, HipWidth, Thigh, Shin, LegThick;
            public float Torso, TorsoW, TorsoD, Shoulder, UpperArm, ForeArm, ArmThick, Head, Neck, Hunch;
        }

        /// <summary>Creates the shared skeleton (joints + basic flesh hitboxes) and returns it for type-specific dressing.</summary>
        public void Skeleton(Transform model, Spec s)
        {
            body.Model = model;
            body.Hips = Joint(model, "Hips", new Vector3(0f, s.HipHeight, 0f));
            body.Spine = Joint(body.Hips, "Spine", new Vector3(0f, 0.04f, 0f), new Vector3(s.Hunch, 0f, 0f));
            body.Head = Joint(body.Spine, "Head", new Vector3(0f, s.Torso + s.Neck + s.Head * 0.5f, 0.02f), new Vector3(-s.Hunch * 0.6f, 0f, 0f));
            float sy = s.Torso - 0.06f;
            body.ArmL = Joint(body.Spine, "ArmL", new Vector3(-s.Shoulder * 0.5f, sy, 0f), new Vector3(-s.Hunch * 0.5f, 0f, 0f));
            body.ArmR = Joint(body.Spine, "ArmR", new Vector3(s.Shoulder * 0.5f, sy, 0f), new Vector3(-s.Hunch * 0.5f, 0f, 0f));
            body.ForeL = Joint(body.ArmL, "ForeL", new Vector3(0f, -s.UpperArm, 0f), new Vector3(-15f, 0f, 0f));
            body.ForeR = Joint(body.ArmR, "ForeR", new Vector3(0f, -s.UpperArm, 0f), new Vector3(-15f, 0f, 0f));
            body.LegL = Joint(body.Hips, "LegL", new Vector3(-s.HipWidth * 0.5f, 0f, 0f));
            body.LegR = Joint(body.Hips, "LegR", new Vector3(s.HipWidth * 0.5f, 0f, 0f));
            body.ShinL = Joint(body.LegL, "ShinL", new Vector3(0f, -s.Thigh, 0f));
            body.ShinR = Joint(body.LegR, "ShinR", new Vector3(0f, -s.Thigh, 0f));
            body.HeadHeight = s.HipHeight + s.Torso + s.Neck + s.Head * 0.5f;

            HitBox(body.Spine, HitZone.Body, new Vector3(0f, s.Torso * 0.5f, 0f), new Vector3(s.TorsoW, s.Torso + 0.08f, s.TorsoD));
            HitSphere(body.Head, HitZone.Head, Vector3.zero, s.Head * 0.55f);
            HitLimb(body.ArmL, s.UpperArm, s.ArmThick * 0.55f);
            HitLimb(body.ArmR, s.UpperArm, s.ArmThick * 0.55f);
            HitLimb(body.ForeL, s.ForeArm, s.ArmThick * 0.5f);
            HitLimb(body.ForeR, s.ForeArm, s.ArmThick * 0.5f);
            HitLimb(body.LegL, s.Thigh, s.LegThick * 0.55f);
            HitLimb(body.LegR, s.Thigh, s.LegThick * 0.55f);
            HitLimb(body.ShinL, s.Shin, s.LegThick * 0.48f);
            HitLimb(body.ShinR, s.Shin, s.LegThick * 0.48f);
        }
    }
}
