using UnityEngine;

/// <summary>Rotates forever (light rays behind panels and cards).</summary>
public class FxSpin : MonoBehaviour
{
    public float speed = 10f;
    void Update() => transform.localRotation = Quaternion.Euler(0, 0, transform.localEulerAngles.z + speed * Time.unscaledDeltaTime);
}
