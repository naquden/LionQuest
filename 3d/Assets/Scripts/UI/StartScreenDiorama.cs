using UnityEngine;

/// <summary>
/// Drives the Start Screen's background diorama: orbits a camera around the island
/// and periodically fires skill animations on the showcased characters.
/// </summary>
public class StartScreenDiorama : MonoBehaviour
{
    [Header("Camera Orbit")]
    [SerializeField] private Transform orbitPivot;
    [SerializeField] private Transform orbitCamera;
    [SerializeField] private float orbitDegreesPerSecond = 4f;
    [SerializeField] private float orbitRadius = 30f;
    [SerializeField] private float orbitHeight = 14f;
    [SerializeField] private float lookAtHeight = 2f;

    [Header("Character Showcase")]
    [SerializeField] private CharacterAnimator[] showcaseCharacters;
    [SerializeField] private float skillInterval = 4.5f;
    [SerializeField] private float skillStagger = 1.5f;

    private float orbitAngleDegrees;
    private float[] skillTimers;
    private int[] nextSkillIndex;

    private void Awake()
    {
        if (orbitPivot == null || orbitCamera == null)
        {
            Debug.LogError("StartScreenDiorama: orbitPivot/orbitCamera not assigned!");
            return;
        }

        if (showcaseCharacters == null || showcaseCharacters.Length == 0)
        {
            Debug.LogError("StartScreenDiorama: no showcaseCharacters assigned!");
            return;
        }

        skillTimers = new float[showcaseCharacters.Length];
        nextSkillIndex = new int[showcaseCharacters.Length];
        for (int i = 0; i < showcaseCharacters.Length; i++)
        {
            skillTimers[i] = -i * skillStagger;
        }
    }

    private void Update()
    {
        UpdateOrbit();
        UpdateSkillShowcase();
    }

    private void UpdateOrbit()
    {
        if (orbitPivot == null || orbitCamera == null) return;

        orbitAngleDegrees += orbitDegreesPerSecond * Time.deltaTime;
        float radians = orbitAngleDegrees * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(Mathf.Sin(radians) * orbitRadius, orbitHeight, Mathf.Cos(radians) * orbitRadius);
        orbitCamera.position = orbitPivot.position + offset;
        orbitCamera.LookAt(orbitPivot.position + Vector3.up * lookAtHeight);
    }

    private void UpdateSkillShowcase()
    {
        if (showcaseCharacters == null) return;

        for (int i = 0; i < showcaseCharacters.Length; i++)
        {
            skillTimers[i] += Time.deltaTime;
            if (skillTimers[i] < skillInterval) continue;

            skillTimers[i] = 0f;

            CharacterAnimator animator = showcaseCharacters[i];
            if (animator == null) continue;

            switch (nextSkillIndex[i])
            {
                case 0: animator.TriggerSkill1(); break;
                case 1: animator.TriggerSkill2(); break;
                default: animator.TriggerSkill3(); break;
            }
            nextSkillIndex[i] = (nextSkillIndex[i] + 1) % 3;
        }
    }
}
