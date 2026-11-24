using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// The four enemy silhouettes (section 4: "humanoid models built from primitives, different silhouettes & colors"):
    /// Runner = lean hunched clawed sprinter (orange), Shooter = upright armoured trooper with rifle (blue/teal visor),
    /// Tank = massive armoured brute with a glowing back weak point, Warden = x2.2 armoured boss with arm cannon.
    /// </summary>
    public static class EnemyBodies
    {
        static Transform NewModel(Transform root, float scale)
        {
            var go = new GameObject("Model");
            go.layer = GameConfig.Layers.Hitbox;
            go.transform.SetParent(root, false);
            go.transform.localScale = Vector3.one * scale;
            return go.transform;
        }

        public static EnemyBody Runner(EnemyBase owner)
        {
            var body = new EnemyBody { Mats = GameAssets.Enemies[(int)EnemyType.Runner] };
            var b = new BodyBuilder(body, owner);
            EnemyMaterials m = body.Mats;
            var s = new BodyBuilder.Spec { HipHeight = 0.86f, HipWidth = 0.2f, Thigh = 0.43f, Shin = 0.43f, LegThick = 0.1f, Torso = 0.5f, TorsoW = 0.32f, TorsoD = 0.22f,
                Shoulder = 0.42f, UpperArm = 0.32f, ForeArm = 0.34f, ArmThick = 0.085f, Head = 0.2f, Neck = 0.06f, Hunch = 28f };
            b.Skeleton(NewModel(owner.transform, 1f), s);
            b.Box(body.Hips, m.Dark, new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0.14f, 0.18f));
            b.Box(body.Spine, m.Body, new Vector3(0f, 0.25f, 0f), new Vector3(0.3f, 0.44f, 0.19f));
            for (int i = 0; i < 3; i++) b.Box(body.Spine, m.Accent, new Vector3(0f, 0.14f + i * 0.13f, -0.11f), new Vector3(0.04f, 0.07f, 0.08f), new Vector3(-30f, 0f, 0f));
            b.Box(body.Head, m.Body, new Vector3(0f, 0f, 0.02f), new Vector3(0.17f, 0.2f, 0.24f));
            b.Box(body.Head, m.Dark, new Vector3(0f, -0.09f, 0.05f), new Vector3(0.15f, 0.05f, 0.17f));
            b.Ball(body.Head, m.Glow, new Vector3(-0.045f, 0.02f, 0.12f), 0.04f);
            b.Ball(body.Head, m.Glow, new Vector3(0.045f, 0.02f, 0.12f), 0.04f);
            b.Box(body.Head, m.Accent, new Vector3(-0.06f, 0.1f, -0.06f), new Vector3(0.03f, 0.03f, 0.15f), new Vector3(30f, -15f, 0f));
            b.Box(body.Head, m.Accent, new Vector3(0.06f, 0.1f, -0.06f), new Vector3(0.03f, 0.03f, 0.15f), new Vector3(30f, 15f, 0f));
            foreach (Transform arm in new[] { body.ArmL, body.ArmR })
            {
                b.Limb(arm, m.Body, s.UpperArm, s.ArmThick);
                b.Box(arm, m.Accent, new Vector3(0f, 0.02f, 0f), new Vector3(0.12f, 0.06f, 0.14f));
            }
            foreach (Transform fore in new[] { body.ForeL, body.ForeR })
            {
                b.Limb(fore, m.Body, s.ForeArm, s.ArmThick * 0.9f);
                for (int c = -1; c <= 1; c++) b.Box(fore, m.Accent, new Vector3(c * 0.025f, -s.ForeArm - 0.04f, 0.03f), new Vector3(0.015f, 0.11f, 0.015f), new Vector3(-20f, 0f, c * 12f));
            }
            DressLegs(b, body, s, m.Body, m.Dark, m.Accent, 0.09f);
            return Finish(body);
        }

        public static EnemyBody Shooter(EnemyBase owner)
        {
            var body = new EnemyBody { Mats = GameAssets.Enemies[(int)EnemyType.Shooter] };
            var b = new BodyBuilder(body, owner);
            EnemyMaterials m = body.Mats;
            var s = new BodyBuilder.Spec { HipHeight = 0.92f, HipWidth = 0.24f, Thigh = 0.45f, Shin = 0.45f, LegThick = 0.13f, Torso = 0.58f, TorsoW = 0.42f, TorsoD = 0.28f,
                Shoulder = 0.5f, UpperArm = 0.3f, ForeArm = 0.28f, ArmThick = 0.11f, Head = 0.26f, Neck = 0.05f, Hunch = 4f };
            b.Skeleton(NewModel(owner.transform, 1f), s);
            b.Box(body.Hips, m.Dark, Vector3.zero, new Vector3(0.38f, 0.14f, 0.24f));
            b.Box(body.Spine, m.Body, new Vector3(0f, 0.28f, 0f), new Vector3(0.42f, 0.5f, 0.26f));
            b.Box(body.Spine, m.Accent, new Vector3(0f, 0.38f, 0.14f), new Vector3(0.36f, 0.26f, 0.05f));
            b.Box(body.Spine, m.Dark, new Vector3(0f, 0.3f, -0.2f), new Vector3(0.3f, 0.38f, 0.16f));
            b.Box(body.Spine, m.Dark, new Vector3(0.1f, 0.62f, -0.24f), new Vector3(0.015f, 0.35f, 0.015f));
            b.Ball(body.Head, m.Body, Vector3.zero, 0.27f);
            b.Box(body.Head, m.Glow, new Vector3(0f, 0.01f, 0.12f), new Vector3(0.2f, 0.06f, 0.05f));
            b.Box(body.Head, m.Dark, new Vector3(0f, -0.08f, 0.09f), new Vector3(0.16f, 0.06f, 0.1f));
            foreach (Transform arm in new[] { body.ArmL, body.ArmR })
            {
                b.Limb(arm, m.Body, s.UpperArm, s.ArmThick);
                b.Box(arm, m.Accent, new Vector3(0f, 0.03f, 0f), new Vector3(0.16f, 0.08f, 0.18f));
            }
            foreach (Transform fore in new[] { body.ForeL, body.ForeR })
            {
                b.Limb(fore, m.Dark, s.ForeArm, s.ArmThick * 0.9f);
                b.Ball(fore, m.Dark, new Vector3(0f, -s.ForeArm - 0.02f, 0f), 0.09f);
            }
            // Rifle held in the right hand: its +Z follows the forearm's -Y so it points forward in the aim pose.
            Transform rifle = b.Joint(body.ForeR, "Rifle", new Vector3(0f, -s.ForeArm, 0.02f), new Vector3(90f, 0f, 0f));
            b.Box(rifle, m.Dark, new Vector3(0f, 0.02f, 0.12f), new Vector3(0.05f, 0.08f, 0.5f));
            b.Box(rifle, m.Dark, new Vector3(0f, 0.03f, 0.45f), new Vector3(0.025f, 0.025f, 0.25f));
            b.Box(rifle, m.Accent, new Vector3(0f, -0.06f, 0.1f), new Vector3(0.04f, 0.1f, 0.05f));
            b.Ball(rifle, m.Glow, new Vector3(0f, 0.03f, 0.58f), 0.035f);
            body.Muzzle = b.Joint(rifle, "Muzzle", new Vector3(0f, 0.03f, 0.6f));
            DressLegs(b, body, s, m.Body, m.Dark, m.Accent, 0.12f);
            return Finish(body);
        }

        public static EnemyBody Tank(EnemyBase owner)
        {
            var body = new EnemyBody { Mats = GameAssets.Enemies[(int)EnemyType.Tank] };
            var b = new BodyBuilder(body, owner);
            EnemyMaterials m = body.Mats;
            var s = new BodyBuilder.Spec { HipHeight = 0.88f, HipWidth = 0.36f, Thigh = 0.42f, Shin = 0.44f, LegThick = 0.2f, Torso = 0.72f, TorsoW = 0.62f, TorsoD = 0.46f,
                Shoulder = 0.72f, UpperArm = 0.36f, ForeArm = 0.36f, ArmThick = 0.2f, Head = 0.24f, Neck = 0f, Hunch = 12f };
            b.Skeleton(NewModel(owner.transform, 1f), s);
            // Frontal armour: the torso hitbox is metal (sparks) and the armour multiplier is direction based (S-16).
            body.Spine.GetComponent<Hitbox>().Surface = SurfaceKind.Metal;
            b.Box(body.Hips, m.Dark, Vector3.zero, new Vector3(0.56f, 0.2f, 0.42f));
            b.Box(body.Spine, m.Body, new Vector3(0f, 0.36f, 0f), new Vector3(0.62f, 0.66f, 0.44f));
            b.Box(body.Spine, m.Dark, new Vector3(0f, 0.42f, 0.24f), new Vector3(0.58f, 0.52f, 0.07f));
            b.Box(body.Spine, m.Accent, new Vector3(0f, 0.2f, 0.27f), new Vector3(0.5f, 0.05f, 0.04f));
            b.Box(body.Spine, m.Accent, new Vector3(0f, 0.62f, 0.27f), new Vector3(0.5f, 0.05f, 0.04f));
            // Glowing back weak point (S-16 rear x2): its own joint/hitbox protruding behind the torso box.
            Transform weak = b.Joint(body.Spine, "WeakPoint", new Vector3(0f, 0.4f, -0.25f));
            b.HitBox(weak, HitZone.WeakPoint, Vector3.zero, new Vector3(0.38f, 0.44f, 0.1f));
            b.Mesh(weak, GameAssets.Cube, m.Glow, Vector3.zero, new Vector3(0.34f, 0.4f, 0.06f), default, false);
            b.Box(weak, m.Dark, new Vector3(0f, 0.23f, 0f), new Vector3(0.44f, 0.06f, 0.09f));
            b.Box(weak, m.Dark, new Vector3(0f, -0.23f, 0f), new Vector3(0.44f, 0.06f, 0.09f));
            b.Box(body.Head, m.Body, Vector3.zero, new Vector3(0.24f, 0.24f, 0.26f));
            b.Box(body.Head, m.Glow, new Vector3(0f, 0.02f, 0.135f), new Vector3(0.16f, 0.035f, 0.02f));
            b.Box(body.Head, m.Dark, new Vector3(0f, 0.13f, 0f), new Vector3(0.26f, 0.04f, 0.28f));
            foreach (Transform arm in new[] { body.ArmL, body.ArmR })
            {
                b.Box(arm, m.Dark, new Vector3(0f, 0.04f, 0f), new Vector3(0.22f, 0.16f, 0.3f));
                b.Limb(arm, m.Body, s.UpperArm, s.ArmThick);
            }
            foreach (Transform fore in new[] { body.ForeL, body.ForeR })
            {
                b.Box(fore, m.Accent, new Vector3(0f, -0.18f, 0.02f), new Vector3(0.2f, 0.36f, 0.22f));
                b.Box(fore, m.Dark, new Vector3(0f, -0.4f, 0.02f), new Vector3(0.2f, 0.18f, 0.2f));
            }
            b.Limb(body.LegL, m.Body, s.Thigh, s.LegThick);
            b.Limb(body.LegR, m.Body, s.Thigh, s.LegThick);
            foreach (Transform shin in new[] { body.ShinL, body.ShinR })
            {
                b.Box(shin, m.Dark, new Vector3(0f, -0.22f, 0.02f), new Vector3(0.18f, 0.44f, 0.2f));
                b.Box(shin, m.Dark, new Vector3(0f, -s.Shin - 0.03f, 0.06f), new Vector3(0.2f, 0.1f, 0.32f));
            }
            return Finish(body);
        }

        public static EnemyBody Warden(EnemyBase owner)
        {
            var body = new EnemyBody { Mats = GameAssets.Enemies[(int)EnemyType.Warden], Scale = GameConfig.Boss.Scale };
            var b = new BodyBuilder(body, owner);
            EnemyMaterials m = body.Mats;
            var s = new BodyBuilder.Spec { HipHeight = 0.9f, HipWidth = 0.3f, Thigh = 0.44f, Shin = 0.45f, LegThick = 0.17f, Torso = 0.66f, TorsoW = 0.56f, TorsoD = 0.38f,
                Shoulder = 0.64f, UpperArm = 0.34f, ForeArm = 0.34f, ArmThick = 0.17f, Head = 0.24f, Neck = 0.05f, Hunch = 8f };
            b.Skeleton(NewModel(owner.transform, GameConfig.Boss.Scale), s);
            b.Box(body.Hips, m.Dark, Vector3.zero, new Vector3(0.5f, 0.18f, 0.34f));
            for (int i = -1; i <= 1; i += 2) b.Box(body.Hips, m.Accent, new Vector3(i * 0.16f, -0.14f, 0.05f), new Vector3(0.16f, 0.22f, 0.3f), new Vector3(0f, 0f, i * 8f));
            b.Box(body.Spine, m.Body, new Vector3(0f, 0.33f, 0f), new Vector3(0.56f, 0.6f, 0.36f));
            b.Box(body.Spine, m.Accent, new Vector3(0f, 0.5f, 0.17f), new Vector3(0.5f, 0.06f, 0.06f));
            b.Box(body.Spine, m.Accent, new Vector3(0f, 0.14f, 0.17f), new Vector3(0.42f, 0.05f, 0.06f));
            b.Ball(body.Spine, m.Glow, new Vector3(0f, 0.34f, 0.18f), 0.17f);
            b.Box(body.Spine, m.Dark, new Vector3(0f, 0.36f, -0.2f), new Vector3(0.4f, 0.44f, 0.1f));
            b.Box(body.Head, m.Body, Vector3.zero, new Vector3(0.22f, 0.26f, 0.26f));
            b.Box(body.Head, m.Accent, new Vector3(0f, 0.16f, -0.02f), new Vector3(0.04f, 0.16f, 0.3f));
            b.Box(body.Head, m.Glow, new Vector3(0f, 0.02f, 0.135f), new Vector3(0.16f, 0.035f, 0.02f));
            b.Box(body.Head, m.Accent, new Vector3(-0.12f, 0.1f, -0.02f), new Vector3(0.03f, 0.03f, 0.18f), new Vector3(-25f, -30f, 0f));
            b.Box(body.Head, m.Accent, new Vector3(0.12f, 0.1f, -0.02f), new Vector3(0.03f, 0.03f, 0.18f), new Vector3(-25f, 30f, 0f));
            foreach (Transform arm in new[] { body.ArmL, body.ArmR })
            {
                b.Box(arm, m.Accent, new Vector3(0f, 0.05f, 0f), new Vector3(0.24f, 0.14f, 0.28f));
                b.Box(arm, m.Accent, new Vector3(0f, 0.14f, 0f), new Vector3(0.04f, 0.16f, 0.04f), new Vector3(0f, 0f, arm == body.ArmL ? 20f : -20f));
                b.Limb(arm, m.Body, s.UpperArm, s.ArmThick);
            }
            b.Limb(body.ForeL, m.Body, s.ForeArm, s.ArmThick);
            for (int c = -1; c <= 1; c++) b.Box(body.ForeL, m.Accent, new Vector3(c * 0.045f, -s.ForeArm - 0.06f, 0.03f), new Vector3(0.03f, 0.16f, 0.03f), new Vector3(-15f, 0f, c * 14f));
            // Right forearm is an arm cannon (barrel along the forearm's -Y axis).
            b.Mesh(body.ForeR, GameAssets.Cylinder, m.Dark, new Vector3(0f, -0.25f, 0f), new Vector3(0.16f, 0.25f, 0.16f));
            b.Mesh(body.ForeR, GameAssets.Cylinder, m.Glow, new Vector3(0f, -0.51f, 0f), new Vector3(0.12f, 0.015f, 0.12f), default, false);
            body.Muzzle = b.Joint(body.ForeR, "Muzzle", new Vector3(0f, -0.56f, 0f), new Vector3(90f, 0f, 0f));
            DressLegs(b, body, s, m.Body, m.Dark, m.Accent, 0.15f);
            return Finish(body);
        }

        static void DressLegs(BodyBuilder b, EnemyBody body, BodyBuilder.Spec s, Material thigh, Material shin, Material accent, float foot)
        {
            b.Limb(body.LegL, thigh, s.Thigh, s.LegThick);
            b.Limb(body.LegR, thigh, s.Thigh, s.LegThick);
            foreach (Transform sh in new[] { body.ShinL, body.ShinR })
            {
                b.Limb(sh, shin, s.Shin, s.LegThick * 0.88f);
                b.Box(sh, accent, new Vector3(0f, -0.02f, s.LegThick * 0.5f), new Vector3(s.LegThick * 0.9f, 0.08f, 0.05f));
                b.Box(sh, shin, new Vector3(0f, -s.Shin - 0.02f, 0.05f), new Vector3(foot, 0.06f, foot * 2.2f));
            }
        }

        static EnemyBody Finish(EnemyBody body)
        {
            body.CaptureBindPose();
            return body;
        }
    }
}
