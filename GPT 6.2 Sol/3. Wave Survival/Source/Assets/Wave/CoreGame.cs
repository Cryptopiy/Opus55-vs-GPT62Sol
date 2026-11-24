using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal enum MatchState { Playing, Paused, Dead, Victory }

    internal sealed partial class CoreGame : MonoBehaviour
    {
        public Arena Arena { get; private set; }
        public PlayerMotor Player { get; private set; }
        public Weapons Guns { get; private set; }
        public WaveDirector Waves { get; private set; }
        public MatchStats Stats { get; } = new();
        public MatchState State { get; private set; }
        public Material[][] EnemyPalette { get; private set; }
        public readonly Vector3[] PathCorners = new Vector3[64];
        public readonly Enemy[] Enemies = new Enemy[EnemyPoolSize];
        public int Alive { get; private set; }
        public int ProjectilesActive => projectiles.Active;
        public bool DebugStats { get; set; }
        public bool DebugAI { get; set; }
        public bool Endless { get; private set; }
        public float HitUntil { get; private set; }
        public bool KillMarker { get; private set; }
        Projectiles projectiles;
        Pickups pickups;
        int livingSlots;
        float playScale = 1;
        Acceptance acceptance;

        void Awake()
        {
            QualitySettings.vSyncCount = VSync;
            Physics.IgnoreLayerCollision(PlayerLayer, EnemyLayer);
            Physics.IgnoreLayerCollision(EnemyLayer, EnemyLayer);
            Arena = new Arena();
            Player = new PlayerMotor();
            EnemyPalette = Enemy.Palette(Arena.Dark);
            Guns = new Weapons(this);
            projectiles = new Projectiles(this);
            pickups = new Pickups(this);
            SetupEffects();
            Waves = new WaveDirector(this);
            SetupHud();
            SetState(MatchState.Playing);
            acceptance = Acceptance.TryStart(this);
        }

        void Update()
        {
            acceptance?.UpdateTestInput();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (State == MatchState.Playing) SetState(MatchState.Paused);
                    else if (State == MatchState.Paused) Resume();
                }
                if (keyboard.f1Key.wasPressedThisFrame) DebugStats = !DebugStats;
                if (keyboard.f2Key.wasPressedThisFrame) DebugAI = !DebugAI;
                if (keyboard.f8Key.wasPressedThisFrame) Hud.ToggleVisibility();
                if (State == MatchState.Playing && keyboard.f3Key.wasPressedThisFrame)
                {
                    if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) Player.Invulnerable = !Player.Invulnerable;
                    else SkipToBoss();
                }
            }
            acceptance?.BeforeTick();
            if (State == MatchState.Playing)
            {
                float dt = Time.deltaTime;
                Stats.Time += dt;
                if (acceptance == null || !acceptance.ControlsPlayer) UpdatePlayer(keyboard, mouse, dt);
                Physics.SyncTransforms();
                int slots = livingSlots;
                for (int i = 0; i < slots; i++) if (Enemies[i].Active) Enemies[i].Tick(dt);
                projectiles.Tick(dt);
                pickups.Tick();
                Waves.Tick();
                UpdateEffects(dt);
            }
            UpdateHud();
            if (keyboard?.f12Key.wasPressedThisFrame == true) Screenshot();
        }

        void UpdatePlayer(Keyboard keyboard, Mouse mouse, float dt)
        {
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) Guns.Select(0);
                if (keyboard.digit2Key.wasPressedThisFrame) Guns.Select(1);
                if (keyboard.digit3Key.wasPressedThisFrame) Guns.Select(2);
            }
            float scroll = mouse?.scroll.ReadValue().y ?? 0;
            if (Mathf.Abs(scroll) > .001f) Guns.Cycle(scroll < 0 ? 1 : -1);
            UpdateMotor(keyboard, mouse, dt);
            Physics.SyncTransforms();
            Guns.Tick(dt, mouse?.leftButton.wasPressedThisFrame ?? false, mouse?.leftButton.isPressed ?? false,
                keyboard?.rKey.wasPressedThisFrame ?? false);
        }

        void UpdateMotor(Keyboard keyboard, Mouse mouse, float dt)
        {
            bool movingStep = Player.Tick(dt, PlayerMotor.Movement(keyboard), mouse?.delta.ReadValue() ?? Vector2.zero,
                keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed),
                keyboard?.cKey.isPressed ?? false, keyboard?.spaceKey.wasPressedThisFrame ?? false,
                Guns.CanAim && (mouse?.rightButton.isPressed ?? false));
            if (movingStep) Footstep();
        }

        public void SetTestTime(float scale)
        {
            playScale = scale;
            if (State == MatchState.Playing) Time.timeScale = scale;
        }

        void SetState(MatchState state)
        {
            State = state;
            bool playing = state == MatchState.Playing;
            Time.timeScale = playing ? playScale : 0;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
        }

        public void Resume() => SetState(MatchState.Playing);
        public void Pause() => SetState(MatchState.Paused);
        public void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void SkipToBoss()
        {
            ClearEnemies();
            Endless = false;
            Waves.Begin(BossWave);
        }

        public void StartEndless()
        {
            Endless = true;
            ClearEnemies();
            Waves.Break(BossWave + 1);
            Resume();
        }

        public void ClearEnemies()
        {
            for (int i = 0; i < livingSlots; i++) Enemies[i].Despawn();
            Alive = 0;
            projectiles.Clear();
            pickups.Clear();
        }

        public Enemy SpawnAtGate(EnemyKind kind, int gate, int ordinal)
        {
            Vector3 position = Arena.Gates[gate];
            Vector3 inward = Mathf.Abs(position.z) > Mathf.Abs(position.x)
                ? new Vector3(0, 0, -Mathf.Sign(position.z)) : new Vector3(-Mathf.Sign(position.x), 0, 0);
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            for (int i = 0; i < 8; i++)
            {
                int slot = (i + ordinal) % 8;
                Vector3 guess = position + side * ((slot % 4 - 1.5f) * 1.18f) + inward * (slot / 4 * 1.8f);
                if (Arena.Sample(guess, out Vector3 sample, 1) && SlotClear(sample)) return Spawn(kind, sample);
            }
            return null;
        }

        public bool SlotClear(Vector3 point, Enemy ignored = null)
        {
            if ((point - Player.Position).sqrMagnitude < 1.5f) return false;
            for (int i = 0; i < livingSlots; i++)
            {
                Enemy enemy = Enemies[i];
                if (enemy != ignored && enemy.Alive && (enemy.Position - point).sqrMagnitude < 1.04f) return false;
            }
            return true;
        }

        public Enemy Spawn(EnemyKind kind, Vector3 point)
        {
            if (Alive >= AliveCap) return null;
            Enemy chosen = null;
            int unused = -1;
            for (int i = 0; i < livingSlots; i++)
            {
                Enemy enemy = Enemies[i];
                if (enemy.Active) continue;
                if (enemy.Kind == kind) { chosen = enemy; break; }
                if (unused < 0) unused = i;
            }
            if (chosen == null)
            {
                int index = livingSlots < Enemies.Length ? livingSlots++ : unused;
                if (index < 0) return null;
                if (Enemies[index] != null) Destroy(Enemies[index].Root.gameObject);
                chosen = Enemies[index] = new Enemy(this, kind, index);
            }
            if (!chosen.Spawn(point)) return null;
            Alive++;
            return chosen;
        }

        public void EnemyDied(Enemy enemy)
        {
            Alive--;
            Stats.Count(enemy.Kind);
            DeathEffect(enemy);
            if (enemy.Kind == EnemyKind.Warden)
            {
                Stats.WavesCleared++;
                SetState(MatchState.Victory);
                return;
            }
            bool heal = enemy.Kind == EnemyKind.Tank || Random.value < HealthChance;
            bool ammo = Random.value < AmmoChance;
            if (heal) pickups.Drop(enemy.Position + (ammo ? enemy.Root.right * .36f : Vector3.zero), true);
            if (ammo) pickups.Drop(enemy.Position - (heal ? enemy.Root.right * .36f : Vector3.zero), false);
        }

        public void DamagePlayer(float amount, Vector3 from, float knockback = 0)
        {
            if (State != MatchState.Playing) return;
            Stats.Damage += Player.Hurt(amount, from, knockback);
            if (Player.HP <= 0) SetState(MatchState.Dead);
        }

        public void FireEnemy(Enemy enemy, float speed, float damage, float yaw)
        {
            Vector3 from = enemy.Eyes + enemy.Root.forward * .48f - Vector3.up * .17f;
            Vector3 direction = Quaternion.AngleAxis(yaw * (Random.value < .5f ? -1 : 1), Vector3.up) *
                (Player.Eyes - from).normalized;
            if (enemy.Kind == EnemyKind.Warden)
                direction = Quaternion.AngleAxis(yaw, Vector3.up) * (Player.Eyes - from).normalized;
            projectiles.Fire(from, direction, speed, damage);
            EnemyShotEffect(enemy.Kind, from);
            acceptance?.Shot(enemy.Kind);
        }

        public void Summon(EnemyKind kind, int count) => Waves.Summon(kind, count);
        public void RegisterHit(bool dead)
        {
            if (dead || Time.time >= HitUntil || !KillMarker) KillMarker = dead;
            HitUntil = Time.time + (KillMarker ? .27f : .14f);
            HitSound(dead);
        }

        public void CoverChosen(Enemy enemy) => acceptance?.Cover(enemy);
        public void SlamWarning(Vector3 point) { DisplaySlam(point); acceptance?.Slam(); }
        public void SlamImpact(Vector3 point) => SlamFx(point);
        public void Footstep() => StepAudio();
        public void ReloadSound(bool done) => ReloadAudio(done);
        public void EnemyVoice(Enemy enemy) => VoiceAudio(enemy);
        public void WaveHorn() => HornAudio();
        public void PickupSound() => PickupAudio();
        public void ShotEffect(WeaponKind kind) => WeaponFx(kind);
        public void Tracer(Vector3 end, bool pellet) => TracerFx(end, pellet);
        public void Impact(Vector3 point, Vector3 normal, SurfaceKind type) => ImpactFx(point, normal, type);
        partial void SetupEffects();
        partial void UpdateEffects(float dt);
        partial void DeathEffect(Enemy enemy);
        partial void EnemyShotEffect(EnemyKind kind, Vector3 point);
        partial void DisplaySlam(Vector3 point);
        partial void SlamFx(Vector3 point);
        partial void StepAudio();
        partial void ReloadAudio(bool done);
        partial void VoiceAudio(Enemy enemy);
        partial void HornAudio();
        partial void PickupAudio();
        partial void HitSound(bool dead);
        partial void WeaponFx(WeaponKind kind);
        partial void TracerFx(Vector3 end, bool pellet);
        partial void ImpactFx(Vector3 point, Vector3 normal, SurfaceKind type);
        partial void SetupHud();
        partial void UpdateHud();
        partial void Screenshot();
    }
}




