using UnityEngine;

namespace WaveSurvival
{
    internal sealed partial class Enemy
    {
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color[] Team =
        {
            Geometry.Hex(0xeb6d43), Geometry.Hex(0x61c8e0), Geometry.Hex(0xf4af49), Geometry.Hex(0xbb4145)
        };
        readonly MaterialPropertyBlock tint = new();
        Renderer[] renderers;
        Color[] originalColors, originalEmission;

        (Transform, Transform, Transform, Transform, Transform, Collider[]) BuildModel()
        {
            var parts = game.EnemyPalette[(int)Kind];
            Material armor = parts[0], trim = parts[1], optic = parts[2];
            float width = Kind == EnemyKind.Tank || Kind == EnemyKind.Warden ? .74f : Kind == EnemyKind.Shooter ? .59f : .51f;
            Transform P(Transform root, string name, Vector3 p, Vector3 s, Material m, PrimitiveType type = PrimitiveType.Cube)
                => Geometry.Part(root, name, p, s, m, type);
            Transform torso = P(figure, "Armored carapace", new(0, 1.12f, 0), new(width, .46f, .38f), armor);
            P(figure, "Chest alloy", new(0, 1.19f, .204f), new(width * .71f, .22f, .08f), trim);
            P(figure, "Chest signal", new(0, 1.19f, .257f), new(width * .4f, .033f, .027f), optic);
            P(figure, "Hip", new(0, .83f, 0), new(width * .72f, .21f, .33f), trim);
            var helmet = P(figure, "Helmet", new(0, 1.56f, 0), new(.36f, .35f, .33f), armor);
            P(figure, "Visor", new(0, 1.605f, .179f), new(.285f, .072f, .04f), optic);
            P(figure, "Jaw plate", new(0, 1.48f, .163f), new(.235f, .095f, .055f), trim);

            var right = Limb(1, true);
            var left = Limb(-1, true);
            var legR = Limb(1, false);
            var legL = Limb(-1, false);
            if (Kind == EnemyKind.Shooter || Kind == EnemyKind.Warden)
            {
                P(right, "Repeater", new(.025f, -.48f, .07f), new(.16f, .40f, .15f), trim);
                P(right, "Repeater coil", new(.025f, -.65f, .075f), new(.115f, .25f, .107f), optic);
                P(figure, "Backpack", new(0, 1.18f, -.245f), new(width * .66f, .43f, .19f), armor);
            }
            else if (Kind == EnemyKind.Tank)
            {
                P(figure, "Rear weak point", new(0, 1.12f, -.26f), new(.32f, .4f, .13f), optic, PrimitiveType.Sphere);
                P(figure, "Rear armor rails", new(0, .89f, -.247f), new(.53f, .09f, .13f), trim);
                P(figure, "Front shield", new(0, 1.29f, .262f), new(.62f, .20f, .15f), armor);
            }
            var body = HurtBox("Body collider", new(0, .855f, 0), new(width + .18f, 1.32f, .56f), false);
            var skull = HurtBox("Head collider", helmet.localPosition, helmet.localScale, true);
            renderers = figure.GetComponentsInChildren<Renderer>(true);
            originalColors = new Color[renderers.Length];
            originalEmission = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalColors[i] = renderers[i].sharedMaterial.GetColor(BaseColor);
                originalEmission[i] = renderers[i].sharedMaterial.GetColor(Emission);
            }
            return (helmet, right, left, legR, legL, new[] { body, skull });

            Transform Limb(int sign, bool arm)
            {
                var joint = Geometry.Root(arm ? "Shoulder joint" : "Hip joint", figure);
                joint.localPosition = arm ? new(sign * (width * .5f + .105f), 1.31f, 0) : new(sign * width * .27f, .78f, 0);
                float broad = arm && width > .65f ? .33f : .20f;
                P(joint, arm ? "Pauldron" : "Thigh", new(0, -.08f, 0),
                    new(broad, arm ? .27f : .31f, .29f), armor);
                P(joint, arm ? "Gauntlet" : "Shin", new(0, arm ? -.36f : -.48f, .015f),
                    new(broad * .87f, arm ? .28f : .40f, .235f), trim);
                if (!arm) P(joint, "Boot", new(0, -.69f, .07f), new(.20f, .17f, .32f), armor);
                return joint;
            }
        }

        BoxCollider HurtBox(string name, Vector3 p, Vector3 size, bool skull)
        {
            var zone = Geometry.Root(name, figure);
            zone.localPosition = p;
            zone.gameObject.layer = GameConfig.EnemyLayer;
            var hit = zone.gameObject.AddComponent<BoxCollider>();
            hit.size = size;
            var info = zone.gameObject.AddComponent<HitZone>();
            info.Enemy = this;
            info.Head = skull;
            return hit;
        }

        void SetGlow(float value)
        {
            Color glow = State == EnemyState.Charge || Kind == EnemyKind.Tank && State == EnemyState.Attack ||
                Kind == EnemyKind.Warden && Phase == 3 ? Geometry.Hex(0xff2e35) : Color.white;
            for (int i = 0; i < renderers.Length; i++)
            {
                tint.SetColor(BaseColor, originalColors[i]);
                tint.SetColor(Emission, originalEmission[i] + glow * (value * 2.6f));
                renderers[i].SetPropertyBlock(tint);
                renderers[i].enabled = true;
            }
        }

        void SetCorpseVisibility(float t)
        {
            // Staggering the pieces gives opaque armor a fade without sorting the entire model.
            for (int i = 0; i < renderers.Length; i++)
            {
                Color color = originalColors[i] * Mathf.Lerp(1, .16f, t);
                color.a = 1;
                tint.SetColor(BaseColor, color);
                tint.SetColor(Emission, originalEmission[i] * (1 - t));
                renderers[i].SetPropertyBlock(tint);
                renderers[i].enabled = t < .64f + (i % 8) * .046f;
            }
        }

        public static Material[][] Palette(Material trim)
        {
            var sets = new Material[Team.Length][];
            for (int i = 0; i < sets.Length; i++)
                sets[i] = new[] { Geometry.Lit(((EnemyKind)i) + " plating", Team[i] * .72f, .38f, .26f, .025f),
                    trim, Geometry.Lit(((EnemyKind)i) + " optics", Team[i], .4f, .15f, 3.2f) };
            return sets;
        }
    }
}
