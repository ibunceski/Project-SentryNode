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
    private static readonly Color CeilingColor = Rgb(20, 20, 25);
    private static readonly Color GuardColor = Rgb(220, 100, 30);
    private static readonly Color GuardEyeColor = Rgb(220, 40, 40);
    private static readonly Color PlayerColor = Rgb(30, 180, 160);
    private static readonly Color PatrolPointColor = Rgb(255, 210, 0);
    private static readonly Color AmbientColor = Rgb(60, 60, 75);
    private static readonly Color MainLightColor = Rgb(255, 245, 220);
    private static readonly Color FillLightColor = Rgb(180, 200, 255);
    private static readonly Color FallbackColor = Rgb(90, 95, 110);

    [MenuItem("Tools/Build Demo Level")]
    public static void BuildDemoLevel()
    {
        int environmentLayer = EnsureLayer(EnvironmentLayerName);
        int playerLayer = EnsureLayer(PlayerLayerName);
        EnsureTag(GroundTagName);

        if (environmentLayer < 0 || playerLayer < 0)
        {
            Debug.LogError("Level build aborted. Required layers could not be created.");
            return;
        }

        RemoveExistingLights();
        RemoveLegacyRoofObjects();
        RemoveExistingCameras();

        GameObject existingRoot = GameObject.Find(RootName);
        if (existingRoot != null)
        {
            Object.DestroyImmediate(existingRoot);
        }

        GameObject root = new GameObject(RootName);

        CreateFloor(root.transform, environmentLayer);
        CreateOuterWalls(root.transform, environmentLayer);
        CreateInteriorDividers(root.transform, environmentLayer);
        CreateRandomObstacles(root.transform, environmentLayer);
        CreateLighting(root.transform);
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

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
        _ = CreatePatrolPoints(root.transform, environmentLayer);

        ConfigureGuardSystems(guardPrimary, player.transform, playerLayer, environmentLayer);
        ConfigureGuardSystems(guardVariant, player.transform, playerLayer, environmentLayer);

        ApplyNamedSceneMaterials(root);
        EnsureAllRenderersHaveExplicitMaterial();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Demo level generated and NavMesh baked with readability color pass.");
    }

    private static void CreateFloor(Transform parent, int environmentLayer)
    {
        GameObject floor = CreateCube(
            "Floor",
            new Vector3(0f, -0.05f, 0f),
            new Vector3(30f, 0.1f, 30f),
            parent,
            environmentLayer);
        floor.tag = GroundTagName;
    }

    private static void CreateOuterWalls(Transform parent, int environmentLayer)
    {
        const float halfSize = 15f;
        const float wallHeight = 3f;
        const float wallThickness = 1f;

        CreateCube(
            "Wall_North",
            new Vector3(0f, wallHeight * 0.5f, halfSize + wallThickness * 0.5f),
            new Vector3(30f + wallThickness, wallHeight, wallThickness),
            parent,
            environmentLayer);

        CreateCube(
            "Wall_South",
            new Vector3(0f, wallHeight * 0.5f, -halfSize - wallThickness * 0.5f),
            new Vector3(30f + wallThickness, wallHeight, wallThickness),
            parent,
            environmentLayer);

        CreateCube(
            "Wall_East",
            new Vector3(halfSize + wallThickness * 0.5f, wallHeight * 0.5f, 0f),
            new Vector3(wallThickness, wallHeight, 30f + wallThickness),
            parent,
            environmentLayer);

        CreateCube(
            "Wall_West",
            new Vector3(-halfSize - wallThickness * 0.5f, wallHeight * 0.5f, 0f),
            new Vector3(wallThickness, wallHeight, 30f + wallThickness),
            parent,
            environmentLayer);
    }

    private static void CreateInteriorDividers(Transform parent, int environmentLayer)
    {
        CreateCube(
            "Divider_A",
            new Vector3(-5f, 1.5f, 0f),
            new Vector3(0.5f, 3f, 18f),
            parent,
            environmentLayer);

        CreateCube(
            "Divider_B",
            new Vector3(5f, 1.5f, 4f),
            new Vector3(0.5f, 3f, 16f),
            parent,
            environmentLayer);

        CreateCube(
            "Divider_C",
            new Vector3(0f, 1.5f, -6f),
            new Vector3(14f, 3f, 0.5f),
            parent,
            environmentLayer);
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

            CreateCube(
                "Obstacle_" + (i + 1),
                new Vector3(position.x, height * 0.5f, position.z),
                new Vector3(width, height, depth),
                parent,
                environmentLayer);
        }
    }

    private static void CreateLighting(Transform parent)
    {
        GameObject mainLightObject = new GameObject("Main Directional Light");
        mainLightObject.transform.SetParent(parent);
        mainLightObject.transform.position = new Vector3(0f, 8f, 0f);
        mainLightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        Light mainDirectionalLight = mainLightObject.AddComponent<Light>();
        mainDirectionalLight.type = LightType.Directional;
        mainDirectionalLight.intensity = 1f;
        mainDirectionalLight.color = MainLightColor;
        mainDirectionalLight.shadows = LightShadows.Soft;
        mainDirectionalLight.shadowStrength = 0.9f;

        GameObject fillLightObject = new GameObject("Fill Directional Light");
        fillLightObject.transform.SetParent(parent);
        fillLightObject.transform.position = new Vector3(0f, 8f, 0f);
        fillLightObject.transform.rotation = Quaternion.Euler(-30f, 150f, 0f);

        Light fillDirectionalLight = fillLightObject.AddComponent<Light>();
        fillDirectionalLight.type = LightType.Directional;
        fillDirectionalLight.intensity = 0.3f;
        fillDirectionalLight.color = FillLightColor;
        fillDirectionalLight.shadows = LightShadows.None;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = AmbientColor;
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

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "GuardVariantHead";
            head.transform.SetParent(guard.transform);
            head.transform.localPosition = new Vector3(0f, 1.95f, 0f);
            head.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            RemoveCollider(head);
            SetLayerRecursively(head, environmentLayer);
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
        }

        GameObject eyeIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeIndicator.name = "EyeIndicator";
        eyeIndicator.transform.SetParent(guard.transform);
        eyeIndicator.transform.localPosition = new Vector3(0f, 1.35f, 0.58f);
        eyeIndicator.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
        RemoveCollider(eyeIndicator);
        SetLayerRecursively(eyeIndicator, environmentLayer);

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

        GameObject cameraObject = new GameObject("PlayerCamera");
        cameraObject.transform.SetParent(player.transform);
        cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Rgb(18, 22, 30);
        camera.tag = "MainCamera";
        cameraObject.AddComponent<AudioListener>();
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

            patrolPoints[i] = point.transform;
        }

        return patrolPoints;
    }

    private static void ConfigureGuardSystems(
        GuardBundle guardBundle,
        Transform playerTarget,
        int playerLayer,
        int environmentLayer)
    {
        SerializedObject guardAiSerialized = new SerializedObject(guardBundle.GuardAI);
        guardAiSerialized.FindProperty("navMeshAgent").objectReferenceValue = guardBundle.Agent;
        guardAiSerialized.FindProperty("visionSystem").objectReferenceValue = guardBundle.VisionSystem;
        guardAiSerialized.FindProperty("hearingSystem").objectReferenceValue = guardBundle.HearingSystem;
        guardAiSerialized.FindProperty("patrolSystem").objectReferenceValue = guardBundle.PatrolSystem;
        SerializedProperty searchSystemProperty = guardAiSerialized.FindProperty("searchSystem");
        if (searchSystemProperty != null)
        {
            SearchSystem searchSystem = guardBundle.Root.GetComponent<SearchSystem>();
            if (searchSystem == null)
            {
                searchSystem = guardBundle.Root.AddComponent<SearchSystem>();
            }

            searchSystemProperty.objectReferenceValue = searchSystem;
        }

        guardAiSerialized.ApplyModifiedPropertiesWithoutUndo();

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

    private static void ApplyNamedSceneMaterials(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            string rendererName = renderer.gameObject.name;
            string lowerName = rendererName.ToLowerInvariant();

            if (rendererName == "Floor")
            {
                SetMaterial(renderer, FloorColor, 0f, 0.1f);
                continue;
            }

            if (rendererName.StartsWith("Wall_"))
            {
                SetMaterial(renderer, OuterWallColor, 0f, 0.05f);
                continue;
            }

            if (rendererName.StartsWith("Divider_") || rendererName.StartsWith("Obstacle_"))
            {
                SetMaterial(renderer, InteriorColor, 0f, 0.08f);
                continue;
            }

            if (rendererName == "GuardVisual" || rendererName == "GuardVariantBody" || rendererName == "GuardVariantHead")
            {
                SetMaterial(renderer, GuardColor, 0f, 0.2f);
                continue;
            }

            if (rendererName == "EyeIndicator")
            {
                SetMaterial(renderer, GuardEyeColor, 0f, 0.18f, GuardEyeColor * 0.45f);
                continue;
            }

            if (rendererName == "PlayerVisual")
            {
                SetMaterial(renderer, PlayerColor, 0f, 0.18f);
                continue;
            }

            if (rendererName == "Marker")
            {
                SetMaterial(renderer, PatrolPointColor, 0f, 0.2f, PatrolPointColor * 0.2f);
                continue;
            }

            if (lowerName.Contains("ceiling") || lowerName.Contains("roof") || lowerName.Contains("cover") || lowerName.Contains("top"))
            {
                SetMaterial(renderer, CeilingColor, 0f, 0.02f);
                continue;
            }

            SetMaterial(renderer, FallbackColor, 0f, 0.08f);
        }
    }

    private static void EnsureAllRenderersHaveExplicitMaterial()
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
                renderer.material = CreateStandardMaterial(FallbackColor, 0f, 0.08f, null);
            }
        }
    }

    private static void SetMaterial(Renderer renderer, Color color, float metallic, float smoothness, Color? emission = null)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.material = CreateStandardMaterial(color, metallic, smoothness, emission);
    }

    private static Material CreateStandardMaterial(Color color, float metallic, float smoothness, Color? emission)
    {
        Shader shader = Shader.Find("Standard");
        Material material = new Material(shader);
        material.color = color;
        material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        material.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));

        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission.Value);
        }

        return material;
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

    private static void RemoveLegacyRoofObjects()
    {
        string[] roofKeywords = { "ceiling", "roof", "top", "cover" };
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform current = allTransforms[i];
            if (current == null)
            {
                continue;
            }

            string lowerName = current.name.ToLowerInvariant();
            for (int j = 0; j < roofKeywords.Length; j++)
            {
                if (!lowerName.Contains(roofKeywords[j]))
                {
                    continue;
                }

                Object.DestroyImmediate(current.gameObject);
                break;
            }
        }
    }

    private static void RemoveExistingLights()
    {
        Light[] existingLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < existingLights.Length; i++)
        {
            if (existingLights[i] != null)
            {
                Object.DestroyImmediate(existingLights[i].gameObject);
            }
        }
    }

    private static void RemoveExistingCameras()
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
            {
                Object.DestroyImmediate(cameras[i].gameObject);
            }
        }
    }

    private static void RemoveCollider(GameObject gameObject)
    {
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
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
