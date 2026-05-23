using UnityEngine;

/// <summary>
/// Shared static alert coordinator for all guards in the scene.
/// </summary>
public static class GuardAlertSystem
{
    /// <summary>
    /// Global alert tiers shared across all guards.
    /// </summary>
    public enum AlertLevel
    {
        /// <summary>
        /// No active global alert.
        /// </summary>
        Normal,

        /// <summary>
        /// Elevated concern after recent detections.
        /// </summary>
        Suspicious,

        /// <summary>
        /// Active full alert after a confirmed player sighting.
        /// </summary>
        FullAlert
    }

    /// <summary>
    /// Gets the current global alert level.
    /// </summary>
    public static AlertLevel CurrentAlert { get; private set; } = AlertLevel.Normal;

    /// <summary>
    /// Gets the globally shared last known player position.
    /// </summary>
    public static Vector3 SharedLastKnownPosition { get; private set; }

    /// <summary>
    /// Gets or sets the decay duration in seconds from full alert back to normal when no new sightings occur.
    /// </summary>
    public static float AlertDecayTime { get; set; } = 15f;

    private static bool hasSeenPlayer;
    private static float lastSeenTime;

    /// <summary>
    /// Reports that a guard has seen the player at a world position.
    /// </summary>
    /// <param name="position">The detected player position.</param>
    public static void ReportPlayerSeen(Vector3 position)
    {
        SharedLastKnownPosition = position;
        CurrentAlert = AlertLevel.FullAlert;
        hasSeenPlayer = true;
        lastSeenTime = Time.time;
    }

    /// <summary>
    /// Updates alert decay from recent full alert sightings.
    /// </summary>
    public static void Tick()
    {
        if (!hasSeenPlayer)
        {
            CurrentAlert = AlertLevel.Normal;
            return;
        }

        float decayDuration = Mathf.Max(0.1f, AlertDecayTime);
        float elapsed = Mathf.Max(0f, Time.time - lastSeenTime);

        if (elapsed >= decayDuration)
        {
            CurrentAlert = AlertLevel.Normal;
            return;
        }

        if (elapsed >= decayDuration * 0.5f)
        {
            CurrentAlert = AlertLevel.Suspicious;
            return;
        }

        CurrentAlert = AlertLevel.FullAlert;
    }
}
