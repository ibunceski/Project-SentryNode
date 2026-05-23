using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
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
        CreatePlayer(root.transform, playerLayer);
        Transform[] patrolPoints = CreatePatrolPoints(root.transform);

        ConfigureGuardSystems(guardPrimary, patrolPoints, playerLayer, environmentLayer);
        ConfigureGuardSystems(guardVariant, patrolPoints, playerLayer, environmentLayer);

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

        for (int i = 0; i < 4; i++)
        {
            float width = Random.Range(1.2f, 3f);
            float depth = Random.Range(1.2f, 3f);
            float height = Random.Range(1.2f, 2.8f);
            float x = Random.Range(-11f, 11f);
            float z = Random.Range(-11f, 11f);

            CreateCube(
                "Obstacle_" + (i + 1),
                new Vector3(x, height * 0.5f, z),
                new Vector3(width, height, depth),
                parent,
                environmentLayer);
        }
    }

    private static void CreateLighting(Transform parent)
    {
        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.SetParent(parent);
        lightObject.transform.position = new Vector3(0f, 10f, 0f);
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Light directionalLight = lightObject.AddComponent<Light>();
        directionalLight.type = LightType.Directional;
        directionalLight.intensity = 1.2f;
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

        GameObject cameraObject = new GameObject("PlayerCamera");
        cameraObject.transform.SetParent(player.transform);
        cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        cameraObject.AddComponent<Camera>();
        SetLayerRecursively(cameraObject, playerLayer);

        return player;
    }

    private static Transform[] CreatePatrolPoints(Transform parent)
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
            patrolPoints[i] = point.transform;
        }

        return patrolPoints;
    }

    private static void ConfigureGuardSystems(GuardBundle guardBundle, Transform[] patrolPoints, int playerLayer, int environmentLayer)
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

        SerializedObject visionSerialized = new SerializedObject(guardBundle.VisionSystem);
        visionSerialized.FindProperty("obstacleMask").intValue = 1 << environmentLayer;
        visionSerialized.FindProperty("playerMask").intValue = 1 << playerLayer;
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
