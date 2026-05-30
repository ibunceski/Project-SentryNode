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
    private static readonly Color DoorColor = Rgb(130, 86, 44);
    private static readonly Color AmbientColor = Rgb(60, 60, 75);
    private static readonly Color MainLightColor = Rgb(255, 245, 220);
    private static readonly Color FillLightColor = Rgb(180, 200, 255);
    private static readonly Color FallbackColor = Rgb(90, 95, 110);

    [MenuItem("Tools/Build Demo Level")]
    public static void BuildDemoLevel()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Build Demo Level is disabled during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        PlayerSoundConfig playerSoundConfig = CapturePlayerSoundConfig();
        GuardSoundConfig guardSoundConfig = CaptureGuardSoundConfig();
        playerSoundConfig = FillMissingPlayerClips(playerSoundConfig);
        guardSoundConfig = FillMissingGuardClips(guardSoundConfig);

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

        LayoutResult layout = BuildIndoorLayout(root.transform, environmentLayer);
        CreateLighting(root.transform);
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

        GuardBundle guardPrimary = CreateGuardVariant(
            root.transform,
            "Guard_A",
            layout.LobbyGuardSpawn,
            environmentLayer,
            false);
        GuardBundle guardVariant = CreateGuardVariant(
            root.transform,
            "Guard_B",
            layout.SecurityGuardSpawn,
            environmentLayer,
            true);

        GameObject player = CreatePlayer(root.transform, playerLayer, layout.PlayerSpawn);

        ConfigureGuardSystems(guardPrimary, player.transform, playerLayer, environmentLayer, layout.LobbyPatrolZone);
        ConfigureGuardSystems(guardVariant, player.transform, playerLayer, environmentLayer, layout.SecurityPatrolZone);
        ApplyPlayerSoundConfig(player.GetComponent<PlayerController>(), playerSoundConfig);
        ApplyGuardSoundConfig(guardPrimary.GuardSoundSystem, guardSoundConfig);
        ApplyGuardSoundConfig(guardVariant.GuardSoundSystem, guardSoundConfig);

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
            new Vector3(52f, 0.1f, 42f),
            parent,
            environmentLayer);
        floor.tag = GroundTagName;
    }

    private static LayoutResult BuildIndoorLayout(Transform parent, int environmentLayer)
    {
        CreateFloor(parent, environmentLayer);
        CreatePerimeter(parent, environmentLayer, 50f, 40f);

        RoomSpec lobby = new RoomSpec(
            "Room_Lobby",
            new Vector3(0f, 0f, -14f),
            new Vector2(14f, 10f),
            new[] { new DoorwaySpec(WallSide.North, 3.4f, 0f) });
        RoomSpec operations = new RoomSpec(
            "Room_Operations",
            new Vector3(0f, 0f, 0f),
            new Vector2(12f, 10f),
            new[]
            {
                new DoorwaySpec(WallSide.South, 3.4f, 0f),
                new DoorwaySpec(WallSide.North, 3.4f, 0f),
                new DoorwaySpec(WallSide.West, 3.2f, 0f),
                new DoorwaySpec(WallSide.East, 3.2f, 0f)
            });
        RoomSpec security = new RoomSpec(
            "Room_Security",
            new Vector3(15f, 0f, 0f),
            new Vector2(10f, 9f),
            new[] { new DoorwaySpec(WallSide.West, 3.2f, 0f) });
        RoomSpec storage = new RoomSpec(
            "Room_Storage",
            new Vector3(-15f, 0f, 0f),
            new Vector2(10f, 9f),
            new[] { new DoorwaySpec(WallSide.East, 3.2f, 0f) });
        RoomSpec restricted = new RoomSpec(
            "Room_Restricted",
            new Vector3(0f, 0f, 14f),
            new Vector2(12f, 9f),
            new[] { new DoorwaySpec(WallSide.South, 3.2f, 0f) });

        ILayoutElement[] layoutElements =
        {
            new RoomLayoutElement(lobby),
            new RoomLayoutElement(operations),
            new RoomLayoutElement(security),
            new RoomLayoutElement(storage),
            new RoomLayoutElement(restricted),
            new CorridorLayoutElement(new CorridorSpec("Corridor_LobbyToOperations", new Vector3(0f, 0f, -7f), 4f, 4.4f, false)),
            new CorridorLayoutElement(new CorridorSpec("Corridor_OperationsToSecurity", new Vector3(8f, 0f, 0f), 4f, 4.2f, true)),
            new CorridorLayoutElement(new CorridorSpec("Corridor_OperationsToStorage", new Vector3(-8f, 0f, 0f), 4f, 4.2f, true)),
            new CorridorLayoutElement(new CorridorSpec("Corridor_OperationsToRestricted", new Vector3(0f, 0f, 7.25f), 4.5f, 4.2f, false))
        };
        for (int i = 0; i < layoutElements.Length; i++)
        {
            layoutElements[i].Build(parent, environmentLayer);
        }

        // Lobby cover and sight breakers.
        CreateCoverObject("LobbyCover_LeftBench", new Vector3(-3.6f, 0.5f, -16.5f), new Vector3(2.8f, 1f, 0.8f), parent, environmentLayer);
        CreateCoverObject("LobbyCover_RightBench", new Vector3(3.6f, 0.5f, -16.5f), new Vector3(2.8f, 1f, 0.8f), parent, environmentLayer);
        CreateCoverObject("LobbyCover_Pillar", new Vector3(0f, 1.05f, -12.2f), new Vector3(1f, 2.1f, 1f), parent, environmentLayer);
        CreateCoverObject("LobbyCover_CenterDesk", new Vector3(4.2f, 0.6f, -14.6f), new Vector3(1.8f, 1.2f, 1f), parent, environmentLayer);

        // Operations room corners and LOS blockers.
        CreateCoverObject("OperationsCover_NW", new Vector3(-4.6f, 0.75f, 3.1f), new Vector3(1.2f, 1.5f, 1.2f), parent, environmentLayer);
        CreateCoverObject("OperationsCover_NE", new Vector3(4.6f, 0.75f, 3.1f), new Vector3(1.2f, 1.5f, 1.2f), parent, environmentLayer);
        CreateCoverObject("OperationsCover_SW", new Vector3(-4.2f, 0.75f, -2.8f), new Vector3(1.2f, 1.5f, 1f), parent, environmentLayer);
        CreateCoverObject("OperationsCover_Mid", new Vector3(2.8f, 0.6f, -2.3f), new Vector3(1.4f, 1.2f, 0.8f), parent, environmentLayer);
        CreateCoverObject("CorridorJunctionCover", new Vector3(-3.1f, 0.65f, 5.9f), new Vector3(0.8f, 1.3f, 0.7f), parent, environmentLayer);

        // Security room props.
        CreateCoverObject("SecurityConsole", new Vector3(17.3f, 0.6f, 2.8f), new Vector3(1.8f, 1.2f, 0.8f), parent, environmentLayer);
        CreateCoverObject("SecurityLocker", new Vector3(18.6f, 1f, -2.8f), new Vector3(0.75f, 2f, 1.1f), parent, environmentLayer);
        CreateCoverObject("SecurityDesk", new Vector3(12.7f, 0.55f, -1.2f), new Vector3(1.2f, 1.1f, 0.8f), parent, environmentLayer);
        CreateCoverObject("SecurityPartition", new Vector3(18.1f, 0.75f, 1.1f), new Vector3(0.7f, 1.5f, 0.55f), parent, environmentLayer);

        // Storage room crates and shelves.
        CreateCoverObject("StorageShelf_West", new Vector3(-17.9f, 1.1f, 0f), new Vector3(0.45f, 2.1f, 4.2f), parent, environmentLayer);
        CreateCoverObject("StorageShelf_East", new Vector3(-12.1f, 1.1f, 0f), new Vector3(0.45f, 2.1f, 4.2f), parent, environmentLayer);
        CreateCoverObject("StorageCrate_A", new Vector3(-17f, 0.5f, -2.6f), new Vector3(0.65f, 0.95f, 0.65f), parent, environmentLayer);
        CreateCoverObject("StorageCrate_B", new Vector3(-16.2f, 0.5f, 1.05f), new Vector3(0.6f, 0.9f, 0.6f), parent, environmentLayer);
        CreateCoverObject("StorageCrate_C", new Vector3(-13.9f, 0.5f, 2.1f), new Vector3(0.65f, 0.95f, 0.65f), parent, environmentLayer);

        // Restricted room barriers.
        CreateCoverObject("RestrictedBarrier_Left", new Vector3(-3f, 0.9f, 14.3f), new Vector3(2f, 1.8f, 1.2f), parent, environmentLayer);
        CreateCoverObject("RestrictedBarrier_Right", new Vector3(3f, 0.9f, 14.3f), new Vector3(2f, 1.8f, 1.2f), parent, environmentLayer);
        CreateCoverObject("RestrictedCore", new Vector3(3.1f, 1.1f, 16.6f), new Vector3(1.6f, 2.2f, 1.2f), parent, environmentLayer);
        CreateCoverObject("RestrictedSideCover", new Vector3(-4.8f, 0.7f, 12.1f), new Vector3(1.2f, 1.4f, 0.9f), parent, environmentLayer);

        PatrolZoneConfig lobbyPatrolZone = CreatePatrolZone(
            parent,
            "PatrolZone_Lobby",
            new Vector3(0f, 0f, -14f),
            new Vector2(12f, 8f),
            environmentLayer);
        PatrolZoneConfig securityPatrolZone = CreatePatrolZone(
            parent,
            "PatrolZone_Security",
            new Vector3(15f, 0f, 0f),
            new Vector2(7f, 6f),
            environmentLayer);
        _ = CreatePatrolZone(
            parent,
            "PatrolZone_Restricted",
            new Vector3(0f, 0f, 14f),
            new Vector2(9f, 7f),
            environmentLayer);

        return new LayoutResult
        {
            PlayerSpawn = new Vector3(-14.5f, 0f, -0.5f),
            LobbyGuardSpawn = new Vector3(-3.2f, 0f, -12.8f),
            SecurityGuardSpawn = new Vector3(16f, 0f, 0.8f),
            LobbyPatrolZone = lobbyPatrolZone,
            SecurityPatrolZone = securityPatrolZone
        };
    }

    private static void CreatePerimeter(Transform parent, int environmentLayer, float width, float depth)
    {
        float halfWidth = width * 0.5f;
        float halfDepth = depth * 0.5f;
        float halfWallHeight = 1.5f;
        float wallThickness = 1f;

        CreateWall("Wall_Outer_North", new Vector3(0f, halfWallHeight, halfDepth), new Vector3(width, 3f, wallThickness), parent, environmentLayer);
        CreateWall("Wall_Outer_South", new Vector3(0f, halfWallHeight, -halfDepth), new Vector3(width, 3f, wallThickness), parent, environmentLayer);
        CreateWall("Wall_Outer_East", new Vector3(halfWidth, halfWallHeight, 0f), new Vector3(wallThickness, 3f, depth), parent, environmentLayer);
        CreateWall("Wall_Outer_West", new Vector3(-halfWidth, halfWallHeight, 0f), new Vector3(wallThickness, 3f, depth), parent, environmentLayer);
    }

    private static void CreateRoom(Transform parent, RoomSpec room, int environmentLayer)
    {
        GameObject roomRoot = new GameObject(room.Name);
        roomRoot.transform.SetParent(parent);
        roomRoot.transform.position = Vector3.zero;

        float halfWidth = room.Size.x * 0.5f;
        float halfDepth = room.Size.y * 0.5f;
        float halfWallHeight = 1.5f;
        float wallThickness = 0.9f;
        const float wallHeight = 3f;

        CreateWallWithDoorway(roomRoot.transform, room.Name + "_Wall_North", room.Center + new Vector3(0f, halfWallHeight, halfDepth), room.Size.x, wallHeight, wallThickness, true, FindDoorway(room, WallSide.North), environmentLayer);
        CreateWallWithDoorway(roomRoot.transform, room.Name + "_Wall_South", room.Center + new Vector3(0f, halfWallHeight, -halfDepth), room.Size.x, wallHeight, wallThickness, true, FindDoorway(room, WallSide.South), environmentLayer);
        CreateWallWithDoorway(roomRoot.transform, room.Name + "_Wall_East", room.Center + new Vector3(halfWidth, halfWallHeight, 0f), room.Size.y, wallHeight, wallThickness, false, FindDoorway(room, WallSide.East), environmentLayer);
        CreateWallWithDoorway(roomRoot.transform, room.Name + "_Wall_West", room.Center + new Vector3(-halfWidth, halfWallHeight, 0f), room.Size.y, wallHeight, wallThickness, false, FindDoorway(room, WallSide.West), environmentLayer);
    }

    private static void CreateCorridor(Transform parent, CorridorSpec corridor, int environmentLayer)
    {
        GameObject corridorRoot = new GameObject(corridor.Name);
        corridorRoot.transform.SetParent(parent);
        corridorRoot.transform.position = Vector3.zero;

        const float wallHeight = 3f;
        const float wallThickness = 0.8f;
        float halfWallHeight = wallHeight * 0.5f;
        float halfWidth = corridor.Width * 0.5f;

        if (corridor.IsHorizontal)
        {
            CreateWall(
                corridor.Name + "_NorthWall",
                new Vector3(corridor.Center.x, halfWallHeight, corridor.Center.z + halfWidth),
                new Vector3(corridor.Length, wallHeight, wallThickness),
                corridorRoot.transform,
                environmentLayer);
            CreateWall(
                corridor.Name + "_SouthWall",
                new Vector3(corridor.Center.x, halfWallHeight, corridor.Center.z - halfWidth),
                new Vector3(corridor.Length, wallHeight, wallThickness),
                corridorRoot.transform,
                environmentLayer);
            return;
        }

        CreateWall(
            corridor.Name + "_EastWall",
            new Vector3(corridor.Center.x + halfWidth, halfWallHeight, corridor.Center.z),
            new Vector3(wallThickness, wallHeight, corridor.Length),
            corridorRoot.transform,
            environmentLayer);
        CreateWall(
            corridor.Name + "_WestWall",
            new Vector3(corridor.Center.x - halfWidth, halfWallHeight, corridor.Center.z),
            new Vector3(wallThickness, wallHeight, corridor.Length),
            corridorRoot.transform,
            environmentLayer);
    }

    private static void CreateWallWithDoorway(
        Transform parent,
        string wallName,
        Vector3 center,
        float wallLength,
        float wallHeight,
        float wallThickness,
        bool horizontal,
        DoorwaySpec? doorway,
        int environmentLayer)
    {
        if (!doorway.HasValue)
        {
            Vector3 wallSize = horizontal
                ? new Vector3(wallLength, wallHeight, wallThickness)
                : new Vector3(wallThickness, wallHeight, wallLength);
            CreateWall(wallName, center, wallSize, parent, environmentLayer);
            return;
        }

        DoorwaySpec doorwayValue = doorway.Value;
        float openingWidth = Mathf.Clamp(doorwayValue.Width, 0.5f, wallLength - 0.5f);
        float clearLength = Mathf.Max(0.1f, wallLength - openingWidth);
        float halfSegmentLength = clearLength * 0.5f;
        float maxCenterOffset = clearLength * 0.5f;
        float centerOffset = Mathf.Clamp(doorwayValue.Offset, -maxCenterOffset, maxCenterOffset);
        float gapOffset = openingWidth * 0.5f;

        CreateDoorway(
            parent,
            wallName,
            center,
            halfSegmentLength,
            wallHeight,
            wallThickness,
            horizontal,
            centerOffset,
            gapOffset,
            environmentLayer);
    }

    private static void CreateDoorway(
        Transform parent,
        string wallName,
        Vector3 wallCenter,
        float segmentLength,
        float wallHeight,
        float wallThickness,
        bool horizontal,
        float centerOffset,
        float gapOffset,
        int environmentLayer)
    {
        Vector3 axis = horizontal ? Vector3.right : Vector3.forward;
        Vector3 segmentOffset = axis * (gapOffset + segmentLength * 0.5f);
        Vector3 centeredWall = wallCenter + axis * centerOffset;
        Vector3 wallSize = horizontal
            ? new Vector3(segmentLength, wallHeight, wallThickness)
            : new Vector3(wallThickness, wallHeight, segmentLength);

        CreateWall(wallName + "_A", centeredWall - segmentOffset, wallSize, parent, environmentLayer);
        CreateWall(wallName + "_B", centeredWall + segmentOffset, wallSize, parent, environmentLayer);
        CreateDoor(
            parent,
            wallName + "_Door",
            centeredWall,
            gapOffset * 2f,
            wallHeight,
            wallThickness,
            horizontal,
            environmentLayer);
    }

    private static void CreateDoor(
        Transform parent,
        string doorName,
        Vector3 doorwayCenter,
        float openingWidth,
        float wallHeight,
        float wallThickness,
        bool horizontal,
        int environmentLayer)
    {
        float doorWidth = Mathf.Max(0.2f, openingWidth - 0.08f);
        float doorHeight = Mathf.Max(1.8f, wallHeight - 0.25f);
        float doorThickness = Mathf.Max(0.08f, wallThickness * 0.35f);
        Vector3 slideAxis = horizontal ? Vector3.right : Vector3.forward;

        Vector3 doorPosition = doorwayCenter;
        doorPosition.y = doorwayCenter.y - (wallHeight * 0.5f) + (doorHeight * 0.5f);

        GameObject doorRoot = new GameObject(doorName);
        doorRoot.transform.SetParent(parent);
        doorRoot.transform.position = doorPosition;
        doorRoot.transform.rotation = Quaternion.identity;
        SetLayerRecursively(doorRoot, environmentLayer);
        Door door = doorRoot.AddComponent<Door>();

        SerializedObject serializedDoor = new SerializedObject(door);
        serializedDoor.FindProperty("slideDirection").vector3Value = slideAxis;
        serializedDoor.FindProperty("slideDistance").floatValue = doorWidth;
        serializedDoor.FindProperty("openSpeed").floatValue = 8f;
        serializedDoor.FindProperty("defaultAutoCloseDelay").floatValue = 1.35f;
        serializedDoor.ApplyModifiedPropertiesWithoutUndo();

        GameObject doorPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorPanel.name = doorName + "_Panel";
        doorPanel.transform.SetParent(doorRoot.transform, false);
        doorPanel.transform.localPosition = Vector3.zero;
        doorPanel.transform.localRotation = Quaternion.identity;
        doorPanel.transform.localScale = horizontal
            ? new Vector3(doorWidth, doorHeight, doorThickness)
            : new Vector3(doorThickness, doorHeight, doorWidth);
        SetLayerRecursively(doorPanel, environmentLayer);
    }

    private static GameObject CreateWall(string name, Vector3 center, Vector3 size, Transform parent, int environmentLayer)
    {
        return CreateCube(name, center, size, parent, environmentLayer);
    }

    private static void CreateCoverObject(string name, Vector3 center, Vector3 size, Transform parent, int environmentLayer)
    {
        CreateCube(name, center, size, parent, environmentLayer);
    }

    private static PatrolZoneConfig CreatePatrolZone(
        Transform parent,
        string name,
        Vector3 center,
        Vector2 size,
        int environmentLayer)
    {
        GameObject zoneRoot = new GameObject(name);
        zoneRoot.transform.SetParent(parent);
        zoneRoot.transform.position = center;
        zoneRoot.layer = environmentLayer;

        return new PatrolZoneConfig
        {
            Name = name,
            Root = zoneRoot.transform,
            MinRadius = Mathf.Max(1.5f, Mathf.Min(size.x, size.y) * 0.3f),
            MaxRadius = Mathf.Max(3.25f, Mathf.Min(size.x, size.y) * 0.65f),
            MinStepDistance = Mathf.Max(1f, Mathf.Min(size.x, size.y) * 0.2f)
        };
    }

    private static DoorwaySpec? FindDoorway(RoomSpec room, WallSide side)
    {
        if (room.Doorways == null)
        {
            return null;
        }

        for (int i = 0; i < room.Doorways.Length; i++)
        {
            if (room.Doorways[i].Side == side)
            {
                return room.Doorways[i];
            }
        }

        return null;
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
        guard.transform.position = SnapToNavMesh(position, 3f);
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
        agent.radius = 0.3f;
        agent.height = 1.9f;
        agent.baseOffset = 0f;
        agent.stoppingDistance = 0.3f;
        agent.angularSpeed = 360f;
        agent.autoBraking = false;

        GuardAI guardAI = guard.AddComponent<GuardAI>();
        GuardDoorInteractor guardDoorInteractor = guard.AddComponent<GuardDoorInteractor>();
        VisionSystem visionSystem = guard.AddComponent<VisionSystem>();
        HearingSystem hearingSystem = guard.AddComponent<HearingSystem>();
        PatrolSystem patrolSystem = guard.AddComponent<PatrolSystem>();
        GuardSoundSystem guardSoundSystem = guard.AddComponent<GuardSoundSystem>();
        guard.AddComponent<GuardDebugVisualizer>();

        SerializedObject serializedDoorInteractor = new SerializedObject(guardDoorInteractor);
        serializedDoorInteractor.FindProperty("detectionMask").intValue = 1 << environmentLayer;
        serializedDoorInteractor.FindProperty("detectionDistance").floatValue = 1.75f;
        serializedDoorInteractor.FindProperty("closeDelayAfterPass").floatValue = 1.2f;
        serializedDoorInteractor.ApplyModifiedPropertiesWithoutUndo();

        return new GuardBundle
        {
            Root = guard,
            Agent = agent,
            GuardAI = guardAI,
            VisionSystem = visionSystem,
            HearingSystem = hearingSystem,
            PatrolSystem = patrolSystem,
            GuardSoundSystem = guardSoundSystem
        };
    }

    private static GameObject CreatePlayer(Transform parent, int playerLayer, Vector3 spawnPosition)
    {
        GameObject player = new GameObject("Player");
        player.transform.SetParent(parent);
        player.transform.position = spawnPosition;
        player.transform.rotation = Quaternion.identity;
        SetLayerRecursively(player, playerLayer);

        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 1.8f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.radius = 0.3f;

        player.AddComponent<PlayerController>();
        PlayerInteraction playerInteraction = player.AddComponent<PlayerInteraction>();

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

        SerializedObject interactionSerialized = new SerializedObject(playerInteraction);
        interactionSerialized.FindProperty("cameraTransform").objectReferenceValue = cameraObject.transform;
        interactionSerialized.ApplyModifiedPropertiesWithoutUndo();

        return player;
    }

    private static void ConfigureGuardSystems(
        GuardBundle guardBundle,
        Transform playerTarget,
        int playerLayer,
        int environmentLayer,
        PatrolZoneConfig patrolZoneConfig)
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

        ApplyPatrolZoneConfig(guardBundle.PatrolSystem, patrolZoneConfig);
    }

    private static void ApplyPatrolZoneConfig(PatrolSystem patrolSystem, PatrolZoneConfig patrolZoneConfig)
    {
        if (patrolSystem == null)
        {
            return;
        }

        SerializedObject patrolSerialized = new SerializedObject(patrolSystem);
        SerializedProperty patrolCenterProperty = patrolSerialized.FindProperty("patrolCenter");
        if (patrolCenterProperty != null)
        {
            patrolCenterProperty.objectReferenceValue = patrolZoneConfig.Root;
        }

        SerializedProperty constrainToCenterProperty = patrolSerialized.FindProperty("constrainToPatrolCenter");
        if (constrainToCenterProperty != null)
        {
            // Let guards roam across connected rooms/corridors instead of staying anchored to one room.
            constrainToCenterProperty.boolValue = false;
        }

        float roamMinRadius = Mathf.Max(4f, patrolZoneConfig.MinRadius);
        float roamMaxRadius = Mathf.Max(12f, patrolZoneConfig.MaxRadius * 2.25f);
        patrolSerialized.FindProperty("minWanderRadius").floatValue = roamMinRadius;
        patrolSerialized.FindProperty("maxWanderRadius").floatValue = roamMaxRadius;
        patrolSerialized.FindProperty("minStepDistance").floatValue = Mathf.Min(Mathf.Max(2.5f, patrolZoneConfig.MinStepDistance), roamMaxRadius * 0.7f);
        patrolSerialized.FindProperty("waypointTolerance").floatValue = 0.45f;
        patrolSerialized.FindProperty("minWaitDuration").floatValue = 0.1f;
        patrolSerialized.FindProperty("maxWaitDuration").floatValue = 0.35f;
        patrolSerialized.FindProperty("maxSampleAttempts").intValue = 24;
        patrolSerialized.FindProperty("navMeshSampleRange").floatValue = 3f;
        SerializedProperty navRecoverRangeProperty = patrolSerialized.FindProperty("navMeshRecoverRange");
        if (navRecoverRangeProperty != null)
        {
            navRecoverRangeProperty.floatValue = 4f;
        }

        SerializedProperty maxStallDurationProperty = patrolSerialized.FindProperty("maxStallDuration");
        if (maxStallDurationProperty != null)
        {
            maxStallDurationProperty.floatValue = 1f;
        }

        patrolSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Vector3 SnapToNavMesh(Vector3 desiredPosition, float sampleRange)
    {
        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, Mathf.Max(0.5f, sampleRange), NavMesh.AllAreas))
        {
            return hit.position;
        }

        return desiredPosition;
    }

    private static PlayerSoundConfig CapturePlayerSoundConfig()
    {
        PlayerController existingPlayerController = Object.FindFirstObjectByType<PlayerController>();
        if (existingPlayerController == null)
        {
            return PlayerSoundConfig.Default;
        }

        SerializedObject serializedPlayer = new SerializedObject(existingPlayerController);
        return new PlayerSoundConfig
        {
            IsValid = true,
            WalkFootstepClip = serializedPlayer.FindProperty("walkFootstepClip").objectReferenceValue as AudioClip,
            SprintFootstepClip = serializedPlayer.FindProperty("sprintFootstepClip").objectReferenceValue as AudioClip,
            CrouchFootstepClip = serializedPlayer.FindProperty("crouchFootstepClip").objectReferenceValue as AudioClip,
            WalkFootstepInterval = serializedPlayer.FindProperty("walkFootstepInterval").floatValue,
            SprintFootstepInterval = serializedPlayer.FindProperty("sprintFootstepInterval").floatValue,
            CrouchFootstepInterval = serializedPlayer.FindProperty("crouchFootstepInterval").floatValue,
            WalkFootstepVolume = serializedPlayer.FindProperty("walkFootstepVolume").floatValue,
            SprintFootstepVolume = serializedPlayer.FindProperty("sprintFootstepVolume").floatValue,
            CrouchFootstepVolume = serializedPlayer.FindProperty("crouchFootstepVolume").floatValue,
            WalkNoiseRadius = serializedPlayer.FindProperty("walkNoiseRadius").floatValue,
            SprintNoiseRadius = serializedPlayer.FindProperty("sprintNoiseRadius").floatValue,
            CrouchNoiseRadius = serializedPlayer.FindProperty("crouchNoiseRadius").floatValue,
            MoveInputThreshold = serializedPlayer.FindProperty("moveInputThreshold").floatValue
        };
    }

    private static GuardSoundConfig CaptureGuardSoundConfig()
    {
        GuardSoundSystem[] existingGuardSounds = Object.FindObjectsByType<GuardSoundSystem>(FindObjectsSortMode.None);
        if (existingGuardSounds == null || existingGuardSounds.Length == 0)
        {
            return GuardSoundConfig.Default;
        }

        GuardSoundSystem source = existingGuardSounds[0];
        for (int i = 0; i < existingGuardSounds.Length; i++)
        {
            GuardSoundSystem candidate = existingGuardSounds[i];
            if (candidate != null && candidate.gameObject.name == "Guard_A")
            {
                source = candidate;
                break;
            }
        }

        SerializedObject serializedGuardSound = new SerializedObject(source);
        return new GuardSoundConfig
        {
            IsValid = true,
            SuspiciousEnterClip = serializedGuardSound.FindProperty("suspiciousEnterClip").objectReferenceValue as AudioClip,
            ChaseEnterClip = serializedGuardSound.FindProperty("chaseEnterClip").objectReferenceValue as AudioClip,
            InvestigatingEnterClip = serializedGuardSound.FindProperty("investigatingEnterClip").objectReferenceValue as AudioClip,
            ReturnToPatrolClip = serializedGuardSound.FindProperty("returnToPatrolClip").objectReferenceValue as AudioClip,
            PatrolFootstepsLoopClip = serializedGuardSound.FindProperty("patrolFootstepsLoopClip").objectReferenceValue as AudioClip,
            ChasingFootstepsLoopClip = serializedGuardSound.FindProperty("chasingFootstepsLoopClip").objectReferenceValue as AudioClip,
            SearchingLoopClip = serializedGuardSound.FindProperty("searchingLoopClip").objectReferenceValue as AudioClip,
            PatrolLoopVolume = serializedGuardSound.FindProperty("patrolLoopVolume").floatValue,
            PatrolLoopPitch = serializedGuardSound.FindProperty("patrolLoopPitch").floatValue,
            ChaseLoopVolume = serializedGuardSound.FindProperty("chaseLoopVolume").floatValue,
            ChaseLoopPitch = serializedGuardSound.FindProperty("chaseLoopPitch").floatValue,
            SearchingLoopVolume = serializedGuardSound.FindProperty("searchingLoopVolume").floatValue,
            SearchingLoopPitch = serializedGuardSound.FindProperty("searchingLoopPitch").floatValue,
            MovingSpeedThreshold = serializedGuardSound.FindProperty("movingSpeedThreshold").floatValue
        };
    }

    private static void ApplyPlayerSoundConfig(PlayerController playerController, PlayerSoundConfig config)
    {
        if (!config.IsValid || playerController == null)
        {
            return;
        }

        SerializedObject serializedPlayer = new SerializedObject(playerController);
        serializedPlayer.FindProperty("walkFootstepClip").objectReferenceValue = config.WalkFootstepClip;
        serializedPlayer.FindProperty("sprintFootstepClip").objectReferenceValue = config.SprintFootstepClip;
        serializedPlayer.FindProperty("crouchFootstepClip").objectReferenceValue = config.CrouchFootstepClip;
        serializedPlayer.FindProperty("walkFootstepInterval").floatValue = config.WalkFootstepInterval;
        serializedPlayer.FindProperty("sprintFootstepInterval").floatValue = config.SprintFootstepInterval;
        serializedPlayer.FindProperty("crouchFootstepInterval").floatValue = config.CrouchFootstepInterval;
        serializedPlayer.FindProperty("walkFootstepVolume").floatValue = config.WalkFootstepVolume;
        serializedPlayer.FindProperty("sprintFootstepVolume").floatValue = config.SprintFootstepVolume;
        serializedPlayer.FindProperty("crouchFootstepVolume").floatValue = config.CrouchFootstepVolume;
        serializedPlayer.FindProperty("walkNoiseRadius").floatValue = config.WalkNoiseRadius;
        serializedPlayer.FindProperty("sprintNoiseRadius").floatValue = config.SprintNoiseRadius;
        serializedPlayer.FindProperty("crouchNoiseRadius").floatValue = config.CrouchNoiseRadius;
        serializedPlayer.FindProperty("moveInputThreshold").floatValue = config.MoveInputThreshold;
        serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyGuardSoundConfig(GuardSoundSystem guardSoundSystem, GuardSoundConfig config)
    {
        if (!config.IsValid || guardSoundSystem == null)
        {
            return;
        }

        SerializedObject serializedGuardSound = new SerializedObject(guardSoundSystem);
        serializedGuardSound.FindProperty("suspiciousEnterClip").objectReferenceValue = config.SuspiciousEnterClip;
        serializedGuardSound.FindProperty("chaseEnterClip").objectReferenceValue = config.ChaseEnterClip;
        serializedGuardSound.FindProperty("investigatingEnterClip").objectReferenceValue = config.InvestigatingEnterClip;
        serializedGuardSound.FindProperty("returnToPatrolClip").objectReferenceValue = config.ReturnToPatrolClip;
        serializedGuardSound.FindProperty("patrolFootstepsLoopClip").objectReferenceValue = config.PatrolFootstepsLoopClip;
        serializedGuardSound.FindProperty("chasingFootstepsLoopClip").objectReferenceValue = config.ChasingFootstepsLoopClip;
        serializedGuardSound.FindProperty("searchingLoopClip").objectReferenceValue = config.SearchingLoopClip;
        serializedGuardSound.FindProperty("patrolLoopVolume").floatValue = config.PatrolLoopVolume;
        serializedGuardSound.FindProperty("patrolLoopPitch").floatValue = config.PatrolLoopPitch;
        serializedGuardSound.FindProperty("chaseLoopVolume").floatValue = config.ChaseLoopVolume;
        serializedGuardSound.FindProperty("chaseLoopPitch").floatValue = config.ChaseLoopPitch;
        serializedGuardSound.FindProperty("searchingLoopVolume").floatValue = config.SearchingLoopVolume;
        serializedGuardSound.FindProperty("searchingLoopPitch").floatValue = config.SearchingLoopPitch;
        serializedGuardSound.FindProperty("movingSpeedThreshold").floatValue = config.MovingSpeedThreshold;
        serializedGuardSound.ApplyModifiedPropertiesWithoutUndo();
    }

    private static PlayerSoundConfig FillMissingPlayerClips(PlayerSoundConfig config)
    {
        if (!config.IsValid)
        {
            config = PlayerSoundConfig.Default;
        }

        if (config.WalkFootstepClip == null)
        {
            config.WalkFootstepClip = LoadAudioClipByName("walkFootstepClip");
        }

        if (config.SprintFootstepClip == null)
        {
            config.SprintFootstepClip = LoadAudioClipByName("sprintFootstepClip");
        }

        if (config.CrouchFootstepClip == null)
        {
            config.CrouchFootstepClip = LoadAudioClipByName("crouchFootstepClip");
        }

        return config;
    }

    private static GuardSoundConfig FillMissingGuardClips(GuardSoundConfig config)
    {
        if (!config.IsValid)
        {
            config = GuardSoundConfig.Default;
        }

        if (config.SuspiciousEnterClip == null)
        {
            config.SuspiciousEnterClip = LoadAudioClipByName("suspiciousEnterClip");
        }

        if (config.ChaseEnterClip == null)
        {
            config.ChaseEnterClip = LoadAudioClipByName("chaseEnterClip");
        }

        if (config.InvestigatingEnterClip == null)
        {
            config.InvestigatingEnterClip = LoadAudioClipByName("investigatingEnterClip");
        }

        if (config.ReturnToPatrolClip == null)
        {
            config.ReturnToPatrolClip = LoadAudioClipByName("returnToPatrolClip");
        }

        if (config.PatrolFootstepsLoopClip == null)
        {
            config.PatrolFootstepsLoopClip = LoadAudioClipByName("patrolFootstepsLoopClip");
        }

        if (config.ChasingFootstepsLoopClip == null)
        {
            config.ChasingFootstepsLoopClip = LoadAudioClipByName("chasingFootstepsLoopClip");
        }

        if (config.SearchingLoopClip == null)
        {
            config.SearchingLoopClip = LoadAudioClipByName("searchingLoopClip");
        }

        return config;
    }

    private static AudioClip LoadAudioClipByName(string clipName)
    {
        if (string.IsNullOrEmpty(clipName))
        {
            return null;
        }

        string[] clipGuids = AssetDatabase.FindAssets(clipName + " t:AudioClip", new[] { "Assets/Audio" });
        if (clipGuids == null || clipGuids.Length == 0)
        {
            return null;
        }

        string clipPath = AssetDatabase.GUIDToAssetPath(clipGuids[0]);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
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

            if (lowerName.Contains("_door_"))
            {
                SetMaterial(renderer, DoorColor, 0.05f, 0.3f);
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

            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                renderer.sharedMaterial = CreateStandardMaterial(FallbackColor, 0f, 0.08f, null);
            }
        }
    }

    private static void SetMaterial(Renderer renderer, Color color, float metallic, float smoothness, Color? emission = null)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sharedMaterial = CreateStandardMaterial(color, metallic, smoothness, emission);
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

    private interface ILayoutElement
    {
        void Build(Transform parent, int environmentLayer);
    }

    private readonly struct RoomLayoutElement : ILayoutElement
    {
        private readonly RoomSpec roomSpec;

        public RoomLayoutElement(RoomSpec roomSpec)
        {
            this.roomSpec = roomSpec;
        }

        public void Build(Transform parent, int environmentLayer)
        {
            CreateRoom(parent, roomSpec, environmentLayer);
        }
    }

    private readonly struct CorridorLayoutElement : ILayoutElement
    {
        private readonly CorridorSpec corridorSpec;

        public CorridorLayoutElement(CorridorSpec corridorSpec)
        {
            this.corridorSpec = corridorSpec;
        }

        public void Build(Transform parent, int environmentLayer)
        {
            CreateCorridor(parent, corridorSpec, environmentLayer);
        }
    }

    private enum WallSide
    {
        North,
        South,
        East,
        West
    }

    private readonly struct DoorwaySpec
    {
        public DoorwaySpec(WallSide side, float width, float offset)
        {
            Side = side;
            Width = width;
            Offset = offset;
        }

        public WallSide Side { get; }
        public float Width { get; }
        public float Offset { get; }
    }

    private readonly struct RoomSpec
    {
        public RoomSpec(string name, Vector3 center, Vector2 size, DoorwaySpec[] doorways)
        {
            Name = name;
            Center = center;
            Size = size;
            Doorways = doorways;
        }

        public string Name { get; }
        public Vector3 Center { get; }
        public Vector2 Size { get; }
        public DoorwaySpec[] Doorways { get; }
    }

    private readonly struct CorridorSpec
    {
        public CorridorSpec(string name, Vector3 center, float length, float width, bool isHorizontal)
        {
            Name = name;
            Center = center;
            Length = length;
            Width = width;
            IsHorizontal = isHorizontal;
        }

        public string Name { get; }
        public Vector3 Center { get; }
        public float Length { get; }
        public float Width { get; }
        public bool IsHorizontal { get; }
    }

    private struct PatrolZoneConfig
    {
        public string Name;
        public Transform Root;
        public float MinRadius;
        public float MaxRadius;
        public float MinStepDistance;
    }

    private struct LayoutResult
    {
        public Vector3 PlayerSpawn;
        public Vector3 LobbyGuardSpawn;
        public Vector3 SecurityGuardSpawn;
        public PatrolZoneConfig LobbyPatrolZone;
        public PatrolZoneConfig SecurityPatrolZone;
    }

    private struct GuardBundle
    {
        public GameObject Root;
        public NavMeshAgent Agent;
        public GuardAI GuardAI;
        public VisionSystem VisionSystem;
        public HearingSystem HearingSystem;
        public PatrolSystem PatrolSystem;
        public GuardSoundSystem GuardSoundSystem;
    }

    private struct PlayerSoundConfig
    {
        public static readonly PlayerSoundConfig Invalid = new PlayerSoundConfig { IsValid = false };
        public static readonly PlayerSoundConfig Default = new PlayerSoundConfig
        {
            IsValid = true,
            WalkFootstepInterval = 0.5f,
            SprintFootstepInterval = 0.3f,
            CrouchFootstepInterval = 0.75f,
            WalkFootstepVolume = 0.35f,
            SprintFootstepVolume = 0.6f,
            CrouchFootstepVolume = 0.2f,
            WalkNoiseRadius = 4.5f,
            SprintNoiseRadius = 8f,
            CrouchNoiseRadius = 0f,
            MoveInputThreshold = 0.01f
        };

        public bool IsValid;
        public AudioClip WalkFootstepClip;
        public AudioClip SprintFootstepClip;
        public AudioClip CrouchFootstepClip;
        public float WalkFootstepInterval;
        public float SprintFootstepInterval;
        public float CrouchFootstepInterval;
        public float WalkFootstepVolume;
        public float SprintFootstepVolume;
        public float CrouchFootstepVolume;
        public float WalkNoiseRadius;
        public float SprintNoiseRadius;
        public float CrouchNoiseRadius;
        public float MoveInputThreshold;
    }

    private struct GuardSoundConfig
    {
        public static readonly GuardSoundConfig Invalid = new GuardSoundConfig { IsValid = false };
        public static readonly GuardSoundConfig Default = new GuardSoundConfig
        {
            IsValid = true,
            PatrolLoopVolume = 0.2f,
            PatrolLoopPitch = 0.95f,
            ChaseLoopVolume = 0.65f,
            ChaseLoopPitch = 1.25f,
            SearchingLoopVolume = 0.35f,
            SearchingLoopPitch = 1f,
            MovingSpeedThreshold = 0.05f
        };

        public bool IsValid;
        public AudioClip SuspiciousEnterClip;
        public AudioClip ChaseEnterClip;
        public AudioClip InvestigatingEnterClip;
        public AudioClip ReturnToPatrolClip;
        public AudioClip PatrolFootstepsLoopClip;
        public AudioClip ChasingFootstepsLoopClip;
        public AudioClip SearchingLoopClip;
        public float PatrolLoopVolume;
        public float PatrolLoopPitch;
        public float ChaseLoopVolume;
        public float ChaseLoopPitch;
        public float SearchingLoopVolume;
        public float SearchingLoopPitch;
        public float MovingSpeedThreshold;
    }
}
