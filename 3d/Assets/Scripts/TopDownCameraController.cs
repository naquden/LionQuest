using UnityEngine;

/// <summary>
/// Tilted top-down camera controller for isometric-style gameplay
/// Provides a fixed-angle camera that follows the player
/// </summary>
public class TopDownCameraController : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;
    
    [Header("Camera Position")]
    [SerializeField] private float distance = 15f; // Distance from target
    [SerializeField] private float height = 10f; // Height above target
    [SerializeField] private float followSpeed = 10f;
    [SerializeField] private Vector3 lookAtOffset = Vector3.zero; // Offset for where camera looks (useful for aiming above/below player)
    
    [Header("Camera Angle")]
    [SerializeField] private float tiltAngle = 45f; // Angle from horizontal (0 = top-down, 90 = side view)
    [SerializeField] private float rotationAngle = 45f; // Rotation around Y-axis for isometric look
    
    [Header("Camera Settings")]
    [SerializeField] private bool useOrthographic = false;
    [SerializeField] private float orthographicSize = 10f;
    [SerializeField] private float fieldOfView = 60f;
    
    private Camera cam;
    private Vector3 currentVelocity;
    
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
        // If no target is assigned, try to find the player
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }
        
        // Set initial position
        if (target != null)
        {
            UpdateCameraPosition();
        }
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        UpdateCameraPosition();
    }
    
    private void UpdateCameraPosition()
    {
        // Calculate the camera's desired position based on tilt and rotation
        // Convert angles to radians for calculations
        float tiltRad = tiltAngle * Mathf.Deg2Rad;
        float rotationRad = rotationAngle * Mathf.Deg2Rad;
        
        // Calculate horizontal distance based on tilt angle
        float horizontalDistance = distance * Mathf.Cos(tiltRad);
        float verticalDistance = distance * Mathf.Sin(tiltRad);
        
        // Calculate position offset based on rotation angle
        float offsetX = horizontalDistance * Mathf.Sin(rotationRad);
        float offsetZ = -horizontalDistance * Mathf.Cos(rotationRad);
        float offsetY = verticalDistance + height;
        
        // Calculate desired position relative to target
        Vector3 desiredPosition = target.position + new Vector3(offsetX, offsetY, offsetZ);
        
        // Smoothly follow the target
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, 1f / followSpeed);
        
        // Look at the target (with optional offset)
        Vector3 lookAtPoint = target.position + lookAtOffset;
        transform.LookAt(lookAtPoint);
    }
    
    /// <summary>
    /// Set the target to follow
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
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

