using UnityEngine;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed class MatchStats
    {
        public int Shots, Hits, Headshots, Score, WavesCleared;
        public readonly int[] Kills = new int[4];
        public float Damage, Time;
        public float Accuracy => Shots > 0 ? (float)Hits / Shots : 0;
        public string Rank
        {
            get
            {
                if (Accuracy >= RankSAccuracy && Damage <= RankSDamage && Time <= RankSTime) return "S";
                if (Accuracy >= RankAAccuracy && Damage <= RankADamage && Time <= RankATime) return "A";
                if (Accuracy >= RankBAccuracy && Damage <= RankBDamage && Time <= RankBTime) return "B";
                return "C";
            }
        }

        public void Count(EnemyKind kind)
        {
            Kills[(int)kind]++;
            Score += kind switch { EnemyKind.Runner => 100, EnemyKind.Shooter => 200, EnemyKind.Tank => 450, _ => 3000 };
        }
    }
}
