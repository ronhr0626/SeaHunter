namespace SeaHunter.Game;

// How far away the ship is
public enum Lane { Far, Middle, Near }

public enum ShipType { PatrolBoat, Freighter, Battleship }

public class Ship
{
    // ===== Tweak these =====

    // Y position where each lane's hulls touch the water (lower number = higher on screen)
    public const double FarLaneY = 58;
    public const double MiddleLaneY = 88;
    public const double NearLaneY = 120;

    // How small each lane looks (1.0 = full size)
    public const double FarScale = 0.7;
    public const double MiddleScale = 0.85;
    public const double NearScale = 1.0;

    // Bonus points multiplier for distance
    public const int FarBonus = 3;
    public const int MiddleBonus = 2;
    public const int NearBonus = 1;

    // =======================

    public ShipType Type { get; }
    public Lane Lane { get; }
    public double X { get; set; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }
    public double Speed { get; }  // Negative = moving left
    public int Points { get; }
    public int Health { get; private set; }

    public bool MovingRight => Speed > 0;
    public bool IsOffScreen => MovingRight ? X > GameWorld.Width : X + Width < 0;

    public Ship(ShipType type, bool movingRight)
    {
        Type = type;

        // Base stats per type. Smallest ship sails furthest away, biggest closest.
        double baseWidth, baseHeight, baseSpeed;
        int basePoints;

        switch (type)
        {
            case ShipType.PatrolBoat:
                Lane = Lane.Far;
                baseWidth = 50; baseHeight = 14; baseSpeed = 80; basePoints = 150; Health = 1;
                break;
            case ShipType.Freighter:
                Lane = Lane.Middle;
                baseWidth = 90; baseHeight = 22; baseSpeed = 45; basePoints = 100; Health = 1;
                break;
            default: // Battleship
                Lane = Lane.Near;
                baseWidth = 120; baseHeight = 28; baseSpeed = 30; basePoints = 50; Health = 2;
                break;
        }

        // Lane settings
        var (laneY, scale, bonus) = Lane switch
        {
            Lane.Far => (FarLaneY, FarScale, FarBonus),
            Lane.Middle => (MiddleLaneY, MiddleScale, MiddleBonus),
            _ => (NearLaneY, NearScale, NearBonus)
        };

        Width = baseWidth * scale;
        Height = baseHeight * scale;
        Speed = (movingRight ? 1 : -1) * baseSpeed * scale;
        Points = basePoints * bonus;

        // Hull bottom sits on the lane line
        Y = laneY - Height;

        // Start just outside the screen on the side it comes from
        X = movingRight ? -Width : GameWorld.Width;
    }

    public void Update(double dt) => X += Speed * dt;

    // Returns true if this hit sank the ship
    public bool TakeHit()
    {
        Health--;
        return Health <= 0;
    }

    // Simple rectangle overlap test
    public bool Overlaps(double x, double y, double width, double height) =>
        x < X + Width && x + width > X &&
        y < Y + Height && y + height > Y;
}