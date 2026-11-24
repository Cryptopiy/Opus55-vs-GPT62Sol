using UnityEngine;
using UnityEngine.Rendering;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Lighthouse on the cliff (I-23): the Blender model's separate lamp rotates once every 6 s, a
    /// procedural cone with an additive "volumetric" shader and a spot light follow the same angle.
    /// Beam and light fade in at night and in fog; windows/lamp glow via the night emission in Prop.shader.
    /// </summary>
    public sealed class Lighthouse : MonoBehaviour
    {
        public float Angle { get; private set; }
        public Light Spot { get; private set; }
        public float BeamVisibility { get; private set; }

        Transform lamp;
        Quaternion lampBaseRotation;
        Transform beamPivot;
        Material beamMaterial;

        public static Lighthouse Create(IslandAssets assets, MaterialLibrary mats, PoiPlan plan, Transform parent)
        {
            var go = PropPlacer.Spawn(assets.lighthouse, plan.Lighthouse - Vector3.up * 0.05f, Quaternion.Euler(0f, plan.LighthouseYaw, 0f), 1f, parent);
            if (go == null) return null;
            var lh = go.AddComponent<Lighthouse>();
            lh.Setup(mats);
            return lh;
        }

        void Setup(MaterialLibrary mats)
        {
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name.Contains("Lamp")) lamp = mf.transform;
                else if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
            Vector3 lampPos = lamp != null ? lamp.position : transform.position + Vector3.up * (C.LighthouseHeight - 2.5f);
            if (lamp != null) lampBaseRotation = lamp.localRotation;

            beamPivot = new GameObject("BeamPivot").transform;
            beamPivot.SetParent(transform, false);
            beamPivot.position = lampPos;

            beamMaterial = new Material(mats.Beam);
            var cone = new GameObject("BeamCone");
            cone.transform.SetParent(beamPivot, false);
            cone.transform.localRotation = Quaternion.Euler(4f, 0f, 0f); // slight downward tilt towards the sea
            cone.AddComponent<MeshFilter>().sharedMesh = BuildCone(70f, 5.5f, 0.35f, 24, 12);
            var mr = cone.AddComponent<MeshRenderer>();
            mr.sharedMaterial = beamMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var spotGo = new GameObject("BeamSpot");
            spotGo.transform.SetParent(beamPivot, false);
            spotGo.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            Spot = spotGo.AddComponent<Light>();
            Spot.type = LightType.Spot;
            Spot.range = C.LighthouseSpotRange;
            Spot.spotAngle = C.LighthouseSpotAngle;
            Spot.innerSpotAngle = C.LighthouseSpotAngle * 0.4f;
            Spot.color = new Color(1f, 0.88f, 0.62f);
            Spot.shadows = LightShadows.None;
        }

        /// <summary>Cone along +Z with apex at the origin; uv.y = 0 at the lamp .. 1 at the far end.</summary>
        static Mesh BuildCone(float length, float endRadius, float startRadius, int sides, int rings)
        {
            var verts = new Vector3[(sides + 1) * (rings + 1)];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float rad = Mathf.Lerp(startRadius, endRadius, t);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    int i = r * (sides + 1) + s;
                    verts[i] = new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad, t * length);
                    normals[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), -(endRadius - startRadius) / length).normalized;
                    uvs[i] = new Vector2(s / (float)sides, t);
                }
            }
            var tris = new int[sides * rings * 6];
            int k = 0;
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < sides; s++)
            {
                int a = r * (sides + 1) + s, b = a + sides + 1;
                tris[k++] = a; tris[k++] = b; tris[k++] = a + 1;
                tris[k++] = a + 1; tris[k++] = b; tris[k++] = b + 1;
            }
            var mesh = new Mesh { name = "BeamCone", vertices = verts, normals = normals, uv = uvs, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update()
        {
            Angle = Mathf.Repeat(Angle + Time.deltaTime * 360f / C.LighthouseRotationSeconds, 360f);
            // The lens of the Blender lamp points along its local -Z after the glTF conversion.
            if (lamp != null) lamp.localRotation = lampBaseRotation * Quaternion.Euler(0f, Angle, 0f);
            Vector3 lensDir = lamp != null ? -lamp.forward : Quaternion.Euler(0f, Angle, 0f) * Vector3.forward;
            lensDir.y = 0f;
            if (lensDir.sqrMagnitude > 1e-4f) beamPivot.rotation = Quaternion.LookRotation(lensDir.normalized, Vector3.up);

            float night = Game.TimeOfDay != null ? Game.TimeOfDay.NightFactor : 0f;
            float fog = Game.Weather != null ? Mathf.Clamp01(Game.Weather.Current.FogDensity / (3.912f / 300f)) : 0f;
            BeamVisibility = Mathf.Clamp01(Mathf.Max(night, fog * 0.85f));
            beamMaterial.SetFloat(ShaderIds.Intensity, BeamVisibility);
            // Inverse-square falloff: ~0.18 at 50 m, enough to sweep visibly over the island at night.
            Spot.intensity = 450f * BeamVisibility;
            Spot.enabled = BeamVisibility > 0.01f;
        }
    }
}
