using UnityEngine;

/// <summary>
/// Tilted top-down camera controller for isometric-style gameplay
/// Provides a fixed-angle camera that follows a single player or the centralized point between multiple players
/// </summary>
public class TopDownCameraController : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Single target to follow (legacy support). If players list is empty, will use this.")]
    [SerializeField] private Transform target;
    
    [Tooltip("List of players to track. Camera will follow the centralized point between all active players.")]
    [SerializeField] private Transform[] players;
    
    [Tooltip("If true, automatically find all GameObjects with 'Player' tag and add them to the players list.")]
    [SerializeField] private bool autoFindPlayers = false;
    
    [Header("Camera Position")]
    [SerializeField] private float distance = 15f; // Distance from target
    [SerializeField] private float height = 10f; // Height above target
    [SerializeField] private float followSpeed = 10f;
    [SerializeField] private Vector3 lookAtOffset = Vector3.zero; // Offset for where camera looks (useful for aiming above/below player)
    
    [Header("Multi-Player Settings")]
    [Tooltip("Minimum distance between players before camera starts zooming out. 0 = no zoom adjustment.")]
    [SerializeField] private float minPlayerDistance = 0f;
    
    [Tooltip("Maximum distance between players. Camera will zoom out to fit all players within this distance.")]
    [SerializeField] private float maxPlayerDistance = 20f;
    
    [Tooltip("Additional distance added to camera based on player spread. Higher = more zoom out when players are far apart.")]
    [SerializeField] private float distanceMultiplier = 0.5f;
    
    [Header("Camera Angle")]
    [SerializeField] private float tiltAngle = 45f; // Angle from horizontal (0 = top-down, 90 = side view)
    [SerializeField] private float rotationAngle = 45f; // Rotation around Y-axis for isometric look
    
    [Header("Camera Settings")]
    [SerializeField] private bool useOrthographic = false;
    [SerializeField] private float orthographicSize = 10f;
    [SerializeField] private float fieldOfView = 60f;
    
    private Camera cam;
    private Vector3 currentVelocity;
    private Vector3 centralizedPosition;
    
    private void Awake()
    {
        cam = GetComponent<Camera>();
        
        // Set camera projection
        if (useOrthographic)
        {
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
        }
        else
        {
            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
        }
        
        // Set initial rotation for tilted isometric view
        transform.rotation = Quaternion.Euler(tiltAngle, rotationAngle, 0f);
    }
    
    private void Start()
    {
        // Auto-find players if enabled
        if (autoFindPlayers)
        {
            GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
            if (playerObjects.Length > 0)
            {
                players = new Transform[playerObjects.Length];
                for (int i = 0; i < playerObjects.Length; i++)
                {
                    players[i] = playerObjects[i].transform;
                }
            }
            else
            {
                Debug.LogError($"TopDownCameraController on '{gameObject.name}': 'autoFindPlayers' is enabled but no GameObjects with 'Player' tag were found in the scene! Please ensure players are tagged correctly or manually assign the 'players' array.");
            }
        }
        
        // Validate that we have at least one target (either players list or single target)
        if ((players == null || players.Length == 0) && target == null)
        {
            Debug.LogError($"TopDownCameraController on '{gameObject.name}': No players or target assigned! Camera will not follow anything. Please assign either the 'players' array or 'target' in the inspector, or enable 'autoFindPlayers'.");
        }
        
        // Set initial position
        if (HasValidTarget())
        {
            UpdateCameraPosition();
        }
        else
        {
            Debug.LogError($"TopDownCameraController on '{gameObject.name}': No valid target found! Camera will not update. Please ensure players are assigned and active, or assign a target.");
        }
    }
    
    private void LateUpdate()
    {
        if (!HasValidTarget()) return;
        
        UpdateCameraPosition();
    }
    
    /// <summary>
    /// Check if there's a valid target (either single target or at least one player in the list)
    /// </summary>
    private bool HasValidTarget()
    {
        if (players != null && players.Length > 0)
        {
            // Check if at least one player is valid
            foreach (Transform player in players)
            {
                if (player != null) return true;
            }
        }
        return target != null;
    }
    
    /// <summary>
    /// Calculate the centralized position between all active players
    /// </summary>
    private Vector3 CalculateCentralizedPosition()
    {
        // If using single target, return its position
        if (target != null && (players == null || players.Length == 0))
        {
            return target.position;
        }
        
        // Calculate average position of all active players
        Vector3 sum = Vector3.zero;
        int activePlayerCount = 0;
        
        if (players != null)
        {
            foreach (Transform player in players)
            {
                if (player != null)
                {
                    sum += player.position;
                    activePlayerCount++;
                }
            }
        }
        
        if (activePlayerCount == 0)
        {
            // Fallback to single target if available
            if (target != null)
            {
                return target.position;
            }
            return transform.position; // No valid targets, stay in place
        }
        
        centralizedPosition = sum / activePlayerCount;
        return centralizedPosition;
    }
    
    /// <summary>
    /// Calculate the maximum distance between players for dynamic zoom
    /// </summary>
    private float CalculatePlayerSpread()
    {
        if (players == null || players.Length < 2)
        {
            return 0f;
        }
        
        float maxDistance = 0f;
        
        // Find the maximum distance between any two players
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null) continue;
            
            for (int j = i + 1; j < players.Length; j++)
            {
                if (players[j] == null) continue;
                
                float dist = Vector3.Distance(players[i].position, players[j].position);
                if (dist > maxDistance)
                {
                    maxDistance = dist;
                }
            }
        }
        
        return maxDistance;
    }
    
    private void UpdateCameraPosition()
    {
        // Calculate centralized position between all players
        Vector3 centerPosition = CalculateCentralizedPosition();
        
        // Calculate dynamic distance based on player spread
        float currentDistance = distance;
        if (players != null && players.Length > 1)
        {
            float playerSpread = CalculatePlayerSpread();
            if (playerSpread > minPlayerDistance)
            {
                // Add extra distance based on how far apart players are
                float spreadFactor = Mathf.Clamp01((playerSpread - minPlayerDistance) / maxPlayerDistance);
                currentDistance += spreadFactor * distanceMultiplier * maxPlayerDistance;
            }
        }
        
        // Calculate the camera's desired position based on tilt and rotation
        // Convert angles to radians for calculations
        float tiltRad = tiltAngle * Mathf.Deg2Rad;
        float rotationRad = rotationAngle * Mathf.Deg2Rad;
        
        // Calculate horizontal distance based on tilt angle
        float horizontalDistance = currentDistance * Mathf.Cos(tiltRad);
        float verticalDistance = currentDistance * Mathf.Sin(tiltRad);
        
        // Calculate position offset based on rotation angle
        float offsetX = horizontalDistance * Mathf.Sin(rotationRad);
        float offsetZ = -horizontalDistance * Mathf.Cos(rotationRad);
        float offsetY = verticalDistance + height;
        
        // Calculate desired position relative to centralized position
        Vector3 desiredPosition = centerPosition + new Vector3(offsetX, offsetY, offsetZ);
        
        // Smoothly follow the centralized position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, 1f / followSpeed);
        
        // Look at the centralized position (with optional offset)
        Vector3 lookAtPoint = centerPosition + lookAtOffset;
        transform.LookAt(lookAtPoint);
    }
    
    /// <summary>
    /// Set the target to follow (legacy method for single target)
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    /// <summary>
    /// Set the list of players to track
    /// </summary>
    public void SetPlayers(Transform[] newPlayers)
    {
        players = newPlayers;
    }
    
    /// <summary>
    /// Add a player to the tracking list
    /// </summary>
    public void AddPlayer(Transform player)
    {
        if (player == null) return;
        
        if (players == null)
        {
            players = new Transform[] { player };
        }
        else
        {
            // Check if player already exists
            foreach (Transform p in players)
            {
                if (p == player) return;
            }
            
            // Add new player
            System.Array.Resize(ref players, players.Length + 1);
            players[players.Length - 1] = player;
        }
    }
    
    /// <summary>
    /// Remove a player from the tracking list
    /// </summary>
    public void RemovePlayer(Transform player)
    {
        if (players == null || player == null) return;
        
        int index = System.Array.IndexOf(players, player);
        if (index >= 0)
        {
            // Create new array without the removed player
            Transform[] newPlayers = new Transform[players.Length - 1];
            for (int i = 0, j = 0; i < players.Length; i++)
            {
                if (i != index)
                {
                    newPlayers[j++] = players[i];
                }
            }
            players = newPlayers;
        }
    }
    
    /// <summary>
    /// Get the current centralized position between all players
    /// </summary>
    public Vector3 GetCentralizedPosition()
    {
        return centralizedPosition;
    }
    
    /// <summary>
    /// Get the camera component (for viewport checking)
    /// </summary>
    public Camera GetCamera()
    {
        return cam;
    }
    
    /// <summary>
    /// Adjust camera angle at runtime
    /// </summary>
    public void SetTiltAngle(float angle)
    {
        tiltAngle = Mathf.Clamp(angle, 0f, 90f);
    }
    
    /// <summary>
    /// Adjust camera rotation at runtime
    /// </summary>
    public void SetRotationAngle(float angle)
    {
        rotationAngle = angle;
    }
}

