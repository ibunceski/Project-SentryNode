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
    private static bool hasActiveSuspicion;
    private static float lastSeenTime;

    /// <summary>
    /// Resets all global alert state to defaults.
    /// </summary>
    public static void Reset()
    {
        CurrentAlert = AlertLevel.Normal;
        SharedLastKnownPosition = Vector3.zero;
        hasSeenPlayer = false;
        hasActiveSuspicion = false;
        lastSeenTime = 0f;
    }

    /// <summary>
    /// Reports that a guard has seen the player at a world position.
    /// </summary>
    /// <param name="position">The detected player position.</param>
    public static void ReportPlayerSeen(Vector3 position)
    {
        SharedLastKnownPosition = position;
        CurrentAlert = AlertLevel.FullAlert;
        hasSeenPlayer = true;
        hasActiveSuspicion = true;
        lastSeenTime = Time.time;
    }

    /// <summary>
    /// Reports a suspicious event that should raise global alert to suspicious when not already full alert.
    /// </summary>
    /// <param name="position">The suspicious world position.</param>
    public static void ReportSuspicious(Vector3 position)
    {
        SharedLastKnownPosition = position;
        hasActiveSuspicion = true;
        lastSeenTime = Time.time;

        if (CurrentAlert != AlertLevel.FullAlert)
        {
            CurrentAlert = AlertLevel.Suspicious;
        }
    }

    /// <summary>
    /// Updates alert decay from recent full alert sightings.
    /// </summary>
    public static void Tick()
    {
        if (!hasActiveSuspicion && !hasSeenPlayer)
        {
            CurrentAlert = AlertLevel.Normal;
            return;
        }

        float decayDuration = Mathf.Max(0.1f, AlertDecayTime);
        float elapsed = Mathf.Max(0f, Time.time - lastSeenTime);

        if (elapsed >= decayDuration)
        {
            CurrentAlert = AlertLevel.Normal;
            hasSeenPlayer = false;
            hasActiveSuspicion = false;
            return;
        }

        if (hasSeenPlayer && elapsed < decayDuration * 0.5f)
        {
            CurrentAlert = AlertLevel.FullAlert;
            return;
        }

        if (elapsed < decayDuration)
        {
            CurrentAlert = AlertLevel.Suspicious;
            return;
        }
    }
}
