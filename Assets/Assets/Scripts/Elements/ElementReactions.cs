using System;
using UnityEngine;

public enum ReactionType { None, Steam, Firestorm, FlameStrike, WindRain, MudRain, Tornado }

public static class ElementReactions
{
    // type, position, ball yang terlibat, elemen bola, elemen peg
    public static event Action<ReactionType, Vector2, BallController, ElementType, ElementType> OnReaction;

    public static bool TryTrigger(ElementType ballElem, ElementType pegElem, Vector2 at, BallController ball)
    {
        if (CardEffects.I != null && CardEffects.I.elementaryMasteryActive)
            ballElem = CardEffects.OppositeOf(pegElem);

        if (!GetReaction(ballElem, pegElem, out var type)) return false;
        OnReaction?.Invoke(type, at, ball, ballElem, pegElem);
        SaveManager.I?.RegisterElementReaction(type.ToString());
        return true;
    }

    public static bool GetReaction(ElementType a, ElementType b, out ReactionType type)
    {
        type = ReactionType.None;

        // tak ada reaksi jika netral atau sama
        if (a == ElementType.Neutral || b == ElementType.Neutral || a == b) return false;

        // urutkan supaya order-agnostic (Fire+Water == Water+Fire)
        var x = (int)a < (int)b ? a : b;
        var y = (int)a < (int)b ? b : a;

        if (x == ElementType.Fire && y == ElementType.Water) { type = ReactionType.Steam; return true; }
        if (x == ElementType.Fire && y == ElementType.Wind) { type = ReactionType.Firestorm; return true; }
        if (x == ElementType.Fire && y == ElementType.Earth) { type = ReactionType.FlameStrike; return true; }
        if (x == ElementType.Water && y == ElementType.Wind) { type = ReactionType.WindRain; return true; }
        if (x == ElementType.Water && y == ElementType.Earth) { type = ReactionType.MudRain; return true; }
        if (x == ElementType.Wind && y == ElementType.Earth) { type = ReactionType.Tornado; return true; }

        return false;
    }
}
