using System.Collections.Generic;
using UnityEngine;
using Wpn = DestructionSandbox.GameConfig.Weapons;

namespace DestructionSandbox
{
    /// <summary>Fixed pools of cannonballs, shells and orbs. When a pool is exhausted the oldest one is reused.</summary>
    public sealed class ProjectileManager : MonoBehaviour
    {
        private readonly List<Projectile>[] pools = new List<Projectile>[Wpn.WeaponCount];
        private readonly List<Projectile>[] active = new List<Projectile>[Wpn.WeaponCount];

        public int TotalPooled { get; private set; }

        public void Init(MaterialLibrary materials, MeshLibrary meshes)
        {
            int[] sizes = { Wpn.CannonballPool, Wpn.ShellPool, Wpn.OrbPool };
            for (int t = 0; t < Wpn.WeaponCount; t++)
            {
                pools[t] = new List<Projectile>(sizes[t]);
                active[t] = new List<Projectile>(sizes[t]);
                for (int i = 0; i < sizes[t]; i++)
                {
                    pools[t].Add(Projectile.Create(transform, (WeaponType)t, i, materials, meshes));
                    TotalPooled++;
                }
            }
        }

        public Projectile Spawn(WeaponType type, Vector3 position, Vector3 velocity)
        {
            int t = (int)type;
            Projectile p;
            if (pools[t].Count > 0)
            {
                p = pools[t][pools[t].Count - 1];
                pools[t].RemoveAt(pools[t].Count - 1);
            }
            else
            {
                p = active[t][0];                 // oldest in flight
                active[t].RemoveAt(0);
                p.Deactivate();
            }
            p.Launch(position, velocity);
            active[t].Add(p);
            return p;
        }

        public void Recycle(Projectile p)
        {
            if (!p.InUse) return;
            int t = (int)p.Type;
            active[t].Remove(p);
            p.Deactivate();
            pools[t].Add(p);
        }

        public void ClearAll()
        {
            for (int t = 0; t < Wpn.WeaponCount; t++)
            {
                for (int i = active[t].Count - 1; i >= 0; i--)
                {
                    Projectile p = active[t][i];
                    p.Deactivate();
                    pools[t].Add(p);
                }
                active[t].Clear();
            }
        }

        /// <summary>X key: remove spent cannonballs lying around (debris) but keep anything still in flight.</summary>
        public void ClearSpent()
        {
            List<Projectile> balls = active[(int)WeaponType.Cannonball];
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                Projectile p = balls[i];
                if (!p.HasImpacted) continue;
                balls.RemoveAt(i);
                p.Deactivate();
                pools[(int)WeaponType.Cannonball].Add(p);
            }
        }

        public int ActiveCount()
        {
            int n = 0;
            for (int t = 0; t < Wpn.WeaponCount; t++) n += active[t].Count;
            return n;
        }
    }
}
