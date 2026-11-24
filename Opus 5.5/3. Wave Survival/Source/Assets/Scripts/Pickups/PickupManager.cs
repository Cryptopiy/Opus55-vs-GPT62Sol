using UnityEngine;
using UnityEngine.Rendering;

namespace WaveSurvival
{
    public enum PickupKind { Health, Ammo }

    /// <summary>
    /// S-22 drops: health pack (+25 HP, red cross; 30% chance, guaranteed from tanks) and ammo crate (+30 rifle /
    /// +6 shells, blue; 40% chance). Pooled; they rotate and bob, last 20 s, blink during the last 5 s and are collected
    /// by walking over them.
    /// </summary>
    public sealed class PickupManager : MonoBehaviour
    {
        struct Item
        {
            public GameObject Go;
            public Transform T;
            public PickupKind Kind;
            public Vector3 Base;
            public float Life, Phase;
            public bool Active;
        }

        Item[] items;
        int next;
        public int ActiveCount { get; private set; }

        public static PickupManager Create(Transform parent)
        {
            var go = new GameObject("Pickups");
            go.transform.SetParent(parent, false);
            PickupManager m = go.AddComponent<PickupManager>();
            m.Build();
            return m;
        }

        void Build()
        {
            items = new Item[GameConfig.Pickups.Pool];
            for (int i = 0; i < items.Length; i++)
            {
                PickupKind kind = i % 2 == 0 ? PickupKind.Health : PickupKind.Ammo;
                GameObject go = kind == PickupKind.Health ? BuildHealth() : BuildAmmo();
                go.transform.SetParent(transform, false);
                go.SetActive(false);
                items[i] = new Item { Go = go, T = go.transform, Kind = kind };
            }
        }

        static void Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(mesh.name);
            go.layer = GameConfig.Layers.Pickup;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        static GameObject BuildHealth()
        {
            var root = new GameObject("HealthPack") { layer = GameConfig.Layers.Pickup };
            Transform t = root.transform;
            Part(t, GameAssets.Cube, GameAssets.HealthBox, Vector3.zero, new Vector3(0.46f, 0.3f, 0.46f));
            foreach (float y in new[] { 0.152f })
            {
                Part(t, GameAssets.Cube, GameAssets.HealthCrossMat, new Vector3(0f, y, 0f), new Vector3(0.32f, 0.01f, 0.1f));
                Part(t, GameAssets.Cube, GameAssets.HealthCrossMat, new Vector3(0f, y, 0f), new Vector3(0.1f, 0.01f, 0.32f));
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Part(t, GameAssets.Cube, GameAssets.HealthCrossMat, new Vector3(0f, 0f, s * 0.232f), new Vector3(0.22f, 0.07f, 0.01f));
                Part(t, GameAssets.Cube, GameAssets.HealthCrossMat, new Vector3(0f, 0f, s * 0.232f), new Vector3(0.07f, 0.22f, 0.01f));
                Part(t, GameAssets.Cube, GameAssets.HealthCrossMat, new Vector3(s * 0.232f, 0f, 0f), new Vector3(0.01f, 0.22f, 0.07f));
                Part(t, GameAssets.Cube, GameAssets.HealthCrossMat, new Vector3(s * 0.232f, 0f, 0f), new Vector3(0.01f, 0.07f, 0.22f));
            }
            return root;
        }

        static GameObject BuildAmmo()
        {
            var root = new GameObject("AmmoCrate") { layer = GameConfig.Layers.Pickup };
            Transform t = root.transform;
            Part(t, GameAssets.Cube, GameAssets.AmmoBox, Vector3.zero, new Vector3(0.5f, 0.3f, 0.36f));
            Part(t, GameAssets.Cube, GameAssets.AmmoGlow, new Vector3(0f, 0.05f, 0f), new Vector3(0.52f, 0.05f, 0.38f));
            for (int i = 0; i < 3; i++)
            {
                Part(t, GameAssets.Cylinder, GameAssets.Brass, new Vector3(-0.1f + i * 0.1f, 0.2f, 0f), new Vector3(0.045f, 0.06f, 0.045f));
                Part(t, GameAssets.Sphere, GameAssets.Brass, new Vector3(-0.1f + i * 0.1f, 0.26f, 0f), Vector3.one * 0.045f);
            }
            return root;
        }

        public void RollDrops(EnemyBase e)
        {
            if (e.Type == EnemyType.Warden) return;
            bool health = e.Type == EnemyType.Tank || Random.value < GameConfig.Pickups.HealthChance;
            bool ammo = Random.value < GameConfig.Pickups.AmmoChance;
            Vector3 side = e.transform.right * 0.45f;
            if (health) Spawn(PickupKind.Health, e.transform.position + (ammo ? side : Vector3.zero));
            if (ammo) Spawn(PickupKind.Ammo, e.transform.position - (health ? side : Vector3.zero));
        }

        public void Spawn(PickupKind kind, Vector3 position)
        {
            // Rest on whatever is below (floor, platform or ramp).
            Vector3 ground = position;
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, GameConfig.Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                ground = hit.point;
            int idx = -1;
            for (int k = 0; k < items.Length; k++)
            {
                int i = (next + k) % items.Length;
                if (!items[i].Active && items[i].Kind == kind) { idx = i; break; }
            }
            if (idx < 0)
            {
                // Pool exhausted: recycle the oldest pickup of this kind.
                for (int k = 0; k < items.Length; k++)
                {
                    int i = (next + k) % items.Length;
                    if (items[i].Kind == kind) { idx = i; Deactivate(ref items[i]); break; }
                }
            }
            next = (idx + 1) % items.Length;
            ref Item it = ref items[idx];
            it.Base = ground + Vector3.up * GameConfig.Pickups.HoverHeight;
            it.Life = GameConfig.Pickups.Lifetime;
            it.Phase = Random.value * 6.28f;
            it.Active = true;
            ActiveCount++;
            it.T.position = it.Base;
            it.Go.SetActive(true);
        }

        void Deactivate(ref Item it)
        {
            if (!it.Active) return;
            it.Active = false;
            ActiveCount--;
            it.Go.SetActive(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            PlayerRig player = Game.Player;
            float time = Time.time;
            for (int i = 0; i < items.Length; i++)
            {
                ref Item it = ref items[i];
                if (!it.Active) continue;
                it.Life -= dt;
                if (it.Life <= 0f) { Deactivate(ref it); continue; }
                it.T.position = it.Base + Vector3.up * (Mathf.Sin(time * GameConfig.Pickups.BobSpeed + it.Phase) * GameConfig.Pickups.BobHeight);
                it.T.rotation = Quaternion.Euler(0f, time * GameConfig.Pickups.SpinSpeed + it.Phase * 57f, 0f);
                // Blink faster and faster during the last 5 s.
                bool visible = true;
                if (it.Life < GameConfig.Pickups.BlinkTime)
                {
                    float rate = Mathf.Lerp(10f, 3f, it.Life / GameConfig.Pickups.BlinkTime);
                    visible = Mathf.Sin(time * rate * Mathf.PI) > -0.2f;
                }
                SetVisible(it.T, visible);
                if (player != null && player.IsAlive && Game.IsPlaying &&
                    MathUtil.FlatDistance(player.Position, it.Base) < GameConfig.Pickups.CollectRadius &&
                    Mathf.Abs(player.Position.y + 0.5f - it.Base.y) < 1.4f && TryCollect(it.Kind, player))
                {
                    Game.Fx.PickupBurst(it.Base, it.Kind == PickupKind.Health ? new Color(1f, 0.15f, 0.1f) : new Color(0.2f, 0.5f, 1f));
                    Deactivate(ref it);
                }
            }
        }

        static void SetVisible(Transform t, bool on)
        {
            for (int c = 0; c < t.childCount; c++)
            {
                Transform child = t.GetChild(c);
                if (child.gameObject.activeSelf != on) child.gameObject.SetActive(on);
            }
        }

        static bool TryCollect(PickupKind kind, PlayerRig player)
        {
            if (kind == PickupKind.Health)
            {
                if (!player.Health.Heal(GameConfig.Pickups.HealthAmount)) return false; // full health: leave it
                Game.Audio.Play2D(SoundId.PickupHealth, 0.9f);
                Game.Hud.Notify("+25 HEALTH", new Color(1f, 0.35f, 0.3f));
                return true;
            }
            int a = player.Weapons.AddAmmo(WeaponId.Rifle, GameConfig.Pickups.AmmoRifle);
            int b = player.Weapons.AddAmmo(WeaponId.Shotgun, GameConfig.Pickups.AmmoShells);
            if (a + b <= 0) return false; // both reserves full: leave it
            Game.Audio.Play2D(SoundId.PickupAmmo, 0.9f);
            Game.Hud.Notify("+AMMO", new Color(0.4f, 0.65f, 1f));
            return true;
        }
    }
}
