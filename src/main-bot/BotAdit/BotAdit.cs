using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class BotAdit : Bot
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
    private int movementCounter = 0;
    private const double WallMargin = 80.0;

    // ===== TUNING =====
    private const int    MaxScanAge         = 3;
    private const double FireAngleThreshold = 3.0;
    private const int    FireCooldown       = 5;

    // ===== RADAR STATE =====
    // Arah sweep saat tidak ada target valid: +1 = kanan, -1 = kiri
    private int radarSweepDir = 1;

    // =====================================================
    static void Main(string[] args) => new BotAdit().Start();
    public BotAdit() : base(BotInfo.FromFile("BotAdit.json")) {}

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

        sweepAccumulator = 0;
        radarSweepDir    = 1;
        enemies.Clear();

        while (IsRunning)
        {
            movementCounter++;
            CleanEnemies();
            DoMovement();
            DoGunAndRadar();
            Go();
        }
    }

    // =====================================================
    // MOVEMENT
    // =====================================================
    private void DoMovement()
    {
        if (IsNearWall())
        {
            double centerAngle = AngleTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
            double turnNeeded  = Normalize(centerAngle - Direction);
            TurnRate           = Math.Sign(turnNeeded) * Math.Min(Math.Abs(turnNeeded), 10);
            TargetSpeed        = 8 * moveDirection;
            return;
        }

        TargetSpeed = Math.Sin(TurnNumber * 0.13) * 8.0 * moveDirection;

        if (movementCounter % (45 + rand.Next(0, 20)) == 0)
            moveDirection *= -1;

        if (movementCounter % (30 + rand.Next(0, 15)) == 0)
            TurnRate = rand.Next(-8, 9);
    }

    // =====================================================
    // GUN & RADAR
    //
    // LOGIKA RADAR (FIX UTAMA):
    //
    //   CASE 1 — Tidak ada musuh di dictionary sama sekali
    //            → sweep penuh 360 terus
    //
    //   CASE 2 — Ada musuh, data masih segar (scanAge <= MaxScanAge)
    //            → lock radar ke target, lakukan predictive aim + fire
    //
    //   CASE 3 — Ada musuh tapi data sudah basi (scanAge > MaxScanAge)
    //            → JANGAN lock ke posisi lama, sweep aktif ke seluruh arena
    //              sampai scan segar kembali
    //
    // Sebelumnya: CASE 3 melakukan radar lock ke posisi lama
    // lalu return → radar stuck di tempat kosong.
    // =====================================================
    private void DoGunAndRadar()
    {
        // ── CASE 1: Tidak ada data musuh sama sekali ───────────
        if (enemies.Count == 0)
        {
            SweepRadar();
            return;
        }

        // Pilih target terbaik berdasarkan scoring
        var targetKv = enemies
            .OrderBy(e => e.Value.Distance * 0.6 + e.Value.Energy * 2.0)
            .First();

        EnemyData enemy  = targetKv.Value;
        long      scanAge = TurnNumber - enemy.LastSeen;

        // ── CASE 3: Data basi → sweep aktif, jangan lock ──────
        if (scanAge > MaxScanAge)
        {
            SweepRadar();
            return;
        }

        // ── CASE 2: Data segar → lock radar + aim + fire ──────

        // Radar lock ke arah target (overshoot kecil agar tidak lepas)
        double dirToEnemy = AngleTo(enemy.X, enemy.Y);
        double radarTurn  = Normalize(dirToEnemy - RadarDirection);
        SetTurnRadarLeft(radarTurn * 2.0);

        // Predictive aiming
        double firePower   = CalculateFirePower(enemy);
        double bulletSpeed = 20.0 - 3.0 * firePower;
        double travelTime  = enemy.Distance / bulletSpeed;

        double headingRad = enemy.Heading * Math.PI / 180.0;
        double predictedX = Clamp(enemy.X + Math.Cos(headingRad) * enemy.Speed * travelTime,
                                  20, ArenaWidth  - 25);
        double predictedY = Clamp(enemy.Y + Math.Sin(headingRad) * enemy.Speed * travelTime,
                                  20, ArenaHeight - 25);

        double aimAngle = AngleTo(predictedX, predictedY);
        double gunTurn  = Normalize(aimAngle - GunDirection);
        SetTurnGunLeft(gunTurn);
        

        // Tembak hanya jika gun lurus + cooldown habis
        if (Math.Abs(gunTurn) < FireAngleThreshold &&
            TurnNumber - enemy.LastFiredAt >= FireCooldown){
            if (Energy < 60) {
            SetFire(1);
            }else{
            Fire(firePower);
            enemy.LastFiredAt = TurnNumber;
        }
        // Ram jika musuh hampir mati
        if (enemy.Distance < 100 && enemy.Energy < 20)
            TargetSpeed = 8;
    }
    }

    // =====================================================
    // SWEEP RADAR
    // Memutar radar aktif ke seluruh arena.
    // Ganti arah sweep setiap 180 derajat agar tidak muter satu sisi.
    // =====================================================
    private double sweepAccumulator = 0;

    private void SweepRadar()
    {
        double step = 20.0; // derajat per turn saat sweep
        sweepAccumulator += step;

        if (sweepAccumulator >= 360)
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
        TargetSpeed    = 6 * moveDirection;
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        moveDirection *= -1;
        TurnRate       = rand.Next(-12, 13);
        TargetSpeed    = rand.Next(5, 9) * moveDirection;
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
