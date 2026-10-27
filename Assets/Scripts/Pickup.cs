using UnityEngine;

public class Pickup : MonoBehaviour
{
    public enum PickupType { Pellet, PowerPellet, Cherry }

    public PickupType type;

    // Fired when a pickup is destroyed in a loaded scene (GameManager counts pellets with it).
    public static event System.Action<Pickup> Eaten;

    void OnDestroy()
    {
        // Ignore destruction caused by the scene unloading (Exit button, Game Over, stopping Play).
        if (!gameObject.scene.isLoaded) return;

        Eaten?.Invoke(this);
    }
}