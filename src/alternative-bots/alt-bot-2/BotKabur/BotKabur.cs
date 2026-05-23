using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class BotKabur : Bot
{
    // ===== ENEMY TRACKING =====
    private Dictionary<int, EnemyData> enemies = new();

    private class EnemyData
    {
        public double X;
        public double Y;
        public double Energy;
        public double Distance;
        public double Heading;
        public double Speed;
        public long LastSeen;
        public long LastFiredAt;
    }

    // ===== MOVEMENT STATE =====
    private int moveDirection = 1;
    private readonly Random rand = new Random();
    private const double WallMargin = 80.0;

    // ===== TUNING =====
    private const int    MaxScanAge         = 3;
    private const double FireAngleThreshold = 3.0;
    private const int    FireCooldown       = 5;
    private const double DangerZone         = 200.0; // Jarak bahaya untuk kabur

    // ===== RADAR STATE =====
    private int radarSweepDir = 1;
    private double sweepAccumulator = 0;

    // =====================================================
    static void Main(string[] args) => new BotKabur().Start();
    public BotKabur() : base(BotInfo.FromFile("BotKabur.json")) {}

    // =====================================================
    // RUN LOOP
    // =====================================================
    public override void Run()
    {
        BodyColor   = Color.Black;
        TurretColor = Color.Red;
        RadarColor  = Color.DarkRed;
        BulletColor = Color.Orange;
        ScanColor   = Color.Yellow;
        MaxSpeed    = 8;

        while (IsRunning)
        {
            CleanEnemies();
            DoMovement();
            DoGunAndRadar();
            Go();
        }
    }

    // =====================================================
    // MOVEMENT - KABUR DARI KERUMUNAN
    // =====================================================
    private void DoMovement()
    {
        // Prioritas 1: Kabur dari wall
        if (IsNearWall())
        {
            double centerAngle = AngleTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
            double turnNeeded  = Normalize(centerAngle - Direction);
            TurnRate           = Math.Sign(turnNeeded) * Math.Min(Math.Abs(turnNeeded), 10);
            TargetSpeed        = 8;
            return;
        }

        // Prioritas 2: Kabur dari kerumunan musuh
        if (enemies.Count > 0)
        {
            // Hitung center of mass musuh
            double crowdX = enemies.Values.Average(e => e.X);
            double crowdY = enemies.Values.Average(e => e.Y);
            double distToCrowd = Math.Sqrt(Math.Pow(X - crowdX, 2) + Math.Pow(Y - crowdY, 2));

            // Cari musuh terdekat
            var nearest = enemies.Values.OrderBy(e => e.Distance).First();

            // Jika ada musuh dekat, kabur dari dia
            if (nearest.Distance < DangerZone)
            {
                // Kabur berlawanan arah dari musuh terdekat
                double escapeAngle = AngleTo(nearest.X, nearest.Y) + 180;
                double turnNeeded  = Normalize(escapeAngle - Direction);
                TurnRate           = Math.Sign(turnNeeded) * Math.Min(Math.Abs(turnNeeded), 10);
                TargetSpeed        = 8;
                return;
            }

            // Jika kerumunan terlalu dekat, kabur dari center of mass
            if (distToCrowd < DangerZone * 1.5)
            {
                double escapeAngle = AngleTo(crowdX, crowdY) + 180;
                double turnNeeded  = Normalize(escapeAngle - Direction);
                TurnRate           = Math.Sign(turnNeeded) * Math.Min(Math.Abs(turnNeeded), 8);
                TargetSpeed        = 7;
                return;
            }
        }

        // Default: Gerakan unpredictable
        TargetSpeed = 6;
        if (TurnNumber % 30 == 0)
            TurnRate = rand.Next(-8, 9);
    }

    // =====================================================
    // GUN & RADAR
    // =====================================================
    private void DoGunAndRadar()
    {
        if (enemies.Count == 0)
        {
            SweepRadar();
            return;
        }

        // Prioritas target: musuh terdekat dengan energi rendah
        var targetKv = enemies
            .OrderBy(e => e.Value.Distance * 0.5 + e.Value.Energy * 3.0)
            .First();

        EnemyData enemy  = targetKv.Value;
        long      scanAge = TurnNumber - enemy.LastSeen;

        if (scanAge > MaxScanAge)
        {
            SweepRadar();
            return;
        }

        // Radar lock
        double dirToEnemy = AngleTo(enemy.X, enemy.Y);
        double radarTurn  = Normalize(dirToEnemy - RadarDirection);
        SetTurnRadarLeft(radarTurn * 2.0);

        // Predictive aiming
        double firePower   = CalculateFirePower(enemy);
        double bulletSpeed = 20.0 - 3.0 * firePower;
        double travelTime  = enemy.Distance / bulletSpeed;

        double headingRad = enemy.Heading * Math.PI / 180.0;
        double predictedX = Clamp(enemy.X + Math.Cos(headingRad) * enemy.Speed * travelTime,
                                  20, ArenaWidth  - 20);
        double predictedY = Clamp(enemy.Y + Math.Sin(headingRad) * enemy.Speed * travelTime,
                                  20, ArenaHeight - 20);

        double aimAngle = AngleTo(predictedX, predictedY);
        double gunTurn  = Normalize(aimAngle - GunDirection);
        SetTurnGunLeft(gunTurn);

        // Fire
        if (Math.Abs(gunTurn) < FireAngleThreshold &&
            TurnNumber - enemy.LastFiredAt >= FireCooldown)
        {
            Fire(firePower);
            enemy.LastFiredAt = TurnNumber;
        }
    }

    // =====================================================
    // SWEEP RADAR
    // =====================================================
    private void SweepRadar()
    {
        double step = 45.0;
        sweepAccumulator += step;

        if (sweepAccumulator >= 180)
        {
            radarSweepDir    *= -1;
            sweepAccumulator  = 0;
        }

        if (radarSweepDir > 0)
            SetTurnRadarRight(step);
        else
            SetTurnRadarLeft(step);
    }

    // =====================================================
    // FIREPOWER
    // =====================================================
    private double CalculateFirePower(EnemyData enemy)
    {
        if (enemy.Energy   < 15)  return 3.0;
        if (enemy.Distance < 120) return 3.0;
        if (enemy.Distance < 300) return 2.0;
        return 1.0;
    }

    // =====================================================
    // EVENTS
    // =====================================================
    public override void OnScannedBot(ScannedBotEvent e)
    {
        if (!enemies.TryGetValue(e.ScannedBotId, out var existing))
            existing = new EnemyData();

        existing.X        = e.X;
        existing.Y        = e.Y;
        existing.Energy   = e.Energy;
        existing.Distance = DistanceTo(e.X, e.Y);
        existing.Heading  = e.Direction;
        existing.Speed    = e.Speed;
        existing.LastSeen = TurnNumber;

        enemies[e.ScannedBotId] = existing;
    }

    public override void OnHitWall(HitWallEvent e)
    {
        moveDirection *= -1;
        TargetSpeed    = 8 * moveDirection;
        TurnRate       = rand.Next(60, 121) * moveDirection;
    }

    public override void OnHitBot(HitBotEvent e)
    {
        Fire(3);
        moveDirection *= -1;
        TargetSpeed    = 8 * moveDirection;
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        moveDirection *= -1;
        TurnRate       = rand.Next(-12, 13);
        TargetSpeed    = 8 * moveDirection;
    }

    public override void OnBotDeath(BotDeathEvent e)
    {
        enemies.Remove(e.VictimId);
    }

    // =====================================================
    // HELPERS
    // =====================================================
    private void CleanEnemies()
    {
        var stale = enemies
            .Where(x => TurnNumber - x.Value.LastSeen > 10)
            .Select(x => x.Key)
            .ToList();
        foreach (var id in stale)
            enemies.Remove(id);
    }

    private bool IsNearWall() =>
        X < WallMargin || X > ArenaWidth  - WallMargin ||
        Y < WallMargin || Y > ArenaHeight - WallMargin;

    private double AngleTo(double x, double y) =>
        Math.Atan2(y - Y, x - X) * 180.0 / Math.PI;

    private static double Normalize(double angle)
    {
        while (angle >  180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }

    private static double Clamp(double v, double min, double max) =>
        Math.Max(min, Math.Min(max, v));
}