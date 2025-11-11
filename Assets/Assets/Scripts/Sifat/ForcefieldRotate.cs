using UnityEngine;

/// Tempelkan ke GameObject forcefield.
/// Akan memutar terus di sekitar sumbu yang dipilih.
public class ForcefieldRotate : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Vector3 rotationSpeed = new Vector3(0f, 0f, 30f);
    // contoh default: 30 derajat per detik di sumbu Z

    void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
    }
}
