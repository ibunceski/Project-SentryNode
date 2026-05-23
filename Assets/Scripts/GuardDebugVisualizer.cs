using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Renders debug information for guard AI systems without changing runtime logic or state.
/// </summary>
public class GuardDebugVisualizer : MonoBehaviour
{
    [SerializeField]
    private bool showDebug = true;

    [SerializeField]
    private float labelHeight = 2.2f;

    private GuardAI guardAI;
    private VisionSystem visionSystem;
    private HearingSystem hearingSystem;
    private NavMeshAgent navMeshAgent;
    private GUIStyle boldStyle;
    private GUIStyle regularStyle;
    private GUIStyle alertStyle;
    private GUIStyle barTextStyle;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void OnGUI()
    {
        if (!showDebug)
        {
            return;
        }

        if (guardAI == null)
        {
            CacheReferences();
            if (guardAI == null)
            {
                return;
            }
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 worldPosition = transform.position + Vector3.up * labelHeight;
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z <= 0f)
        {
            return;
        }

        EnsureStyles();

        string stateLine = "STATE: " + guardAI.CurrentGuardState.ToString().ToUpperInvariant();
        string nodeLine = "NODE: " + (string.IsNullOrEmpty(guardAI.ActiveNodeName) ? "None" : guardAI.ActiveNodeName);
        string alertLine = "ALERT: " + GuardAlertSystem.CurrentAlert.ToString().ToUpperInvariant();
        Color alertColor = EvaluateAlertColor(GuardAlertSystem.CurrentAlert);

        float x = screenPoint.x;
        float y = Screen.height - screenPoint.y;
        DrawOutlinedLabel(new Vector2(x, y), stateLine, boldStyle);
        DrawOutlinedLabel(new Vector2(x, y + 18f), nodeLine, regularStyle);
        DrawOutlinedLabelColored(new Vector2(x, y + 36f), alertLine, alertStyle, alertColor);
        DrawSuspicionBar(new Vector2(x, y - 18f));
    }

    private void OnDrawGizmos()
    {
        DrawDebugGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        DrawDebugGizmos();
    }

    private void DrawDebugGizmos()
    {
        if (!showDebug)
        {
            return;
        }

        if (guardAI == null || hearingSystem == null || navMeshAgent == null)
        {
            CacheReferences();
        }

        DrawNavMeshPath();
        DrawLastKnownPosition();
        DrawNoisePulse();
    }

    private void DrawNavMeshPath()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            return;
        }

        NavMeshPath path = navMeshAgent.path;
        if (path == null || path.corners == null || path.corners.Length < 2)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            Gizmos.DrawLine(path.corners[i], path.corners[i + 1]);
        }
    }

    private void DrawLastKnownPosition()
    {
        if (visionSystem == null || !visionSystem.HasLastKnownPosition)
        {
            return;
        }

        Vector3 p = visionSystem.LastKnownPosition;
        float halfSize = 0.25f;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            p + new Vector3(-halfSize, 0f, -halfSize),
            p + new Vector3(halfSize, 0f, halfSize));
        Gizmos.DrawLine(
            p + new Vector3(-halfSize, 0f, halfSize),
            p + new Vector3(halfSize, 0f, -halfSize));
    }

    private void DrawNoisePulse()
    {
        if (hearingSystem == null || !hearingSystem.HeardNoise)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
        float radius = Mathf.Lerp(0.3f, 0.8f, pulse);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(hearingSystem.NoisePosition, radius);
    }

    private void DrawOutlinedLabel(Vector2 position, string text, GUIStyle style)
    {
        GUIStyle outlineStyle = new GUIStyle(style);
        outlineStyle.normal.textColor = Color.black;

        Rect rect = new Rect(position.x - 120f, position.y - 10f, 240f, 22f);
        Rect outlineRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height);
        GUI.Label(outlineRect, text, outlineStyle);
        GUI.Label(rect, text, style);
    }

    private void DrawOutlinedLabelColored(Vector2 position, string text, GUIStyle style, Color textColor)
    {
        GUIStyle fillStyle = new GUIStyle(style);
        fillStyle.normal.textColor = textColor;

        GUIStyle outlineStyle = new GUIStyle(style);
        outlineStyle.normal.textColor = Color.black;

        Rect rect = new Rect(position.x - 120f, position.y - 10f, 240f, 22f);
        Rect outlineRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height);
        GUI.Label(outlineRect, text, outlineStyle);
        GUI.Label(rect, text, fillStyle);
    }

    private void DrawSuspicionBar(Vector2 center)
    {
        if (visionSystem == null)
        {
            return;
        }

        float normalized = Mathf.Clamp01(visionSystem.Suspicion / 100f);
        Color fillColor = EvaluateSuspicionColor(normalized);

        float width = 120f;
        float height = 12f;
        Rect outer = new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        Rect inner = new Rect(outer.x + 1f, outer.y + 1f, (width - 2f) * normalized, height - 2f);

        DrawRect(outer, Color.black);
        DrawRect(new Rect(outer.x + 1f, outer.y + 1f, width - 2f, height - 2f), new Color(0.12f, 0.12f, 0.12f, 0.9f));
        DrawRect(inner, fillColor);

        if (barTextStyle == null)
        {
            barTextStyle = new GUIStyle(GUI.skin.label);
            barTextStyle.alignment = TextAnchor.MiddleCenter;
            barTextStyle.fontSize = 10;
            barTextStyle.fontStyle = FontStyle.Bold;
            barTextStyle.normal.textColor = Color.white;
        }

        string text = Mathf.RoundToInt(visionSystem.Suspicion).ToString();
        DrawOutlinedText(outer, text, barTextStyle);
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static Color EvaluateSuspicionColor(float normalized)
    {
        if (normalized <= 0.5f)
        {
            return Color.Lerp(Color.green, Color.yellow, normalized * 2f);
        }

        return Color.Lerp(Color.yellow, Color.red, (normalized - 0.5f) * 2f);
    }

    private static Color EvaluateAlertColor(GuardAlertSystem.AlertLevel alertLevel)
    {
        if (alertLevel == GuardAlertSystem.AlertLevel.FullAlert)
        {
            return Color.red;
        }

        if (alertLevel == GuardAlertSystem.AlertLevel.Suspicious)
        {
            return Color.yellow;
        }

        return Color.gray;
    }

    private void DrawOutlinedText(Rect rect, string text, GUIStyle style)
    {
        GUIStyle outlineStyle = new GUIStyle(style);
        outlineStyle.normal.textColor = Color.black;

        Rect outlineRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height);
        GUI.Label(outlineRect, text, outlineStyle);
        GUI.Label(rect, text, style);
    }

    private void EnsureStyles()
    {
        if (boldStyle == null)
        {
            boldStyle = new GUIStyle(GUI.skin.label);
            boldStyle.alignment = TextAnchor.MiddleCenter;
            boldStyle.fontStyle = FontStyle.Bold;
            boldStyle.normal.textColor = Color.white;
            boldStyle.richText = false;
            boldStyle.clipping = TextClipping.Overflow;
        }

        if (regularStyle == null)
        {
            regularStyle = new GUIStyle(GUI.skin.label);
            regularStyle.alignment = TextAnchor.MiddleCenter;
            regularStyle.fontStyle = FontStyle.Normal;
            regularStyle.normal.textColor = Color.white;
            regularStyle.richText = false;
            regularStyle.clipping = TextClipping.Overflow;
        }

        if (alertStyle == null)
        {
            alertStyle = new GUIStyle(GUI.skin.label);
            alertStyle.alignment = TextAnchor.MiddleCenter;
            alertStyle.fontStyle = FontStyle.Bold;
            alertStyle.richText = false;
            alertStyle.clipping = TextClipping.Overflow;
        }
    }

    private void CacheReferences()
    {
        if (guardAI == null)
        {
            guardAI = GetComponent<GuardAI>();
        }

        if (visionSystem == null)
        {
            visionSystem = GetComponent<VisionSystem>();
        }

        if (hearingSystem == null)
        {
            hearingSystem = GetComponent<HearingSystem>();
        }

        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }
    }
}
