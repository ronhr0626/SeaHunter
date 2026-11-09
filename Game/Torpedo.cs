namespace SeaHunter.Game;

public class Torpedo
{
    public const double Width = 3;
    public const double Height = 10;
    public const double Speed = 140; // Pixels per second, straight up

    public double X { get; set; }
    public double Y { get; set; }

    public void Update(double dt) => Y -= Speed * dt;
}