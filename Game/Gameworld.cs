using System;
using System.Collections.Generic;

namespace SeaHunter.Game;

public class GameWorld
{
    // Virtual resolution: everything is drawn at 640x360 and scaled up to the window
    public const double Width = 640;
    public const double Height = 360;

    // Y positions measured on the background image
    public const double Horizon = 42;    // Where the sky meets the sea
    public const double WaterLine = 128; // Underwater starts here
    public const double SeaFloor = 335;

    // Torpedo rules
    public const int MaxTorpedoes = 5;
    public const double ReloadTime = 2.5;    // Seconds to reload when empty
    public const double FireCooldown = 0.25; // Seconds between shots

    // Player rules
    public const int StartLives = 3;
    public const double InvulnerableTime = 2.0; // Seconds of safety after being hit

    public Submarine Player { get; } = new() { X = 80, Y = 220 };
    public List<Torpedo> Torpedoes { get; } = new();
    public List<Ship> Ships { get; } = new();
    public List<DepthCharge> DepthCharges { get; } = new();
    public List<Explosion> Explosions { get; } = new();

    public int Score { get; private set; }
    public int Lives { get; private set; } = StartLives;
    public bool IsGameOver => Lives <= 0;

    public int Ammo { get; private set; } = MaxTorpedoes;
    public bool IsReloading => Ammo == 0;

    // 0.0 = just started reloading, 1.0 = done
    public double ReloadProgress => IsReloading ? 1 - _reloadTimer / ReloadTime : 1;

    public double InvulnerableTimeLeft { get; private set; }
    public bool IsInvulnerable => InvulnerableTimeLeft > 0;

    private readonly Random _rng = new();
    private double _reloadTimer;
    private double _cooldown;
    private double _spawnTimer = 1;

    public void Update(double dt, InputState input)
    {
        // Let explosions finish after game over, but freeze everything else
        if (IsGameOver)
        {
            UpdateExplosions(dt);
            return;
        }

        UpdatePlayer(dt, input);
        UpdateShips(dt);
        UpdateTorpedoes(dt);
        UpdateDepthCharges(dt);
        CheckHits();
        UpdateExplosions(dt);
    }

    private void UpdatePlayer(double dt, InputState input)
    {
        Player.Update(dt, input);
        Player.X = Math.Clamp(Player.X, 0, Width - Player.Width);
        Player.Y = Math.Clamp(Player.Y, WaterLine + 6, SeaFloor - Player.Height);

        InvulnerableTimeLeft = Math.Max(0, InvulnerableTimeLeft - dt);

        _cooldown -= dt;
        if (input.Fire)
            TryFire();

        if (IsReloading)
        {
            _reloadTimer -= dt;
            if (_reloadTimer <= 0)
                Ammo = MaxTorpedoes;
        }
    }

    private void TryFire()
    {
        if (IsReloading || _cooldown > 0) return;

        // Launch from the middle of the submarine
        double x = Player.X + Player.Width / 2 - Torpedo.Width / 2;
        Torpedoes.Add(new Torpedo { X = x, Y = Player.Y - Torpedo.Height });

        Ammo--;
        _cooldown = FireCooldown;

        if (Ammo == 0)
            _reloadTimer = ReloadTime;
    }

    private void UpdateShips(double dt)
    {
        // Spawn a new ship every 1.5 to 3.5 seconds
        _spawnTimer -= dt;
        if (_spawnTimer <= 0)
        {
            var type = (ShipType)_rng.Next(3);
            bool movingRight = _rng.Next(2) == 0;
            Ships.Add(new Ship(type, movingRight));
            _spawnTimer = 1.5 + _rng.NextDouble() * 2;
        }

        foreach (var ship in Ships)
        {
            ship.Update(dt);
            TryDropDepthCharge(ship, dt);
        }

        Ships.RemoveAll(s => s.IsOffScreen);
    }

    private void TryDropDepthCharge(Ship ship, double dt)
    {
        // Unarmed ships and ships outside the screen never drop
        if (ship.DropInterval <= 0 || !ship.IsOnScreen) return;

        ship.DropTimer -= dt;
        if (ship.DropTimer > 0) return;

        DepthCharges.Add(new DepthCharge
        {
            X = ship.X + ship.Width / 2 - DepthCharge.Size / 2,
            Y = WaterLine
        });

        // Some randomness so drops don't feel robotic (70% to 130% of the interval)
        ship.DropTimer = ship.DropInterval * (0.7 + _rng.NextDouble() * 0.6);
    }

    private void UpdateTorpedoes(double dt)
    {
        foreach (var torpedo in Torpedoes)
            torpedo.Update(dt);

        // Missed everything and passed the horizon
        Torpedoes.RemoveAll(t => t.Y + Torpedo.Height < Horizon);
    }

    private void UpdateDepthCharges(double dt)
    {
        // Hitbox is a bit smaller than the sprite, so near misses feel fair
        double hitX = Player.X + Player.Width * 0.15;
        double hitY = Player.Y + Player.Height * 0.2;
        double hitWidth = Player.Width * 0.7;
        double hitHeight = Player.Height * 0.6;

        for (int i = DepthCharges.Count - 1; i >= 0; i--)
        {
            var c = DepthCharges[i];
            c.Update(dt);

            // Reached the sea floor: explode harmlessly
            if (c.Y + DepthCharge.Size >= SeaFloor)
            {
                Explosions.Add(new Explosion { X = c.X + DepthCharge.Size / 2, Y = SeaFloor - 4 });
                DepthCharges.RemoveAt(i);
                continue;
            }

            bool hitsPlayer =
                c.X < hitX + hitWidth && c.X + DepthCharge.Size > hitX &&
                c.Y < hitY + hitHeight && c.Y + DepthCharge.Size > hitY;

            if (hitsPlayer && !IsInvulnerable)
            {
                Explosions.Add(new Explosion
                {
                    X = c.X + DepthCharge.Size / 2,
                    Y = c.Y + DepthCharge.Size / 2
                });
                DepthCharges.RemoveAt(i);

                Lives--;
                InvulnerableTimeLeft = InvulnerableTime;
            }
        }
    }

    private void CheckHits()
    {
        // Loop backwards so we can remove torpedoes while looping
        for (int i = Torpedoes.Count - 1; i >= 0; i--)
        {
            var t = Torpedoes[i];
            var hit = Ships.Find(s => s.Overlaps(t.X, t.Y, Torpedo.Width, Torpedo.Height));
            if (hit == null) continue;

            Torpedoes.RemoveAt(i);
            Explosions.Add(new Explosion
            {
                X = t.X + Torpedo.Width / 2,
                Y = hit.Y + hit.Height / 2
            });

            // Battleships survive the first hit
            if (hit.TakeHit())
            {
                Score += hit.Points;
                Ships.Remove(hit);
            }
        }
    }

    private void UpdateExplosions(double dt)
    {
        foreach (var explosion in Explosions)
            explosion.Update(dt);

        Explosions.RemoveAll(e => e.IsDone);
    }
}