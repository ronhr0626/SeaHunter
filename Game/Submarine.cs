namespace SeaHunter.Game;

public class Submarine
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width => 100;
    public double Height => 31;

    // Speed in pixels per second (in the 640x360 game world)
    public double SpeedX { get; set; } = 90;
    public double SpeedY { get; set; } = 50;

    public bool FacingRight { get; private set; } = true;

    // dt = seconds since last frame, so speed is the same on fast and slow PCs
    public void Update(double dt, InputState input)
    {
        if (input.Left)
        {
            X -= SpeedX * dt;
            FacingRight = false;
        }
        if (input.Right)
        {
            X += SpeedX * dt;
            FacingRight = true;
        }
        if (input.Up) Y -= SpeedY * dt;
        if (input.Down) Y += SpeedY * dt;
    }
}