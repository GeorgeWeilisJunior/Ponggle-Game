using UnityEngine;
using UnityEngine.UI;

public class CardSpriteView : MonoBehaviour
{
    [SerializeField] Image target;           // drag Image di prefab
    [SerializeField] bool preferFullSprite = true;

    public void Bind(CardData card)
    {
        if (!card || !target) return;

        // gunakan sprite penuh jika ada; kalau kosong, jatuh ke icon
        Sprite sp = (preferFullSprite && card.fullCardSprite) ? card.fullCardSprite : card.icon;
        target.sprite = sp;
        target.enabled = sp != null;
        target.preserveAspect = true;
    }
}
