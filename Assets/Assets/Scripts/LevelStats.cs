[System.Serializable]
public struct LevelStats
{
    public int totalScore;     // kumulatif sepanjang game (HUD kiri-atas)
    public int levelScore;     // skor yang didapat di level ini saja
    public bool winOnFirstTry;  // TRUE bila belum pernah tekan Retry
    public int shotsTaken;
    public int freeBalls;
    public float percentCleared; // 0–100
    public int shotPoints;     // semua poin hasil hit peg + multiplier
    public int feverPoints;    // poin lubang fever + free-ball 25 000
}
