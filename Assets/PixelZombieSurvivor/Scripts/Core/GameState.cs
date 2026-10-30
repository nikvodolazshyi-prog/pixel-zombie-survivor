namespace PixelZombieSurvivor.Core
{
    /// <summary>
    /// High-level state owned by <see cref="GameManager"/>.
    /// Wave details live in the WaveManager; this state is used to gate input and UI.
    /// </summary>
    public enum GameState
    {
        Intermission = 0,
        WaveActive = 1,
        GameOver = 2
    }
}
