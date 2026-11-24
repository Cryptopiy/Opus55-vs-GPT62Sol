using UnityEngine;
using UnityEngine.Rendering;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed class Viewmodel
    {
        readonly CoreGame game;
        readonly Transform rig;
        readonly Transform[] models = new Transform[GameConfig.Weapons.Length];
        readonly Transform[] muzzles = new Transform[GameConfig.Weapons.Length];
        readonly Transform[] pumps = new Transform[GameConfig.Weapons.Length];
        readonly Transform flashAnchor;
        readonly SpriteRenderer flash;
        readonly Light muzzleLight;
        readonly Material steel, polymer, nickel, strip, wood, glove, sleeve;
        Vector2 sway;
        int selected;
        public Vector3 Muzzle => muzzles[selected].position;
        public Vector3 Eject => models[selected].TransformPoint(new(.075f, .018f, .10f));

        public Viewmodel(CoreGame game)
        {
            this.game = game;
            rig = Geometry.Root("First person weapon rig", game.Player.View);
            steel = Geometry.Lit("Parkerized receiver", Geometry.Hex(0x45565d), .33f, .36f, .04f);
            polymer = Geometry.Lit("Molded grip", Geometry.Hex(0x19282e), .24f, .12f, .045f);
            nickel = Geometry.Lit("Machined slide", Geometry.Hex(0x95aab0), .5f, .35f, .035f);
            strip = Geometry.Lit("Tritium sight", Geometry.Hex(0x70ead6), .4f, 0, 1.6f);
            wood = Geometry.Lit("Copper nickel steel", Geometry.Hex(0x925b37), .37f, .24f, .055f);
            glove = Geometry.Lit("Reinforced gloves", Geometry.Hex(0x282c29), .27f, .06f, .015f);
            sleeve = Geometry.Lit("Field coat", Geometry.Hex(0x394b49), .28f, 0, .025f);
            models[0] = Make("Service pistol", Pistol);
            models[1] = Make("Assault rifle", Rifle);
            models[2] = Make("Pump shotgun", Shotgun);
            foreach (Transform model in models)
                foreach (Renderer render in model.GetComponentsInChildren<Renderer>())
                    render.shadowCastingMode = ShadowCastingMode.Off;
            models[1].gameObject.SetActive(false);
            models[2].gameObject.SetActive(false);
            flashAnchor = Geometry.Root("Pooled flash", rig);
            flash = flashAnchor.gameObject.AddComponent<SpriteRenderer>();
            var texture = Effects.Mask("Muzzle corona", 64, (x, y) =>
            {
                float angle = Mathf.Atan2(y, x);
                float radius = Mathf.Sqrt(x * x + y * y);
                return Mathf.Clamp01(1 - radius * (1.42f + Mathf.Cos(angle * 7) * .42f)) * Mathf.Clamp01((1 - radius) * 2);
            });
            flash.sprite = Geometry.Own(Sprite.Create(texture, new(0, 0, texture.width, texture.height), new(.5f, .5f), texture.width));
            flash.sharedMaterial = Effects.Ink(new Color(3.9f, 1.47f, .38f, 1), texture);
            muzzleLight = flashAnchor.gameObject.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.color = Geometry.Hex(0xffb454);
            muzzleLight.range = 4.6f;
            flash.enabled = muzzleLight.enabled = false;
            Update(1);
        }

        Transform Make(string name, System.Action<Transform> build)
        {
            Transform model = Geometry.Root(name, rig);
            build(model);
            return model;
        }

        Transform P(Transform parent, string name, Vector3 p, Vector3 s, Material material, PrimitiveType type = PrimitiveType.Cube)
            => Geometry.Part(parent, name, p, s, material, type);

        void Pistol(Transform root)
        {
            P(root, "Frame", new(0, -.006f, .14f), new(.094f, .078f, .24f), polymer);
            P(root, "Slide", new(0, .049f, .155f), new(.105f, .065f, .31f), nickel);
            P(root, "Extractor", new(.055f, .054f, .123f), new(.007f, .025f, .072f), steel);
            for (int i = 0; i < 6; i++)
                P(root, "Slide serration", new(0, .050f, .024f + i * .016f), new(.108f, .051f, .006f), steel);
            var grip = P(root, "Grip", new(0, -.103f, .072f), new(.082f, .18f, .101f), polymer);
            grip.localRotation = Quaternion.Euler(-18, 0, 0);
            P(root, "Magazine plate", new(0, -.192f, .053f), new(.086f, .025f, .105f), nickel);
            P(root, "Trigger guard front", new(0, -.067f, .196f), new(.057f, .065f, .015f), steel);
            P(root, "Trigger guard bottom", new(0, -.10f, .161f), new(.056f, .015f, .086f), steel);
            P(root, "Bore", new(0, .052f, .306f), new(.061f, .062f, .016f), polymer, PrimitiveType.Sphere);
            Sights(root, .086f, .278f, .037f);
            Hands(root, .105f, false);
            muzzles[0] = Geometry.Root("Pistol muzzle", root);
            muzzles[0].localPosition = new(0, .051f, .326f);
        }

        void Rifle(Transform root)
        {
            P(root, "Receiver", new(0, .002f, .214f), new(.123f, .126f, .35f), steel);
            P(root, "Upper rail", new(0, .075f, .238f), new(.104f, .032f, .43f), nickel);
            P(root, "Buffer stock", new(0, -.031f, -.061f), new(.109f, .125f, .205f), polymer);
            P(root, "Handguard", new(0, -.012f, .434f), new(.129f, .124f, .245f), polymer);
            for (int i = 0; i < 9; i++)
            {
                P(root, "Rail lug", new(0, .096f, .122f + i * .044f), new(.103f, .016f, .021f), steel);
                P(root, "Vent", new(0, .014f, .335f + i * .025f), new(.132f, .027f, .009f), steel);
            }
            var magazine = P(root, "Magazine", new(0, -.146f, .223f), new(.09f, .183f, .123f), polymer);
            magazine.localRotation = Quaternion.Euler(12, 0, 0);
            P(root, "Bolt carrier", new(.065f, .021f, .191f), new(.009f, .048f, .13f), nickel);
            P(root, "Safety indicator", new(.066f, -.013f, .095f), new(.009f, .021f, .053f), strip);
            Barrel(root, .57f, .19f, .047f, nickel);
            Barrel(root, .691f, .077f, .069f, steel);
            Sights(root, .111f, .515f, .106f);
            Hands(root, .39f, true);
            muzzles[1] = Geometry.Root("Rifle muzzle", root);
            muzzles[1].localPosition = new(0, .025f, .743f);
        }

        void Shotgun(Transform root)
        {
            P(root, "Receiver", new(0, .014f, .181f), new(.139f, .139f, .28f), steel);
            P(root, "Top cover", new(0, .085f, .188f), new(.125f, .021f, .28f), nickel);
            P(root, "Breech", new(.073f, .025f, .187f), new(.01f, .055f, .139f), polymer);
            P(root, "Stock", new(0, -.034f, -.014f), new(.135f, .15f, .178f), wood);
            Barrel(root, .475f, .44f, .086f, steel, .043f);
            Barrel(root, .456f, .37f, .057f, nickel, -.053f);
            pumps[2] = Geometry.Root("Pump and support hand", root);
            P(pumps[2], "Ribbed pump", new(0, -.059f, .437f), new(.142f, .103f, .202f), wood);
            for (int i = 0; i < 7; i++)
                P(pumps[2], "Pump grip rib", new(0, -.061f, .355f + i * .025f), new(.145f, .109f, .012f), polymer);
            Sights(root, .099f, .652f, .086f);
            Hands(root, .42f, true, pumps[2]);
            muzzles[2] = Geometry.Root("Shotgun muzzle", root);
            muzzles[2].localPosition = new(0, .043f, .71f);
        }

        void Barrel(Transform root, float z, float length, float width, Material metal, float y = .025f)
        {
            var barrel = P(root, "Barrel", new(0, y, z), new(width, length * .5f, width), metal, PrimitiveType.Cylinder);
            barrel.localRotation = Quaternion.Euler(90, 0, 0);
        }

        void Sights(Transform root, float height, float forward, float rear)
        {
            P(root, "Front sight", new(0, height, forward), new(.033f, .038f, .038f), polymer);
            P(root, "Front sight insert", new(0, height + .021f, forward), new(.015f, .011f, .014f), strip);
            for (int i = -1; i <= 1; i += 2)
            {
                P(root, "Rear sight ear", new(i * .037f, height + .004f, rear), new(.028f, .044f, .048f), polymer);
                P(root, "Rear sight insert", new(i * .037f, height + .022f, rear - .025f), new(.013f, .010f, .012f), strip);
            }
        }

        void Hands(Transform root, float supportZ, bool longGun, Transform support = null)
        {
            P(root, "Right glove", new(.055f, -.101f, .079f), new(.125f, .12f, .15f), glove, PrimitiveType.Sphere);
            P(support ?? root, "Left glove", new(-.054f, longGun ? -.074f : -.089f, supportZ),
                new(.123f, .106f, .14f), glove, PrimitiveType.Sphere);
            var arm = P(root, "Right sleeve", new(.115f, -.195f, -.009f), new(.14f, .16f, .35f), sleeve, PrimitiveType.Capsule);
            arm.localRotation = Quaternion.Euler(62, -20, 0);
            arm = P(root, "Left sleeve", new(-.103f, -.181f, supportZ - .11f),
                new(.137f, .20f, .15f), sleeve, PrimitiveType.Capsule);
            arm.localRotation = Quaternion.Euler(63, 13, -14);
        }

        public void Flash()
        {
            flashAnchor.position = Muzzle;
            flashAnchor.rotation = game.Player.View.rotation * Quaternion.Euler(0, 0, Random.Range(0, 360));
            flashAnchor.localScale = Vector3.one * (selected == 2 ? .37f : .25f);
            muzzleLight.intensity = selected == 2 ? 14 : 9;
            flash.enabled = muzzleLight.enabled = true;
        }

        public void Update(float dt)
        {
            int active = game.Guns.Selected;
            if (active != selected)
            {
                models[selected].gameObject.SetActive(false);
                selected = active;
                models[selected].gameObject.SetActive(true);
            }
            float since = Time.time - game.Guns.LastShot;
            if (since >= FlashTime) flash.enabled = muzzleLight.enabled = false;
            float kick = Mathf.Clamp01(1 - since / (selected == 2 ? .27f : .14f));
            float aim = Mathf.SmoothStep(0, 1, game.Player.Aim);
            float phase = game.Player.Gait;
            float motion = game.Player.Motion;
            float down = game.Guns.Stage;
            sway = Vector2.Lerp(sway, Vector2.ClampMagnitude(game.Player.LookDelta, 24), 1 - Mathf.Exp(-12 * dt));
            Vector3 hip = selected == 0 ? new(.216f, -.211f, .34f) : new(.239f, -.242f, .30f);
            Vector3 sight = new(0, selected == 1 ? -.13f : -.113f, selected == 0 ? .31f : .25f);
            rig.localPosition = Vector3.Lerp(hip, sight, aim)
                + new Vector3(Mathf.Sin(phase) * .021f * motion - sway.x * .0008f,
                    -down * .22f + Mathf.Cos(phase * 2) * .017f * motion - sway.y * .00055f,
                    -kick * kick * (selected == 2 ? .075f : .036f));
            rig.localRotation = Quaternion.Euler(-kick * 9 + down * 27 - sway.y * .21f,
                sway.x * .17f - (1 - aim) * 5, (1 - aim) * -4 - sway.x * .28f);
            if (selected == 2)
            {
                float pump = Mathf.Clamp01((since - .13f) / (GameConfig.Weapons[2].Interval - .13f));
                pumps[2].localPosition = new(0, 0, -.137f * Mathf.Sin(pump * Mathf.PI));
            }
        }
    }
}


