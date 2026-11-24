using UnityEngine;
using UnityEngine.Rendering;

namespace WaveSurvival
{
    /// <summary>Pooled physics shell casing (S-12 shell ejection for rifle/pistol). Clinks once, then returns to the pool.</summary>
    public sealed class ShellCasing : MonoBehaviour
    {
        Rigidbody body;
        Transform visual;
        float life;
        bool clinked;
        ComponentPool<ShellCasing> owner;

        public static ShellCasing Create(Transform parent)
        {
            var go = new GameObject("Shell");
            go.SetActive(false);
            go.layer = GameConfig.Layers.Debris;
            go.transform.SetParent(parent, false);
            var vis = new GameObject("Mesh");
            vis.layer = GameConfig.Layers.Debris;
            vis.transform.SetParent(go.transform, false);
            vis.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            vis.AddComponent<MeshFilter>().sharedMesh = GameAssets.Cylinder;
            var mr = vis.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GameAssets.Brass;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var col = go.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = 0.006f;
            col.height = 0.04f;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.01f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            ShellCasing s = go.AddComponent<ShellCasing>();
            s.body = rb;
            s.visual = vis.transform;
            return s;
        }

        public void Launch(Vector3 pos, Vector3 vel, bool pistol, ComponentPool<ShellCasing> pool)
        {
            owner = pool;
            life = GameConfig.Fx.ShellLife;
            clinked = false;
            float len = pistol ? 0.019f : 0.045f;
            visual.localScale = new Vector3(0.011f, len * 0.5f, 0.011f);
            transform.SetPositionAndRotation(pos, Random.rotation);
            gameObject.SetActive(true);
            body.position = pos;
            body.linearVelocity = vel;
            body.angularVelocity = Random.insideUnitSphere * 25f;
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) owner.Release(this);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (clinked) return;
            clinked = true;
            Game.Audio?.PlayAt(SoundId.ShellClink, transform.position, 0.45f, Random.Range(0.85f, 1.2f));
        }
    }
}
