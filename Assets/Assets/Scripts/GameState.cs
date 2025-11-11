public enum GameState
{
    Idle,           // menunggu pemain menembak
    BallInPlay,     // bola sedang terbang
    FeverFinalTurn, // last orange peg sudah hancur
    EndLevelSuccess // bola hilang + fever selesai
}
