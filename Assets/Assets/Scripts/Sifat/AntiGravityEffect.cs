using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Tempel otomatis ke bola saat efek anti-gravity aktif.
/// Menangani stacking (refCount) dan restore nilai asli ketika tak ada efek.
[DisallowMultipleComponent]
public class AntiGravityEffect : MonoBehaviour
{
    struct Entry { public float gravity, drag; public PhysicsMaterial2D mat; public float until; }
    readonly List<Entry> entries = new();

    Rigidbody2D rb;
    Collider2D col;

    float origGravity, origDrag;
    PhysicsMaterial2D origMat;
    bool captured;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    void CaptureOriginal()
    {
        if (captured || !rb || !col) return;
        captured = true;
        origGravity = rb.gravityScale;
        origDrag = rb.drag;
        origMat = col.sharedMaterial;
    }

    /// Tambah/refresh satu efek sampai `duration` detik dari sekarang.
    public void Apply(float gravity, float drag, PhysicsMaterial2D mat, float duration)
    {
        if (!rb || !col) return;
        CaptureOriginal();

        float until = Time.time + duration;
        // tambahkan entri
        entries.Add(new Entry { gravity = gravity, drag = drag, mat = mat, until = until });
        RecomputeNow();
        StartCoroutine(CleanupLoop());
    }

    IEnumerator CleanupLoop()
    {
        while (entries.Count > 0)
        {
            bool changed = false;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (Time.time >= entries[i].until)
                {
                    entries.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                // ✔️ Jangan akses index saat kosong
                if (entries.Count == 0) break;
                RecomputeNow();
            }

            yield return null;
        }

        // Pulihkan nilai asli ketika sudah tidak ada efek
        if (captured && rb && col)
        {
            rb.gravityScale = origGravity;
            rb.drag = origDrag;
            col.sharedMaterial = origMat;
        }
        captured = false;
    }

    void RecomputeNow()
    {
        if (!rb || !col) return;

        // ✔️ Aman: kalau tak ada efek aktif, pulihkan nilai asli
        if (entries.Count == 0)
        {
            if (captured)
            {
                rb.gravityScale = origGravity;
                rb.drag = origDrag;
                col.sharedMaterial = origMat;
            }
            return;
        }

        // Pakai entry terakhir (efek terbaru menimpa yang lama)
        var last = entries[entries.Count - 1];
        rb.gravityScale = last.gravity;
        rb.drag = last.drag;
        col.sharedMaterial = last.mat;
    }
}
