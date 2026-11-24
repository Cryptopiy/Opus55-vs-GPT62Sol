namespace WaveSurvival
{
    /// <summary>Run statistics shown on the death / victory screens (S-07, S-24).</summary>
    public sealed class GameStats
    {
        public int ShotsFired;     // one per trigger pull (a shotgun blast counts once)
        public int ShotsHit;       // shots that hit at least one enemy
        public int Headshots;      // shots that hit at least one enemy head
        public readonly int[] Kills = new int[4];
        public float DamageTaken;
        public float DamageDealt;
        public int Score;
        public int WavesCleared;
        public float PlayTime;

        public float Accuracy => ShotsFired > 0 ? (float)ShotsHit / ShotsFired : 0f;

        public int TotalKills => Kills[0] + Kills[1] + Kills[2] + Kills[3];

        public void AddKill(EnemyType type, bool headshot)
        {
            Kills[(int)type]++;
            switch (type)
            {
                case EnemyType.Runner: Score += GameConfig.Score.Runner; break;
                case EnemyType.Shooter: Score += GameConfig.Score.Shooter; break;
                case EnemyType.Tank: Score += GameConfig.Score.Tank; break;
                default: Score += GameConfig.Score.Warden; break;
            }
            if (headshot) Score += GameConfig.Score.HeadshotKillBonus;
        }

        /// <summary>S-24 rank from the explicit thresholds in <see cref="GameConfig.Rank"/>.</summary>
        public string Rank()
        {
            float acc = Accuracy;
            if (acc >= GameConfig.Rank.SAccuracy && DamageTaken <= GameConfig.Rank.SDamageTaken && PlayTime <= GameConfig.Rank.STime) return "S";
            if (acc >= GameConfig.Rank.AAccuracy && DamageTaken <= GameConfig.Rank.ADamageTaken && PlayTime <= GameConfig.Rank.ATime) return "A";
            if (acc >= GameConfig.Rank.BAccuracy && DamageTaken <= GameConfig.Rank.BDamageTaken) return "B";
            return "C";
        }
    }
}
