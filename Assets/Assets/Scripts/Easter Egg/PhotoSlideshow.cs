using UnityEngine;

public class PhotoSlideshow : MonoBehaviour
{
    [Header("Target")]
    public SpriteRenderer spriteRenderer;

    [Header("Foto-foto yang akan diputar")]
    public Sprite[] photos;

    [Header("Pengaturan Waktu")]
    public float interval = 3f;       // lama tampil sebelum fade ke gambar berikut
    public float fadeDuration = 0.5f; // lama fade in/out

    private int currentIndex = 0;
    private float timer = 0f;
    private bool isFading = false;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (photos.Length > 0)
            spriteRenderer.sprite = photos[0];
    }

    private void Update()
    {
        if (photos.Length == 0 || isFading) return;

        timer += Time.deltaTime;

        if (timer >= interval)
        {
            timer = 0f;
            StartCoroutine(FadeToNextImage());
        }
    }

    private System.Collections.IEnumerator FadeToNextImage()
    {
        isFading = true;

        // Fade Out
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            spriteRenderer.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        // Ganti gambar
        currentIndex = (currentIndex + 1) % photos.Length;
        spriteRenderer.sprite = photos[currentIndex];

        // Fade In
        t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            spriteRenderer.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        isFading = false;
    }
}
