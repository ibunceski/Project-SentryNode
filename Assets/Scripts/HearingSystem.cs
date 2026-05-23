using System;
using UnityEngine;

/// <summary>
/// Receives world noise events and tracks whether this listener recently heard one.
/// </summary>
public class HearingSystem : MonoBehaviour
{
    /// <summary>
    /// Represents the category of an emitted noise event.
    /// </summary>
    public enum NoiseType
    {
        /// <summary>
        /// Noise produced by footsteps or locomotion.
        /// </summary>
        Footstep,

        /// <summary>
        /// Noise produced by physical objects.
        /// </summary>
        Object,

        /// <summary>
        /// Noise produced by interactions such as switches or doors.
        /// </summary>
        Interaction
    }

    private readonly struct NoiseEvent
    {
        public NoiseEvent(Vector3 position, float radius, NoiseType type)
        {
            Position = position;
            Radius = radius;
            Type = type;
        }

        public Vector3 Position { get; }
        public float Radius { get; }
        public NoiseType Type { get; }
    }

    private static event Action<NoiseEvent> NoiseReported;

    [SerializeField]
    private float noiseDuration = 5f;

    [SerializeField]
    private bool heardNoise;

    [SerializeField]
    private Vector3 noisePosition;

    [SerializeField]
    private float noiseRadius;

    [SerializeField]
    private NoiseType lastNoiseType;

    private float noiseTimer;

    /// <summary>
    /// Gets a value indicating whether this listener currently remembers a heard noise.
    /// </summary>
    public bool HeardNoise => heardNoise;

    /// <summary>
    /// Gets the latest heard noise position.
    /// </summary>
    public Vector3 NoisePosition => noisePosition;

    /// <summary>
    /// Gets the latest heard noise position.
    /// </summary>
    public Vector3 NoiseSourcePosition => noisePosition;

    /// <summary>
    /// Gets the type of the latest heard noise.
    /// </summary>
    public NoiseType LastNoiseType => lastNoiseType;

    /// <summary>
    /// Reports a noise event to all hearing systems.
    /// </summary>
    /// <param name="position">World position where noise originated.</param>
    /// <param name="radius">Maximum hearing radius of the noise.</param>
    /// <param name="type">Category of the noise.</param>
    public static void ReportNoise(Vector3 position, float radius, NoiseType type)
    {
        if (radius <= 0f)
        {
            return;
        }

        NoiseReported?.Invoke(new NoiseEvent(position, radius, type));
    }

    /// <summary>
    /// Clears the current heard-noise state immediately.
    /// </summary>
    public void ClearNoise()
    {
        heardNoise = false;
        noiseTimer = 0f;
    }

    private void OnEnable()
    {
        NoiseReported += OnNoiseReported;
    }

    private void OnDisable()
    {
        NoiseReported -= OnNoiseReported;
    }

    private void Update()
    {
        if (!heardNoise)
        {
            return;
        }

        noiseTimer -= Time.deltaTime;
        if (noiseTimer <= 0f)
        {
            heardNoise = false;
            noiseTimer = 0f;
        }
    }

    private void OnDrawGizmos()
    {
        if (!heardNoise)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(noisePosition, noiseRadius);
    }

    private void OnNoiseReported(NoiseEvent noiseEvent)
    {
        float sqrDistance = (transform.position - noiseEvent.Position).sqrMagnitude;
        float sqrRadius = noiseEvent.Radius * noiseEvent.Radius;
        if (sqrDistance > sqrRadius)
        {
            return;
        }

        heardNoise = true;
        noisePosition = noiseEvent.Position;
        noiseRadius = noiseEvent.Radius;
        lastNoiseType = noiseEvent.Type;
        noiseTimer = Mathf.Max(0.01f, noiseDuration);
    }
}
