using System;
using Robocode.TankRoyale.BotApi.Graphics;
using System.Collections.Generic;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;


public class Push : Bot
{
    private Dictionary<int, (double Energy, double X, double Y)> scannedEnemies = new();

    static void Main(string[] args) => new Push().Start();

    Push() : base(BotInfo.FromFile("Push.json")) { }

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
            MovingRL();
            Go(); 
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        scannedEnemies[e.ScannedBotId] = (e.Energy, e.X, e.Y);
        Console.WriteLine($"Detected Bot ID:{e.ScannedBotId} | HP:{e.Energy} | Pos:({e.X:F1}, {e.Y:F1})");
        if (Energy < 60)
        {
            SetBack(50);
            SetFire(1);
        }else{
            double firePower = e.Energy < 50 ? 4 : 2; 
            Fire(firePower);
        }
    }

    private void AttackLowestHpEnemy()
    {
        if (scannedEnemies.Count == 0) return;

        else {   
            int    targetId     = -1;
            double lowestHp     = double.MaxValue;
            double targetX      = 0;
            double targetY      = 0;

            foreach (var (id, data) in scannedEnemies)
            {
                if (data.Energy < lowestHp)
                {
                    lowestHp = data.Energy;
                    targetId = id;
                    targetX  = data.X ;
                    targetY  = data.Y ;
                }
            }
            
            Console.WriteLine($"[GREEDY] Targeting Bot ID:{targetId} with HP:{lowestHp}");
            double radarTurn = RadarBearingTo(targetX, targetY);
            SetTurnRadarLeft(2*radarTurn);

            double gunBearing = GunBearingTo(targetX, targetY);
            TurnGunLeft(gunBearing);
        }
    }

    public override void OnHitBot(HitBotEvent e)
    {
        var body = BearingTo(e.X, e.Y);
        if (body > -10 && body < 10)
        {
            Fire(10);
        }
        if (e.IsRammed)
        {
            TurnLeft(10);
        }
    }

    public override void OnHitWall(HitWallEvent e)
    {
        Console.WriteLine("Kena dinding! Balik arah...");
        Back(150);
        TurnRight(45);

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
        TurnRight(45);  
        Forward(50);
    }

    private int DistanceTurn = 0;
    private void MovingRL (){
        if (DistanceTurn % 5 == 0) {
            Back(90);
            Forward(120);  
        }
        if (DistanceTurn % 5 == 3) {
            Back(80);
            Forward(100); 
        }
        DistanceTurn++;
    }

    private void ScanArena()
    {
        SetTurnRadarLeft(360);
    }
}
