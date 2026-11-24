using UnityEngine;

namespace WaveSurvival
{
    internal static class Geometry
    {
        static readonly System.Collections.Generic.List<Object> assets = new();

        public static T Own<T>(T asset) where T : Object
        {
            assets.Add(asset);
            return asset;
        }

        public static void ReleaseAssets()
        {
            foreach (Object asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
        }

        public static Transform Root(string name, Transform parent = null)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        static Material litTemplate;

        public static Material Lit(string name, Color color, float smoothness = .25f, float metallic = 0, float emission = 0)
        {
            if (litTemplate == null) litTemplate = Resources.Load<Material>("WaveLit");
            var material = Own(new Material(litTemplate) { name = name });
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            return material;
        }

        public static Transform Part(Transform parent, string name, Vector3 position, Vector3 size,
            Material material, PrimitiveType shape = PrimitiveType.Cube, bool solid = false)
        {
            var item = GameObject.CreatePrimitive(shape);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = size;
            item.layer = solid ? GameConfig.WorldLayer : GameConfig.ArtLayer;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
            {
                var collider = item.GetComponent<Collider>();
                collider.enabled = false;
                Object.Destroy(collider);
            }
            else if (shape == PrimitiveType.Cylinder)
            {
                // A primitive cylinder comes with a capsule. Bake the actual column silhouette.
                var capsule = item.GetComponent<CapsuleCollider>();
                capsule.enabled = false;
                Object.Destroy(capsule);
                item.AddComponent<MeshCollider>().sharedMesh = item.GetComponent<MeshFilter>().sharedMesh;
            }
            return item.transform;
        }

        public static Color Hex(uint rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        }
    }
}



