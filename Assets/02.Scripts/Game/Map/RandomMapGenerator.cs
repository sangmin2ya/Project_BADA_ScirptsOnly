using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class RandomMapGenerator : NetworkBehaviour
{
    #region Constants
    private const int MAP_SIZE = 50;
    private const int GRID_UNIT = 5;
    private const int MAX_ROOM_PLACE_ATTEMPTS = 2000;
    private const int MAX_DOOR_PLACE_ATTEMPTS = 1000;
    private const int MAX_CENTER_FIND_ATTEMPTS = 20;
    #endregion

    #region Room Type Enum
    public enum 방타입
    {
        비어있음 = 0,
        소형사무실 = 1,
        대형사무실 = 2,
        화장실 = 3,
        회의실 = 4,
        계단 = 5,
        엘레베이터 = 6,
        창고 = 7,
        복도 = 8,
        외부문 = 9,
        문 = 10
    }
    #endregion

    #region Room Data Structure
    [System.Serializable]
    public struct RoomData
    {
        public 방타입 type;
        public int x;
        public int y;
        public int width;
        public int height;
        public int connections;
        public int doorCount;
        public string side; // 외부문 전용

        public RoomData(방타입 roomType, int posX, int posY, int roomWidth, int roomHeight)
        {
            type = roomType;
            x = posX;
            y = posY;
            width = roomWidth;
            height = roomHeight;
            connections = 0;
            doorCount = 0;
            side = "";
        }
    }

    [System.Serializable]
    public struct RoomPrefabData
    {
        public 방타입 roomType;
        public GameObject prefab;
        public int width;
        public int height;
    }

    [System.Serializable]
    public struct WallSegment
    {
        public string side;
        public int x;
        public int y;
        public int doorX;
        public int doorY;
        public string roomSide;
    }
    #endregion

    #region Serialized Fields
    [Header("방 프리팹 설정")]
    [SerializeField] private RoomPrefabData[] roomPrefabs;

    [Header("복도 및 문 프리팹")]
    [SerializeField] private GameObject corridorPrefab;
    [SerializeField] private GameObject doorPrefab;
    [SerializeField] private GameObject externalDoorPrefab;

    [Header("생성된 오브젝트 부모")]
    [SerializeField] private Transform roomParent;
    [SerializeField] private Transform corridorParent;
    [SerializeField] private Transform doorParent;
    #endregion

    #region Private Fields
    private 방타입[,] _map;
    private List<RoomData> _rooms;
    private List<GameObject> _generatedObjects;
    private Dictionary<방타입, RoomPrefabData> _roomPrefabDict;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        GenerateRandomMap();
    }
    #endregion

    #region Context Menu Methods
    [ContextMenu("맵 재생성")]
    [Server]
    public void RegenerateMap()
    {
        ClearAllGeneratedObjects();
        GenerateRandomMap();
    }

    [ContextMenu("맵 초기화")]
    public void ClearMap()
    {
        ClearAllGeneratedObjects();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 랜덤 맵을 생성합니다
    /// </summary>
    [Server]
    public void GenerateRandomMap()
    {
        try
        {
            InitializeMap();
            GenerateRooms();
            GenerateExternalDoors();
            GenerateCorridors();
            SpawnRoomPrefabs();
            RpcDisableConnectedDoors();

            Debug.Log("맵 생성이 완료되었습니다.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"맵 생성 중 오류 발생: {ex.Message}");
            ClearAllGeneratedObjects();
        }
    }
    #endregion

    #region Initialization Methods
    /// <summary>
    /// 컴포넌트들을 초기화합니다
    /// </summary>
    private void InitializeComponents()
    {
        _generatedObjects = new List<GameObject>();
        _roomPrefabDict = new Dictionary<방타입, RoomPrefabData>();

        // 방 프리팹 딕셔너리 생성
        foreach (RoomPrefabData prefabData in roomPrefabs)
        {
            if (!_roomPrefabDict.ContainsKey(prefabData.roomType))
            {
                _roomPrefabDict.Add(prefabData.roomType, prefabData);
            }
        }

        // 부모 오브젝트가 없으면 생성
        CreateParentObjects();
    }

    /// <summary>
    /// 부모 오브젝트들을 생성합니다
    /// </summary>
    private void CreateParentObjects()
    {
        if (roomParent == null)
        {
            GameObject roomParentObj = new GameObject("Rooms");
            roomParentObj.transform.SetParent(transform);
            roomParent = roomParentObj.transform;
        }

        if (corridorParent == null)
        {
            GameObject corridorParentObj = new GameObject("Corridors");
            corridorParentObj.transform.SetParent(transform);
            corridorParent = corridorParentObj.transform;
        }

        if (doorParent == null)
        {
            GameObject doorParentObj = new GameObject("Doors");
            doorParentObj.transform.SetParent(transform);
            doorParent = doorParentObj.transform;
        }
    }

    /// <summary>
    /// 맵 배열을 초기화합니다
    /// </summary>
    private void InitializeMap()
    {
        _map = new 방타입[MAP_SIZE, MAP_SIZE];
        _rooms = new List<RoomData>();

        for (int x = 0; x < MAP_SIZE; x++)
        {
            for (int y = 0; y < MAP_SIZE; y++)
            {
                _map[x, y] = 방타입.비어있음;
            }
        }
    }
    #endregion

    #region Room Generation Methods
    /// <summary>
    /// 모든 방들을 생성합니다
    /// </summary>
    private void GenerateRooms()
    {
        // 생성할 방 목록 정의
        List<방타입> roomsToPlace = new List<방타입>
        {
            방타입.소형사무실,
            방타입.대형사무실,
            방타입.화장실,
            방타입.회의실,
            방타입.계단,
            방타입.엘레베이터,
            방타입.창고
        };

        foreach (방타입 roomType in roomsToPlace)
        {
            if (_roomPrefabDict.ContainsKey(roomType))
            {
                RoomPrefabData prefabData = _roomPrefabDict[roomType];
                PlaceRoomWithRetry(roomType, prefabData.width, prefabData.height);
            }
        }
    }

    /// <summary>
    /// 재시도 로직을 포함하여 방을 배치합니다
    /// </summary>
    private void PlaceRoomWithRetry(방타입 roomType, int width, int height)
    {
        int attempts = 0;
        bool placed = false;

        while (!placed && attempts < MAX_ROOM_PLACE_ATTEMPTS)
        {
            int x = AlignToGrid(Random.Range(0, MAP_SIZE - width));
            int y = AlignToGrid(Random.Range(0, MAP_SIZE - height));

            if (CanPlaceRoom(x, y, width, height))
            {
                PlaceRoom(roomType, x, y, width, height);
                placed = true;
                Debug.Log($"{roomType} 배치 완료: ({x}, {y})");
            }

            attempts++;
        }

        if (!placed)
        {
            Debug.LogWarning($"{roomType} 배치 실패. 최대 시도 횟수 초과.");
        }
    }

    /// <summary>
    /// 외부 문들을 생성합니다
    /// </summary>
    private void GenerateExternalDoors()
    {
        string[] sides = { "top", "right", "bottom", "left" };
        List<string> selectedSides = new List<string>();

        // 2개의 서로 다른 방향 선택
        while (selectedSides.Count < 2)
        {
            string side = sides[Random.Range(0, sides.Length)];
            if (!selectedSides.Contains(side))
            {
                selectedSides.Add(side);
            }
        }

        foreach (string side in selectedSides)
        {
            PlaceExternalDoor(side);
        }
    }

    /// <summary>
    /// 지정된 방향에 외부 문을 배치합니다
    /// </summary>
    private void PlaceExternalDoor(string side)
    {
        int attempts = 0;
        bool placed = false;

        while (!placed && attempts < MAX_DOOR_PLACE_ATTEMPTS)
        {
            int x, y, width, height;

            switch (side)
            {
                case "top":
                    x = AlignToGrid(Random.Range(0, MAP_SIZE - 10));
                    y = 0;
                    width = 10;
                    height = 5;
                    break;
                case "bottom":
                    x = AlignToGrid(Random.Range(0, MAP_SIZE - 10));
                    y = MAP_SIZE - 5;
                    width = 10;
                    height = 5;
                    break;
                case "left":
                    x = 0;
                    y = AlignToGrid(Random.Range(0, MAP_SIZE - 10));
                    width = 5;
                    height = 10;
                    break;
                case "right":
                    x = MAP_SIZE - 5;
                    y = AlignToGrid(Random.Range(0, MAP_SIZE - 10));
                    width = 5;
                    height = 10;
                    break;
                default:
                    Debug.LogError($"알 수 없는 방향: {side}");
                    return;
            }

            if (CanPlaceRoom(x, y, width, height))
            {
                // 복도로 배치
                for (int i = x; i < x + width; i++)
                {
                    for (int j = y; j < y + height; j++)
                    {
                        if (i >= 0 && i < MAP_SIZE && j >= 0 && j < MAP_SIZE)
                        {
                            _map[i, j] = 방타입.복도;
                        }
                    }
                }

                RoomData externalDoor = new RoomData(방타입.외부문, x, y, width, height);
                externalDoor.side = side;
                _rooms.Add(externalDoor);

                placed = true;
                Debug.Log($"외부문 생성 완료: {side} 방향 ({x}, {y})");
            }

            attempts++;
        }

        if (!placed)
        {
            Debug.LogWarning($"외부문 {side} 배치 실패. 최대 시도 횟수 초과.");
        }
    }
    #endregion

    #region Corridor Generation Methods
    /// <summary>
    /// 모든 복도를 생성합니다
    /// </summary>
    private void GenerateCorridors()
    {
        HashSet<RoomData> connectedRooms = new HashSet<RoomData>();
        int corridorId = 1;

        while (connectedRooms.Count < _rooms.Count)
        {
            List<RoomData> unconnectedRooms = GetUnconnectedRooms(connectedRooms);

            if (unconnectedRooms.Count == 0)
            {
                break;
            }

            List<RoomData> roomsToConnect = SelectRoomsForCorridor(unconnectedRooms, connectedRooms);

            if (roomsToConnect.Count >= 2)
            {
                CreateMultiRoomCorridor(roomsToConnect, corridorId);

                for (int i = 0; i < roomsToConnect.Count; i++)
                {
                    RoomData room = roomsToConnect[i];
                    connectedRooms.Add(room);
                    room.connections++;
                    roomsToConnect[i] = room;
                }

                corridorId++;
            }
            else if (unconnectedRooms.Count == 1 && connectedRooms.Count > 0)
            {
                ConnectLastRoom(unconnectedRooms[0], connectedRooms);
                connectedRooms.Add(unconnectedRooms[0]);
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// 연결되지 않은 방들을 반환합니다
    /// </summary>
    private List<RoomData> GetUnconnectedRooms(HashSet<RoomData> connectedRooms)
    {
        List<RoomData> unconnectedRooms = new List<RoomData>();

        foreach (RoomData room in _rooms)
        {
            if (!connectedRooms.Contains(room))
            {
                unconnectedRooms.Add(room);
            }
        }

        return unconnectedRooms;
    }

    /// <summary>
    /// 복도에 연결할 방들을 선택합니다
    /// </summary>
    private List<RoomData> SelectRoomsForCorridor(List<RoomData> unconnectedRooms, HashSet<RoomData> connectedRooms)
    {
        List<RoomData> roomsToConnect = new List<RoomData>();

        // 연결할 방 개수 결정 (2-4개)
        int numRooms = Mathf.Min(4, Mathf.Max(2, Random.Range(2, 5)));

        if (connectedRooms.Count == 0)
        {
            // 첫 번째 복도: 연결되지 않은 방들만 선택
            for (int i = 0; i < Mathf.Min(numRooms, unconnectedRooms.Count); i++)
            {
                roomsToConnect.Add(unconnectedRooms[i]);
            }
        }
        else
        {
            // 연결되지 않은 방 1개 이상 + 연결된 방들도 포함 가능
            int unconnectedCount = Mathf.CeilToInt(numRooms / 2f); // 최소 절반은 연결되지 않은 방

            // 연결되지 않은 방들 추가
            for (int i = 0; i < Mathf.Min(unconnectedCount, unconnectedRooms.Count); i++)
            {
                roomsToConnect.Add(unconnectedRooms[i]);
            }

            // 남은 자리에 연결된 방들도 추가 (단, 추가 문 생성 가능한 방만)
            if (roomsToConnect.Count < numRooms && connectedRooms.Count > 0)
            {
                List<RoomData> connectedArray = new List<RoomData>(connectedRooms);
                int remainingSlots = numRooms - roomsToConnect.Count;

                // 추가 문을 만들 수 있는 연결된 방들만 필터링
                List<RoomData> availableConnectedRooms = connectedArray.FindAll(room =>
                {
                    int maxDoors = GetMaxDoorsForRoom(room.type);
                    return room.doorCount < maxDoors;
                });

                if (availableConnectedRooms.Count > 0 && roomsToConnect.Count > 0)
                {
                    RoomData baseRoom = roomsToConnect[0];
                    availableConnectedRooms.Sort((a, b) => CalculateDistance(baseRoom, a).CompareTo(CalculateDistance(baseRoom, b)));

                    for (int i = 0; i < Mathf.Min(remainingSlots, availableConnectedRooms.Count); i++)
                    {
                        if (Random.Range(0f, 1f) > 0.3f) // 70% 확률로 연결된 방도 포함
                        {
                            roomsToConnect.Add(availableConnectedRooms[i]);
                        }
                    }
                }
            }
        }

        return roomsToConnect;
    }

    /// <summary>
    /// 다중 방 복도를 생성합니다
    /// </summary>
    private void CreateMultiRoomCorridor(List<RoomData> roomsToConnect, int corridorId)
    {
        if (roomsToConnect.Count == 2)
        {
            CreateDirectCorridor(roomsToConnect[0], roomsToConnect[1]);
        }
        else if (roomsToConnect.Count == 3)
        {
            CreateTShapedCorridor(roomsToConnect);
        }
        else if (roomsToConnect.Count >= 4)
        {
            CreateCrossShapedCorridor(roomsToConnect);
        }
    }

    /// <summary>
    /// 직선 복도를 생성합니다
    /// </summary>
    private void CreateDirectCorridor(RoomData room1, RoomData room2)
    {
        List<WallSegment> segments1 = GetWallSegments(room1);
        List<WallSegment> segments2 = GetWallSegments(room2);

        WallSegment bestSeg1 = new WallSegment();
        WallSegment bestSeg2 = new WallSegment();
        float minDistance = float.MaxValue;
        bool foundBestSegments = false;

        // 가장 가까운 벽면 구간들 찾기
        foreach (WallSegment seg1 in segments1)
        {
            foreach (WallSegment seg2 in segments2)
            {
                float dist = Mathf.Abs(seg1.x + 2.5f - seg2.x - 2.5f) +
                           Mathf.Abs(seg1.y + 2.5f - seg2.y - 2.5f);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestSeg1 = seg1;
                    bestSeg2 = seg2;
                    foundBestSegments = true;
                }
            }
        }

        if (foundBestSegments)
        {
            // 문 생성
            CreateDoor(room1, bestSeg1);
            CreateDoor(room2, bestSeg2);

            // L자형 복도로 연결
            CreateLShapedCorridor(bestSeg1, bestSeg2);

            room1.connections++;
            room2.connections++;
        }
    }

    /// <summary>
    /// T자형 복도를 생성합니다
    /// </summary>
    private void CreateTShapedCorridor(List<RoomData> rooms)
    {
        // 중앙 허브 위치 계산
        float centerX = 0f;
        float centerY = 0f;

        foreach (RoomData room in rooms)
        {
            centerX += room.x + room.width * 0.5f;
            centerY += room.y + room.height * 0.5f;
        }

        centerX /= rooms.Count;
        centerY /= rooms.Count;

        int hubX = AlignToGrid((int)centerX);
        int hubY = AlignToGrid((int)centerY);

        // 안전한 중앙 위치 찾기
        Vector2Int adjustedHub = FindSafeCenterPosition(hubX, hubY, 10, 10);

        // 10x10 중앙 허브 생성
        CreateCorridorSegment(adjustedHub.x, adjustedHub.y, 10, 10);

        // 각 방을 허브에 연결
        Vector2 hubCenter = new Vector2(adjustedHub.x + 5, adjustedHub.y + 5);
        foreach (RoomData room in rooms)
        {
            ConnectRoomToHub(room, hubCenter);
        }

        Debug.Log($"T자형 복도 생성: 중심 ({hubCenter.x}, {hubCenter.y})");
    }

    /// <summary>
    /// 십자형 복도를 생성합니다
    /// </summary>
    private void CreateCrossShapedCorridor(List<RoomData> rooms)
    {
        // 중앙 허브 위치 계산
        float centerX = 0f;
        float centerY = 0f;

        foreach (RoomData room in rooms)
        {
            centerX += room.x + room.width * 0.5f;
            centerY += room.y + room.height * 0.5f;
        }

        centerX /= rooms.Count;
        centerY /= rooms.Count;

        int hubX = AlignToGrid((int)centerX);
        int hubY = AlignToGrid((int)centerY);

        // 안전한 중앙 위치 찾기
        Vector2Int adjustedHub = FindSafeCenterPosition(hubX, hubY, 15, 15);

        // 15x15 중앙 허브 생성
        CreateCorridorSegment(adjustedHub.x, adjustedHub.y, 15, 15);

        // 각 방을 허브에 연결
        Vector2 hubCenter = new Vector2(adjustedHub.x + 7.5f, adjustedHub.y + 7.5f);
        foreach (RoomData room in rooms)
        {
            ConnectRoomToHub(room, hubCenter);
        }

        Debug.Log($"십자형 복도 생성: 중심 ({hubCenter.x}, {hubCenter.y})");
    }

    /// <summary>
    /// 마지막 남은 방을 연결합니다
    /// </summary>
    private void ConnectLastRoom(RoomData lastRoom, HashSet<RoomData> connectedRooms)
    {
        List<RoomData> connectedArray = new List<RoomData>(connectedRooms);

        // 가장 가까운 연결된 방 찾기
        connectedArray.Sort((a, b) => CalculateDistance(lastRoom, a).CompareTo(CalculateDistance(lastRoom, b)));
        RoomData closestRoom = connectedArray[0];

        Debug.Log($"마지막 방 연결: {lastRoom.type} ↔ {closestRoom.type}");
        CreateDirectCorridor(lastRoom, closestRoom);
    }
    #endregion

    #region Door Management Methods
    /// <summary>
    /// 연결된 문들을 비활성화합니다
    /// </summary>
    [ClientRpc]
    private void RpcDisableConnectedDoors()
    {
        // TODO: 복도와 연결된 위치의 문들을 비활성화
        // - 각 방의 문 위치 확인
        // - 복도와 겹치는 문 찾기
        // - 해당 문 오브젝트 비활성화
    }

    /// <summary>
    /// 방의 벽면 세그먼트들을 반환합니다
    /// </summary>
    private List<WallSegment> GetWallSegments(RoomData room)
    {
        List<WallSegment> segments = new List<WallSegment>();

        // 상단 벽
        for (int i = 0; i < room.width / 5; i++)
        {
            WallSegment segment = new WallSegment();
            segment.side = "top";
            segment.x = room.x + i * 5;
            segment.y = room.y - 5;
            segment.doorX = room.x + i * 5;
            segment.doorY = room.y - 1;
            segment.roomSide = "top";
            segments.Add(segment);
        }

        // 하단 벽
        for (int i = 0; i < room.width / 5; i++)
        {
            WallSegment segment = new WallSegment();
            segment.side = "bottom";
            segment.x = room.x + i * 5;
            segment.y = room.y + room.height;
            segment.doorX = room.x + i * 5;
            segment.doorY = room.y + room.height;
            segment.roomSide = "bottom";
            segments.Add(segment);
        }

        // 좌측 벽
        for (int i = 0; i < room.height / 5; i++)
        {
            WallSegment segment = new WallSegment();
            segment.side = "left";
            segment.x = room.x - 5;
            segment.y = room.y + i * 5;
            segment.doorX = room.x - 1;
            segment.doorY = room.y + i * 5;
            segment.roomSide = "left";
            segments.Add(segment);
        }

        // 우측 벽
        for (int i = 0; i < room.height / 5; i++)
        {
            WallSegment segment = new WallSegment();
            segment.side = "right";
            segment.x = room.x + room.width;
            segment.y = room.y + i * 5;
            segment.doorX = room.x + room.width;
            segment.doorY = room.y + i * 5;
            segment.roomSide = "right";
            segments.Add(segment);
        }

        return segments;
    }

    /// <summary>
    /// 지정된 위치에 문을 생성합니다
    /// </summary>
    private bool CreateDoor(RoomData room, WallSegment segment)
    {
        if (room.type == 방타입.외부문)
        {
            return true; // 외부문은 항상 성공
        }

        // 특수 방 문 개수 제한 확인
        int maxDoors = GetMaxDoorsForRoom(room.type);

        if (room.doorCount >= maxDoors)
        {
            Debug.Log($"{room.type} 문 개수 제한 도달 ({room.doorCount}/{maxDoors})");
            return false; // 문 개수 초과
        }

        // 이미 복도와 연결되어 있으면 추가 문 생성하지 않음 (특수방만)
        if (room.connections > 0 && GetMaxDoorsForRoom(room.type) == 1)
        {
            Debug.Log($"{room.type} 이미 연결되어 있음, 추가 문 생성 안함");
            return false;
        }

        if (segment.roomSide == "top" || segment.roomSide == "bottom")
        {
            // 수평 문 (길이 5)
            for (int i = 0; i < 5; i++)
            {
                int x = segment.doorX + i;
                int y = segment.doorY;
                if (x >= 0 && x < MAP_SIZE && y >= 0 && y < MAP_SIZE)
                {
                    _map[x, y] = 방타입.문;
                }
            }
        }
        else
        {
            // 수직 문 (길이 5)
            for (int i = 0; i < 5; i++)
            {
                int x = segment.doorX;
                int y = segment.doorY + i;
                if (x >= 0 && x < MAP_SIZE && y >= 0 && y < MAP_SIZE)
                {
                    _map[x, y] = 방타입.문;
                }
            }
        }

        room.doorCount++;
        Debug.Log($"{room.type} 문 생성 완료 ({room.doorCount}/{maxDoors})");
        return true;
    }
    #endregion

    #region Prefab Spawning Methods
    /// <summary>
    /// 모든 방 프리팹들을 생성합니다
    /// </summary>
    [Server]
    private void SpawnRoomPrefabs()
    {
        foreach (RoomData room in _rooms)
        {
            RpcSpawnRoomPrefab(room);
        }

        RpcSpawnCorridorPrefabs();
        RpcSpawnDoorPrefabs();
    }

    /// <summary>
    /// 개별 방 프리팹을 생성합니다
    /// </summary>
    [ClientRpc]
    private void RpcSpawnRoomPrefab(RoomData room)
    {
        if (_roomPrefabDict.ContainsKey(room.type))
        {
            RoomPrefabData prefabData = _roomPrefabDict[room.type];
            Vector3 position = new Vector3(room.x, room.y, 0);

            GameObject roomObj = Instantiate(prefabData.prefab, position, Quaternion.identity, roomParent);
            roomObj.name = $"{room.type}_{room.x}_{room.y}";

            _generatedObjects.Add(roomObj);
        }
    }

    /// <summary>
    /// 복도 프리팹들을 생성합니다
    /// </summary>
    [ClientRpc]
    private void RpcSpawnCorridorPrefabs()
    {
        if (corridorPrefab == null)
        {
            return;
        }

        for (int x = 0; x < MAP_SIZE; x++)
        {
            for (int y = 0; y < MAP_SIZE; y++)
            {
                if (_map[x, y] == 방타입.복도)
                {
                    Vector3 position = new Vector3(x, y, 0);
                    GameObject corridorObj = Instantiate(corridorPrefab, position, Quaternion.identity, corridorParent);
                    corridorObj.name = $"Corridor_{x}_{y}";

                    _generatedObjects.Add(corridorObj);
                }
            }
        }
    }

    /// <summary>
    /// 문 프리팹들을 생성합니다
    /// </summary>
    [ClientRpc]
    private void RpcSpawnDoorPrefabs()
    {
        if (doorPrefab == null)
        {
            return;
        }

        for (int x = 0; x < MAP_SIZE; x++)
        {
            for (int y = 0; y < MAP_SIZE; y++)
            {
                if (_map[x, y] == 방타입.문)
                {
                    Vector3 position = new Vector3(x, y, 0);
                    GameObject doorObj = Instantiate(doorPrefab, position, Quaternion.identity, doorParent);
                    doorObj.name = $"Door_{x}_{y}";

                    _generatedObjects.Add(doorObj);
                }
                else if (_map[x, y] == 방타입.외부문)
                {
                    Vector3 position = new Vector3(x, y, 0);
                    GameObject externalDoorObj = Instantiate(externalDoorPrefab, position, Quaternion.identity, doorParent);
                    externalDoorObj.name = $"ExternalDoor_{x}_{y}";

                    _generatedObjects.Add(externalDoorObj);
                }
            }
        }
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// 안전한 중앙 위치를 찾습니다
    /// </summary>
    private Vector2Int FindSafeCenterPosition(int idealX, int idealY, int width, int height)
    {
        int bestX = idealX;
        int bestY = idealY;

        for (int attempts = 0; attempts < MAX_CENTER_FIND_ATTEMPTS; attempts++)
        {
            bool canPlace = true;

            for (int i = bestX - 5; i < bestX + width + 5; i++)
            {
                for (int j = bestY - 5; j < bestY + height + 5; j++)
                {
                    if (i >= 0 && i < MAP_SIZE && j >= 0 && j < MAP_SIZE)
                    {
                        방타입 cellType = _map[i, j];
                        if (cellType != 방타입.비어있음 && cellType != 방타입.복도)
                        {
                            canPlace = false;
                            break;
                        }
                    }
                }
                if (!canPlace) break;
            }

            if (canPlace) break;

            // 위치 조정
            bestX = AlignToGrid(idealX + Random.Range(-15, 16));
            bestY = AlignToGrid(idealY + Random.Range(-15, 16));
            bestX = Mathf.Max(5, Mathf.Min(MAP_SIZE - width - 5, bestX));
            bestY = Mathf.Max(5, Mathf.Min(MAP_SIZE - height - 5, bestY));
        }

        return new Vector2Int(bestX, bestY);
    }

    /// <summary>
    /// 복도 구간을 생성합니다
    /// </summary>
    private void CreateCorridorSegment(int x, int y, int width, int height)
    {
        for (int i = x; i < x + width; i++)
        {
            for (int j = y; j < y + height; j++)
            {
                if (i >= 0 && i < MAP_SIZE && j >= 0 && j < MAP_SIZE)
                {
                    방타입 cellType = _map[i, j];
                    if (cellType == 방타입.비어있음 || cellType == 방타입.복도)
                    {
                        _map[i, j] = 방타입.복도;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 방을 허브에 연결합니다
    /// </summary>
    private void ConnectRoomToHub(RoomData room, Vector2 hubCenter)
    {
        List<WallSegment> segments = GetWallSegments(room);

        // 허브 중심에 가장 가까운 벽면 구간 선택
        WallSegment bestSegment = default;
        float minDistance = float.MaxValue;
        bool foundBestSegment = false;

        foreach (WallSegment segment in segments)
        {
            float dist = Mathf.Abs(segment.x + 2.5f - hubCenter.x) + Mathf.Abs(segment.y + 2.5f - hubCenter.y);
            if (dist < minDistance)
            {
                minDistance = dist;
                bestSegment = segment;
                foundBestSegment = true;
            }
        }

        if (foundBestSegment)
        {
            // 문 생성
            CreateDoor(room, bestSegment);

            // 방에서 허브까지 직선 복도 생성
            CreateStraightCorridorToHub(bestSegment, hubCenter);
        }
    }

    /// <summary>
    /// 세그먼트에서 허브까지 직선 복도를 생성합니다
    /// </summary>
    private void CreateStraightCorridorToHub(WallSegment segment, Vector2 hubCenter)
    {
        int segX = segment.x + 2;
        int segY = segment.y + 2;
        int hubX = (int)hubCenter.x;
        int hubY = (int)hubCenter.y;

        if (segment.roomSide == "top" || segment.roomSide == "bottom")
        {
            // 세로 방향 연결
            int startY = segment.roomSide == "top" ? segment.y : segment.y + 1;
            int endY = hubY;
            int minY = Mathf.Min(startY, endY);
            int maxY = Mathf.Max(startY, endY);

            CreateCorridorSegment(segX - 2, minY, 5, maxY - minY + 1);

            // 허브까지 수평 연결
            int minX = Mathf.Min(segX, hubX);
            int maxX = Mathf.Max(segX, hubX);
            CreateCorridorSegment(minX - 2, hubY - 2, maxX - minX + 5, 5);
        }
        else
        {
            // 가로 방향 연결
            int startX = segment.roomSide == "left" ? segment.x : segment.x + 1;
            int endX = hubX;
            int minX = Mathf.Min(startX, endX);
            int maxX = Mathf.Max(startX, endX);

            CreateCorridorSegment(minX, segY - 2, maxX - minX + 1, 5);

            // 허브까지 수직 연결
            int minY = Mathf.Min(segY, hubY);
            int maxY = Mathf.Max(segY, hubY);
            CreateCorridorSegment(hubX - 2, minY - 2, 5, maxY - minY + 5);
        }
    }

    /// <summary>
    /// L자형 복도를 생성합니다
    /// </summary>
    private void CreateLShapedCorridor(WallSegment seg1, WallSegment seg2)
    {
        int x1 = seg1.x;
        int y1 = seg1.y;
        int x2 = seg2.x;
        int y2 = seg2.y;

        if (Random.Range(0f, 1f) > 0.5f)
        {
            // 수평 먼저, 그 다음 수직
            int minX = Mathf.Min(x1, x2);
            int maxX = Mathf.Max(x1, x2);
            int corridorY = AlignToGrid(y1);

            // 수평 복도 (폭 5)
            CreateCorridorSegment(minX, corridorY, maxX - minX + 5, 5);

            // 수직 복도 (폭 5)
            int minY = Mathf.Min(y1, y2);
            int maxY = Mathf.Max(y1, y2);
            int corridorX = AlignToGrid(x2);
            CreateCorridorSegment(corridorX, minY, 5, maxY - minY + 5);
        }
        else
        {
            // 수직 먼저, 그 다음 수평
            int minY = Mathf.Min(y1, y2);
            int maxY = Mathf.Max(y1, y2);
            int corridorX = AlignToGrid(x1);

            // 수직 복도 (폭 5)
            CreateCorridorSegment(corridorX, minY, 5, maxY - minY + 5);

            // 수평 복도 (폭 5)
            int minX = Mathf.Min(x1, x2);
            int maxX = Mathf.Max(x1, x2);
            int corridorY = AlignToGrid(y2);
            CreateCorridorSegment(minX, corridorY, maxX - minX + 5, 5);
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 값을 격자 단위로 정렬합니다
    /// </summary>
    private int AlignToGrid(int value)
    {
        return (value / GRID_UNIT) * GRID_UNIT;
    }

    /// <summary>
    /// 지정된 위치에 방을 배치할 수 있는지 확인합니다
    /// </summary>
    private bool CanPlaceRoom(int x, int y, int width, int height)
    {
        if (x + width > MAP_SIZE || y + height > MAP_SIZE)
        {
            return false;
        }

        int minX = Mathf.Max(0, x - GRID_UNIT);
        int maxX = Mathf.Min(MAP_SIZE, x + width + GRID_UNIT);
        int minY = Mathf.Max(0, y - GRID_UNIT);
        int maxY = Mathf.Min(MAP_SIZE, y + height + GRID_UNIT);

        for (int i = minX; i < maxX; i++)
        {
            for (int j = minY; j < maxY; j++)
            {
                if (_map[i, j] != 방타입.비어있음)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 지정된 위치에 방을 배치합니다
    /// </summary>
    private void PlaceRoom(방타입 roomType, int x, int y, int width, int height)
    {
        for (int i = x; i < x + width; i++)
        {
            for (int j = y; j < y + height; j++)
            {
                _map[i, j] = roomType;
            }
        }

        RoomData newRoom = new RoomData(roomType, x, y, width, height);
        _rooms.Add(newRoom);
    }

    /// <summary>
    /// 두 방 사이의 거리를 계산합니다
    /// </summary>
    private float CalculateDistance(RoomData room1, RoomData room2)
    {
        float centerX1 = room1.x + room1.width * 0.5f;
        float centerY1 = room1.y + room1.height * 0.5f;
        float centerX2 = room2.x + room2.width * 0.5f;
        float centerY2 = room2.y + room2.height * 0.5f;

        return Mathf.Abs(centerX1 - centerX2) + Mathf.Abs(centerY1 - centerY2);
    }

    /// <summary>
    /// 방의 최대 문 개수를 반환합니다
    /// </summary>
    private int GetMaxDoorsForRoom(방타입 roomType)
    {
        switch (roomType)
        {
            case 방타입.화장실:
            case 방타입.엘레베이터:
            case 방타입.계단:
                return 1;
            default:
                return int.MaxValue;
        }
    }

    /// <summary>
    /// 생성된 모든 오브젝트를 제거합니다
    /// </summary>
    [Server]
    private void ClearAllGeneratedObjects()
    {
        if (_generatedObjects != null)
        {
            foreach (GameObject obj in _generatedObjects)
            {
                if (obj != null)
                {
                    NetworkServer.Destroy(obj);
                }
            }
            _generatedObjects.Clear();
        }

        // 부모 오브젝트들의 자식도 정리
        ClearParentChildren();
    }

    /// <summary>
    /// 부모 오브젝트들의 남은 자식들을 정리합니다
    /// </summary>
    private void ClearParentChildren()
    {
        if (roomParent != null)
        {
            ClearChildren(roomParent);
        }

        if (corridorParent != null)
        {
            ClearChildren(corridorParent);
        }

        if (doorParent != null)
        {
            ClearChildren(doorParent);
        }
    }

    /// <summary>
    /// 지정된 Transform의 모든 자식을 제거합니다
    /// </summary>
    private void ClearChildren(Transform parent)
    {
        int childCount = parent.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
            else
            {
                DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }
    }
    #endregion

    #region Debug Methods
    /// <summary>
    /// 맵 상태를 콘솔에 출력합니다
    /// </summary>
    [ContextMenu("맵 상태 출력")]
    private void PrintMapStatus()
    {
        if (_rooms == null)
        {
            Debug.Log("맵이 생성되지 않았습니다.");
            return;
        }

        Debug.Log($"총 방 개수: {_rooms.Count}");

        foreach (RoomData room in _rooms)
        {
            Debug.Log($"{room.type}: 위치({room.x}, {room.y}), 크기({room.width}x{room.height}), 연결수: {room.connections}, 문 개수: {room.doorCount}");
        }
    }
    #endregion
}