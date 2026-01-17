using UnityEngine;

/// <summary>
/// A pickup item that heals the player on contact.
/// </summary>
public class HealthPickup : MonoBehaviour
{
    [SerializeField] private float healAmount = 25f;
    [SerializeField] private GameObject pickupEffect; // Optional particle effect

    private void OnTriggerEnter(Collider other)
    {
        TopDownPlayerController player = other.GetComponent<TopDownPlayerController>();
        if (player != null && player.IsAlive)
        {
            // Only pickup if damaged
            if (player.CurrentHealth < player.MaxHealth)
            {
                player.Heal(healAmount);
                
                if (pickupEffect != null)
                {
                    Instantiate(pickupEffect, transform.position, Quaternion.identity);
                }
                
                Debug.Log($"[Pickup] Player picked up health pack (+{healAmount})");
                Destroy(gameObject);
            }
        }
    }
}
