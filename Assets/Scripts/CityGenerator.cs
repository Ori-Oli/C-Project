using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum TileType
{
    Empty,
    Road,
    Building
}

[System.Serializable]
public class CityCell
{
    public int x;
    public int y;
    public TileType tileType;
    public bool walkable;
    public int moveCost;
    public bool hasTrashCan;
    public GameObject spawnedObject;
    public int buildingFootprintId;
    public bool isBuildingAnchor;
    public int buildingWidth;
    public int buildingHeight;

    public CityCell(int x, int y)
    {
        this.x = x;
        this.y = y;
        tileType = TileType.Empty;
        walkable = false;
        moveCost = 0;
        hasTrashCan = false;
        spawnedObject = null;
        buildingFootprintId = -1;
        isBuildingAnchor = false;
        buildingWidth = 0;
        buildingHeight = 0;
    }
}

public class RoadConnection
{
    public bool up;
    public bool down;
    public bool left;
    public bool right;

    public int Count()
    {
        int count = 0;
        if (up)
        {
            count++;
        }

        if (down)
        {
            count++;
        }

        if (left)
        {
            count++;
        }

        if (right)
        {
            count++;
        }

        return count;
    }
}

public class CityGenerator : MonoBehaviour
{
    [Header("Grid Settings")]
    [Min(1)] public int width = 50;
    [Min(1)] public int height = 50;
    [Tooltip("World-space spacing between grid cells. Set this to the actual X/Z size of one tile prefab.")]
    [Min(0.01f)] public float tileSize = 1f;

    [Header("Prefabs")]
    public GameObject roadPrefab;
    public GameObject cornerRoadPrefab;
    public GameObject tJunctionRoadPrefab;
    public GameObject intersectionRoadPrefab;
    public GameObject buildingPrefab;
    public GameObject emptyGroundPrefab;
    public GameObject trashCanPrefab;

    [Header("Generation Settings")]
    public bool useMapFile = true;
    public TextAsset mapTextAsset;
    public string mapFilePath = "Assets/Scripts/50x50.map";
    public bool syncGridSizeToMap = true;
    public bool mapFirstLineIsTop = true;
    public bool populateBuildingsOnMapEmptyCells = false;
    public bool useIrregularRoadNetwork = true;
    [Min(1)] public int roadIntervalX = 5;
    [Min(1)] public int roadIntervalY = 5;
    [Min(1)] public int minBlockSize = 4;
    [Min(1)] public int maxBlockSize = 10;
    public bool useVariableBlockSize = true;

    [Header("Irregular Road Network")]
    [Min(0)] public int mainVerticalRoadCount = 3;
    [Min(0)] public int mainHorizontalRoadCount = 3;
    [Min(0)] public int partialRoadSegmentCount = 8;
    [Min(1)] public int minRoadSpacing = 4;
    [Min(1)] public int minPartialRoadLength = 4;
    [Min(1)] public int maxPartialRoadLength = 15;
    public bool forceBorderRoads = true;

    [Header("Building Layout")]
    [Min(1)] public int minBuildingWidth = 1;
    [Min(1)] public int maxBuildingWidth = 4;
    [Min(1)] public int minBuildingHeight = 1;
    [Min(1)] public int maxBuildingHeight = 3;
    [Range(0f, 1f)] public float buildingFillChance = 0.75f;
    [Range(0f, 1f)] public float emptySpaceChance = 0.25f;
    [Min(1)] public int maxPlacementAttemptsPerBlock = 50;
    public bool spawnBuildingsAsFootprints = true;
    public bool scaleBuildingFootprintsToArea = true;

    [Header("Prefab Scaling")]
    [Tooltip("Scales road/building prefabs down or up so their renderer footprint fits one grid tile.")]
    public bool fitTilePrefabsToTileSize = true;

    [Header("Trash Cans")]
    [Range(0f, 1f)] public float trashCanProbability = 0.1f;
    [Range(0f, 1f)] public float trashCanDensityMultiplier = 0.33f;
    public bool generateOnStart = true;
    public bool logGenerationDiagnostics = true;
    public bool enableRoadDebugLog = false;

    [Header("Collection Depot")]
    public bool spawnDepotMarker = true;
    public Vector3 depotMarkerScale = new Vector3(0.8f, 0.5f, 0.8f);
    public Color depotMarkerColor = new Color(0.05f, 0.55f, 1f, 1f);

    [Header("Gizmo Colors")]
    public Color roadGizmoColor = new Color(0.15f, 0.15f, 0.15f, 0.35f);
    public Color buildingGizmoColor = new Color(0.6f, 0.25f, 0.15f, 0.35f);
    public Color emptyGizmoColor = new Color(1f, 1f, 1f, 0.08f);
    public Color trashCanGizmoColor = new Color(0f, 0.8f, 0.25f, 0.75f);

    public CityCell[,] Grid => grid;
    public CityCell CollectionDepotCell => collectionDepotCell;
    public Vector2Int CollectionDepotGridPosition => collectionDepotCell != null
        ? new Vector2Int(collectionDepotCell.x, collectionDepotCell.y)
        : new Vector2Int(-1, -1);
    public event System.Action CityGenerated;

    private CityCell[,] grid;
    private CityCell collectionDepotCell;
    private Transform generatedRoot;
    private readonly List<int> verticalRoadXs = new List<int>();
    private readonly List<int> horizontalRoadYs = new List<int>();
    private readonly List<BuildingFootprint> buildingFootprints = new List<BuildingFootprint>();

    private class BuildingFootprint
    {
        public int id;
        public int startX;
        public int startY;
        public int width;
        public int height;
    }

    private enum BuildingShapeBias
    {
        Balanced,
        Wide,
        Tall
    }

    private static readonly Vector2Int[] NeighborDirections =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateCity();
        }
    }

    public void GenerateCity()
    {
        ClearCity();
        ClampGenerationSettings();
        if (useMapFile && TryLoadMapLines(out List<string> mapLines))
        {
            InitializeGridFromMap(mapLines);
            if (populateBuildingsOnMapEmptyCells)
            {
                PopulateBuildingBlocks();
            }
        }
        else
        {
            InitializeGrid();
            if (useIrregularRoadNetwork)
            {
                CreateIrregularRoadNetwork();
            }
            else
            {
                CreateRoadCoordinateLists();
                CreateRoadsFromFullLines();
            }

            PopulateBuildingBlocks();
        }

        PlaceTrashCans();
        LogGenerationDiagnostics();
        SpawnPrefabsFromGrid();
        CityGenerated?.Invoke();
    }

    public void ClearCity()
    {
        if (generatedRoot != null)
        {
            DestroyGeneratedRoot();
        }

        grid = null;
        collectionDepotCell = null;
        verticalRoadXs.Clear();
        horizontalRoadYs.Clear();
        buildingFootprints.Clear();
    }

    public bool IsInsideGrid(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public CityCell GetCell(int x, int y)
    {
        if (grid == null)
        {
            Debug.LogWarning("City grid has not been generated yet.");
            return null;
        }

        if (!IsInsideGrid(x, y))
        {
            Debug.LogWarning($"Grid position is outside the city: ({x}, {y})");
            return null;
        }

        return grid[x, y];
    }

    public List<CityCell> GetNeighbors(CityCell cell)
    {
        List<CityCell> neighbors = new List<CityCell>();

        if (cell == null || grid == null)
        {
            return neighbors;
        }

        foreach (Vector2Int direction in NeighborDirections)
        {
            int nextX = cell.x + direction.x;
            int nextY = cell.y + direction.y;

            if (!IsInsideGrid(nextX, nextY))
            {
                continue;
            }

            CityCell neighbor = grid[nextX, nextY];
            if (neighbor.walkable)
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    private void InitializeGrid()
    {
        grid = new CityCell[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = new CityCell(x, y);
            }
        }
    }

    private bool TryLoadMapLines(out List<string> mapLines)
    {
        mapLines = new List<string>();

        if (mapTextAsset != null)
        {
            ReadMapLinesFromText(mapTextAsset.text, mapLines);
        }
        else if (!string.IsNullOrWhiteSpace(mapFilePath))
        {
            string fullPath = Path.IsPathRooted(mapFilePath)
                ? mapFilePath
                : Path.Combine(Application.dataPath, "..", mapFilePath);

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"Map file was not found: {fullPath}. Falling back to procedural generation.");
                return false;
            }

            ReadMapLinesFromText(File.ReadAllText(fullPath), mapLines);
        }

        if (mapLines.Count == 0)
        {
            Debug.LogWarning("Map file is empty or not assigned. Falling back to procedural generation.");
            return false;
        }

        return true;
    }

    private void ReadMapLinesFromText(string text, List<string> mapLines)
    {
        string[] rawLines = text.Split('\n');
        foreach (string rawLine in rawLines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            mapLines.Add(line);
        }
    }

    private void InitializeGridFromMap(List<string> mapLines)
    {
        int mapHeight = mapLines.Count;
        int mapWidth = GetMaxMapLineLength(mapLines);

        if (syncGridSizeToMap)
        {
            width = mapWidth;
            height = mapHeight;
        }

        InitializeGrid();

        int parseWidth = Mathf.Min(width, mapWidth);
        int parseHeight = Mathf.Min(height, mapHeight);

        for (int lineIndex = 0; lineIndex < parseHeight; lineIndex++)
        {
            string line = mapLines[lineIndex];
            int y = mapFirstLineIsTop ? parseHeight - 1 - lineIndex : lineIndex;

            for (int x = 0; x < parseWidth; x++)
            {
                char value = x < line.Length ? line[x] : '0';
                ApplyMapValueToCell(x, y, value);
            }
        }

        Debug.Log($"Loaded map into grid: mapSize={mapWidth}x{mapHeight}, gridSize={width}x{height}, source={GetMapSourceName()}");
    }

    private int GetMaxMapLineLength(List<string> mapLines)
    {
        int maxLength = 0;
        foreach (string line in mapLines)
        {
            maxLength = Mathf.Max(maxLength, line.Length);
        }

        return maxLength;
    }

    private void ApplyMapValueToCell(int x, int y, char value)
    {
        if (!IsInsideGrid(x, y))
        {
            return;
        }

        if (value == '1')
        {
            SetRoadCell(x, y);
            return;
        }

        if (value == 'A')
        {
            SetRoadCell(x, y);
            collectionDepotCell = grid[x, y];
            return;
        }

        if (value != '0')
        {
            Debug.LogWarning($"Unsupported map value '{value}' at ({x}, {y}). Treating it as Empty.");
        }

        CityCell cell = grid[x, y];
        cell.tileType = TileType.Empty;
        cell.walkable = false;
        cell.moveCost = 0;
        cell.hasTrashCan = false;
    }

    private string GetMapSourceName()
    {
        if (mapTextAsset != null)
        {
            return mapTextAsset.name;
        }

        return string.IsNullOrWhiteSpace(mapFilePath) ? "None" : mapFilePath;
    }

    private void CreateRoadsFromFullLines()
    {
        HashSet<int> verticalRoadSet = new HashSet<int>(verticalRoadXs);
        HashSet<int> horizontalRoadSet = new HashSet<int>(horizontalRoadYs);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CityCell cell = grid[x, y];
                bool isRoad = verticalRoadSet.Contains(x) || horizontalRoadSet.Contains(y);

                if (isRoad)
                {
                    cell.tileType = TileType.Road;
                    cell.walkable = true;
                    cell.moveCost = 1;
                }
                else
                {
                    cell.tileType = TileType.Empty;
                    cell.walkable = false;
                    cell.moveCost = 0;
                }
            }
        }
    }

    private void CreateIrregularRoadNetwork()
    {
        if (forceBorderRoads)
        {
            CreateBorderRoads();
        }

        CreateMainRoads();
        CreatePartialRoadSegments();
    }

    private void CreateBorderRoads()
    {
        for (int x = 0; x < width; x++)
        {
            SetRoadCell(x, 0);
            SetRoadCell(x, height - 1);
        }

        for (int y = 0; y < height; y++)
        {
            SetRoadCell(0, y);
            SetRoadCell(width - 1, y);
        }
    }

    private void CreateMainRoads()
    {
        verticalRoadXs.Clear();
        horizontalRoadYs.Clear();

        CreateSpacedRoadCoordinates(verticalRoadXs, width, mainVerticalRoadCount);
        CreateSpacedRoadCoordinates(horizontalRoadYs, height, mainHorizontalRoadCount);

        foreach (int roadX in verticalRoadXs)
        {
            for (int y = 0; y < height; y++)
            {
                SetRoadCell(roadX, y);
            }
        }

        foreach (int roadY in horizontalRoadYs)
        {
            for (int x = 0; x < width; x++)
            {
                SetRoadCell(x, roadY);
            }
        }
    }

    private void CreateSpacedRoadCoordinates(List<int> roadCoordinates, int length, int requestedCount)
    {
        if (length <= 2 || requestedCount <= 0)
        {
            return;
        }

        int attempts = requestedCount * 20;
        while (roadCoordinates.Count < requestedCount && attempts > 0)
        {
            attempts--;
            int coordinate = Random.Range(1, length - 1);
            if (IsFarEnoughFromRoadCoordinates(coordinate, roadCoordinates, length))
            {
                roadCoordinates.Add(coordinate);
            }
        }

        roadCoordinates.Sort();
    }

    private bool IsFarEnoughFromRoadCoordinates(int coordinate, List<int> existingCoordinates, int length)
    {
        foreach (int existingCoordinate in existingCoordinates)
        {
            if (Mathf.Abs(coordinate - existingCoordinate) < minRoadSpacing)
            {
                return false;
            }
        }

        if (forceBorderRoads && (coordinate < minRoadSpacing || coordinate > length - 1 - minRoadSpacing))
        {
            return false;
        }

        return true;
    }

    private void CreatePartialRoadSegments()
    {
        int createdSegments = 0;
        int attempts = Mathf.Max(1, partialRoadSegmentCount * 20);

        while (createdSegments < partialRoadSegmentCount && attempts > 0)
        {
            attempts--;

            if (!TryGetRandomRoadCell(out Vector2Int anchor))
            {
                return;
            }

            bool horizontal = Random.value < 0.5f;
            int maxLength = horizontal ? width : height;
            int maxSegmentLength = Mathf.Max(1, Mathf.Min(maxPartialRoadLength, maxLength));
            int minSegmentLength = Mathf.Min(minPartialRoadLength, maxSegmentLength);
            int segmentLength = Random.Range(minSegmentLength, maxSegmentLength + 1);

            if (horizontal)
            {
                int startX = GetSegmentStartContainingAnchor(anchor.x, segmentLength, width);
                int endX = startX + segmentLength - 1;
                if (!HorizontalSegmentWouldAddRoad(anchor.y, startX, endX))
                {
                    continue;
                }

                DrawHorizontalRoadSegment(anchor.y, startX, endX);
            }
            else
            {
                int startY = GetSegmentStartContainingAnchor(anchor.y, segmentLength, height);
                int endY = startY + segmentLength - 1;
                if (!VerticalSegmentWouldAddRoad(anchor.x, startY, endY))
                {
                    continue;
                }

                DrawVerticalRoadSegment(anchor.x, startY, endY);
            }

            createdSegments++;
        }
    }

    private bool TryGetRandomRoadCell(out Vector2Int roadCell)
    {
        List<Vector2Int> roadCells = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].tileType == TileType.Road)
                {
                    roadCells.Add(new Vector2Int(x, y));
                }
            }
        }

        if (roadCells.Count == 0)
        {
            roadCell = Vector2Int.zero;
            return false;
        }

        roadCell = roadCells[Random.Range(0, roadCells.Count)];
        return true;
    }

    private int GetSegmentStartContainingAnchor(int anchorCoordinate, int segmentLength, int mapLength)
    {
        int offsetInsideSegment = Random.Range(0, segmentLength);
        int start = anchorCoordinate - offsetInsideSegment;
        return Mathf.Clamp(start, 0, mapLength - segmentLength);
    }

    private void DrawHorizontalRoadSegment(int y, int startX, int endX)
    {
        startX = Mathf.Clamp(startX, 0, width - 1);
        endX = Mathf.Clamp(endX, 0, width - 1);

        for (int x = Mathf.Min(startX, endX); x <= Mathf.Max(startX, endX); x++)
        {
            SetRoadCell(x, y);
        }
    }

    private bool HorizontalSegmentWouldAddRoad(int y, int startX, int endX)
    {
        startX = Mathf.Clamp(startX, 0, width - 1);
        endX = Mathf.Clamp(endX, 0, width - 1);

        for (int x = Mathf.Min(startX, endX); x <= Mathf.Max(startX, endX); x++)
        {
            if (grid[x, y].tileType != TileType.Road)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawVerticalRoadSegment(int x, int startY, int endY)
    {
        startY = Mathf.Clamp(startY, 0, height - 1);
        endY = Mathf.Clamp(endY, 0, height - 1);

        for (int y = Mathf.Min(startY, endY); y <= Mathf.Max(startY, endY); y++)
        {
            SetRoadCell(x, y);
        }
    }

    private bool VerticalSegmentWouldAddRoad(int x, int startY, int endY)
    {
        startY = Mathf.Clamp(startY, 0, height - 1);
        endY = Mathf.Clamp(endY, 0, height - 1);

        for (int y = Mathf.Min(startY, endY); y <= Mathf.Max(startY, endY); y++)
        {
            if (grid[x, y].tileType != TileType.Road)
            {
                return true;
            }
        }

        return false;
    }

    private void SetRoadCell(int x, int y)
    {
        if (!IsInsideGrid(x, y))
        {
            return;
        }

        CityCell cell = grid[x, y];
        cell.tileType = TileType.Road;
        cell.walkable = true;
        cell.moveCost = 1;
        cell.hasTrashCan = false;
        cell.buildingFootprintId = -1;
        cell.isBuildingAnchor = false;
        cell.buildingWidth = 0;
        cell.buildingHeight = 0;
    }

    private void PopulateBuildingBlocks()
    {
        if (useIrregularRoadNetwork)
        {
            PopulateIrregularBuildingRegions();
            return;
        }

        for (int xIndex = 0; xIndex < verticalRoadXs.Count - 1; xIndex++)
        {
            int blockMinX = verticalRoadXs[xIndex] + 1;
            int blockMaxX = verticalRoadXs[xIndex + 1] - 1;

            for (int yIndex = 0; yIndex < horizontalRoadYs.Count - 1; yIndex++)
            {
                int blockMinY = horizontalRoadYs[yIndex] + 1;
                int blockMaxY = horizontalRoadYs[yIndex + 1] - 1;

                if (blockMinX > blockMaxX || blockMinY > blockMaxY)
                {
                    continue;
                }

                PopulateSingleBlock(blockMinX, blockMaxX, blockMinY, blockMaxY);
            }
        }
    }

    private void PopulateIrregularBuildingRegions()
    {
        bool[,] visited = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (visited[x, y] || grid[x, y].tileType != TileType.Empty)
                {
                    continue;
                }

                PopulateSingleBlockFromEmptyRegion(x, y, visited);
            }
        }
    }

    private void PopulateSingleBlockFromEmptyRegion(int startX, int startY, bool[,] visited)
    {
        Queue<Vector2Int> open = new Queue<Vector2Int>();
        open.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        int minX = startX;
        int maxX = startX;
        int minY = startY;
        int maxY = startY;

        while (open.Count > 0)
        {
            Vector2Int current = open.Dequeue();
            minX = Mathf.Min(minX, current.x);
            maxX = Mathf.Max(maxX, current.x);
            minY = Mathf.Min(minY, current.y);
            maxY = Mathf.Max(maxY, current.y);

            foreach (Vector2Int direction in NeighborDirections)
            {
                int nextX = current.x + direction.x;
                int nextY = current.y + direction.y;

                if (!IsInsideGrid(nextX, nextY) || visited[nextX, nextY])
                {
                    continue;
                }

                if (grid[nextX, nextY].tileType != TileType.Empty)
                {
                    continue;
                }

                visited[nextX, nextY] = true;
                open.Enqueue(new Vector2Int(nextX, nextY));
            }
        }

        PopulateSingleBlock(minX, maxX, minY, maxY);
    }

    private void PopulateSingleBlock(int minX, int maxX, int minY, int maxY)
    {
        BuildingShapeBias shapeBias = GetRandomBuildingShapeBias();

        for (int attempt = 0; attempt < maxPlacementAttemptsPerBlock; attempt++)
        {
            if (Random.value > buildingFillChance || Random.value < emptySpaceChance)
            {
                continue;
            }

            Vector2Int buildingSize = GetRandomBuildingSize(shapeBias);
            int buildingWidth = Mathf.Min(buildingSize.x, maxX - minX + 1);
            int buildingHeight = Mathf.Min(buildingSize.y, maxY - minY + 1);

            int startX = Random.Range(minX, maxX - buildingWidth + 2);
            int startY = Random.Range(minY, maxY - buildingHeight + 2);

            if (!CanPlaceBuildingFootprint(startX, startY, buildingWidth, buildingHeight))
            {
                continue;
            }

            PlaceBuildingFootprint(startX, startY, buildingWidth, buildingHeight);
        }
    }

    private BuildingShapeBias GetRandomBuildingShapeBias()
    {
        float roll = Random.value;
        if (roll < 0.33f)
        {
            return BuildingShapeBias.Wide;
        }

        if (roll < 0.66f)
        {
            return BuildingShapeBias.Tall;
        }

        return BuildingShapeBias.Balanced;
    }

    private Vector2Int GetRandomBuildingSize(BuildingShapeBias shapeBias)
    {
        int widthRangeMax = maxBuildingWidth + 1;
        int heightRangeMax = maxBuildingHeight + 1;
        int buildingWidth = Random.Range(minBuildingWidth, widthRangeMax);
        int buildingHeight = Random.Range(minBuildingHeight, heightRangeMax);

        if (shapeBias == BuildingShapeBias.Wide && maxBuildingWidth > minBuildingWidth)
        {
            buildingWidth = Random.Range(Mathf.Max(minBuildingWidth, maxBuildingWidth / 2), widthRangeMax);
            buildingHeight = Random.Range(minBuildingHeight, Mathf.Max(minBuildingHeight, maxBuildingHeight / 2) + 1);
        }
        else if (shapeBias == BuildingShapeBias.Tall && maxBuildingHeight > minBuildingHeight)
        {
            buildingWidth = Random.Range(minBuildingWidth, Mathf.Max(minBuildingWidth, maxBuildingWidth / 2) + 1);
            buildingHeight = Random.Range(Mathf.Max(minBuildingHeight, maxBuildingHeight / 2), heightRangeMax);
        }

        return new Vector2Int(buildingWidth, buildingHeight);
    }

    private bool CanPlaceBuildingFootprint(int startX, int startY, int buildingWidth, int buildingHeight)
    {
        for (int x = startX; x < startX + buildingWidth; x++)
        {
            for (int y = startY; y < startY + buildingHeight; y++)
            {
                if (!IsInsideGrid(x, y) || grid[x, y].tileType != TileType.Empty)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void PlaceBuildingFootprint(int startX, int startY, int buildingWidth, int buildingHeight)
    {
        int footprintId = buildingFootprints.Count;
        buildingFootprints.Add(new BuildingFootprint
        {
            id = footprintId,
            startX = startX,
            startY = startY,
            width = buildingWidth,
            height = buildingHeight
        });

        for (int x = startX; x < startX + buildingWidth; x++)
        {
            for (int y = startY; y < startY + buildingHeight; y++)
            {
                CityCell cell = grid[x, y];
                cell.tileType = TileType.Building;
                cell.walkable = false;
                cell.moveCost = 0;
                cell.buildingFootprintId = footprintId;
                cell.isBuildingAnchor = x == startX && y == startY;
                cell.buildingWidth = buildingWidth;
                cell.buildingHeight = buildingHeight;
            }
        }
    }

    private void PlaceTrashCans()
    {
        float actualTrashCanProbability = Mathf.Clamp01(trashCanProbability * trashCanDensityMultiplier);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CityCell cell = grid[x, y];
                if (cell.tileType != TileType.Road)
                {
                    continue;
                }

                if (cell == collectionDepotCell)
                {
                    continue;
                }

                if (Random.value <= actualTrashCanProbability)
                {
                    cell.hasTrashCan = true;
                    cell.walkable = true;
                    cell.moveCost = 2;
                }
            }
        }
    }

    private void SpawnPrefabsFromGrid()
    {
        generatedRoot = new GameObject("Generated City").transform;
        generatedRoot.SetParent(transform);
        generatedRoot.localPosition = Vector3.zero;
        generatedRoot.localRotation = Quaternion.identity;
        generatedRoot.localScale = Vector3.one;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CityCell cell = grid[x, y];
                Vector3 worldPosition = GridToWorldPosition(x, y);

                SpawnTilePrefab(cell, worldPosition);
                SpawnTrashCanPrefab(cell, worldPosition);
                SpawnDepotMarker(cell, worldPosition);
            }
        }
    }

    private void SpawnTilePrefab(CityCell cell, Vector3 worldPosition)
    {
        GameObject prefabToSpawn = null;

        switch (cell.tileType)
        {
            case TileType.Road:
                SpawnRoadPrefab(cell, worldPosition);
                return;
            case TileType.Building:
                SpawnBuildingPrefab(cell);
                return;
            case TileType.Empty:
                prefabToSpawn = emptyGroundPrefab;
                break;
        }

        if (prefabToSpawn == null)
        {
            if (cell.tileType == TileType.Empty)
            {
                return;
            }

            Debug.LogWarning($"Missing prefab for {cell.tileType}. Grid cell ({cell.x}, {cell.y}) was not spawned.");
            return;
        }

        GameObject spawned = Instantiate(prefabToSpawn, worldPosition, Quaternion.identity, generatedRoot);
        spawned.name = $"{cell.tileType}_{cell.x}_{cell.y}";
        FitObjectFootprintToSize(spawned, tileSize, tileSize, false);
        cell.spawnedObject = spawned;
    }

    private void SpawnRoadPrefab(CityCell cell, Vector3 worldPosition)
    {
        GameObject prefabToSpawn = SelectRoadPrefab(cell.x, cell.y, out Quaternion rotation);

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"Missing road prefab. Grid cell ({cell.x}, {cell.y}) was not spawned.");
            return;
        }

        GameObject spawned = Instantiate(prefabToSpawn, worldPosition, rotation, generatedRoot);
        spawned.name = $"{GetRoadPrefabKind(cell.x, cell.y)}_{cell.x}_{cell.y}";
        FitObjectFootprintToSize(spawned, tileSize, tileSize, false);
        cell.spawnedObject = spawned;

        LogRoadPrefabSelection(cell.x, cell.y, prefabToSpawn, rotation);
    }

    private GameObject SelectRoadPrefab(int x, int y, out Quaternion rotation)
    {
        float rotationY = 0f;

        if (IsMapCorner(x, y))
        {
            rotationY = GetCornerRotationY(x, y);
            rotation = Quaternion.Euler(0f, rotationY, 0f);
            // For a 2D top-down setup, use Quaternion.Euler(0f, 0f, rotationY) instead.
            return cornerRoadPrefab != null ? cornerRoadPrefab : roadPrefab;
        }

        RoadConnection connection = GetRoadConnection(x, y);
        if (connection.Count() == 4 && intersectionRoadPrefab != null)
        {
            rotation = Quaternion.identity;
            return intersectionRoadPrefab;
        }

        if (connection.Count() == 3)
        {
            rotationY = GetTJunctionRotationY(connection);
            rotation = Quaternion.Euler(0f, rotationY, 0f);
            // For a 2D top-down setup, use Quaternion.Euler(0f, 0f, rotationY) instead.
            return tJunctionRoadPrefab != null ? tJunctionRoadPrefab : roadPrefab;
        }

        rotation = GetDefaultRoadRotation(x, y);
        return roadPrefab;
    }

    private string GetRoadPrefabKind(int x, int y)
    {
        if (IsMapCorner(x, y))
        {
            return "RoadCorner";
        }

        int connectionCount = GetRoadConnection(x, y).Count();
        if (connectionCount == 4 && intersectionRoadPrefab != null)
        {
            return "RoadIntersection";
        }

        if (connectionCount == 3)
        {
            return "RoadTJunction";
        }

        return "Road";
    }

    private float GetCornerRotationY(int x, int y)
    {
        // User-facing map coordinates are 1-based:
        // (1,1) -> 0-based (0,0), (width,1) -> (width-1,0),
        // (1,height) -> (0,height-1), (width,height) -> (width-1,height-1).
        if (x == width - 1 && y == height - 1)
        {
            return -90f;
        }

        if (x == width - 1 && y == 0)
        {
            return 0f;
        }

        if (x == 0 && y == 0)
        {
            return 90f;
        }

        if (x == 0 && y == height - 1)
        {
            return 180f;
        }

        return 0f;
    }

    private float GetTJunctionRotationY(RoadConnection connection)
    {
        if (!connection.down)
        {
            return 0f;
        }

        if (!connection.left)
        {
            return 90f;
        }

        if (!connection.up)
        {
            return 180f;
        }

        if (!connection.right)
        {
            return 270f;
        }

        return 0f;
    }

    private Quaternion GetDefaultRoadRotation(int x, int y)
    {
        bool hasHorizontalNeighbor = IsRoad(x - 1, y) || IsRoad(x + 1, y);
        bool hasVerticalNeighbor = IsRoad(x, y - 1) || IsRoad(x, y + 1);
        bool isHorizontalOnly = hasHorizontalNeighbor && !hasVerticalNeighbor;
        return isHorizontalOnly ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
    }

    private void LogRoadPrefabSelection(int x, int y, GameObject selectedPrefab, Quaternion rotation)
    {
        if (!enableRoadDebugLog)
        {
            return;
        }

        RoadConnection connection = GetRoadConnection(x, y);
        Debug.Log(
            $"Road prefab selected: grid=({x},{y}), map1Based=({x + 1},{y + 1}), " +
            $"up={connection.up}, down={connection.down}, left={connection.left}, right={connection.right}, " +
            $"kind={GetRoadPrefabKind(x, y)}, prefab={GetPrefabName(selectedPrefab)}, rotationY={rotation.eulerAngles.y:0.###}");
    }

    private bool IsFourWayRoadIntersection(CityCell cell)
    {
        if (cell == null || cell.tileType != TileType.Road)
        {
            return false;
        }

        return IsRoad(cell.x, cell.y + 1)
            && IsRoad(cell.x + 1, cell.y)
            && IsRoad(cell.x, cell.y - 1)
            && IsRoad(cell.x - 1, cell.y);
    }

    public bool IsRoad(int x, int y)
    {
        return IsInsideGrid(x, y) && grid[x, y].tileType == TileType.Road;
    }

    public RoadConnection GetRoadConnection(int x, int y)
    {
        return new RoadConnection
        {
            up = IsRoad(x, y + 1),
            down = IsRoad(x, y - 1),
            left = IsRoad(x - 1, y),
            right = IsRoad(x + 1, y)
        };
    }

    public bool IsMapCorner(int x, int y)
    {
        return (x == 0 && y == 0)
            || (x == width - 1 && y == 0)
            || (x == 0 && y == height - 1)
            || (x == width - 1 && y == height - 1);
    }

    private void SpawnBuildingPrefab(CityCell cell)
    {
        if (spawnBuildingsAsFootprints && !cell.isBuildingAnchor)
        {
            return;
        }

        if (buildingPrefab == null)
        {
            Debug.LogWarning($"Missing prefab for {cell.tileType}. Grid cell ({cell.x}, {cell.y}) was not spawned.");
            return;
        }

        int footprintWidth = spawnBuildingsAsFootprints ? Mathf.Max(1, cell.buildingWidth) : 1;
        int footprintHeight = spawnBuildingsAsFootprints ? Mathf.Max(1, cell.buildingHeight) : 1;
        Vector3 buildingPosition = GetFootprintCenterWorldPosition(cell.x, cell.y, footprintWidth, footprintHeight);
        GameObject spawned = Instantiate(buildingPrefab, buildingPosition, Quaternion.identity, generatedRoot);
        spawned.name = spawnBuildingsAsFootprints
            ? $"Building_{cell.x}_{cell.y}_{footprintWidth}x{footprintHeight}"
            : $"Building_{cell.x}_{cell.y}";

        if (scaleBuildingFootprintsToArea)
        {
            FitObjectFootprintToSize(spawned, footprintWidth * tileSize, footprintHeight * tileSize, spawnBuildingsAsFootprints);
        }
        else
        {
            FitObjectFootprintToSize(spawned, tileSize, tileSize, false);
        }

        AssignSpawnedObjectToBuildingFootprint(cell, spawned);
    }

    private void AssignSpawnedObjectToBuildingFootprint(CityCell anchorCell, GameObject spawned)
    {
        if (!spawnBuildingsAsFootprints)
        {
            anchorCell.spawnedObject = spawned;
            return;
        }

        for (int x = anchorCell.x; x < anchorCell.x + anchorCell.buildingWidth; x++)
        {
            for (int y = anchorCell.y; y < anchorCell.y + anchorCell.buildingHeight; y++)
            {
                if (IsInsideGrid(x, y) && grid[x, y].buildingFootprintId == anchorCell.buildingFootprintId)
                {
                    grid[x, y].spawnedObject = spawned;
                }
            }
        }
    }

    private void SpawnTrashCanPrefab(CityCell cell, Vector3 worldPosition)
    {
        if (!cell.hasTrashCan)
        {
            return;
        }

        if (trashCanPrefab == null)
        {
            Debug.LogWarning($"Missing trash can prefab. Trash can at ({cell.x}, {cell.y}) was not spawned.");
            return;
        }

        // If the trash can visually overlaps the road prefab, adjust this offset for your asset pivot and height.
        Vector3 trashCanPosition = worldPosition + new Vector3(0f, 0.05f, 0f);
        GameObject spawnedTrashCan = Instantiate(trashCanPrefab, trashCanPosition, Quaternion.identity, generatedRoot);
        spawnedTrashCan.name = $"TrashCan_{cell.x}_{cell.y}";
        cell.spawnedObject = spawnedTrashCan;

        TrashCanStatus status = spawnedTrashCan.GetComponent<TrashCanStatus>();
        if (status == null)
        {
            status = spawnedTrashCan.AddComponent<TrashCanStatus>();
        }

        status.Initialize(new Vector2Int(cell.x, cell.y));
    }

    private void SpawnDepotMarker(CityCell cell, Vector3 worldPosition)
    {
        if (!spawnDepotMarker || cell != collectionDepotCell)
        {
            return;
        }

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = $"CollectionDepot_A_{cell.x}_{cell.y}";
        marker.transform.SetParent(generatedRoot, true);

        Vector3 markerScale = new Vector3(
            tileSize * Mathf.Max(0.01f, depotMarkerScale.x),
            tileSize * Mathf.Max(0.01f, depotMarkerScale.y),
            tileSize * Mathf.Max(0.01f, depotMarkerScale.z));
        marker.transform.localScale = markerScale;
        marker.transform.position = worldPosition + Vector3.up * (markerScale.y * 0.5f);

        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = depotMarkerColor;
        }
    }

    public Vector3 GridToWorldPosition(int x, int y)
    {
        return transform.TransformPoint(GridToLocalPosition(x, y));
    }

    private Vector3 GridToLocalPosition(int x, int y)
    {
        return new Vector3(x * tileSize, 0f, y * tileSize);
    }

    private Vector3 GetFootprintCenterWorldPosition(int x, int y, int footprintWidth, int footprintHeight)
    {
        float centerX = (x + (footprintWidth - 1) * 0.5f) * tileSize;
        float centerY = (y + (footprintHeight - 1) * 0.5f) * tileSize;
        return transform.TransformPoint(new Vector3(centerX, 0f, centerY));
    }

    private void FitObjectFootprintToSize(GameObject spawned, float targetWidth, float targetHeight, bool allowNonUniformXZScale)
    {
        if (!fitTilePrefabsToTileSize || spawned == null)
        {
            return;
        }

        Vector2 footprint = GetObjectFootprintXZ(spawned);
        if (footprint.x <= 0f || footprint.y <= 0f)
        {
            return;
        }

        if (allowNonUniformXZScale)
        {
            Vector3 localScale = spawned.transform.localScale;
            localScale.x *= targetWidth / footprint.x;
            localScale.z *= targetHeight / footprint.y;
            spawned.transform.localScale = localScale;
            return;
        }

        float scaleMultiplier = Mathf.Min(targetWidth / footprint.x, targetHeight / footprint.y);
        spawned.transform.localScale *= scaleMultiplier;
    }

    private void ClampGenerationSettings()
    {
        minBlockSize = Mathf.Max(1, minBlockSize);
        maxBlockSize = Mathf.Max(minBlockSize, maxBlockSize);
        mainVerticalRoadCount = Mathf.Max(0, mainVerticalRoadCount);
        mainHorizontalRoadCount = Mathf.Max(0, mainHorizontalRoadCount);
        partialRoadSegmentCount = Mathf.Max(0, partialRoadSegmentCount);
        minRoadSpacing = Mathf.Max(1, minRoadSpacing);
        minPartialRoadLength = Mathf.Max(1, minPartialRoadLength);
        maxPartialRoadLength = Mathf.Max(minPartialRoadLength, maxPartialRoadLength);
        minBuildingWidth = Mathf.Max(1, minBuildingWidth);
        maxBuildingWidth = Mathf.Max(minBuildingWidth, maxBuildingWidth);
        minBuildingHeight = Mathf.Max(1, minBuildingHeight);
        maxBuildingHeight = Mathf.Max(minBuildingHeight, maxBuildingHeight);
        maxPlacementAttemptsPerBlock = Mathf.Max(1, maxPlacementAttemptsPerBlock);
        roadIntervalX = Mathf.Max(1, roadIntervalX);
        roadIntervalY = Mathf.Max(1, roadIntervalY);
    }

    private void CreateRoadCoordinateLists()
    {
        verticalRoadXs.Clear();
        horizontalRoadYs.Clear();

        if (useVariableBlockSize)
        {
            CreateVariableRoadCoordinates(verticalRoadXs, width);
            CreateVariableRoadCoordinates(horizontalRoadYs, height);
            return;
        }

        CreateFixedRoadCoordinates(verticalRoadXs, width, roadIntervalX);
        CreateFixedRoadCoordinates(horizontalRoadYs, height, roadIntervalY);
    }

    private void CreateVariableRoadCoordinates(List<int> roadCoordinates, int length)
    {
        if (length <= 0)
        {
            return;
        }

        roadCoordinates.Add(0);

        int current = 0;
        while (current < length - 1)
        {
            int blockSize = Random.Range(minBlockSize, maxBlockSize + 1);
            current += blockSize;

            if (current >= length - 1)
            {
                current = length - 1;
            }

            if (roadCoordinates[roadCoordinates.Count - 1] != current)
            {
                roadCoordinates.Add(current);
            }
        }
    }

    private void CreateFixedRoadCoordinates(List<int> roadCoordinates, int length, int interval)
    {
        if (length <= 0)
        {
            return;
        }

        for (int coordinate = 0; coordinate < length; coordinate += interval)
        {
            roadCoordinates.Add(coordinate);
        }

        if (roadCoordinates[roadCoordinates.Count - 1] != length - 1)
        {
            roadCoordinates.Add(length - 1);
        }
    }

    private void LogGenerationDiagnostics()
    {
        if (!logGenerationDiagnostics)
        {
            return;
        }

        Debug.Log(
            $"CityGenerator diagnostics: tileSize={tileSize}, " +
            $"generatorScale={transform.lossyScale}, " +
            $"worldStepX={Vector3.Distance(GridToWorldPosition(0, 0), GridToWorldPosition(1, 0)):0.###}, " +
            $"worldStepY={Vector3.Distance(GridToWorldPosition(0, 0), GridToWorldPosition(0, 1)):0.###}, " +
            $"useMapFile={useMapFile}, mapSource={GetMapSourceName()}, syncGridSizeToMap={syncGridSizeToMap}, " +
            $"populateBuildingsOnMapEmptyCells={populateBuildingsOnMapEmptyCells}, " +
            $"irregularRoadNetwork={useIrregularRoadNetwork}, forceBorderRoads={forceBorderRoads}, " +
            $"mainVerticalRoads={mainVerticalRoadCount}, mainHorizontalRoads={mainHorizontalRoadCount}, " +
            $"partialRoadSegments={partialRoadSegmentCount}, minRoadSpacing={minRoadSpacing}, " +
            $"partialRoadLength=({minPartialRoadLength}-{maxPartialRoadLength}), roadComponents={CountRoadComponents()}, " +
            $"variableBlocks={useVariableBlockSize}, minBlockSize={minBlockSize}, maxBlockSize={maxBlockSize}, " +
            $"fitTilePrefabsToTileSize={fitTilePrefabsToTileSize}, " +
            $"verticalRoads={verticalRoadXs.Count}, horizontalRoads={horizontalRoadYs.Count}, " +
            $"roadCells={CountCellsOfType(TileType.Road)}, buildingCells={CountCellsOfType(TileType.Building)}, emptyCells={CountCellsOfType(TileType.Empty)}, " +
            $"intersections={CountFourWayRoadIntersections()}, intersectionPrefab={GetPrefabName(intersectionRoadPrefab)}, " +
            $"buildingFootprints={buildingFootprints.Count}, spawnBuildingsAsFootprints={spawnBuildingsAsFootprints}, " +
            $"buildingSize=({minBuildingWidth}-{maxBuildingWidth}, {minBuildingHeight}-{maxBuildingHeight}), " +
            $"buildingFillChance={buildingFillChance:0.###}, emptySpaceChance={emptySpaceChance:0.###}, " +
            $"trashCanProbability={trashCanProbability:0.###}, trashCanDensityMultiplier={trashCanDensityMultiplier:0.###}, " +
            $"actualTrashCanProbability={Mathf.Clamp01(trashCanProbability * trashCanDensityMultiplier):0.###}, " +
            $"roadPrefab={GetPrefabName(roadPrefab)}, roadFootprint={GetPrefabFootprintXZ(roadPrefab)}, " +
            $"buildingPrefab={GetPrefabName(buildingPrefab)}, buildingFootprint={GetPrefabFootprintXZ(buildingPrefab)}");
    }

    private int CountCellsOfType(TileType tileType)
    {
        if (grid == null)
        {
            return 0;
        }

        int count = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].tileType == tileType)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private int CountFourWayRoadIntersections()
    {
        if (grid == null)
        {
            return 0;
        }

        int count = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (IsFourWayRoadIntersection(grid[x, y]))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private int CountRoadComponents()
    {
        if (grid == null)
        {
            return 0;
        }

        bool[,] visited = new bool[width, height];
        int components = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (visited[x, y] || grid[x, y].tileType != TileType.Road)
                {
                    continue;
                }

                components++;
                MarkConnectedRoadComponent(x, y, visited);
            }
        }

        return components;
    }

    private void MarkConnectedRoadComponent(int startX, int startY, bool[,] visited)
    {
        Queue<Vector2Int> open = new Queue<Vector2Int>();
        open.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        while (open.Count > 0)
        {
            Vector2Int current = open.Dequeue();

            foreach (Vector2Int direction in NeighborDirections)
            {
                int nextX = current.x + direction.x;
                int nextY = current.y + direction.y;

                if (!IsInsideGrid(nextX, nextY) || visited[nextX, nextY])
                {
                    continue;
                }

                if (grid[nextX, nextY].tileType != TileType.Road)
                {
                    continue;
                }

                visited[nextX, nextY] = true;
                open.Enqueue(new Vector2Int(nextX, nextY));
            }
        }
    }

    private string GetPrefabName(GameObject prefab)
    {
        return prefab != null ? prefab.name : "None";
    }

    private Vector2 GetPrefabFootprintXZ(GameObject prefab)
    {
        if (prefab == null)
        {
            return Vector2.zero;
        }

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return Vector2.zero;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return new Vector2(bounds.size.x, bounds.size.z);
    }

    private Vector2 GetObjectFootprintXZ(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return Vector2.zero;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return new Vector2(bounds.size.x, bounds.size.z);
    }

    private void DestroyGeneratedRoot()
    {
        if (Application.isPlaying)
        {
            Destroy(generatedRoot.gameObject);
        }
        else
        {
            DestroyImmediate(generatedRoot.gameObject);
        }

        generatedRoot = null;
    }

    private void OnDrawGizmos()
    {
        if (grid == null)
        {
            return;
        }

        Vector3 gizmoSize = new Vector3(tileSize, 0.05f, tileSize);
        Vector3 trashCanGizmoSize = Vector3.one * tileSize * 0.35f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CityCell cell = grid[x, y];
                Vector3 center = GridToLocalPosition(x, y);

                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.color = GetGizmoColor(cell);
                Gizmos.DrawCube(center, gizmoSize);
                Gizmos.DrawWireCube(center, gizmoSize);

                if (cell.hasTrashCan)
                {
                    Gizmos.color = trashCanGizmoColor;
                    Gizmos.DrawSphere(center + Vector3.up * 0.25f, trashCanGizmoSize.x);
                }
            }
        }

        Gizmos.matrix = Matrix4x4.identity;
    }

    private Color GetGizmoColor(CityCell cell)
    {
        switch (cell.tileType)
        {
            case TileType.Road:
                return roadGizmoColor;
            case TileType.Building:
                return buildingGizmoColor;
            case TileType.Empty:
                return emptyGizmoColor;
            default:
                return Color.magenta;
        }
    }
}
