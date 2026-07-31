using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Instant hitscan beam: raycasts on Fire(), damages everything in range up to pierceCount, then
/// flashes and fades its LineRenderer visual over visualDuration before destroying itself.
/// Attach to beam prefabs (laser, etc.). Unlike Projectile, there is no travelling trigger volume —
/// all damage resolution happens synchronously inside Fire().
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class LaserBeam : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugBeam = false;

    private LineRenderer lineRenderer;
    private float visualDuration;
    private float elapsed;
    private bool isFiring = false;
    private Color baseStartColor;
    private Color baseEndColor;
    private float baseWidth;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
    }

    /// <summary>
    /// Fire the beam: raycast from origin along direction, damage up to pierceCount targets tagged
    /// targetTag (skipping owner and its children), and start the flash-and-fade visual.
    /// </summary>
    public void Fire(Vector3 origin, Vector3 direction, float range, float damage,
        float knockbackForce, int pierceCount, float visualDuration, float beamWidth,
        GameObject owner, string targetTag)
    {
        this.visualDuration = visualDuration > 0f ? visualDuration : 0.25f;
        elapsed = 0f;
        isFiring = true;

        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, range);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 knockbackDir = dir;
        knockbackDir.y = 0f;
        knockbackDir = knockbackDir.sqrMagnitude > 0.0001f ? knockbackDir.normalized : dir;

        Vector3 endpoint = origin + dir * range;
        int hitsDealt = 0;
        var alreadyHit = new HashSet<GameObject>();

        foreach (RaycastHit hit in hits)
        {
            GameObject other = hit.collider.gameObject;

            if (owner != null && (other == owner || other.transform.IsChildOf(owner.transform)))
            {
                continue;
            }

            if (hit.collider.isTrigger)
            {
                continue;
            }

            bool isTarget = !string.IsNullOrEmpty(targetTag) && other.CompareTag(targetTag);

            if (!isTarget)
            {
                // Solid, non-target collider (e.g. a wall) stops the beam's visual reach.
                endpoint = hit.point;
                if (debugBeam)
                {
                    Debug.Log($"[LaserBeam] Stopped by non-target collider '{other.name}' at {hit.point}");
                }
                break;
            }

            if (alreadyHit.Contains(other) || hitsDealt >= pierceCount)
            {
                continue;
            }
            alreadyHit.Add(other);
            hitsDealt++;

            Enemy enemy = other.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, knockbackDir, knockbackForce);
                if (debugBeam)
                {
                    Debug.Log($"[LaserBeam] Hit enemy {other.name} for {damage} damage!");
                }
            }
            else
            {
                TopDownPlayerController player = other.GetComponent<TopDownPlayerController>();
                if (player != null)
                {
                    player.TakeDamage(damage, knockbackDir, knockbackForce);
                    if (debugBeam)
                    {
                        Debug.Log($"[LaserBeam] Hit player {other.name} for {damage} damage!");
                    }
                }
            }

            if (hitsDealt >= pierceCount)
            {
                endpoint = hit.point;
                break;
            }
        }

        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, endpoint);
        lineRenderer.startWidth = beamWidth;
        lineRenderer.endWidth = beamWidth;
        baseStartColor = lineRenderer.startColor;
        baseEndColor = lineRenderer.endColor;
        baseWidth = beamWidth;

        Destroy(gameObject, this.visualDuration);
    }

    private void Update()
    {
        if (!isFiring) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / visualDuration);
        float fade = 1f - t;

        Color start = baseStartColor;
        Color end = baseEndColor;
        start.a = baseStartColor.a * fade;
        end.a = baseEndColor.a * fade;
        lineRenderer.startColor = start;
        lineRenderer.endColor = end;

        float width = baseWidth * fade;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
    }
}
