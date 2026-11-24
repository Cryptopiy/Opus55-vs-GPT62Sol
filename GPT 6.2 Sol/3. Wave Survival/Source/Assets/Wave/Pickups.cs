using System;
using UnityEngine;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed class Pickups
    {
        struct Supply
        {
            public Transform Root, Model;
            public Vector3 Origin;
            public float Born;
            public bool Health;
        }
        readonly Supply[] items = new Supply[PickupPoolSize];
        readonly CoreGame game;

        public Pickups(CoreGame game)
        {
            this.game = game;
            var root = Geometry.Root("Pooled pickups", game.transform);
            var baseMaterial = game.Arena.Dark;
            Material medical = Geometry.Lit("Medical red", Geometry.Hex(0xff4c56), .3f, 0, 2);
            Material ammo = Geometry.Lit("Ammo blue", Geometry.Hex(0x509cff), .3f, 0, 2);
            for (int i = 0; i < items.Length; i++)
            {
                bool health = i % 2 == 0;
                var shell = Geometry.Root(health ? "Field medkit" : "Ammunition", root);
                var model = Geometry.Root("Pickup model", shell);
                Geometry.Part(model, "Chassis", Vector3.zero, new(.62f, .23f, .47f), baseMaterial);
                Geometry.Part(model, "Face plate", new(0, .115f, 0), new(.51f, .035f, .37f), health ? game.Arena.Metal : ammo);
                if (health)
                {
                    Geometry.Part(model, "Cross horizontal", new(0, .15f, 0), new(.38f, .032f, .09f), medical);
                    Geometry.Part(model, "Cross vertical", new(0, .15f, 0), new(.09f, .032f, .31f), medical);
                    Geometry.Part(model, "Forward cross horizontal", new(0, 0, .24f), new(.27f, .058f, .024f), medical);
                    Geometry.Part(model, "Forward cross vertical", new(0, 0, .24f), new(.062f, .17f, .024f), medical);
                }
                else
                {
                    for (int line = -1; line <= 1; line++)
                        Geometry.Part(model, "Charge stripe", new(line * .15f, .15f, 0), new(.065f, .04f, .29f), game.Arena.Dark);
                    Geometry.Part(model, "Status strip", new(0, .025f, .24f), new(.38f, .055f, .024f), ammo);
                }
                items[i].Root = shell;
                items[i].Model = model;
                items[i].Health = health;
                shell.gameObject.SetActive(false);
            }
        }

        public void Drop(Vector3 point, bool health)
        {
            int index = -1;
            float oldest = float.PositiveInfinity;
            for (int i = health ? 0 : 1; i < items.Length; i += 2)
            {
                if (!items[i].Root.gameObject.activeSelf) { index = i; break; }
                if (items[i].Born >= oldest) continue;
                oldest = items[i].Born;
                index = i;
            }
            ref Supply item = ref items[index];
            item.Born = Time.time;
            item.Origin = point + Vector3.up * .4f;
            item.Root.position = item.Origin;
            item.Root.gameObject.SetActive(true);
            item.Model.gameObject.SetActive(true);
        }

        public void Tick()
        {
            foreach (ref Supply item in items.AsSpan())
            {
                if (!item.Root.gameObject.activeSelf) continue;
                float age = Time.time - item.Born;
                if (age >= DropLife)
                {
                    item.Root.gameObject.SetActive(false);
                    continue;
                }
                if ((game.Player.Position + Vector3.up * .4f - item.Origin).sqrMagnitude < 1.15f)
                {
                    if (item.Health) game.Player.Heal(DropHeal);
                    else game.Guns.AddAmmo(DropRifle, DropShells);
                    item.Root.gameObject.SetActive(false);
                    game.PickupSound();
                    continue;
                }
                item.Model.gameObject.SetActive(age < DropLife - DropBlink || Mathf.Repeat(age * 5, 1) < .62f);
                item.Root.SetPositionAndRotation(item.Origin + Vector3.up * (.14f * Mathf.Sin(age * 3)),
                    Quaternion.Euler(16, age * 84, 0));
            }
        }

        public void Clear()
        {
            foreach (Supply item in items) item.Root.gameObject.SetActive(false);
        }
    }
}

