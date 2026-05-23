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
    private GUIStyle hudStyle;
    private Renderer[] cachedRenderers;

    private void Awake()
    {
        guardAI = GetComponent<GuardAI>();
        if (guardAI == null)
        {
            Debug.LogError("[GuardDebugVisualizer] Missing GuardAI component on guard GameObject.");
        }

        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void OnGUI()
    {
        if (!showDebug || guardAI == null)
        {
            return;
        }

        Camera hudCamera = ResolveHudCamera();
        if (hudCamera == null)
        {
            return;
        }

        Vector3 worldPos = GetLabelWorldPosition();
        Vector3 screenPos = hudCamera.WorldToScreenPoint(worldPos);
        if (screenPos.z <= 0f)
        {
            return;
        }

        screenPos.y = Screen.height - screenPos.y;

        float w = 220f;
        float h = 62f;
        Rect rect = new Rect(screenPos.x - (w * 0.5f), screenPos.y - h, w, h);

        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label);
            hudStyle.alignment = TextAnchor.UpperCenter;
            hudStyle.fontStyle = FontStyle.Bold;
            hudStyle.fontSize = 12;
            hudStyle.clipping = TextClipping.Overflow;
            hudStyle.normal.textColor = Color.white;
        }

        string labelText = GetLabelText();
        GUI.color = Color.black;
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), labelText, hudStyle);
        GUI.color = Color.white;
        GUI.Label(rect, labelText, hudStyle);

        if (visionSystem != null)
        {
            DrawSuspicionBar(new Vector2(screenPos.x, screenPos.y + 8f));
        }

        GUI.color = Color.white;
    }

    private static Camera ResolveHudCamera()
    {
        PlayerController playerController = Object.FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            Camera playerCamera = playerController.GetComponentInChildren<Camera>();
            if (playerCamera != null && playerCamera.enabled && playerCamera.gameObject.activeInHierarchy)
            {
                return playerCamera;
            }
        }

        Camera main = Camera.main;
        if (main != null && main.enabled && main.gameObject.activeInHierarchy)
        {
            return main;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy && camera.cameraType == CameraType.Game)
            {
                return camera;
            }
        }

        return null;
    }

    private string GetLabelText()
    {
        string state = guardAI != null ? guardAI.CurrentGuardState.ToString().ToUpperInvariant() : "UNKNOWN";
        string node = guardAI != null ? guardAI.ActiveNodeName : "-";
        string alert = GuardAlertSystem.CurrentAlert.ToString().ToUpperInvariant();
        return $"STATE: {state}\nNODE: {node}\nALERT: {alert}";
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

        cachedRenderers = GetComponentsInChildren<Renderer>();
    }

    private Vector3 GetLabelWorldPosition()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>();
        }

        float highestY = transform.position.y + labelHeight;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            highestY = Mathf.Max(highestY, renderer.bounds.max.y + 0.35f);
        }

        return new Vector3(transform.position.x, highestY, transform.position.z);
    }
}
