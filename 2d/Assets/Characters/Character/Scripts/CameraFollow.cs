using UnityEngine;

namespace LionQuest.Character
{
    /// <summary>
    /// Camera controller that follows a list of targets by calculating their center point.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField] private Transform[] targets;
        
        [Header("Follow Settings")]
        [SerializeField] private float smoothSpeed = 0.125f;
        [SerializeField] private bool smoothFollow = true;
        
        [Header("Camera Offset")]
        [SerializeField] private Vector3 offset = Vector3.zero;
        
        private Vector3 velocity = Vector3.zero;
        
        private void LateUpdate()
        {
            if (targets == null || targets.Length == 0)
                return;
            
            // Calculate center point of all active targets
            Vector3 centerPoint = CalculateCenterPoint();
            
            // Apply offset
            Vector3 desiredPosition = centerPoint + offset;
            
            // Smooth follow or instant
            if (smoothFollow)
            {
                transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothSpeed);
            }
            else
            {
                transform.position = desiredPosition;
            }
        }
        
        private Vector3 CalculateCenterPoint()
        {
            if (targets == null || targets.Length == 0)
                return transform.position;
            
            Vector3 center = Vector3.zero;
            int activeTargets = 0;
            
            foreach (Transform t in targets)
            {
                if (t != null && t.gameObject.activeInHierarchy)
                {
                    center += t.position;
                    activeTargets++;
                }
            }
            
            if (activeTargets > 0)
            {
                center /= activeTargets;
            }
            else
            {
                return transform.position;
            }
            
            return center;
        }
        
        /// <summary>
        /// Set the list of targets to follow
        /// </summary>
        public void SetTargets(Transform[] newTargets)
        {
            targets = newTargets;
        }
        
        /// <summary>
        /// Add a target to the list
        /// </summary>
        public void AddTarget(Transform newTarget)
        {
            if (targets == null)
            {
                targets = new Transform[] { newTarget };
            }
            else
            {
                System.Array.Resize(ref targets, targets.Length + 1);
                targets[targets.Length - 1] = newTarget;
            }
        }
        
        /// <summary>
        /// Remove a target from the list
        /// </summary>
        public void RemoveTarget(Transform targetToRemove)
        {
            if (targets == null) return;
            
            int index = System.Array.IndexOf(targets, targetToRemove);
            if (index >= 0)
            {
                for (int i = index; i < targets.Length - 1; i++)
                {
                    targets[i] = targets[i + 1];
                }
                System.Array.Resize(ref targets, targets.Length - 1);
            }
        }
        
        /// <summary>
        /// Clear all targets
        /// </summary>
        public void ClearTargets()
        {
            targets = null;
        }
    }
}
