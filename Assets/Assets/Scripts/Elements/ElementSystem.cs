using System;

public static class ElementSystem
{
    // nilai yang ditampilkan di UI Next Ball
    public static ElementType Next { get; private set; } = ElementType.Neutral;

    public static event Action<ElementType> OnNextChanged;

    public static void SetNext(ElementType e)
    {
        if (Next == e) return;
        Next = e;
        OnNextChanged?.Invoke(Next);
    }

    /// Ambil elemen untuk bola baru, lalu LANGSUNG reset ke Neutral (single-use).
    public static ElementType ConsumeForNewBall()
    {
        var e = Next;                // elemen untuk tembakan ini
        SetNext(ElementType.Neutral); // reset UI Next → Neutral segera
        return e;
    }

    /// Panggil di awal level / reset manual kalau perlu.
    public static void Reset() => SetNext(ElementType.Neutral);
}
