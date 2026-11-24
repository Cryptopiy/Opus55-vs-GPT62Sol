using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using static DestructionRange.SandboxConfig;

namespace DestructionRange
{
    internal sealed class DetailRun
    {
        readonly Sandbox game;
        readonly Keyboard keys;
        bool passed = true;

        public DetailRun(Sandbox game)
        {
            this.game = game;
            keys = InputSystem.AddDevice<Keyboard>("Sandbox movement probe");
            game.ProbeKeyboard = keys;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        }

        public IEnumerator Execute()
        {
            yield return new WaitForSeconds(.5f);
            yield return Movement();
            yield return Materials();
            yield return Fracture();
            yield return Fuse();
            Debug.Log($"DETAIL result pass={passed}");
            SandboxProbe.Exit(passed ? 0 : 1);
        }

        IEnumerator Movement()
        {
            float z = game.Player.Position.z;
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.W));
            yield return null;
            float start = Time.time;
            z = game.Player.Position.z;
            yield return new WaitForSeconds(.45f);
            float walking = (game.Player.Position.z - z) / (Time.time - start);
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.W, Key.LeftShift));
            yield return null;
            start = Time.time;
            z = game.Player.Position.z;
            yield return new WaitForSeconds(.45f);
            float sprinting = (game.Player.Position.z - z) / (Time.time - start);
            float standing = game.Player.Position.y;
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Space));
            yield return null;
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            float highest = standing;
            float land = Time.time + 1.15f;
            while (Time.time < land)
            {
                highest = Mathf.Max(highest, game.Player.Position.y);
                yield return null;
            }
            float jump = highest - standing;
            Check($"movement walk={walking:F2} sprint={sprinting:F2} jump={jump:F3}", Mathf.Abs(walking - Move) < .45f &&
                Mathf.Abs(sprinting - Sprint) < .45f && Mathf.Abs(jump - Jump) < .045f);
        }

        IEnumerator Materials()
        {
            bool correct = true;
            for (int layout = 0; layout < LayoutNames.Length; layout++)
            {
                game.Rebuild((Layout)layout);
                yield return null;
                var totals = new int[MatterSpecs.Length];
                int wedges = 0;
                bool valid = true;
                for (int i = 0; i < game.Pieces.Total; i++)
                {
                    var p = game.Pieces.TestBlock(i);
                    totals[(int)p.Kind]++;
                    var spec = MatterSpecs[(int)p.Kind];
                    var material = p.Box.enabled ? p.Box.sharedMaterial : p.Wedge.sharedMaterial;
                    valid &= Mathf.Approximately(material.dynamicFriction, spec.Friction) &&
                        Mathf.Approximately(material.staticFriction, spec.Friction) && Mathf.Approximately(material.bounciness, spec.Bounce);
                    if (p.Box.enabled)
                        valid &= Mathf.Approximately(p.Body.mass, p.Size.x * p.Size.y * p.Size.z * spec.Density);
                    else
                    {
                        wedges++;
                        valid &= p.Wedge.enabled && p.Wedge.sharedMesh != null && p.Wedge.convex;
                    }
                }
                correct &= valid;
                if ((Layout)layout == Layout.Tower)
                    correct &= totals[0] == TowerWidth * TowerWidth * StoneFloors &&
                        totals[1] == TowerWidth * TowerWidth * (WoodEndFloor - StoneFloors);
                if ((Layout)layout == Layout.Arch)
                    correct &= wedges == ArchStones && totals[2] > 0;
                if ((Layout)layout == Layout.Pyramid)
                    correct &= game.Pieces.Total == BlockCapacity && totals[2] == PyramidGlass;
                Check($"layout={game.Current} stone={totals[0]} wood={totals[1]} glass={totals[2]} wedges={wedges} rigidMaterial={valid}", correct);
            }
        }

        IEnumerator Fracture()
        {
            var parent = game.Pieces.TestBlock(game.Pieces.Total - 1);
            // Isolate the specimen so the unsupported pyramid does not wake during the retirement check.
            for (int i = 0; i < game.Pieces.Total - 1; i++)
                game.Pieces.TestBlock(i).gameObject.SetActive(false);
            Physics.SyncTransforms();
            parent.Body.position = new Vector3(18, 2.5f, 0);
            float threshold = MatterSpecs[(int)parent.Kind].Threshold;
            Vector3 p = parent.Body.position;
            game.Pieces.Impact(parent, threshold, 0, p);
            game.Pieces.Step(0);
            bool strict = parent.gameObject.activeSelf && game.Pieces.ActiveShards == 0;
            bool displaced = game.Run.Destroyed == 1;
            float mass = parent.Body.mass;
            Vector3 orientationSpeed = new(1, 2, 3);
            Vector3 velocity = new(3, 1, 2);
            parent.Body.angularVelocity = orientationSpeed;
            parent.Body.linearVelocity = velocity;
            game.Pieces.Impact(parent, threshold + .01f, 0, p);
            game.Pieces.Step(0);
            bool shares = game.Pieces.ActiveShards == ShardsPerBreak;
            foreach (var shard in game.Pieces.TestShards())
            {
                Vector3 expected = velocity + Vector3.Cross(orientationSpeed, shard.Body.position - p);
                shares &= Mathf.Approximately(shard.Body.mass, mass / ShardsPerBreak) &&
                    (shard.Body.angularVelocity - orientationSpeed).sqrMagnitude < .0001f &&
                    (shard.Body.linearVelocity - expected).sqrMagnitude < .0001f;
            }
            Check($"fracture strictGreater={strict} sharesAndMotion={shares} shards={game.Pieces.ActiveShards} displaced={displaced} counted={game.Run.Destroyed} score={game.Run.Score}",
                strict && shares && displaced && game.Run.Destroyed == 1 && game.Run.Score == MatterSpecs[(int)Matter.Glass].Points);

            var fragment = game.Pieces.TestNewest;
            for (int depth = 0; depth < 2; depth++)
            {
                game.Pieces.Impact(fragment, threshold + 1, 0, fragment.Body.position);
                game.Pieces.Step(0);
                fragment = game.Pieces.TestNewest;
            }
            int count = game.Pieces.ActiveShards;
            game.Pieces.Impact(fragment, threshold + 1, 0, fragment.Body.position);
            game.Pieces.Step(0);
            Check($"subdivision minimum={fragment.Size:F3} countBefore={count} countAfter={game.Pieces.ActiveShards}",
                fragment.Size.x < SmallestShard && fragment.Size.y < SmallestShard && fragment.Size.z < SmallestShard &&
                count == ShardsPerBreak + 2 * (ShardsPerBreak - 1) && game.Pieces.ActiveShards == count);

            int index = 0;
            foreach (var shard in game.Pieces.TestShards())
            {
                shard.Body.rotation = Quaternion.identity;
                shard.Body.position = new Vector3(19 + index % 6 * 1.7f, shard.Size.y * .5f, -6 + index / 6 * 1.7f);
                shard.Body.linearVelocity = Vector3.zero;
                shard.Body.angularVelocity = Vector3.zero;
                index++;
            }
            Physics.SyncTransforms();
            foreach (var shard in game.Pieces.TestShards()) shard.Body.Sleep();
            yield return new WaitForSeconds(ShardWait + .25f);
            bool shrinking = fragment.gameObject.activeSelf && fragment.transform.localScale.x < fragment.BaseScale.x;
            yield return new WaitForSeconds(ShardShrink + .05f);
            Check($"shard retirement shrinking={shrinking} returned={count - game.Pieces.ActiveShards}", shrinking && game.Pieces.ActiveShards == 0);
        }

        IEnumerator Fuse()
        {
            game.Restart();
            game.Guns.Select(Weapon.Shell);
            game.Player.Aim(new Vector3(0, 58, -28));
            float firedAt = Time.time;
            bool fired = game.Guns.Fire();
            bool locked = !game.Guns.Fire();
            while (game.Guns.Detonations == 0 && Time.time - firedAt < ShellFuse + .2f) yield return null;
            float elapsed = Time.time - firedAt;
            bool ammo = game.Guns.Ammo == Weapons[(int)Weapon.Shell].Ammo - 1;
            game.Restart();
            bool refilled = game.Guns.Ammo == Weapons[(int)Weapon.Shell].Ammo;
            Check($"shell fuse={elapsed:F3} cooldownLocked={locked} ammoCounted={ammo} refilled={refilled}",
                fired && locked && ammo && refilled && Mathf.Abs(elapsed - ShellFuse) < .1f);
        }

        void Check(string label, bool result)
        {
            passed &= result;
            Debug.Log($"DETAIL {label} pass={result}");
        }
    }
}
