using UnityEngine;
using UnityEngine.Rendering;

namespace WaveSurvival
{
    /// <summary>
    /// S-23 jump-slam telegraph: a red ring of the slam radius on the floor with an inner ring that grows to meet it
    /// exactly when the boss lands, so the player can read the timing.
    /// </summary>
    public sealed class TelegraphRing : MonoBehaviour
    {
        Transform outer, inner;
        MeshRenderer outerR, innerR;
        MaterialPropertyBlock mpb;
        float duration, t, radius;
        bool active;

        public static TelegraphRing Create(Transform parent)
        {
            var go = new GameObject("TelegraphRing");
            go.transform.SetParent(parent, false);
            TelegraphRing ring = go.AddComponent<TelegraphRing>();
            ring.mpb = new MaterialPropertyBlock();
            ring.outer = ring.Quad("Outer", out ring.outerR);
            ring.inner = ring.Quad("Inner", out ring.innerR);
            go.SetActive(false);
            return ring;
        }

        Transform Quad(string name, out MeshRenderer mr)
        {
            var q = new GameObject(name);
            q.transform.SetParent(transform, false);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lay the -Z facing quad flat, facing up
            q.AddComponent<MeshFilter>().sharedMesh = GameAssets.Quad;
            mr = q.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GameAssets.TelegraphRing;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return q.transform;
        }

        public void Show(Vector3 pos, float r, float time)
        {
            transform.position = pos + Vector3.up * 0.06f;
            radius = r;
            duration = Mathf.Max(0.05f, time);
            t = 0f;
            active = true;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            active = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!active) return;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float d = radius * 2f;
            outer.localScale = new Vector3(d, d, 1f);
            float innerD = Mathf.Max(0.05f, d * k);
            inner.localScale = new Vector3(innerD, innerD, 1f);
            float pulse = 0.65f + 0.35f * Mathf.Sin(t * 30f);
            mpb.SetColor(MaterialFactory.BaseColor, new Color(1f, 0.15f, 0.06f, 0.55f + 0.45f * k) * pulse * 2f);
            outerR.SetPropertyBlock(mpb);
            mpb.SetColor(MaterialFactory.BaseColor, new Color(1f, 0.35f, 0.1f, 0.8f) * 1.6f);
            innerR.SetPropertyBlock(mpb);
            if (t >= duration + 0.15f) Hide();
        }
    }
}
