using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles World Space health bar updates and billboarding.
/// Automatically connects to Enemy or TopDownPlayerController on parent.
/// </summary>
public class HealthBar : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The foreground image that will be filled based on health")]
    [SerializeField] private Image fillImage;
    
    [Tooltip("The canvas component (optional, will try to find)")]
    [SerializeField] private Canvas canvas;
    
    [Header("Settings")]
    [Tooltip("If true, always faces the camera")]
    [SerializeField] private bool billboard = true;
    [Tooltip("Offset above the character")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2f, 0);
    
    private Camera mainCamera;
    private Enemy enemy;
    private TopDownPlayerController player;
    
    private void Awake()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
    }
    
    private void Start()
    {
        mainCamera = Camera.main;
        if (canvas != null && mainCamera != null)
        {
            canvas.worldCamera = mainCamera;
        }
        
        // Auto-connect to health components
        ConnectToHealthComponent();
    }
    
    private void ConnectToHealthComponent()
    {
        // Try Enemy
        enemy = GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            enemy.OnHealthChanged += UpdateHealth;
            UpdateHealth(enemy.CurrentHealth, enemy.MaxHealth);
            Debug.Log($"[HealthBar] Connected to Enemy: {enemy.gameObject.name}");
            return;
        }
        
        // Try Player
        player = GetComponentInParent<TopDownPlayerController>();
        if (player != null)
        {
            player.OnHealthChanged += UpdateHealth;
            UpdateHealth(player.CurrentHealth, player.MaxHealth);
            Debug.Log($"[HealthBar] Connected to Player: {player.gameObject.name}");
            return;
        }
        
        Debug.LogWarning($"[HealthBar] Could not find Enemy or TopDownPlayerController on parent of {gameObject.name}");
    }
    
    private void LateUpdate()
    {
        if (billboard && mainCamera != null)
        {
            // Face the camera
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                             mainCamera.transform.rotation * Vector3.up);
        }
    }
    
    private void UpdateHealth(float current, float max)
    {
        if (fillImage != null)
        {
            float pct = Mathf.Clamp01(current / max);
            fillImage.fillAmount = pct;
            Debug.Log($"[HealthBar] Updated fill to {pct:F2} ({current}/{max})"); 
        }
        else
        {
            Debug.LogWarning($"[HealthBar] Fill Image is not assigned on {gameObject.name}!");
        }
    }
    
    private void OnDestroy()
    {
        if (enemy != null) enemy.OnHealthChanged -= UpdateHealth;
        if (player != null) player.OnHealthChanged -= UpdateHealth;
    }
}
