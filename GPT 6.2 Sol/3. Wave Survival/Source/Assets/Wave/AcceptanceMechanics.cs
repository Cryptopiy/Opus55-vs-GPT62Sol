using System.Collections.Generic;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed partial class Acceptance
    {
        Enemy testTank;
        Transform medkit, spare;
        float deathAt, hurtAt, supplyAt;
        Vector3 beforeCharge;
        bool tankWound, tankRush, earlyCorpse, blink, ammoObserved;

        void Mechanics()
        {
            float dt = Time.deltaTime;
            float time = Time.time;
            PlayerMotor player = game.Player;
            switch (stage)
            {
                case 0:
                    game.ClearEnemies();
                    game.Waves.Break(StartWave);
                    game.SetTestTime(2);
                    player.Invulnerable = false;
                    player.Place(new Vector3(-21, .02f, -19), Vector3.forward);
                    beforeCharge = player.Position;
                    if (Arena.Sample(new Vector3(-21, 0, -11), out Vector3 point)) testTank = game.Spawn(EnemyKind.Tank, point);
                    Debug.Log($"WAVE_CHECK geometry floor={game.Arena.Root.Find("Floor").localScale} " +
                        $"wall={game.Arena.Root.Find("Perimeter wall/Concrete wall").localScale.y} " +
                        $"platform={game.Arena.Root.Find("Central platform").localScale} lamps={game.Arena.Root.GetComponentsInChildren<Light>().Length}");
                    timer = time;
                    Next();
                    return;
                case 1:
                    player.Tick(dt, Vector2.zero, Vector2.zero, false, false, false, false);
                    if (testTank == null)
                    {
                        Debug.Log("WAVE_CHECK mechanics tank spawn failed");
                        Finish();
                        return;
                    }
                    tankWound |= testTank.State == EnemyState.Attack;
                    tankRush |= testTank.State == EnemyState.Charge;
                    if (player.HP >= Health && time - timer < 7) return;
                    hurtAt = time;
                    Next();
                    return;
                case 2:
                    player.Tick(dt, Vector2.zero, Vector2.zero, false, false, false, false);
                    if (time < hurtAt + .32f) return;
                    Debug.Log($"WAVE_CHECK tankDamage hp={player.HP} telegraph={tankWound} charge={tankRush} " +
                        $"state={testTank.State} knockback={(player.Position - beforeCharge).magnitude:F2} stunArmor={testTank.Armor(testTank.Root.forward):F2}");
                    CheckSound();
                    player.Invulnerable = true;
                    testTank.Hurt(testTank.Maximum * 4, player.Position, false);
                    deathAt = time;
                    medkit = FindSupply("Field medkit");
                    if (medkit != null)
                        player.Place(medkit.position - Vector3.up * .4f, Vector3.forward);
                    Next();
                    return;
                case 3:
                    Debug.Log($"WAVE_CHECK tankDrop found={medkit != null} healHP={player.HP} corpse={testTank.State}");
                    player.Place(new Vector3(19, .02f, -20), Vector3.forward);
                    GenerateSupplies();
                    Next();
                    return;
                case 4:
                    if (time < deathAt + CorpseTime - .1f) earlyCorpse |= testTank.Active;
                    if (!ammoObserved)
                    {
                        ammoObserved = true;
                        Debug.Log($"WAVE_CHECK ammoPickup rifle={game.Guns.ReserveFor(1)} shotgun={game.Guns.ReserveFor(2)}");
                        player.Place(new Vector3(19, .02f, -20), Vector3.forward);
                    }
                    if (time - supplyAt >= DropLife - DropBlink && spare != null && spare.gameObject.activeSelf)
                        blink |= !spare.GetChild(0).gameObject.activeSelf;
                    if (time - supplyAt < DropLife + .12f) return;
                    Debug.Log($"WAVE_CHECK corpseBefore={earlyCorpse} corpseExpired={!testTank.Active} " +
                        $"dropBlink={blink} dropExpired={spare != null && !spare.gameObject.activeSelf}");
                    Finish();
                    return;
            }
        }

        void GenerateSupplies()
        {
            UnityEngine.Random.InitState(821);
            for (int i = 0; i < 8; i++)
            {
                Vector3 guess = new(-17 + i * 2.3f, 0, -20);
                if (!Arena.Sample(guess, out Vector3 plant)) continue;
                Enemy enemy = game.Spawn(EnemyKind.Tank, plant);
                enemy?.Hurt(EnemyHealth[(int)EnemyKind.Tank] * 4, game.Player.Position, false);
            }

            Transform ammo = FindSupply("Ammunition");
            spare = FindSupply("Field medkit", ammo == null ? null : ammo.position);
            supplyAt = Time.time;
            Debug.Log($"WAVE_CHECK ammoDrop found={ammo != null} rifleBefore={game.Guns.ReserveFor(1)} shotgunBefore={game.Guns.ReserveFor(2)}");
            if (ammo != null) game.Player.Place(ammo.position - Vector3.up * .4f, Vector3.forward);
        }

        Transform FindSupply(string name, Vector3? awayFrom = null)
        {
            Transform pool = game.transform.Find("Pooled pickups");
            for (int i = 0; i < pool.childCount; i++)
            {
                Transform item = pool.GetChild(i);
                if (item.gameObject.activeSelf && item.name == name &&
                    (!awayFrom.HasValue || (item.position - awayFrom.Value).sqrMagnitude > 4)) return item;
            }
            return null;
        }

        void CheckSound()
        {
            var expected = new HashSet<string>(System.Enum.GetNames(typeof(Sound)));
            var data = new float[1024];
            int generated = 0;
            foreach (AudioClip clip in Resources.FindObjectsOfTypeAll<AudioClip>())
            {
                if (!expected.Remove(clip.name) || !clip.GetData(data, 0)) continue;
                foreach (float sample in data)
                    if (Mathf.Abs(sample) > .001f)
                    {
                        generated++;
                        break;
                    }
            }

            bool positional = false;
            Transform audio = game.transform.Find("Synthesized audio");
            foreach (AudioSource source in audio.GetComponentsInChildren<AudioSource>())
                positional |= source.clip != null && source.clip.name == nameof(Sound.Tank) && source.spatialBlend == 1;
            Debug.Log($"WAVE_CHECK audio synthesizedClips={generated} tankPositional={positional}");
        }

        void LogCounters()
        {
            var available = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(available);
            foreach (ProfilerRecorderHandle handle in available)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                string name = description.Name;
                if (name.StartsWith("GC.") || name.Contains("Draw Calls") || name.Contains("Allocated In Frame"))
                    Debug.Log($"WAVE_CHECK counter {description.Category.Name}/{name} {description.UnitType}");
            }
        }
    }
}

