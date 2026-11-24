using UnityEngine;
using UnityEngine.Rendering;

namespace WaveSurvival
{
    /// <summary>One first-person weapon model with its animated parts and anchor points.</summary>
    public sealed class Viewmodel
    {
        public Transform Root, Muzzle, EjectPort, Sight;
        public Transform Magazine, Slide, Pump, Shell;
        public Vector3 MagBase, SlideBase, PumpBase, ShellBase;
        public Renderer[] Flash;
        public Vector3 HipPos, HipEuler, AdsPos;
        public float RecoilBack, RecoilPitch;

        public void SetFlash(bool on)
        {
            for (int i = 0; i < Flash.Length; i++)
            {
                Flash[i].enabled = on;
                if (on)
                {
                    // Random roll and size every shot so consecutive flashes never look identical.
                    Transform t = Flash[i].transform;
                    float s = Random.Range(0.75f, 1.25f);
                    if (i == 0)
                    {
                        t.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                        t.localScale = new Vector3(0.15f * s, 0.15f * s, 1f);
                    }
                    else t.localScale = new Vector3(0.24f * s, 0.085f * s, 1f);
                }
            }
        }
    }

    /// <summary>Builds the three weapon viewmodels from primitives (S-08..S-10 "viewmodels built from primitives").</summary>
    public static class ViewmodelFactory
    {
        public static Viewmodel[] BuildAll(Transform root)
        {
            return new[] { Pistol(root), Rifle(root), Shotgun(root) };
        }

        static Transform Node(Transform parent, string name, Vector3 pos, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.layer = GameConfig.Layers.Viewmodel;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go.transform;
        }

        static MeshRenderer Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            Transform t = Node(parent, mesh.name, pos, euler);
            t.localScale = scale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = t.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return mr;
        }

        // Cylinders are 2 units tall along Y: rotate 90 deg on X to lie along the barrel (Z).
        static void Tube(Transform parent, Material mat, Vector3 pos, float diameter, float length) =>
            Part(parent, GameAssets.Cylinder, mat, pos, new Vector3(diameter, length * 0.5f, diameter), new Vector3(90f, 0f, 0f));

        static Renderer[] FlashQuads(Transform muzzle)
        {
            var a = Part(muzzle, GameAssets.Quad, GameAssets.MuzzleFlash, new Vector3(0f, 0f, 0.02f), new Vector3(0.15f, 0.15f, 1f));
            var b = Part(muzzle, GameAssets.Quad, GameAssets.MuzzleFlash, new Vector3(0f, 0f, 0.1f), new Vector3(0.24f, 0.085f, 1f), new Vector3(0f, 90f, 0f));
            a.enabled = false;
            b.enabled = false;
            return new Renderer[] { a, b };
        }

        static void Arm(Transform parent, Vector3 handPos, Vector3 handEuler, Vector3 handSize, Vector3 armPos, Vector3 armEuler)
        {
            Part(parent, GameAssets.Cube, GameAssets.Polymer, handPos, handSize, handEuler);           // glove
            Part(parent, GameAssets.Cube, GameAssets.Enemies[3].Dark, armPos, new Vector3(0.068f, 0.068f, 0.3f), armEuler); // sleeve
        }

        static Viewmodel Begin(Transform root, string name, Vector3 hip, Vector3 hipEuler, float back, float pitch)
        {
            Transform t = Node(root, name, hip, hipEuler);
            t.gameObject.SetActive(false);
            return new Viewmodel { Root = t, HipPos = hip, HipEuler = hipEuler, RecoilBack = back, RecoilPitch = pitch };
        }

        static void Finish(Viewmodel v, float adsDistance)
        {
            // ADS pose: place the sight anchor on the eye's forward axis, gun level (S-11 "weapon centers on screen").
            v.AdsPos = new Vector3(0f, 0f, adsDistance) - v.Sight.localPosition;
        }

        static Viewmodel Pistol(Transform root)
        {
            Viewmodel v = Begin(root, "Pistol", new Vector3(0.12f, -0.115f, 0.3f), new Vector3(0f, -2f, 0f), 0.035f, 9f);
            Transform r = v.Root;
            Part(r, GameAssets.Cube, GameAssets.Polymer, new Vector3(0f, -0.003f, 0.05f), new Vector3(0.03f, 0.022f, 0.15f));
            v.Slide = Node(r, "Slide", new Vector3(0f, 0.022f, 0.055f));
            v.SlideBase = v.Slide.localPosition;
            Part(v.Slide, GameAssets.Cube, GameAssets.Gunmetal, Vector3.zero, new Vector3(0.032f, 0.03f, 0.19f));
            Part(v.Slide, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(0.007f, 0.019f, -0.088f), new Vector3(0.006f, 0.008f, 0.006f));
            Part(v.Slide, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(-0.007f, 0.019f, -0.088f), new Vector3(0.006f, 0.008f, 0.006f));
            Part(v.Slide, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(0f, 0.019f, 0.088f), new Vector3(0.004f, 0.008f, 0.005f));
            Part(v.Slide, GameAssets.Sphere, GameAssets.SightGlow, new Vector3(0f, 0.022f, 0.0905f), Vector3.one * 0.0035f);
            Tube(r, GameAssets.Steel, new Vector3(0f, 0.024f, 0.152f), 0.014f, 0.008f);
            Part(r, GameAssets.Cube, GameAssets.Polymer, new Vector3(0f, -0.06f, -0.008f), new Vector3(0.029f, 0.1f, 0.048f), new Vector3(12f, 0f, 0f));
            Part(r, GameAssets.Cube, GameAssets.Polymer, new Vector3(0f, -0.022f, 0.035f), new Vector3(0.008f, 0.004f, 0.045f));
            Part(r, GameAssets.Cube, GameAssets.Steel, new Vector3(0f, -0.016f, 0.03f), new Vector3(0.004f, 0.014f, 0.004f));
            v.Magazine = Node(r, "Magazine", new Vector3(0f, -0.11f, -0.018f), new Vector3(12f, 0f, 0f));
            v.MagBase = v.Magazine.localPosition;
            Part(v.Magazine, GameAssets.Cube, GameAssets.Gunmetal, Vector3.zero, new Vector3(0.025f, 0.016f, 0.042f));
            Part(v.Magazine, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(0f, 0.05f, 0f), new Vector3(0.022f, 0.09f, 0.036f));
            Arm(r, new Vector3(0.004f, -0.055f, -0.012f), new Vector3(12f, 0f, 0f), new Vector3(0.05f, 0.075f, 0.066f),
                new Vector3(0.025f, -0.1f, -0.17f), new Vector3(28f, -8f, 0f));
            Arm(r, new Vector3(-0.024f, -0.072f, 0.004f), new Vector3(12f, 0f, -25f), new Vector3(0.048f, 0.05f, 0.07f),
                new Vector3(-0.085f, -0.12f, -0.15f), new Vector3(30f, 24f, 0f));
            v.Muzzle = Node(r, "Muzzle", new Vector3(0f, 0.024f, 0.158f));
            v.EjectPort = Node(r, "EjectPort", new Vector3(0.018f, 0.032f, 0.07f));
            v.Sight = Node(r, "Sight", new Vector3(0f, 0.045f, -0.033f));
            v.Flash = FlashQuads(v.Muzzle);
            Finish(v, 0.26f);
            return v;
        }

        static Viewmodel Rifle(Transform root)
        {
            Viewmodel v = Begin(root, "Rifle", new Vector3(0.11f, -0.13f, 0.26f), new Vector3(0f, -1.5f, 0f), 0.03f, 3.2f);
            Transform r = v.Root;
            Part(r, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(0f, 0f, 0.04f), new Vector3(0.048f, 0.065f, 0.28f));
            Part(r, GameAssets.Cube, GameAssets.Steel, new Vector3(0f, 0.038f, 0.06f), new Vector3(0.026f, 0.01f, 0.26f));
            Part(r, GameAssets.Cube, GameAssets.Polymer, new Vector3(0f, -0.002f, 0.3f), new Vector3(0.054f, 0.056f, 0.24f));
            Tube(r, GameAssets.Steel, new Vector3(0f, 0.004f, 0.5f), 0.018f, 0.18f);
            Tube(r, GameAssets.Gunmetal, new Vector3(0f, 0.004f, 0.6f), 0.026f, 0.05f);
            Part(r, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(0f, 0.045f, 0.4f), new Vector3(0.006f, 0.03f, 0.006f));
            Part(r, GameAssets.Cube, GameAssets.Polymer, new Vector3(0f, -0.012f, -0.21f), new Vector3(0.044f, 0.065f, 0.22f));
            Part(r, GameAssets.Cube, GameAssets.Polymer, new Vector3(0f, -0.015f, -0.325f), new Vector3(0.046f, 0.085f, 0.014f));
            Part(r, GameAssets.Cube, GameAssets.Polymer, new Vector3(0f, -0.07f, -0.035f), new Vector3(0.032f, 0.085f, 0.038f), new Vector3(18f, 0f, 0f));
            Part(r, GameAssets.Cube, GameAssets.Steel, new Vector3(0.03f, 0.012f, -0.03f), new Vector3(0.012f, 0.01f, 0.03f));
            v.Magazine = Node(r, "Magazine", new Vector3(0f, -0.035f, 0.11f));
            v.MagBase = v.Magazine.localPosition;
            Part(v.Magazine, GameAssets.Cube, GameAssets.Polymer, new Vector3(0f, -0.055f, 0.006f), new Vector3(0.032f, 0.11f, 0.058f), new Vector3(-8f, 0f, 0f));
            Part(v.Magazine, GameAssets.Cube, GameAssets.Polymer, new Vector3(0f, -0.118f, 0.022f), new Vector3(0.032f, 0.045f, 0.056f), new Vector3(-20f, 0f, 0f));
            // Open red-dot frame (4 bars) so the ADS view stays unobstructed.
            Part(r, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(0f, 0.05f, 0.03f), new Vector3(0.028f, 0.012f, 0.05f));
            Part(r, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(0f, 0.0935f, 0.04f), new Vector3(0.04f, 0.005f, 0.03f));
            Part(r, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(0f, 0.0565f, 0.04f), new Vector3(0.04f, 0.005f, 0.03f));
            Part(r, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(-0.0175f, 0.075f, 0.04f), new Vector3(0.005f, 0.037f, 0.03f));
            Part(r, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(0.0175f, 0.075f, 0.04f), new Vector3(0.005f, 0.037f, 0.03f));
            Part(r, GameAssets.Sphere, GameAssets.SightGlow, new Vector3(0f, 0.075f, 0.046f), Vector3.one * 0.0032f);
            Arm(r, new Vector3(0.002f, -0.07f, -0.04f), new Vector3(18f, 0f, 0f), new Vector3(0.05f, 0.07f, 0.06f),
                new Vector3(0.03f, -0.12f, -0.2f), new Vector3(20f, -6f, 0f));
            Arm(r, new Vector3(-0.006f, -0.04f, 0.3f), new Vector3(0f, 0f, -10f), new Vector3(0.056f, 0.045f, 0.08f),
                new Vector3(-0.075f, -0.11f, 0.15f), new Vector3(25f, 32f, 0f));
            v.Muzzle = Node(r, "Muzzle", new Vector3(0f, 0.004f, 0.63f));
            v.EjectPort = Node(r, "EjectPort", new Vector3(0.026f, 0.014f, 0.06f));
            v.Sight = Node(r, "Sight", new Vector3(0f, 0.075f, 0.046f));
            v.Flash = FlashQuads(v.Muzzle);
            Finish(v, 0.2f);
            return v;
        }

        static Viewmodel Shotgun(Transform root)
        {
            Viewmodel v = Begin(root, "Shotgun", new Vector3(0.115f, -0.135f, 0.24f), new Vector3(0f, -1.5f, 0f), 0.06f, 7f);
            Transform r = v.Root;
            Part(r, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.065f, 0.22f));
            Tube(r, GameAssets.Steel, new Vector3(0f, 0.016f, 0.38f), 0.022f, 0.56f);
            Tube(r, GameAssets.Gunmetal, new Vector3(0f, -0.014f, 0.33f), 0.024f, 0.44f);
            Part(r, GameAssets.Cube, GameAssets.Wood, new Vector3(0f, -0.025f, -0.26f), new Vector3(0.042f, 0.07f, 0.3f), new Vector3(6f, 0f, 0f));
            Part(r, GameAssets.Cube, GameAssets.Polymer, new Vector3(0f, -0.04f, -0.41f), new Vector3(0.044f, 0.09f, 0.015f), new Vector3(6f, 0f, 0f));
            Part(r, GameAssets.Cube, GameAssets.Wood, new Vector3(0f, -0.045f, -0.1f), new Vector3(0.036f, 0.06f, 0.07f), new Vector3(20f, 0f, 0f));
            Part(r, GameAssets.Cube, GameAssets.Gunmetal, new Vector3(0f, 0.028f, -0.09f), new Vector3(0.02f, 0.006f, 0.01f));
            Part(r, GameAssets.Sphere, GameAssets.SightGlow, new Vector3(0f, 0.031f, 0.65f), Vector3.one * 0.0055f);
            v.Pump = Node(r, "Pump", new Vector3(0f, -0.014f, 0.3f));
            v.PumpBase = v.Pump.localPosition;
            Part(v.Pump, GameAssets.Cube, GameAssets.Wood, Vector3.zero, new Vector3(0.046f, 0.044f, 0.15f));
            Arm(v.Pump, new Vector3(-0.004f, -0.02f, 0f), new Vector3(0f, 0f, -8f), new Vector3(0.055f, 0.05f, 0.085f),
                new Vector3(-0.07f, -0.09f, -0.13f), new Vector3(22f, 32f, 0f));
            Arm(r, new Vector3(0.002f, -0.05f, -0.1f), new Vector3(20f, 0f, 0f), new Vector3(0.05f, 0.07f, 0.065f),
                new Vector3(0.03f, -0.11f, -0.25f), new Vector3(18f, -6f, 0f));
            v.Shell = Node(r, "Shell", new Vector3(0.0f, -0.05f, 0.05f));
            v.ShellBase = v.Shell.localPosition;
            Tube(v.Shell, GameAssets.ShellHull, Vector3.zero, 0.019f, 0.05f);
            Tube(v.Shell, GameAssets.Brass, new Vector3(0f, 0f, -0.028f), 0.02f, 0.01f);
            v.Shell.gameObject.SetActive(false);
            v.Muzzle = Node(r, "Muzzle", new Vector3(0f, 0.016f, 0.67f));
            v.EjectPort = Node(r, "EjectPort", new Vector3(0.026f, 0.01f, 0.02f));
            v.Sight = Node(r, "Sight", new Vector3(0f, 0.031f, -0.09f));
            v.Flash = FlashQuads(v.Muzzle);
            Finish(v, 0.22f);
            return v;
        }
    }
}
