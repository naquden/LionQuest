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
    // Defaults match what every player-following scene is generated with (see Level1Generator /
    // AgentGymGenerator), so a rig added by hand frames the player like the shipped ones instead of
    // needing the numbers rediscovered.
    [SerializeField] private float distance = 11f; // Distance from target
    [Tooltip("Vertical offset of the point the camera frames. This is the 'aim at the head, not the " +
             "feet' control — player pivots sit at ground level.")]
    [SerializeField] private float height = 0f;
    [SerializeField] private float followSpeed = 10f;
    // There was also a `lookAtOffset` Vector3 here. It was never read by anything, and it could not
    // meaningfully be wired: this rig holds a fixed rotation on purpose (LookAt jitters as the target
    // moves), so the only thing an aim offset can do is shift the framed point — which is exactly
    // what `height` above already does. Two fields for one job meant the scenes set lookAtOffset to
    // (0, 1.5, 0) expecting head-height framing and silently got none.

    [Header("Multi-Player Settings")]
    [Tooltip("Minimum distance between players before camera starts zooming out. 0 = no zoom adjustment.")]
    [SerializeField] private float minPlayerDistance = 0f;
    
    [Tooltip("Spread at which the camera reaches its widest framing. Beyond this the solve keeps " +
             "widening anyway — this is a comfort reference, not a cap.")]
    [SerializeField] private float maxPlayerDistance = 20f;

    [Tooltip("How much of the extra spread to convert into camera distance. 1 = fit the party exactly, " +
             ">1 = leave headroom around them.")]
    [SerializeField] private float distanceMultiplier = 0.5f;

    [Tooltip("Hard ceiling on pull-back so a stray player cannot zoom the party into specks. Past " +
             "this the camera stops widening and far players simply leave frame.")]
    [SerializeField] private float maxCameraDistance = 90f;

    [Header("Camera Angle")]
    [SerializeField] private float tiltAngle = 60f; // Angle from horizontal (0 = top-down, 90 = side view)
    [SerializeField] private float rotationAngle = 45f; // Rotation around Y-axis for isometric look
    
    [Header("Camera Settings")]
    [SerializeField] private bool useOrthographic = false;
    [SerializeField] private float orthographicSize = 10f;
    [SerializeField] private float fieldOfView = 60f;
    
    private Camera cam;
    private Vector3 currentVelocity;
    private Vector3 centralizedPosition;

    /// <summary>
    /// Whether a tracked transform should influence framing. A corpse must not: it cannot walk back
    /// to the party, so including it drags the view toward a body forever and pulls the living
    /// players toward the edge of frame. Cached per transform to avoid a GetComponent per frame per
    /// player. A transform with no controller (a plain target, or a test stand-in) always counts.
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<Transform, TopDownPlayerController> controllerCache
        = new System.Collections.Generic.Dictionary<Transform, TopDownPlayerController>();

    private bool CountsForFraming(Transform player)
    {
        if (player == null) return false;

        if (!controllerCache.TryGetValue(player, out TopDownPlayerController controller))
        {
            controller = player.GetComponent<TopDownPlayerController>();
            controllerCache[player] = controller;
        }

        return controller == null || controller.IsAlive;
    }

    /// <summary>
    /// True while at least one tracked player is alive. When it goes false the framing switches back
    /// to counting everybody, so the game-over shot still frames the bodies rather than leaving the
    /// camera parked wherever it happened to be when the last player fell.
    /// </summary>
    private bool AnyLivingTrackedPlayer()
    {
        if (players == null) return false;

        foreach (Transform player in players)
        {
            if (CountsForFraming(player)) return true;
        }
        return false;
    }
    
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
    /// Whether this transform contributes to this frame's framing. <paramref name="livingOnly"/> is
    /// decided once per frame by UpdateCameraPosition so the centre and the spread always agree on
    /// which players they are looking at — computing it separately in each would let them disagree on
    /// the frame a player dies and snap the view.
    /// </summary>
    private bool IsFramingTarget(Transform player, bool livingOnly)
    {
        if (player == null) return false;
        return !livingOnly || CountsForFraming(player);
    }

    /// <summary>
    /// Calculate the centralized position between all active players
    /// </summary>
    private Vector3 CalculateCentralizedPosition(bool livingOnly)
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
                if (IsFramingTarget(player, livingOnly))
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
    private float CalculatePlayerSpread(bool livingOnly)
    {
        if (players == null || players.Length < 2)
        {
            return 0f;
        }

        float maxDistance = 0f;

        // Find the maximum distance between any two players
        for (int i = 0; i < players.Length; i++)
        {
            if (!IsFramingTarget(players[i], livingOnly)) continue;

            for (int j = i + 1; j < players.Length; j++)
            {
                if (!IsFramingTarget(players[j], livingOnly)) continue;

                float dist = Vector3.Distance(players[i].position, players[j].position);
                if (dist > maxDistance)
                {
                    maxDistance = dist;
                }
            }
        }

        return maxDistance;
    }

    /// <summary>
    /// Camera distance needed to fit a party of the given spread, capped at maxCameraDistance.
    ///
    /// The old solve was <c>distance + Clamp01((spread - min) / maxPlayerDistance) * multiplier *
    /// maxPlayerDistance</c>, which saturates: once the party is maxPlayerDistance apart the factor
    /// pins at 1 and the camera never widens again, so in Level1 (maxPlayerDistance 24, multiplier
    /// 0.5 → hard stop at distance 23) two players who split to opposite beaches — well over 24
    /// apart on a 68-unit island — simply walked out of frame with no way to see them. Widening is
    /// now proportional with no saturation point, and bounded by an explicit ceiling instead of by an
    /// accident of the comfort reference.
    /// </summary>
    private float SolveCameraDistance(float spread)
    {
        float excess = spread - minPlayerDistance;
        if (excess <= 0f) return distance;

        return Mathf.Min(distance + excess * distanceMultiplier, maxCameraDistance);
    }

    private void UpdateCameraPosition()
    {
        // Decided once and passed down: see IsFramingTarget.
        bool livingOnly = AnyLivingTrackedPlayer();

        // Calculate centralized position between all players
        Vector3 centerPosition = CalculateCentralizedPosition(livingOnly);

        // Calculate dynamic distance based on player spread
        float currentDistance = distance;
        if (players != null && players.Length > 1)
        {
            currentDistance = SolveCameraDistance(CalculatePlayerSpread(livingOnly));
        }

        // Calculate the camera's desired position based on tilt and rotation
        // Convert angles to radians for calculations
        float tiltRad = tiltAngle * Mathf.Deg2Rad;
        float rotationRad = rotationAngle * Mathf.Deg2Rad;
        
        // Calculate horizontal distance based on tilt angle
        float horizontalDistance = currentDistance * Mathf.Cos(tiltRad);
        float verticalDistance = currentDistance * Mathf.Sin(tiltRad);
        
        // Calculate position offset based on rotation angle
        float offsetX = -horizontalDistance * Mathf.Sin(rotationRad);
        float offsetZ = -horizontalDistance * Mathf.Cos(rotationRad);
        float offsetY = verticalDistance;
        
        // Apply height offset to the target position (Look at head instead of feet)
        Vector3 targetPosWithHeight = centerPosition + new Vector3(0f, height, 0f);
        
        // Calculate desired position relative to centralized position
        Vector3 desiredPosition = targetPosWithHeight + new Vector3(offsetX, offsetY, offsetZ);
        
        // Smoothly follow the centralized position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, 1f / followSpeed);
        
        // Maintain fixed rotation (do NOT use LookAt, as it causes jitter when target moves)
        transform.rotation = Quaternion.Euler(tiltAngle, rotationAngle, 0f);
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

