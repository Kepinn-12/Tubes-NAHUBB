using System;
using Robocode.TankRoyale.BotApi.Graphics;
using System.Collections.Generic;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class BotKabur : Bot
{
    private Dictionary<int, (double Energy, double X, double Y)> scannedEnemies = new();

    static void Main(string[] args) => new BotKabur().Start();

    BotKabur() : base(BotInfo.FromFile("BotKabur.json")) { }

    public override void Run()
    {
        BodyColor   = Color.Yellow;
        TurretColor = Color.Navy;
        RadarColor  = Color.Cyan;
        BulletColor = Color.Red;
        ScanColor   = Color.Cyan;
        GunColor    = Color.White;

        while (IsRunning)
        {
            AttackLowestHpEnemy();
            ScanArena();
            FleeIfDanger();
            Go();
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        scannedEnemies[e.ScannedBotId] = (e.Energy, e.X, e.Y);
        Console.WriteLine($"Detected Bot ID:{e.ScannedBotId} | HP:{e.Energy} | Pos:({e.X:F1}, {e.Y:F1})");
    }

    private void AttackLowestHpEnemy()
    {
        if (scannedEnemies.Count == 0) return;

        int    targetId = -1;
        double lowestHp = double.MaxValue;
        double targetX  = 0;
        double targetY  = 0;

        foreach (var (id, data) in scannedEnemies)
        {
            if (data.Energy < lowestHp)
            {
                lowestHp = data.Energy;
                targetId = id;
                targetX  = data.X;
                targetY  = data.Y;
            }
        }

        Console.WriteLine($"[GREEDY] Targeting Bot ID:{targetId} with HP:{lowestHp}");

        double radarTurn = RadarBearingTo(targetX, targetY);
        SetTurnRadarLeft(2 * radarTurn);

        double gunBearing = GunBearingTo(targetX, targetY);
        SetTurnGunLeft(gunBearing);

        double firePower = lowestHp < 50 ? 4 : 2;
        SetFire(firePower);
    }

    // Kabur jika ada musuh terlalu dekat atau banyak musuh di sekitar
    private void FleeIfDanger()
    {
        if (scannedEnemies.Count == 0) return;

        int    nearCount   = 0;
        double nearestDist = double.MaxValue;
        double nearestX    = 0;
        double nearestY    = 0;

        foreach (var (id, data) in scannedEnemies)
        {
            double dist = DistanceTo(data.X, data.Y);
            if (dist < 250)
            {
                nearCount++;
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestX    = data.X;
                    nearestY    = data.Y;
                }
            }
        }

        // Kabur jika terlalu dekat (< 150px) atau dikelilingi (>= 2 musuh dekat)
        if (nearestDist < 150 || nearCount >= 2)
        {
            Console.WriteLine($"[FLEE] {nearCount} enemy nearby! Fleeing...");
            double fleeBearing = BearingTo(nearestX, nearestY) + 180;
            SetTurnLeft(fleeBearing);
            SetForward(120);
        }
    }

    private void ScanArena()
    {
        SetTurnRadarLeft(360);
    }

    public override void OnHitBot(HitBotEvent e)
    {
        var body = BearingTo(e.X, e.Y);
        if (body > -10 && body < 10)
        {
            SetFire(3);
        }
        if (e.IsRammed)
        {
            SetTurnLeft(10);
        }
    }

    public override void OnHitWall(HitWallEvent e)
    {
        Console.WriteLine("Kena dinding! Balik arah...");
        SetBack(150);
        SetTurnRight(45);
    }

    public override void OnBotDeath(BotDeathEvent e)
    {
        if (scannedEnemies.ContainsKey(e.VictimId))
        {
            scannedEnemies.Remove(e.VictimId);
            Console.WriteLine($"[KILL] Bot#{e.VictimId} eliminated!");
        }
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        SetTurnRight(45);
        SetForward(50);
    }

    private double DistanceTo(double x, double y)
    {
        double dx = x - X;
        double dy = y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}