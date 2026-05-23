using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds a reproducible stealth demo level in the active scene.
/// </summary>
public static class LevelBuilder
{
    private const string RootName = "__DemoLevelRoot";
    private const string EnvironmentLayerName = "Environment";
    private const string PlayerLayerName = "Player";
    private const string GroundTagName = "Ground";

    private static readonly Color FloorColor = Rgb(30, 30, 35);
    private static readonly Color OuterWallColor = Rgb(55, 65, 80);
    private static readonly Color InteriorColor = Rgb(70, 80, 95);
    private static readonly Color GuardColor = Rgb(220, 100, 30);
    private static readonly Color GuardEyeColor = Rgb(220, 40, 40);
    private static readonly Color PlayerColor = Rgb(30, 180, 160);
    private static readonly Color PatrolPointColor = Rgb(255, 210, 0);
    private static readonly Color MainLightColor = Rgb(255, 245, 220);
    private static readonly Color FillLightColor = Rgb(180, 200, 255);
    private static readonly Color AmbientColor = Rgb(80, 80, 95);
    private static readonly Color FallbackColor = Rgb(100, 100, 110);

    [MenuItem("Tools/Build Demo Level")]
    public static void BuildDemoLevel()
    {
        GuardAlertSystem.Reset();

        int environmentLayer = EnsureLayer(EnvironmentLayerName);
        int playerLayer = EnsureLayer(PlayerLayerName);
        EnsureTag(GroundTagName);

        if (environmentLayer < 0 || playerLayer < 0)
        {
            Debug.LogError("Level build aborted. Required layers could not be created.");
            return;
        }

        string[] roofNames = { "Ceiling", "Roof", "Top", "Cover" };
        foreach (string roofName in roofNames)
        {
            GameObject roofObject = GameObject.Find(roofName);
            if (roofObject != null)
            {
                Object.DestroyImmediate(roofObject);
            }
        }

        GameObject existingRoot = GameObject.Find(RootName);
        if (existingRoot != null)
        {
            Object.DestroyImmediate(existingRoot);
        }

        RemoveExistingCameras();
        GameObject root = new GameObject(RootName);

        CreateFloor(root.transform, environmentLayer);
        CreateOuterWalls(root.transform, environmentLayer);
        CreateInteriorDividers(root.transform, environmentLayer);
        CreateRandomObstacles(root.transform, environmentLayer);
        RebuildLighting(root.transform);

        GuardBundle guardPrimary = CreateGuardVariant(
            root.transform,
            "Guard_A",
            new Vector3(-10f, 0f, -10f),
            environmentLayer,
            false);
        GuardBundle guardVariant = CreateGuardVariant(
            root.transform,
            "Guard_B",
            new Vector3(10f, 0f, 10f),
            environmentLayer,
            true);
        GameObject player = CreatePlayer(root.transform, playerLayer);
        Transform[] patrolPoints = CreatePatrolPoints(root.transform, environmentLayer);

        ConfigureGuardSystems(guardPrimary, patrolPoints, player.transform, playerLayer, environmentLayer);
        ConfigureGuardSystems(guardVariant, patrolPoints, player.transform, playerLayer, environmentLayer);
        EnsureAllRenderersHaveAssignedMaterialInScene();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        Debug.Log("Demo level generated and NavMesh baked.");
    }

    private static void CreateFloor(Transform parent, int environmentLayer)
    {
        GameObject floor = CreateCube(
            "Floor",
            new Vector3(0f, -0.05f, 0f),
            new Vector3(30f, 0.1f, 30f),
            parent,
            environmentLayer);
        AssignMaterial(floor, FloorColor, 0f, 0.1f);
        floor.tag = GroundTagName;
    }

    private static void CreateOuterWalls(Transform parent, int environmentLayer)
    {
        const float halfSize = 15f;
        const float wallHeight = 3f;
        const float wallThickness = 1f;

        GameObject north = CreateCube(
            "Wall_North",
            new Vector3(0f, wallHeight * 0.5f, halfSize + wallThickness * 0.5f),
            new Vector3(30f + wallThickness, wallHeight, wallThickness),
            parent,
            environmentLayer);
        AssignMaterial(north, OuterWallColor, 0f, 0.05f);

        GameObject south = CreateCube(
            "Wall_South",
            new Vector3(0f, wallHeight * 0.5f, -halfSize - wallThickness * 0.5f),
            new Vector3(30f + wallThickness, wallHeight, wallThickness),
            parent,
            environmentLayer);
        AssignMaterial(south, OuterWallColor, 0f, 0.05f);

        GameObject east = CreateCube(
            "Wall_East",
            new Vector3(halfSize + wallThickness * 0.5f, wallHeight * 0.5f, 0f),
            new Vector3(wallThickness, wallHeight, 30f + wallThickness),
            parent,
            environmentLayer);
        AssignMaterial(east, OuterWallColor, 0f, 0.05f);

        GameObject west = CreateCube(
            "Wall_West",
            new Vector3(-halfSize - wallThickness * 0.5f, wallHeight * 0.5f, 0f),
            new Vector3(wallThickness, wallHeight, 30f + wallThickness),
            parent,
            environmentLayer);
        AssignMaterial(west, OuterWallColor, 0f, 0.05f);
    }

    private static void CreateInteriorDividers(Transform parent, int environmentLayer)
    {
        GameObject dividerA = CreateCube(
            "Divider_A",
            new Vector3(-5f, 1.5f, 0f),
            new Vector3(0.5f, 3f, 18f),
            parent,
            environmentLayer);
        AssignMaterial(dividerA, InteriorColor, 0f, 0.08f);

        GameObject dividerB = CreateCube(
            "Divider_B",
            new Vector3(5f, 1.5f, 4f),
            new Vector3(0.5f, 3f, 16f),
            parent,
            environmentLayer);
        AssignMaterial(dividerB, InteriorColor, 0f, 0.08f);

        GameObject dividerC = CreateCube(
            "Divider_C",
            new Vector3(0f, 1.5f, -6f),
            new Vector3(14f, 3f, 0.5f),
            parent,
            environmentLayer);
        AssignMaterial(dividerC, InteriorColor, 0f, 0.08f);
    }

    private static void CreateRandomObstacles(Transform parent, int environmentLayer)
    {
        Random.InitState(42);
        Vector3[] reservedPositions =
        {
            new Vector3(-10f, 0f, -10f),
            new Vector3(10f, 0f, 10f),
            new Vector3(0f, 0f, 0f),
            new Vector3(-12f, 0f, -12f),
            new Vector3(12f, 0f, -12f),
            new Vector3(12f, 0f, 12f),
            new Vector3(-12f, 0f, 12f)
        };

        for (int i = 0; i < 4; i++)
        {
            float width = Random.Range(1.2f, 3f);
            float depth = Random.Range(1.2f, 3f);
            float height = Random.Range(1.2f, 2.8f);
            Vector3 position = FindObstaclePosition(reservedPositions, 3.5f);

            GameObject obstacle = CreateCube(
                "Obstacle_" + (i + 1),
                new Vector3(position.x, height * 0.5f, position.z),
                new Vector3(width, height, depth),
                parent,
                environmentLayer);
            AssignMaterial(obstacle, InteriorColor, 0f, 0.08f);
        }
    }

    private static void RebuildLighting(Transform parent)
    {
        Light[] existingLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < existingLights.Length; i++)
        {
            Object.DestroyImmediate(existingLights[i].gameObject);
        }

        GameObject mainLightObject = new GameObject("Main Directional Light");
        mainLightObject.transform.SetParent(parent);
        mainLightObject.transform.position = new Vector3(0f, 8f, 0f);
        mainLightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        Light mainDirectionalLight = mainLightObject.AddComponent<Light>();
        mainDirectionalLight.type = LightType.Directional;
        mainDirectionalLight.intensity = 1f;
        mainDirectionalLight.color = MainLightColor;

        GameObject fillLightObject = new GameObject("Fill Directional Light");
        fillLightObject.transform.SetParent(parent);
        fillLightObject.transform.position = new Vector3(0f, 8f, 0f);
        fillLightObject.transform.rotation = Quaternion.Euler(-30f, 150f, 0f);

        Light fillDirectionalLight = fillLightObject.AddComponent<Light>();
        fillDirectionalLight.type = LightType.Directional;
        fillDirectionalLight.intensity = 0.3f;
        fillDirectionalLight.color = FillLightColor;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = AmbientColor;
    }

    private static void RemoveExistingCameras()
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            Object.DestroyImmediate(camera.gameObject);
        }
    }

    private static GuardBundle CreateGuardVariant(
        Transform parent,
        string guardName,
        Vector3 position,
        int environmentLayer,
        bool isVariant)
    {
        GameObject guard = new GameObject(guardName);
        guard.transform.SetParent(parent);
        guard.transform.position = position;
        guard.transform.rotation = Quaternion.identity;

        if (isVariant)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "GuardVariantBody";
            body.transform.SetParent(guard.transform);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);
            RemoveCollider(body);
            SetLayerRecursively(body, environmentLayer);
            AssignMaterial(body, GuardColor, 0f, 0.2f);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "GuardVariantHead";
            head.transform.SetParent(guard.transform);
            head.transform.localPosition = new Vector3(0f, 1.95f, 0f);
            head.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            RemoveCollider(head);
            SetLayerRecursively(head, environmentLayer);
            AssignMaterial(head, GuardColor, 0f, 0.2f);
        }
        else
        {
            GameObject guardVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            guardVisual.name = "GuardVisual";
            guardVisual.transform.SetParent(guard.transform);
            guardVisual.transform.localPosition = new Vector3(0f, 1f, 0f);
            guardVisual.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            RemoveCollider(guardVisual);
            SetLayerRecursively(guardVisual, environmentLayer);
            AssignMaterial(guardVisual, GuardColor, 0f, 0.2f);
        }

        GameObject eyeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eyeIndicator.name = "EyeIndicator";
        eyeIndicator.transform.SetParent(guard.transform);
        eyeIndicator.transform.localPosition = new Vector3(0f, 1.3f, 0.55f);
        eyeIndicator.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
        RemoveCollider(eyeIndicator);
        SetLayerRecursively(eyeIndicator, environmentLayer);
        AssignMaterial(eyeIndicator, GuardEyeColor, 0f, 0.15f);

        NavMeshAgent agent = guard.AddComponent<NavMeshAgent>();
        agent.speed = 3.5f;
        agent.stoppingDistance = 0.3f;
        agent.angularSpeed = 360f;

        GuardAI guardAI = guard.AddComponent<GuardAI>();
        VisionSystem visionSystem = guard.AddComponent<VisionSystem>();
        HearingSystem hearingSystem = guard.AddComponent<HearingSystem>();
        PatrolSystem patrolSystem = guard.AddComponent<PatrolSystem>();
        guard.AddComponent<GuardDebugVisualizer>();

        return new GuardBundle
        {
            Root = guard,
            Agent = agent,
            GuardAI = guardAI,
            VisionSystem = visionSystem,
            HearingSystem = hearingSystem,
            PatrolSystem = patrolSystem
        };
    }

    private static GameObject CreatePlayer(Transform parent, int playerLayer)
    {
        GameObject player = new GameObject("Player");
        player.transform.SetParent(parent);
        player.transform.position = new Vector3(0f, 0f, 0f);
        player.transform.rotation = Quaternion.identity;
        SetLayerRecursively(player, playerLayer);

        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 1.8f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.radius = 0.3f;

        player.AddComponent<PlayerController>();

        GameObject playerVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerVisual.name = "PlayerVisual";
        playerVisual.transform.SetParent(player.transform);
        playerVisual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        playerVisual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        RemoveCollider(playerVisual);
        SetLayerRecursively(playerVisual, playerLayer);
        AssignMaterial(playerVisual, PlayerColor, 0f, 0.15f);

        GameObject cameraObject = new GameObject("PlayerCamera");
        cameraObject.transform.SetParent(player.transform);
        cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        SetLayerRecursively(cameraObject, playerLayer);

        return player;
    }

    private static Transform[] CreatePatrolPoints(Transform parent, int environmentLayer)
    {
        Vector3[] points =
        {
            new Vector3(-12f, 0f, -12f),
            new Vector3(12f, 0f, -12f),
            new Vector3(12f, 0f, 12f),
            new Vector3(-12f, 0f, 12f)
        };

        Transform[] patrolPoints = new Transform[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            GameObject point = new GameObject("PatrolPoint_" + (i + 1));
            point.transform.SetParent(parent);
            point.transform.position = points[i];
            point.layer = environmentLayer;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Marker";
            marker.transform.SetParent(point.transform);
            marker.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            marker.transform.localScale = new Vector3(0.4f, 0.05f, 0.4f);
            RemoveCollider(marker);
            SetLayerRecursively(marker, environmentLayer);
            AssignMaterial(marker, PatrolPointColor, 0f, 0.15f);

            patrolPoints[i] = point.transform;
        }

        return patrolPoints;
    }

    private static void ConfigureGuardSystems(
        GuardBundle guardBundle,
        Transform[] patrolPoints,
        Transform playerTarget,
        int playerLayer,
        int environmentLayer)
    {
        SerializedObject guardAiSerialized = new SerializedObject(guardBundle.GuardAI);
        guardAiSerialized.FindProperty("navMeshAgent").objectReferenceValue = guardBundle.Agent;
        guardAiSerialized.FindProperty("visionSystem").objectReferenceValue = guardBundle.VisionSystem;
        guardAiSerialized.FindProperty("hearingSystem").objectReferenceValue = guardBundle.HearingSystem;
        guardAiSerialized.FindProperty("patrolSystem").objectReferenceValue = guardBundle.PatrolSystem;
        guardAiSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject patrolSerialized = new SerializedObject(guardBundle.PatrolSystem);
        SerializedProperty patrolPointsProperty = patrolSerialized.FindProperty("patrolPoints");
        patrolPointsProperty.arraySize = patrolPoints.Length;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            patrolPointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = patrolPoints[i];
        }

        patrolSerialized.ApplyModifiedPropertiesWithoutUndo();

        bool isPrimaryGuard = guardBundle.Root != null && guardBundle.Root.name.Contains("Guard_A");
        bool clockwise = isPrimaryGuard;
        int phaseOffset = isPrimaryGuard ? 0 : 1;
        guardBundle.PatrolSystem.SetPatrolPattern(clockwise, phaseOffset);
        guardBundle.PatrolSystem.ResetPatrol();

        SerializedObject visionSerialized = new SerializedObject(guardBundle.VisionSystem);
        visionSerialized.FindProperty("obstacleMask").intValue = 1 << environmentLayer;
        visionSerialized.FindProperty("playerMask").intValue = 1 << playerLayer;
        visionSerialized.FindProperty("playerTarget").objectReferenceValue = playerTarget;
        visionSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Transform parent, int layer)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        SetLayerRecursively(cube, layer);
        GameObjectUtility.SetStaticEditorFlags(cube, StaticEditorFlags.NavigationStatic);
        return cube;
    }

    private static void RemoveCollider(GameObject gameObject)
    {
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
    }

    private static Vector3 FindObstaclePosition(Vector3[] reservedPositions, float minDistance)
    {
        const int maxAttempts = 40;
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 candidate = new Vector3(Random.Range(-11f, 11f), 0f, Random.Range(-11f, 11f));
            bool tooClose = false;

            for (int j = 0; j < reservedPositions.Length; j++)
            {
                if (Vector3.Distance(candidate, reservedPositions[j]) < minDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                return candidate;
            }
        }

        return new Vector3(Random.Range(-9f, 9f), 0f, Random.Range(-9f, 9f));
    }

    private static void AssignMaterial(GameObject gameObject, Color color, float metallic, float smoothness)
    {
        Renderer renderer = gameObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Material material = CreateStandardMaterial(color, metallic, smoothness);
        renderer.material = material;
    }

    private static Material CreateStandardMaterial(Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Standard");
        Material material = new Material(shader);
        material.color = color;
        material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        material.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
        return material;
    }

    private static void EnsureAllRenderersHaveAssignedMaterialInScene()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material material = renderer.material;
            if (material == null)
            {
                renderer.material = CreateStandardMaterial(FallbackColor, 0f, 0.08f);
                continue;
            }

            if (LooksLikeDefaultWhite(material.color))
            {
                renderer.material = CreateStandardMaterial(FallbackColor, 0f, 0.08f);
            }
        }
    }

    private static bool LooksLikeDefaultWhite(Color color)
    {
        return color.r > 0.95f && color.g > 0.95f && color.b > 0.95f;
    }

    private static Color Rgb(byte r, byte g, byte b)
    {
        return new Color32(r, g, b, 255);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0)
        {
            return existing;
        }

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layersProperty = tagManager.FindProperty("layers");

        for (int i = 8; i <= 31; i++)
        {
            SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layerProperty.stringValue))
            {
                continue;
            }

            layerProperty.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return LayerMask.NameToLayer(layerName);
        }

        Debug.LogError("No free user layer slot available for: " + layerName);
        return -1;
    }

    private static void EnsureTag(string tagName)
    {
        if (System.Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, tagName) >= 0)
        {
            return;
        }

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProperty = tagManager.FindProperty("tags");

        int index = tagsProperty.arraySize;
        tagsProperty.InsertArrayElementAtIndex(index);
        tagsProperty.GetArrayElementAtIndex(index).stringValue = tagName;
        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    private struct GuardBundle
    {
        public GameObject Root;
        public NavMeshAgent Agent;
        public GuardAI GuardAI;
        public VisionSystem VisionSystem;
        public HearingSystem HearingSystem;
        public PatrolSystem PatrolSystem;
    }
}
