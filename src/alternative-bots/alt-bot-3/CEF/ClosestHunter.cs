using System;
using System.Collections.Generic;
using System.Linq;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class ClosestHunter : Bot
{
    // ===== GREEDY ALGORITHM COMPONENTS =====
    // Himpunan Kandidat: Semua musuh yang terdeteksi
    private Dictionary<int, EnemyData> scannedEnemies = new();
    
    private class EnemyData
    {
        public double Energy { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public long LastSeen { get; set; }
        public double Distance { get; set; }
    }

    static void Main(string[] args) => new ClosestHunter().Start();
    ClosestHunter() : base(BotInfo.FromFile("ClosestHunter.json")) { }

    public override void Run()
    {
        Console.WriteLine("=== GREEDY BOT: Closest Hunter ===");
        Console.WriteLine("Heuristic: Target nearest enemy");
        Console.WriteLine("Objective: Maximize Ram Damage + Hit Accuracy\n");

        while (IsRunning)
        {
            ScanArena();
            CleanStaleEnemies();
            AttackClosestEnemy();
        }
    }

    // ===== GREEDY SELECTION FUNCTION =====
    // Fungsi Seleksi: Pilih musuh dengan JARAK MINIMUM
    // Fungsi Kelayakan: Musuh harus terdeteksi dalam 5 turn terakhir
    // Fungsi Objektif: Maksimalkan Ram Damage (2x damage) + Bullet Hit Rate
    private void AttackClosestEnemy()
    {
        if (scannedEnemies.Count == 0)
        {
            TurnRadarLeft(45);
            return;
        }

        // GREEDY CHOICE: Pilih musuh TERDEKAT
        var target = scannedEnemies
            .OrderBy(e => e.Value.Distance)  // Sort by distance ascending
            .First();

        int targetId = target.Key;
        EnemyData enemy = target.Value;

        Console.WriteLine($"[GREEDY] Target: Bot#{targetId} | Distance:{enemy.Distance:F0} | HP:{enemy.Energy:F1}");

        // ===== AGGRESSIVE APPROACH STRATEGY =====
        
        double angleToEnemy = AngleTo(enemy.X, enemy.Y);
        double bearingToEnemy = NormalizeAngle(angleToEnemy - Direction);

        // 1. Arahkan badan langsung ke musuh (untuk ram atau close-range shot)
        TurnLeft(bearingToEnemy);

        // 2. Dynamic strategy berdasarkan jarak
        if (enemy.Distance < 150)
        {
            // VERY CLOSE: Ramming attack!
            Console.WriteLine("  >> RAMMING MODE! Full speed ahead!");
            SetForward(200);
            
            // Fire power tinggi di jarak dekat (akurasi tinggi)
            double gunTurn = NormalizeAngle(angleToEnemy - GunDirection);
            TurnGunLeft(gunTurn);
            
            if (GunHeat == 0 && Math.Abs(gunTurn) < 15)
            {
                Fire(3.0);  // Maximum power
                Console.WriteLine("  >> FIRE! Power:3.0 (close range)");
            }
        }
        else if (enemy.Distance < 300)
        {
            // MEDIUM RANGE: Approach with firing
            Console.WriteLine("  >> Approaching with fire support");
            SetForward(100);
            
            double gunTurn = NormalizeAngle(angleToEnemy - GunDirection);
            TurnGunLeft(gunTurn);
            
            if (GunHeat == 0 && Math.Abs(gunTurn) < 10)
            {
                Fire(2.0);
                Console.WriteLine("  >> FIRE! Power:2.0 (medium range)");
            }
        }
        else
        {
            // LONG RANGE: Rush to close distance
            Console.WriteLine("  >> Rushing to close distance");
            SetForward(150);
            
            // Light fire untuk chip damage
            double gunTurn = NormalizeAngle(angleToEnemy - GunDirection);
            TurnGunLeft(gunTurn);
            
            if (GunHeat == 0 && Math.Abs(gunTurn) < 5)
            {
                Fire(1.0);  // Light bullet (fast, save energy)
                Console.WriteLine("  >> FIRE! Power:1.0 (long range)");
            }
        }

        // 3. Lock radar ke target
        double radarTurn = NormalizeAngle(angleToEnemy - RadarDirection);
        TurnRadarLeft(radarTurn);
    }

    // ===== RADAR & EVENT HANDLERS =====
    
    private void ScanArena()
    {
        if (scannedEnemies.Count == 0)
            TurnRadarLeft(45);
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        double distance = DistanceTo(e.X, e.Y);
        
        scannedEnemies[e.ScannedBotId] = new EnemyData
        {
            Energy = e.Energy,
            X = e.X,
            Y = e.Y,
            Distance = distance,
            LastSeen = TurnNumber
        };

        Console.WriteLine($"[SCAN] Bot#{e.ScannedBotId} | Dist:{distance:F0} | HP:{e.Energy:F1}");
    }

    public override void OnBotDeath(BotDeathEvent e)
    {
        if (scannedEnemies.ContainsKey(e.VictimId))
        {
            scannedEnemies.Remove(e.VictimId);
            Console.WriteLine($"[KILL] Bot#{e.VictimId} eliminated!");
        }
    }

    public override void OnHitBot(HitBotEvent e)
    {
        // Ramming successful! Keep pushing
        Console.WriteLine($"[RAM] Hit Bot#{e.VictimId}! Ram damage dealt!");
        
        // Push harder jika HP musuh rendah
        if (scannedEnemies.ContainsKey(e.VictimId) && 
            scannedEnemies[e.VictimId].Energy < 30)
        {
            Console.WriteLine("  >> Enemy weak! Finishing with ram!");
            SetForward(100);
        }
        else
        {
            // Mundur sedikit lalu ram lagi
            Back(20);
        }
    }

    public override void OnHitWall(HitWallEvent e)
    {
        Console.WriteLine("[WALL] Hit wall! Reversing...");
        Back(100);
        TurnRight(90);
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        Console.WriteLine($"[HIT] Took {e.Damage:F1} damage!");
        // Tetap agresif, tidak menghindar
    }

    // ===== HELPER FUNCTIONS =====
    
    private void CleanStaleEnemies()
    {
        var staleEnemies = scannedEnemies
            .Where(e => TurnNumber - e.Value.LastSeen > 5)
            .Select(e => e.Key)
            .ToList();

        foreach (var id in staleEnemies)
        {
            scannedEnemies.Remove(id);
            Console.WriteLine($"[CLEAN] Bot#{id} data stale.");
        }
    }

    private double AngleTo(double x, double y)
    {
        double dx = x - X;
        double dy = y - Y;
        return Math.Atan2(dy, dx) * 180 / Math.PI;
    }

    private double NormalizeAngle(double angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }
}