using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Wpn = DestructionSandbox.GameConfig.Weapons;
using P = DestructionSandbox.GameConfig.Palette;

namespace DestructionSandbox
{
    /// <summary>
    /// D-16 view model: a small cannon assembled from primitives (cylinders, spheres, boxes) attached to the
    /// camera, with recoil (0.15 m kick, 0.2 s recovery), muzzle flash (light + flash sprites + sparks/smoke)
    /// and a coloured band that shows the selected ammunition.
    /// </summary>
    public sealed class CannonView : MonoBehaviour
    {
        private Transform recoilRoot;
        private Transform muzzle;
        private Light flashLight;
        private Material bandMaterial;
        private float recoilTime = float.MaxValue;
        private float flashTime = float.MaxValue;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorBId = Shader.PropertyToID("_ColorB");

        public Vector3 MuzzlePosition => muzzle.position;

        public static CannonView Create(Transform cameraTransform, MaterialLibrary materials, MeshLibrary meshes)
        {
            var root = new GameObject("CannonView");
            root.transform.SetParent(cameraTransform, false);
            root.transform.localPosition = Wpn.CannonViewOffset;
            root.transform.localScale = Vector3.one * Wpn.CannonViewScale;
            var view = root.AddComponent<CannonView>();

            view.recoilRoot = new GameObject("Recoil").transform;
            view.recoilRoot.SetParent(root.transform, false);
            Transform r = view.recoilRoot;

            // Barrel (along +Z), breech ball, muzzle ring, carriage block and side trunnions.
            Part(r, meshes.Cylinder, materials.Metal, new Vector3(0f, 0f, 0.18f), Quaternion.identity, new Vector3(0.16f, 0.16f, 0.62f));
            Part(r, meshes.Cylinder, materials.Metal, new Vector3(0f, 0f, 0.5f), Quaternion.identity, new Vector3(0.2f, 0.2f, 0.07f));
            Part(r, meshes.Sphere, materials.Metal, new Vector3(0f, 0f, -0.13f), Quaternion.identity, new Vector3(0.23f, 0.23f, 0.23f));
            Part(r, meshes.Cylinder, materials.Metal, new Vector3(0f, 0f, -0.02f), Quaternion.Euler(0f, 90f, 0f), new Vector3(0.07f, 0.07f, 0.32f));
            Part(r, meshes.UnitCube, materials.Wood, new Vector3(0f, -0.13f, -0.05f), Quaternion.identity, new Vector3(0.22f, 0.08f, 0.36f));

            view.bandMaterial = new Material(materials.Metal) { name = "AmmoBand" };
            Part(r, meshes.Cylinder, view.bandMaterial, new Vector3(0f, 0f, 0.05f), Quaternion.identity, new Vector3(0.175f, 0.175f, 0.05f));

            view.muzzle = new GameObject("Muzzle").transform;
            view.muzzle.SetParent(r, false);
            view.muzzle.localPosition = new Vector3(0f, 0f, 0.56f);

            var lightGo = new GameObject("MuzzleLight");
            lightGo.transform.SetParent(view.muzzle, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            view.flashLight = lightGo.AddComponent<Light>();
            view.flashLight.type = LightType.Point;
            view.flashLight.color = P.MuzzleFlash;
            view.flashLight.range = Wpn.MuzzleLightRange;
            view.flashLight.intensity = 0f;
            view.flashLight.shadows = LightShadows.None;
            view.flashLight.enabled = false;
            lightGo.AddComponent<UniversalAdditionalLightData>();
            view.SetAmmoColor(WeaponType.Cannonball);
            return view;
        }

        private static GameObject Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = new GameObject("Part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        public void SetAmmoColor(WeaponType type)
        {
            Color c = type == WeaponType.Cannonball ? P.MetalA * 2.2f : type == WeaponType.Shell ? P.ShellA : P.OrbParticle;
            c.a = 1f;
            bandMaterial.SetColor(BaseColorId, c);
            bandMaterial.SetColor(ColorBId, c * 0.7f);
        }

        public Vector3 MuzzleForward => muzzle.forward;

        /// <summary>Recoil kick plus muzzle flash: light here, flash sprites / sparks / smoke from the effects system.</summary>
        public void PlayFire()
        {
            recoilTime = 0f;
            flashTime = 0f;
            flashLight.enabled = true;
            flashLight.intensity = Wpn.MuzzleLightIntensity;
            Game.Effects.MuzzleFlash(muzzle.position, muzzle.forward);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (Game.IsPaused) dt = 0f;

            // Instant kick back, eased recovery (D-16: 0.15 m, 0.2 s).
            recoilTime += dt;
            float k = Mathf.Clamp01(recoilTime / Wpn.RecoilRecoverTime);
            float ease = 1f - (1f - k) * (1f - k);
            // The view model is scaled; divide so the kick is exactly RecoilDistance in camera space.
            recoilRoot.localPosition = new Vector3(0f, 0f, -Wpn.RecoilDistance / Wpn.CannonViewScale * (1f - ease));
            recoilRoot.localRotation = Quaternion.Euler(-6f * (1f - ease), 0f, 0f);

            flashTime += dt;
            if (flashTime < Wpn.MuzzleFlashTime)
            {
                flashLight.intensity = Wpn.MuzzleLightIntensity * (1f - flashTime / Wpn.MuzzleFlashTime);
            }
            else if (flashLight.enabled)
            {
                flashLight.enabled = false;
            }
        }
    }
}
