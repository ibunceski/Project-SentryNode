using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders the guard's field of view as in-game cone geometry on the ground during play mode.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(VisionSystem))]
[RequireComponent(typeof(GuardAI))]
public class GuardVisionRenderer : MonoBehaviour
{
    [Header("Mesh Shape")]
    [SerializeField]
    [Range(20, 30)]
    private int meshSegments = 24;

    [SerializeField]
    private float floorYOffset = 0.05f;

    [SerializeField]
    private float raycastStartHeight = 0.15f;

    [Header("Color Timing")]
    [SerializeField]
    private float colorLerpDuration = 0.3f;

    [Header("Optional References")]
    [SerializeField]
    private VisionSystem visionSystem;

    [SerializeField]
    private GuardAI guardAI;

    [SerializeField]
    private Transform coneRoot;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh coneMesh;
    private Material runtimeMaterial;
    private Color currentColor;
    private bool isInitialized;

    private Vector3[] vertices;
    private Vector2[] uvs;
    private Vector3[] normals;
    private int[] triangles;

    private static readonly string[] TransparentShaderCandidates =
    {
        "Standard",
        "Universal Render Pipeline/Unlit",
        "Unlit/Transparent",
        "Universal Render Pipeline/Lit",
        "Legacy Shaders/Transparent/VertexLit"
    };

    private void Awake()
    {
        ResolveReferences();
        InitializeRuntimeObjects();
    }

    private void OnEnable()
    {
        if (!isInitialized)
        {
            ResolveReferences();
            InitializeRuntimeObjects();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || !isInitialized || visionSystem == null || guardAI == null)
        {
            return;
        }

        if (!TryGetVisionDimensions(out float viewDistance, out float fieldOfViewAngle))
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }

            return;
        }

        if (meshRenderer != null)
        {
            meshRenderer.enabled = true;
        }

        if (coneRoot != null)
        {
            coneRoot.localPosition = new Vector3(0f, floorYOffset, 0f);
            coneRoot.localRotation = Quaternion.identity;
            coneRoot.localScale = Vector3.one;
        }

        RebuildConeMesh(viewDistance, fieldOfViewAngle);
        UpdateConeColor(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (coneMesh != null)
        {
            Destroy(coneMesh);
            coneMesh = null;
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }

    private void OnValidate()
    {
        meshSegments = Mathf.Clamp(meshSegments, 20, 30);
        colorLerpDuration = Mathf.Max(0.01f, colorLerpDuration);
        floorYOffset = Mathf.Max(0f, floorYOffset);
        raycastStartHeight = Mathf.Max(0.01f, raycastStartHeight);

        if (Application.isPlaying && isInitialized && coneRoot != null)
        {
            coneRoot.localPosition = new Vector3(0f, floorYOffset, 0f);
        }
    }

    private void ResolveReferences()
    {
        if (visionSystem == null)
        {
            visionSystem = GetComponent<VisionSystem>();
        }

        if (guardAI == null)
        {
            guardAI = GetComponent<GuardAI>();
        }
    }

    private void InitializeRuntimeObjects()
    {
        if (!Application.isPlaying || visionSystem == null || guardAI == null)
        {
            return;
        }

        EnsureConeObject();
        EnsureMesh();
        EnsureMaterial();

        currentColor = GetColorForState(guardAI.CurrentGuardState);
        ApplyMaterialColor(currentColor);
        isInitialized = coneRoot != null && meshFilter != null && meshRenderer != null && coneMesh != null && runtimeMaterial != null;
    }

    private void EnsureConeObject()
    {
        if (coneRoot == null)
        {
            Transform existing = transform.Find("GuardVisionCone");
            if (existing != null)
            {
                coneRoot = existing;
            }
            else
            {
                GameObject coneObject = new GameObject("GuardVisionCone");
                coneRoot = coneObject.transform;
                coneRoot.SetParent(transform, false);
            }
        }

        coneRoot.localPosition = new Vector3(0f, floorYOffset, 0f);
        coneRoot.localRotation = Quaternion.identity;
        coneRoot.localScale = Vector3.one;

        meshFilter = coneRoot.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = coneRoot.gameObject.AddComponent<MeshFilter>();
        }

        meshRenderer = coneRoot.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = coneRoot.gameObject.AddComponent<MeshRenderer>();
        }

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void EnsureMesh()
    {
        if (meshFilter == null)
        {
            return;
        }

        if (coneMesh == null)
        {
            coneMesh = new Mesh
            {
                name = "GuardVisionConeMesh"
            };
            coneMesh.MarkDynamic();
        }

        meshFilter.sharedMesh = coneMesh;
    }

    private void EnsureMaterial()
    {
        if (meshRenderer == null)
        {
            return;
        }

        if (runtimeMaterial == null)
        {
            Shader shader = FindTransparentShader();
            if (shader == null)
            {
                Debug.LogWarning("[GuardVisionRenderer] Could not find a transparent shader for guard vision cone.");
                return;
            }

            runtimeMaterial = new Material(shader)
            {
                name = "GuardVisionConeMaterial (Runtime)"
            };
            SetupTransparentMaterial(runtimeMaterial, shader.name);
        }

        meshRenderer.sharedMaterial = runtimeMaterial;
    }

    private static Shader FindTransparentShader()
    {
        for (int i = 0; i < TransparentShaderCandidates.Length; i++)
        {
            Shader shader = Shader.Find(TransparentShaderCandidates[i]);
            if (shader != null)
            {
                return shader;
            }
        }

        return null;
    }

    private static void SetupTransparentMaterial(Material material, string shaderName)
    {
        if (material == null)
        {
            return;
        }

        bool isStandard = shaderName == "Standard";

        if (isStandard)
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            return;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetInt("_ZWrite", 0);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private bool TryGetVisionDimensions(out float viewDistance, out float fieldOfViewAngle)
    {
        if (visionSystem == null)
        {
            viewDistance = 0f;
            fieldOfViewAngle = 0f;
            return false;
        }

        viewDistance = visionSystem.ViewDistance;
        fieldOfViewAngle = visionSystem.FieldOfViewAngle;

        viewDistance = Mathf.Max(0f, viewDistance);
        fieldOfViewAngle = Mathf.Clamp(fieldOfViewAngle, 0f, 360f);
        return viewDistance > 0f && fieldOfViewAngle > 0.1f;
    }

    private void RebuildConeMesh(float distance, float fieldOfView)
    {
        if (coneMesh == null)
        {
            return;
        }

        int segmentCount = Mathf.Clamp(meshSegments, 20, 30);
        int vertexCount = segmentCount + 2;
        int triangleCount = segmentCount * 3;
        EnsureMeshArrays(vertexCount, triangleCount);

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0f);
        normals[0] = Vector3.up;

        float halfFov = fieldOfView * 0.5f;
        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            float angle = Mathf.Lerp(-halfFov, halfFov, t) * Mathf.Deg2Rad;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            Vector3 localDirection = new Vector3(sin, 0f, cos);
            float clippedDistance = ResolveClippedDistance(localDirection, distance);

            int vertexIndex = i + 1;
            vertices[vertexIndex] = localDirection * clippedDistance;
            uvs[vertexIndex] = new Vector2(t, 1f);
            normals[vertexIndex] = Vector3.up;

            if (i == segmentCount)
            {
                continue;
            }

            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = vertexIndex;
            triangles[triangleIndex + 2] = vertexIndex + 1;
        }

        coneMesh.Clear(false);
        coneMesh.vertices = vertices;
        coneMesh.uv = uvs;
        coneMesh.normals = normals;
        coneMesh.triangles = triangles;
        coneMesh.RecalculateBounds();
    }

    private void EnsureMeshArrays(int vertexCount, int triangleCount)
    {
        if (vertices == null || vertices.Length != vertexCount)
        {
            vertices = new Vector3[vertexCount];
        }

        if (uvs == null || uvs.Length != vertexCount)
        {
            uvs = new Vector2[vertexCount];
        }

        if (normals == null || normals.Length != vertexCount)
        {
            normals = new Vector3[vertexCount];
        }

        if (triangles == null || triangles.Length != triangleCount)
        {
            triangles = new int[triangleCount];
        }
    }

    private float ResolveClippedDistance(Vector3 localDirection, float maxDistance)
    {
        if (visionSystem == null)
        {
            return maxDistance;
        }

        int occlusionMask = visionSystem.OcclusionMask.value;
        if (occlusionMask == 0)
        {
            return maxDistance;
        }

        Vector3 direction = localDirection.normalized;
        Vector3 worldDirection = transform.TransformDirection(direction);
        Vector3 rayOrigin = transform.position + Vector3.up * raycastStartHeight;

        float remainingDistance = maxDistance;
        float traveledDistance = 0f;
        const float skipEpsilon = 0.02f;

        for (int i = 0; i < 3; i++)
        {
            if (!Physics.Raycast(
                    rayOrigin,
                    worldDirection,
                    out RaycastHit hit,
                    remainingDistance,
                    occlusionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return maxDistance;
            }

            Transform hitTransform = hit.collider != null ? hit.collider.transform : null;
            bool hitSelf = hitTransform != null && (hitTransform == transform || hitTransform.IsChildOf(transform));
            if (!hitSelf)
            {
                return Mathf.Clamp(traveledDistance + hit.distance, 0f, maxDistance);
            }

            float advancedDistance = hit.distance + skipEpsilon;
            traveledDistance += advancedDistance;
            remainingDistance -= advancedDistance;
            if (remainingDistance <= 0f)
            {
                return maxDistance;
            }

            rayOrigin += worldDirection * advancedDistance;
        }

        return maxDistance;
    }

    private void UpdateConeColor(float deltaTime)
    {
        if (runtimeMaterial == null || guardAI == null)
        {
            return;
        }

        Color targetColor = GetColorForState(guardAI.CurrentGuardState);
        float lerpFactor = deltaTime / Mathf.Max(0.01f, colorLerpDuration);
        currentColor = Color.Lerp(currentColor, targetColor, lerpFactor);
        ApplyMaterialColor(currentColor);
    }

    private void ApplyMaterialColor(Color color)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.color = color;

        if (runtimeMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.SetColor("_Color", color);
        }

        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor("_BaseColor", color);
        }
    }

    private static Color GetColorForState(GuardAI.GuardState state)
    {
        switch (state)
        {
            case GuardAI.GuardState.Suspicious:
                return new Color(1f, 0.85f, 0.2f, 0.32f);
            case GuardAI.GuardState.Chasing:
                return new Color(1f, 0.1f, 0.1f, 0.45f);
            case GuardAI.GuardState.Investigating:
            case GuardAI.GuardState.Searching:
                return new Color(1f, 0.55f, 0.12f, 0.34f);
            case GuardAI.GuardState.Patrolling:
            default:
                return new Color(0.22f, 0.78f, 0.95f, 0.28f);
        }
    }
}
