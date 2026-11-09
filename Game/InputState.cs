using System.Collections.Generic;
using Avalonia.Input;

namespace SeaHunter.Game;

// Tracks keys held down (smooth movement) and keys pressed this frame (one-shot actions)
public class InputState
{
    private readonly HashSet<PhysicalKey> _down = new();
    private readonly HashSet<PhysicalKey> _pressed = new();

    public void Press(PhysicalKey key)
    {
        // Add returns false if the key was already down (Windows key repeat), so we ignore repeats
        if (_down.Add(key))
            _pressed.Add(key);
    }

    public void Release(PhysicalKey key) => _down.Remove(key);

    // Called once at the end of every frame
    public void EndFrame() => _pressed.Clear();

    // Held keys
    public bool Left => _down.Contains(PhysicalKey.A) || _down.Contains(PhysicalKey.ArrowLeft);
    public bool Right => _down.Contains(PhysicalKey.D) || _down.Contains(PhysicalKey.ArrowRight);
    public bool Up => _down.Contains(PhysicalKey.W) || _down.Contains(PhysicalKey.ArrowUp);
    public bool Down => _down.Contains(PhysicalKey.S) || _down.Contains(PhysicalKey.ArrowDown);

    // One press = one shot, holding the key does not auto-fire
    public bool Fire => _pressed.Contains(PhysicalKey.Space);
}