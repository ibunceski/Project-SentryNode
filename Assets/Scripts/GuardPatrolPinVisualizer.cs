using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Shows a live in-game marker for a guard's active patrol destination.
/// </summary>
[RequireComponent(typeof(PatrolSystem))]
[RequireComponent(typeof(LineRenderer))]
public class GuardPatrolPinVisualizer : MonoBehaviour
{
    [Header("Visibility")]
    [SerializeField]
    private bool visualizerEnabled = true;

    [SerializeField]
    private bool showOnlyWhilePatrolling = true;

    [Header("Occlusion")]
    [SerializeField]
    private LayerMask visibilityOcclusionMask = Physics.DefaultRaycastLayers;

    [SerializeField]
    private float cameraRayStartOffset = 0.05f;

    [Header("Pin")]
    [SerializeField]
    private float pinHeight = 1.35f;

    [SerializeField]
    private float pinScale = 0.32f;

    [SerializeField]
    private Color pinColor = new Color(1f, 0.35f, 0.2f, 1f);

    [Header("Line")]
    [SerializeField]
    private float lineWidth = 0.045f;

    [SerializeField]
    private float lineFloorOffset = 0.06f;

    [SerializeField]
    private Color lineColor = new Color(1f, 0.45f, 0.2f, 1f);

    [Header("Label")]
    [SerializeField]
    private float labelHeightAbovePin = 0.48f;

    [SerializeField]
    private float labelCharacterSize = 0.08f;

    [SerializeField]
    private int labelFontSize = 44;

    [SerializeField]
    private Color labelColor = Color.white;

    [SerializeField]
    private PatrolSystem patrolSystem;

    [SerializeField]
    private GuardAI guardAI;

    [SerializeField]
    private NavMeshAgent navMeshAgent;

    private LineRenderer lineRenderer;
    private GameObject pinRoot;
    private Transform pinTransform;
    private Transform labelTransform;
    private TextMesh labelTextMesh;
    private Renderer pinRenderer;
    private Material lineMaterialInstance;
    private NavMeshPath reusablePath;

    private void Awake()
    {
        ResolveReferences();
        reusablePath = new NavMeshPath();
        BuildVisualObjects();
        ConfigureLineRenderer();
        ApplyVisualStyles();
        SetLineActive(false);
        SetPinActive(false);
    }

    private void OnValidate()
    {
        pinScale = Mathf.Max(0.05f, pinScale);
        pinHeight = Mathf.Max(0f, pinHeight);
        lineWidth = Mathf.Max(0.005f, lineWidth);
        lineFloorOffset = Mathf.Max(0f, lineFloorOffset);
        cameraRayStartOffset = Mathf.Max(0f, cameraRayStartOffset);
        labelHeightAbovePin = Mathf.Max(0f, labelHeightAbovePin);
        labelCharacterSize = Mathf.Max(0.01f, labelCharacterSize);
        labelFontSize = Mathf.Max(12, labelFontSize);

        if (lineRenderer != null)
        {
            ConfigureLineRenderer();
            ApplyVisualStyles();
        }
    }

    private void Update()
    {
        if (!visualizerEnabled || patrolSystem == null)
        {
            SetLineActive(false);
            SetPinActive(false);
            return;
        }

        if (!ShouldShowPatrolIntent())
        {
            SetLineActive(false);
            SetPinActive(false);
            return;
        }

        Vector3 patrolPoint = patrolSystem.CurrentPatrolPoint;
        Vector3 pinPosition = patrolPoint + (Vector3.up * pinHeight);
        SetLineActive(true);
        UpdatePathLineOnFloor(patrolPoint, pinPosition);

        bool pinVisible = IsPinVisibleFromCamera(pinPosition);
        SetPinActive(pinVisible);
        if (!pinVisible)
        {
            return;
        }

        UpdatePinAndLabelPositions(pinPosition);
        UpdateLabelFacing();
    }

    private void OnDestroy()
    {
        if (lineMaterialInstance != null)
        {
            Destroy(lineMaterialInstance);
            lineMaterialInstance = null;
        }

        if (pinRoot != null)
        {
            Destroy(pinRoot);
            pinRoot = null;
        }
    }

    private void ResolveReferences()
    {
        if (patrolSystem == null)
        {
            patrolSystem = GetComponent<PatrolSystem>();
        }

        if (guardAI == null)
        {
            guardAI = GetComponent<GuardAI>();
        }

        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }
    }

    private void BuildVisualObjects()
    {
        if (pinRoot != null)
        {
            return;
        }

        pinRoot = new GameObject(gameObject.name + "_PatrolPin");
        Transform parent = transform.parent;
        if (parent != null)
        {
            pinRoot.transform.SetParent(parent);
        }

        pinTransform = pinRoot.transform;
        pinTransform.position = transform.position;
        pinTransform.rotation = Quaternion.identity;

        GameObject pinVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pinVisual.name = "PinIcon";
        pinVisual.transform.SetParent(pinTransform, false);
        pinVisual.transform.localPosition = Vector3.zero;
        pinVisual.transform.localScale = Vector3.one * pinScale;
        Collider pinCollider = pinVisual.GetComponent<Collider>();
        if (pinCollider != null)
        {
            Destroy(pinCollider);
        }

        pinRenderer = pinVisual.GetComponent<Renderer>();

        GameObject labelObject = new GameObject("PinLabel");
        labelObject.transform.SetParent(pinTransform, false);
        labelObject.transform.localPosition = new Vector3(0f, labelHeightAbovePin, 0f);
        labelTransform = labelObject.transform;
        labelTextMesh = labelObject.AddComponent<TextMesh>();
        labelTextMesh.anchor = TextAnchor.MiddleCenter;
        labelTextMesh.alignment = TextAlignment.Center;
        labelTextMesh.text = BuildLabelText();
    }

    private void ConfigureLineRenderer()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.numCapVertices = 2;
        lineRenderer.numCornerVertices = 2;

        if (lineMaterialInstance == null)
        {
            lineMaterialInstance = CreateLineMaterial();
        }

        if (lineMaterialInstance != null)
        {
            lineRenderer.material = lineMaterialInstance;
            if (lineMaterialInstance.HasProperty("_Color"))
            {
                lineMaterialInstance.color = lineColor;
            }
        }
    }

    private void ApplyVisualStyles()
    {
        if (pinTransform != null)
        {
            pinTransform.localScale = Vector3.one;
        }

        if (pinRenderer != null)
        {
            Material material = pinRenderer.material;
            if (material != null && material.HasProperty("_Color"))
            {
                material.color = pinColor;
            }
        }

        if (labelTransform != null)
        {
            labelTransform.localPosition = new Vector3(0f, labelHeightAbovePin, 0f);
        }

        if (labelTextMesh != null)
        {
            labelTextMesh.characterSize = labelCharacterSize;
            labelTextMesh.fontSize = labelFontSize;
            labelTextMesh.color = labelColor;
            labelTextMesh.text = BuildLabelText();
        }
    }

    private bool ShouldShowPatrolIntent()
    {
        if (!patrolSystem.HasPatrolTarget)
        {
            return false;
        }

        if (!showOnlyWhilePatrolling || guardAI == null)
        {
            return true;
        }

        return guardAI.CurrentGuardState == GuardAI.GuardState.Patrolling;
    }

    private void UpdatePinAndLabelPositions(Vector3 pinPosition)
    {
        pinTransform.position = pinPosition;

        if (labelTransform != null)
        {
            labelTransform.position = pinPosition + (Vector3.up * labelHeightAbovePin);
        }
    }

    private void UpdatePathLineOnFloor(Vector3 patrolPoint, Vector3 pinPosition)
    {
        if (lineRenderer == null)
        {
            return;
        }

        Vector3[] corners = ResolvePathCorners(patrolPoint);
        if (corners == null || corners.Length < 2)
        {
            Vector3[] fallbackPoints =
            {
                GetFloorPoint(transform.position) + (Vector3.up * lineFloorOffset),
                GetFloorPoint(patrolPoint) + (Vector3.up * lineFloorOffset),
                pinPosition
            };
            lineRenderer.positionCount = fallbackPoints.Length;
            lineRenderer.SetPositions(fallbackPoints);
            return;
        }

        Vector3[] floorPoints = new Vector3[corners.Length + 1];
        for (int i = 0; i < corners.Length; i++)
        {
            floorPoints[i] = GetFloorPoint(corners[i]) + (Vector3.up * lineFloorOffset);
        }

        floorPoints[floorPoints.Length - 1] = pinPosition;

        lineRenderer.positionCount = floorPoints.Length;
        lineRenderer.SetPositions(floorPoints);
    }

    private Vector3[] ResolvePathCorners(Vector3 patrolPoint)
    {
        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            NavMeshPath livePath = navMeshAgent.path;
            if (livePath != null && livePath.corners != null && livePath.corners.Length >= 2)
            {
                return livePath.corners;
            }
        }

        if (reusablePath == null)
        {
            reusablePath = new NavMeshPath();
        }

        if (NavMesh.CalculatePath(transform.position, patrolPoint, NavMesh.AllAreas, reusablePath)
            && reusablePath.status != NavMeshPathStatus.PathInvalid
            && reusablePath.corners != null
            && reusablePath.corners.Length >= 2)
        {
            return reusablePath.corners;
        }

        return null;
    }

    private Vector3 GetFloorPoint(Vector3 source)
    {
        if (NavMesh.SamplePosition(source + (Vector3.up * 0.25f), out NavMeshHit hit, 1.25f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return source;
    }

    private void UpdateLabelFacing()
    {
        if (labelTransform == null)
        {
            return;
        }

        Camera viewCamera = ResolveViewCamera();
        if (viewCamera == null)
        {
            return;
        }

        Vector3 toCamera = labelTransform.position - viewCamera.transform.position;
        if (toCamera.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        labelTransform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
    }

    private bool IsPinVisibleFromCamera(Vector3 pinPosition)
    {
        Camera viewCamera = ResolveViewCamera();
        if (viewCamera == null)
        {
            return true;
        }

        Vector3 origin = viewCamera.transform.position;
        Vector3 toPin = pinPosition - origin;
        float distance = toPin.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        Vector3 direction = toPin / distance;
        Vector3 rayOrigin = origin + direction * cameraRayStartOffset;
        int occlusionMask = BuildOcclusionMask(viewCamera);
        if (occlusionMask == 0)
        {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            direction,
            Mathf.Max(0f, distance - cameraRayStartOffset),
            occlusionMask,
            QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return true;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null)
            {
                continue;
            }

            if (hitTransform.IsChildOf(transform))
            {
                continue;
            }

            if (pinRoot != null && hitTransform.IsChildOf(pinRoot.transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private int BuildOcclusionMask(Camera viewCamera)
    {
        int mask = visibilityOcclusionMask.value;
        if (mask == 0)
        {
            mask = Physics.DefaultRaycastLayers;
        }

        if (viewCamera != null)
        {
            mask &= ~(1 << viewCamera.gameObject.layer);
        }

        return mask;
    }

    private void SetLineActive(bool active)
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = active;
        }
    }

    private void SetPinActive(bool active)
    {
        if (pinRoot != null && pinRoot.activeSelf != active)
        {
            pinRoot.SetActive(active);
        }
    }

    private string BuildLabelText()
    {
        return BuildDisplayName() + "\ncoming here";
    }

    private string BuildDisplayName()
    {
        string guardName = gameObject.name;
        if (string.IsNullOrWhiteSpace(guardName))
        {
            return "Guard";
        }

        return guardName.Replace('_', ' ').Trim();
    }

    private static Camera ResolveViewCamera()
    {
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

    private static Material CreateLineMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader == null ? null : new Material(shader);
    }
}
