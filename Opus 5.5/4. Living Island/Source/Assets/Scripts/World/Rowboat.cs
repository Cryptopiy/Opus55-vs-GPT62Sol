using UnityEngine;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// The Blender rowboat moored in the bay shallows (I-24). Four hull sample points read the CPU wave
    /// model, giving a slight heave, pitch and roll that match the rendered waves; if the sand is higher
    /// than the float position the keel rests on the bottom instead of sinking into it.
    /// </summary>
    public sealed class Rowboat : MonoBehaviour
    {
        public float Bob { get; private set; }

        float halfLength = 1.6f, halfWidth = 0.55f;
        const float KeelDepth = 0.25f;     // keel bottom below the modelled waterline (origin)
        Vector3 anchor;
        float bowYaw;                      // world heading of the bow
        float modelYawOffset;              // turns the model so its long axis follows the bow heading
        IslandData island;

        public static Rowboat Create(IslandAssets assets, IslandData d, PoiPlan plan, Transform parent)
        {
            var go = PropPlacer.Spawn(assets.rowboat, plan.Boat, Quaternion.Euler(0f, plan.BoatYaw, 0f), 1f, parent);
            if (go == null) return null;
            var boat = go.AddComponent<Rowboat>();
            boat.island = d;
            boat.anchor = plan.Boat;
            boat.bowYaw = plan.BoatYaw;
            boat.Setup();
            return boat;
        }

        void Setup()
        {
            // Find the hull's long axis from the mesh bounds (the glTF axis conversion is not assumed).
            var mf = PropPlacer.FirstMesh(gameObject);
            if (mf != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                if (b.size.x > b.size.z) modelYawOffset = -90f;
                halfLength = Mathf.Max(b.size.x, b.size.z) * 0.42f;
                halfWidth = Mathf.Min(b.size.x, b.size.z) * 0.4f;
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;                 // moved by the waves, pushes nothing
            body.interpolation = RigidbodyInterpolation.None;
            LateUpdate();
        }

        float SurfaceOrBottom(Vector3 p)
        {
            float water = WaveModel.HeightAt(p.x, p.z);
            float ground = island.HeightAt(p.x, p.z) + KeelDepth;
            return Mathf.Max(water, ground);
        }

        void LateUpdate()
        {
            if (island == null) return;
            Quaternion bow = Quaternion.Euler(0f, bowYaw, 0f);
            Vector3 fwd = bow * Vector3.forward * halfLength, right = bow * Vector3.right * halfWidth;
            float hBow = SurfaceOrBottom(anchor + fwd), hStern = SurfaceOrBottom(anchor - fwd);
            float hPort = SurfaceOrBottom(anchor - right), hStar = SurfaceOrBottom(anchor + right);
            float h = (hBow + hStern + hPort + hStar) * 0.25f;
            // "Slightly": pitch/roll are softened so the hull does not follow every ripple.
            float pitch = Mathf.Atan2(hStern - hBow, halfLength * 2f) * Mathf.Rad2Deg * 0.6f * C.BoatBobAmount;
            float roll = Mathf.Atan2(hPort - hStar, halfWidth * 2f) * Mathf.Rad2Deg * 0.35f * C.BoatBobAmount;
            Bob = h;
            transform.SetPositionAndRotation(new Vector3(anchor.x, h, anchor.z),
                bow * Quaternion.Euler(pitch, 0f, roll) * Quaternion.Euler(0f, modelYawOffset, 0f));
        }
    }
}
