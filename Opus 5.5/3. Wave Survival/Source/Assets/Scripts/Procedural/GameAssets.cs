using UnityEngine;

namespace WaveSurvival
{
    /// <summary>Per-enemy-type material set: opaque materials plus alpha-blended twins used for the death fade.</summary>
    public sealed class EnemyMaterials
    {
        public Material Body, Accent, Dark, Glow;
        public Material BodyFade, AccentFade, DarkFade, GlowFade;

        public Material FadeOf(Material m)
        {
            if (m == Body) return BodyFade;
            if (m == Accent) return AccentFade;
            if (m == Dark) return DarkFade;
            return GlowFade;
        }
    }

    /// <summary>
    /// Every generated asset (textures, materials, meshes, sprites, font) is created once per application run and
    /// cached here, so restarting the game (scene reload) is instant. Audio clips live in <see cref="SoundLibrary"/>.
    /// </summary>
    public static class GameAssets
    {
        public static bool Ready { get; private set; }

        public static Mesh Cube, Sphere, Capsule, Cylinder, Quad, Ring;
        public static Material Wall, Floor, Ceiling, Beam, Hazard, Tread, Crate, Barrier, GateFrame, GateDoor;
        public static Material LampHousing, LampBulb, Cable;
        public static Material Gunmetal, Polymer, Wood, Brass, SightGlow, Steel, ShellHull;
        public static Material HealthBox, HealthCrossMat, AmmoBox, AmmoGlow;
        public static Material SmokeAlpha, DotAlpha, DotAdditive, MuzzleFlash, Tracer, TelegraphRing;
        public static Material ProjectileShooter, ProjectileBoss, TrailShooter, TrailBoss;
        public static Material DecalConcrete, DecalMetal, DecalBlood, NavDebug, DebugLine;
        public static readonly EnemyMaterials[] Enemies = new EnemyMaterials[4];
        public static Sprite VignetteSprite, DamageArcSprite, CircleSprite, HealthCrossSprite;
        public static readonly Sprite[] WeaponIcons = new Sprite[3];
        public static Font Font;

        public static void Ensure()
        {
            if (Ready) return;
            BuildMeshes();
            BuildEnvironment();
            BuildWeaponsAndPickups();
            BuildFx();
            BuildEnemies();
            BuildUi();
            Ready = true;
            Application.quitting -= Release;
            Application.quitting += Release;
        }

        static Mesh Primitive(PrimitiveType type)
        {
            // Borrow the built-in primitive mesh, then drop the temporary object (and its collider) immediately.
            GameObject go = GameObject.CreatePrimitive(type);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return mesh;
        }

        static void BuildMeshes()
        {
            Cube = Primitive(PrimitiveType.Cube);
            Sphere = Primitive(PrimitiveType.Sphere);
            Capsule = Primitive(PrimitiveType.Capsule);
            Cylinder = Primitive(PrimitiveType.Cylinder);
            Quad = MeshFactory.Quad("DecalQuad");
            Ring = MeshFactory.Ring("Ring", 0.94f, 1f, 96);
        }

        static void BuildEnvironment()
        {
            var wall = SurfaceTextures.ConcreteWall();
            var floor = SurfaceTextures.ConcreteFloor();
            var ceiling = SurfaceTextures.MetalPlate(new Color(0.17f, 0.18f, 0.2f), 1);
            var beam = SurfaceTextures.MetalPlate(new Color(0.3f, 0.25f, 0.17f), 2);
            var frame = SurfaceTextures.MetalPlate(new Color(0.2f, 0.21f, 0.22f), 3);
            var hazard = SurfaceTextures.Hazard();
            var tread = SurfaceTextures.TreadPlate();
            var crate = SurfaceTextures.CrateFace();

            Wall = MaterialFactory.Lit("Wall", Color.white, 0f, 0.18f, wall.Albedo, wall.Normal, 1f);
            Barrier = MaterialFactory.Lit("Barrier", new Color(1.12f, 1.1f, 1.05f), 0f, 0.2f, wall.Albedo, wall.Normal, 1f);
            Floor = MaterialFactory.Lit("Floor", Color.white, 0f, 0.34f, floor.Albedo, floor.Normal, 1f);
            Ceiling = MaterialFactory.Lit("Ceiling", Color.white, 0.55f, 0.32f, ceiling.Albedo, ceiling.Normal, 1f);
            Beam = MaterialFactory.Lit("Beam", Color.white, 0.6f, 0.36f, beam.Albedo, beam.Normal, 1f);
            Hazard = MaterialFactory.Lit("Hazard", Color.white, 0.1f, 0.3f, hazard.Albedo, hazard.Normal, 1f);
            Tread = MaterialFactory.Lit("Tread", Color.white, 0.85f, 0.45f, tread.Albedo, tread.Normal, 1f);
            Crate = MaterialFactory.Lit("Crate", Color.white, 0.35f, 0.35f, crate.Albedo, crate.Normal, 1f);
            GateFrame = MaterialFactory.Lit("GateFrame", Color.white, 0.7f, 0.38f, frame.Albedo, frame.Normal, 1f);
            GateDoor = MaterialFactory.LitEmissive("GateDoor", new Color(0.05f, 0.03f, 0.03f), Color.black, 0.4f, 0.6f);
            LampHousing = MaterialFactory.Lit("LampHousing", new Color(0.12f, 0.13f, 0.12f), 0.7f, 0.4f, frame.Albedo, frame.Normal, 1f);
            LampBulb = MaterialFactory.LitEmissive("LampBulb", new Color(1f, 0.9f, 0.7f), new Color(6f, 4.6f, 3.1f), 0f, 0.8f);
            Cable = MaterialFactory.Lit("Cable", new Color(0.03f, 0.03f, 0.03f), 0f, 0.3f);
        }

        static void BuildWeaponsAndPickups()
        {
            var grime = SurfaceTextures.Grime();
            Texture2D wood = SurfaceTextures.Wood();
            Gunmetal = MaterialFactory.Lit("Gunmetal", new Color(0.24f, 0.25f, 0.27f), 0.5f, 0.55f, grime.Albedo, grime.Normal, 0.6f);
            Steel = MaterialFactory.Lit("Steel", new Color(0.45f, 0.46f, 0.48f), 0.6f, 0.62f, grime.Albedo, grime.Normal, 0.5f);
            Polymer = MaterialFactory.Lit("Polymer", new Color(0.075f, 0.08f, 0.085f), 0f, 0.38f, grime.Albedo, grime.Normal, 0.8f);
            Wood = MaterialFactory.Lit("Wood", Color.white, 0f, 0.45f, wood, grime.Normal, 0.4f);
            Brass = MaterialFactory.Lit("Brass", new Color(0.78f, 0.56f, 0.22f), 1f, 0.72f);
            ShellHull = MaterialFactory.Lit("ShellHull", new Color(0.55f, 0.06f, 0.05f), 0f, 0.5f);
            SightGlow = MaterialFactory.LitEmissive("SightGlow", new Color(1f, 0.1f, 0.05f), new Color(9f, 0.6f, 0.3f), 0f, 0.9f);
            HealthBox = MaterialFactory.Lit("HealthBox", new Color(0.86f, 0.86f, 0.83f), 0.1f, 0.5f, grime.Albedo, grime.Normal, 0.5f);
            HealthCrossMat = MaterialFactory.LitEmissive("HealthCross", new Color(0.8f, 0.05f, 0.05f), new Color(2.6f, 0.15f, 0.12f), 0f, 0.5f);
            AmmoBox = MaterialFactory.Lit("AmmoBox", new Color(0.1f, 0.24f, 0.62f), 0.4f, 0.45f, grime.Albedo, grime.Normal, 0.6f);
            AmmoGlow = MaterialFactory.LitEmissive("AmmoGlow", new Color(0.2f, 0.5f, 1f), new Color(0.6f, 1.6f, 4f), 0f, 0.6f);
        }

        static void BuildFx()
        {
            Texture2D dot = EffectTextures.SoftDot();
            Texture2D smoke = EffectTextures.Smoke();
            SmokeAlpha = MaterialFactory.Particle("SmokeAlpha", smoke, Color.white, false);
            DotAlpha = MaterialFactory.Particle("DotAlpha", dot, Color.white, false);
            DotAdditive = MaterialFactory.Particle("DotAdditive", dot, Color.white, true);
            MuzzleFlash = MaterialFactory.Particle("MuzzleFlash", EffectTextures.MuzzleStar(), new Color(1f, 0.85f, 0.6f, 1f), true);
            Tracer = MaterialFactory.Particle("Tracer", dot, new Color(1f, 0.82f, 0.5f, 1f), true);
            TelegraphRing = MaterialFactory.Particle("TelegraphRing", EffectTextures.RingSprite(), new Color(1f, 0.15f, 0.08f, 1f), true);
            TrailShooter = MaterialFactory.Particle("TrailShooter", dot, new Color(0.3f, 0.9f, 1f, 1f), true);
            TrailBoss = MaterialFactory.Particle("TrailBoss", dot, new Color(1f, 0.35f, 0.15f, 1f), true);
            ProjectileShooter = MaterialFactory.LitEmissive("ProjectileShooter", new Color(0.2f, 0.8f, 1f), new Color(1.5f, 7f, 9f), 0f, 0.8f);
            ProjectileBoss = MaterialFactory.LitEmissive("ProjectileBoss", new Color(1f, 0.4f, 0.1f), new Color(10f, 3f, 0.8f), 0f, 0.8f);
            DecalConcrete = MaterialFactory.Lit("DecalConcrete", Color.white, 0f, 0.15f, EffectTextures.BulletHoleConcrete());
            DecalMetal = MaterialFactory.Lit("DecalMetal", Color.white, 0.8f, 0.55f, EffectTextures.BulletHoleMetal());
            DecalBlood = MaterialFactory.Lit("DecalBlood", Color.white, 0f, 0.75f, EffectTextures.BloodSplat());
            MaterialFactory.MakeTransparent(DecalConcrete);
            MaterialFactory.MakeTransparent(DecalMetal);
            MaterialFactory.MakeTransparent(DecalBlood);
            NavDebug = MaterialFactory.Particle("NavDebug", null, new Color(0.1f, 1f, 0.35f, 0.22f), false);
            DebugLine = MaterialFactory.Particle("DebugLine", null, Color.white, true);
        }

        static EnemyMaterials EnemySet(string name, Color body, Color accent, Color glow, Texture albedo, Texture normal)
        {
            var e = new EnemyMaterials
            {
                // Emission keyword is on for every part so the hit flash (S-14..16) can drive _EmissionColor.
                Body = MaterialFactory.LitEmissive(name + "Body", body, Color.black, 0.25f, 0.42f, albedo, normal, 0.8f),
                Accent = MaterialFactory.LitEmissive(name + "Accent", accent, Color.black, 0.5f, 0.5f, albedo, normal, 0.6f),
                Dark = MaterialFactory.LitEmissive(name + "Dark", new Color(0.06f, 0.06f, 0.065f), Color.black, 0.3f, 0.35f, albedo, normal, 0.8f),
                Glow = MaterialFactory.LitEmissive(name + "Glow", glow, glow * 4f, 0f, 0.8f),
            };
            e.BodyFade = AssetTracker.Track(new Material(e.Body) { name = name + "BodyFade" });
            e.AccentFade = AssetTracker.Track(new Material(e.Accent) { name = name + "AccentFade" });
            e.DarkFade = AssetTracker.Track(new Material(e.Dark) { name = name + "DarkFade" });
            e.GlowFade = AssetTracker.Track(new Material(e.Glow) { name = name + "GlowFade" });
            MaterialFactory.MakeTransparent(e.BodyFade);
            MaterialFactory.MakeTransparent(e.AccentFade);
            MaterialFactory.MakeTransparent(e.DarkFade);
            MaterialFactory.MakeTransparent(e.GlowFade);
            return e;
        }

        static void BuildEnemies()
        {
            var grime = SurfaceTextures.Grime();
            Enemies[(int)EnemyType.Runner] = EnemySet("Runner", new Color(0.34f, 0.31f, 0.27f), new Color(0.8f, 0.4f, 0.06f), new Color(1f, 0.75f, 0.1f), grime.Albedo, grime.Normal);
            Enemies[(int)EnemyType.Shooter] = EnemySet("Shooter", new Color(0.12f, 0.2f, 0.3f), new Color(0.18f, 0.52f, 0.58f), new Color(0.2f, 0.95f, 1f), grime.Albedo, grime.Normal);
            Enemies[(int)EnemyType.Tank] = EnemySet("Tank", new Color(0.24f, 0.24f, 0.26f), new Color(0.46f, 0.13f, 0.08f), new Color(1f, 0.45f, 0.08f), grime.Albedo, grime.Normal);
            Enemies[(int)EnemyType.Warden] = EnemySet("Warden", new Color(0.1f, 0.1f, 0.11f), new Color(0.58f, 0.43f, 0.2f), new Color(1f, 0.12f, 0.06f), grime.Albedo, grime.Normal);
        }

        static Sprite MakeSprite(Texture2D tex) =>
            AssetTracker.Track(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect));

        static void BuildUi()
        {
            VignetteSprite = MakeSprite(EffectTextures.Vignette());
            DamageArcSprite = MakeSprite(EffectTextures.DamageArc());
            CircleSprite = MakeSprite(EffectTextures.Circle());
            HealthCrossSprite = MakeSprite(EffectTextures.HealthCross());
            for (int i = 0; i < 3; i++) WeaponIcons[i] = MakeSprite(EffectTextures.WeaponIcon(i));
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>Destroys generated assets when the application (or editor Play Mode) quits.</summary>
        static void Release()
        {
            Application.quitting -= Release;
            AssetTracker.DestroyAll();
            SoundLibrary.Clear();
            Ready = false;
        }
    }
}
