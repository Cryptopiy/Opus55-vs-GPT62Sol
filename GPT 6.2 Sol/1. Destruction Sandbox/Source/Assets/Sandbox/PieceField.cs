using UnityEngine;
using static DestructionRange.SandboxConfig;

namespace DestructionRange
{
    internal sealed class PieceField
    {
        readonly Sandbox game;
        readonly StructureLayout layouts;
        readonly PhysicsPiece[] blocks = new PhysicsPiece[BlockCapacity], shards = new PhysicsPiece[ShardCapacity];
        readonly PhysicsPiece[] queued = new PhysicsPiece[BlockCapacity + ShardCapacity];
        readonly int[] free = new int[ShardCapacity];
        readonly int[] fractured = new int[MatterSpecs.Length];
        readonly PhysicsMaterial[] friction = new PhysicsMaterial[MatterSpecs.Length];
        readonly Material[,] surfaces;
        readonly Mesh cube;
        int queuedCount, freeCount, oldest = -1, newest = -1;
        public int Total { get; private set; }
        public int ActiveBlocks { get; private set; }
        public int ActiveShards => ShardCapacity - freeCount;
        public int Fractured(Matter kind) => fractured[(int)kind];
        public int ShardEvictions { get; private set; }
        public int Sleeping
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Total; i++)
                    if (blocks[i].gameObject.activeSelf && blocks[i].Body.IsSleeping()) count++;
                for (int i = oldest; i >= 0; i = shards[i].Newer)
                    if (shards[i].Body.IsSleeping()) count++;
                return count;
            }
        }

        public PieceField(Sandbox game, Material[,] surfaces)
        {
            this.game = game;
            this.surfaces = surfaces;
            cube = game.MeshFor(PrimitiveType.Cube);
            for (int i = 0; i < MatterSpecs.Length; i++)
            {
                var spec = MatterSpecs[i];
                friction[i] = new PhysicsMaterial(((Matter)i).ToString())
                {
                    staticFriction = spec.Friction,
                    dynamicFriction = spec.Friction,
                    bounciness = spec.Bounce,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Average
                };
            }
            for (int i = 0; i < blocks.Length; i++) blocks[i] = Create("Block", i, false);
            for (int i = 0; i < shards.Length; i++)
            {
                shards[i] = Create("Shard", i, true);
                free[freeCount++] = shards.Length - i - 1;
            }
            layouts = new StructureLayout(this);
        }

        PhysicsPiece Create(string name, int slot, bool shard)
        {
            var go = game.Node(name + " " + slot);
            go.SetActive(false);
            go.layer = shard ? ShardLayer : BlockLayer;
            var piece = go.AddComponent<PhysicsPiece>();
            piece.Field = this;
            piece.Slot = slot;
            piece.Shard = shard;
            piece.Filter = go.AddComponent<MeshFilter>();
            piece.Renderer = go.AddComponent<MeshRenderer>();
            piece.Box = go.AddComponent<BoxCollider>();
            if (!shard)
            {
                piece.Wedge = go.AddComponent<MeshCollider>();
                piece.Wedge.enabled = false;
                piece.Wedge.convex = true;
            }
            piece.Body = go.AddComponent<Rigidbody>();
            piece.Body.solverIterations = shard ? DebrisSolver : Solver;
            piece.Body.solverVelocityIterations = shard ? DebrisVelocitySolver : VelocitySolver;
            piece.Body.sleepThreshold = SleepSpeed * SleepSpeed * .5f;
            piece.Body.maxLinearVelocity = TopSpeed;
            piece.Body.maxAngularVelocity = 24f;
            piece.Body.linearDamping = shard ? .15f : .025f;
            piece.Body.angularDamping = shard ? .18f : .06f;
            // Speculative contacts cover thin debris without enabling sweep CCD on every block.
            piece.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            return piece;
        }

        public void Reset(Layout layout)
        {
            ClearShards();
            for (int i = 0; i < Total; i++) blocks[i].gameObject.SetActive(false);
            Total = ActiveBlocks = queuedCount = 0;
            ShardEvictions = 0;
            System.Array.Clear(fractured, 0, fractured.Length);
            layouts.Build(layout);
            Physics.SyncTransforms();
            for (int i = 0; i < Total; i++) blocks[i].Body.Sleep();
        }

        public void AddBlock(Matter kind, Vector3 p, Quaternion rotation, Vector3 size, Mesh wedge = null, float volume = 0)
        {
            var piece = blocks[Total++];
            ActiveBlocks++;
            int variants = surfaces.GetLength(1);
            uint hash = unchecked(((uint)Total * 747796405u + 2891336453u) ^ ((uint)Total << 17));
            piece.Tint = (int)((hash ^ (hash >> 15)) % (uint)variants);
            float mass = (volume > 0 ? volume : size.x * size.y * size.z) * MatterSpecs[(int)kind].Density;
            Activate(piece, kind, p, rotation, size, wedge, mass);
        }

        void Activate(PhysicsPiece piece, Matter kind, Vector3 p, Quaternion rotation, Vector3 size, Mesh wedge, float mass)
        {
            piece.Start = p;
            piece.Kind = kind;
            piece.Size = size;
            piece.Mass = mass;
            piece.Counted = false;
            piece.Rest = piece.PendingImpulse = 0;
            piece.BaseScale = wedge == null ? size : Vector3.one;
            piece.transform.localScale = piece.BaseScale;
            piece.transform.SetPositionAndRotation(p, rotation);
            piece.Filter.sharedMesh = wedge != null ? wedge : cube;
            piece.Renderer.sharedMaterial = surfaces[(int)kind, piece.Tint];
            piece.Box.enabled = wedge == null;
            piece.Box.contactOffset = ContactOffset;
            piece.Box.sharedMaterial = friction[(int)kind];
            if (piece.Wedge != null)
            {
                if (wedge != null && piece.Wedge.sharedMesh != wedge) piece.Wedge.sharedMesh = wedge;
                piece.Wedge.enabled = wedge != null;
                piece.Wedge.contactOffset = ContactOffset;
                piece.Wedge.sharedMaterial = friction[(int)kind];
            }
            piece.gameObject.SetActive(true);
            piece.Body.position = p;
            piece.Body.rotation = rotation;
            piece.Body.mass = mass;
            piece.Body.linearVelocity = Vector3.zero;
            piece.Body.angularVelocity = Vector3.zero;
        }

        public void Impact(PhysicsPiece piece, float impulse, float relativeSpeedSquared, Vector3 point)
        {
            if (relativeSpeedSquared > .36f && impulse > 1)
                game.PieceImpact(piece.Kind, point, impulse);
            if (impulse > MatterSpecs[(int)piece.Kind].Threshold)
                QueueBreak(piece, impulse);
        }

        void QueueBreak(PhysicsPiece piece, float impulse)
        {
            if (piece.Shard && piece.Size.x < SmallestShard && piece.Size.y < SmallestShard && piece.Size.z < SmallestShard)
                return;
            if (piece.PendingImpulse == 0) queued[queuedCount++] = piece;
            piece.PendingImpulse = Mathf.Max(piece.PendingImpulse, impulse);
        }

        public void Step(float dt)
        {
            for (int i = 0; i < queuedCount; i++)
            {
                var piece = queued[i];
                if (piece.PendingImpulse > 0 && piece.gameObject.activeSelf) Break(piece);
            }
            queuedCount = 0;

            for (int i = 0; i < Total; i++)
            {
                var piece = blocks[i];
                if (!piece.gameObject.activeSelf || piece.Counted) continue;
                if ((piece.Body.position - piece.Start).sqrMagnitude > Displacement * Displacement)
                    Count(piece);
            }

            for (int i = oldest; i >= 0;)
            {
                var shard = shards[i];
                i = shard.Newer;
                if (!shard.Box.enabled || shard.Body.IsSleeping())
                    shard.Rest += dt;
                else
                    shard.Rest = 0;
                if (shard.Rest <= ShardWait) continue;
                float scale = 1 - (shard.Rest - ShardWait) / ShardShrink;
                if (scale <= 0)
                    Recycle(shard);
                else
                {
                    // Stop the shrinking decoration from catching other pieces or falling through the floor.
                    if (shard.Box.enabled)
                    {
                        shard.Box.enabled = false;
                        shard.Body.isKinematic = true;
                    }
                    shard.transform.localScale = shard.BaseScale * scale;
                }
            }
        }

        void Count(PhysicsPiece piece)
        {
            if (piece.Counted) return;
            piece.Counted = true;
            game.BlockCounted(piece.Kind, piece.Body.position);
        }

        void Break(PhysicsPiece piece)
        {
            Rigidbody body = piece.Body;
            Vector3 center = body.position, linear = body.linearVelocity, angular = body.angularVelocity;
            Quaternion rotation = body.rotation;
            Vector3 size = piece.Size / AxialCuts;
            Matter kind = piece.Kind;
            float mass = piece.Mass / ShardsPerBreak;
            float impact = piece.PendingImpulse;
            int tint = piece.Tint;
            piece.PendingImpulse = 0;
            if (piece.Shard) Recycle(piece);
            else
            {
                Count(piece);
                fractured[(int)kind]++;
                piece.gameObject.SetActive(false);
                ActiveBlocks--;
            }

            // Newly separated pieces inherit the rigid motion, including tangential speed around the centroid.
            for (int y = 0; y < AxialCuts; y++)
            for (int z = 0; z < AxialCuts; z++)
            for (int x = 0; x < AxialCuts; x++)
            {
                Vector3 offset = rotation * Vector3.Scale(size, new Vector3(x - .5f, y - .5f, z - .5f));
                PhysicsPiece shard = GetShard();
                shard.Tint = tint;
                Activate(shard, kind, center + offset, rotation, size, null, mass);
                shard.Body.linearVelocity = Vector3.ClampMagnitude(linear + Vector3.Cross(angular, offset), TopSpeed);
                shard.Body.angularVelocity = angular;
            }
            game.PieceBroke(kind, center, impact);
        }

        PhysicsPiece GetShard()
        {
            if (freeCount == 0)
            {
                Recycle(shards[oldest]);
                ShardEvictions++;
            }
            var shard = shards[free[--freeCount]];
            shard.Body.isKinematic = false;
            shard.Older = newest;
            shard.Newer = -1;
            if (newest >= 0) shards[newest].Newer = shard.Slot;
            else oldest = shard.Slot;
            newest = shard.Slot;
            return shard;
        }

        void Recycle(PhysicsPiece shard)
        {
            if (shard.Older >= 0) shards[shard.Older].Newer = shard.Newer;
            else oldest = shard.Newer;
            if (shard.Newer >= 0) shards[shard.Newer].Older = shard.Older;
            else newest = shard.Older;
            shard.PendingImpulse = 0;
            shard.gameObject.SetActive(false);
            free[freeCount++] = shard.Slot;
        }

        public void ClearShards()
        {
            while (oldest >= 0) Recycle(shards[oldest]);
        }

        public void Radial(Vector3 center, float radius, float strength, float lift, bool pull)
        {
            for (int i = 0; i < Total; i++)
                if (blocks[i].gameObject.activeSelf)
                    Affect(blocks[i], center, radius, strength, lift, pull);
            for (int i = oldest; i >= 0; i = shards[i].Newer)
                if (shards[i].Box.enabled)
                    Affect(shards[i], center, radius, strength, lift, pull);
        }

        void Affect(PhysicsPiece piece, Vector3 center, float radius, float strength, float lift, bool pull)
        {
            Rigidbody body = piece.Body;
            Vector3 away = body.position - center;
            float distance = away.magnitude;
            if (distance >= radius) return;
            if (pull)
                body.AddForce(-away.normalized * Mathf.Min(strength * body.mass, OrbMaxForce));
            else
            {
                float impulse = strength * (1 - distance / radius);
                Vector3 direction = (away + Vector3.up * lift).normalized;
                if (direction == Vector3.zero) direction = Vector3.up;
                body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity + direction * (impulse / body.mass), TopSpeed);
                if (impulse > MatterSpecs[(int)piece.Kind].Threshold) QueueBreak(piece, impulse);
            }
        }

        public void Inspect(out float drift, out float speed, out int invalid)
        {
            drift = speed = 0;
            invalid = 0;
            for (int i = 0; i < Total; i++)
            {
                var piece = blocks[i];
                if (!piece.gameObject.activeSelf) continue;
                drift = Mathf.Max(drift, Vector3.Distance(piece.Start, piece.Body.position));
                Check(piece, ref speed, ref invalid);
            }
            for (int i = oldest; i >= 0; i = shards[i].Newer) Check(shards[i], ref speed, ref invalid);
        }

        internal PhysicsPiece TestBlock(int index) => blocks[index];
        internal PhysicsPiece TestNewest => shards[newest];
        internal System.Collections.Generic.IEnumerable<PhysicsPiece> TestShards()
        {
            for (int i = oldest; i >= 0; i = shards[i].Newer) yield return shards[i];
        }

        static void Check(PhysicsPiece piece, ref float speed, ref int invalid)
        {
            Vector3 p = piece.Body.position;
            Vector3 v = piece.Body.linearVelocity;
            speed = Mathf.Max(speed, v.magnitude);
            if (!float.IsFinite(p.sqrMagnitude) || !float.IsFinite(v.sqrMagnitude) || p.y < -.3f) invalid++;
        }
    }
}
