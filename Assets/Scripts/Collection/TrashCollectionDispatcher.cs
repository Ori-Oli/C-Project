using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Debug = ProjectSilentDebug;

public class TrashCollectionDispatcher : MonoBehaviour
{
    [Header("References")]
    public CityGenerator cityGenerator;
    public List<GarbageTruckController> trucks = new List<GarbageTruckController>();

    [Header("Truck Limits")]
    [Min(1)] public int maxTruckCount = 5;
    public bool spawnPlaceholderTrucksIfNone = true;
    [Min(1)] public int placeholderTruckCount = 5;
    [Tooltip("When enabled, every registered truck is placed on the map cell marked with 'A' after the city is generated.")]
    public bool placeTrucksAtMapDepot = true;
    [Tooltip("Vertical offset used when placing trucks on the map cell marked with 'A'.")]
    public float mapDepotTruckHeight = 0.25f;

    [Header("Discovery")]
    public bool autoFindReferences = true;
    public bool logDispatchWarnings = true;
    public bool logTruckPlacement = true;

    private readonly List<TrashCanStatus> trashCans = new List<TrashCanStatus>();
    private readonly List<TrashCanStatus> pendingTrashCans = new List<TrashCanStatus>();
    private Coroutine initializeWhenReadyRoutine;

    private void Awake()
    {
        if (autoFindReferences && cityGenerator == null)
        {
            cityGenerator = FindAnyObjectByType<CityGenerator>();
        }
    }

    private void OnEnable()
    {
        if (cityGenerator != null)
        {
            cityGenerator.CityGenerated += HandleCityGenerated;
        }
    }

    private void OnDisable()
    {
        if (cityGenerator != null)
        {
            cityGenerator.CityGenerated -= HandleCityGenerated;
        }

        UnsubscribeTrashCans();

        if (initializeWhenReadyRoutine != null)
        {
            StopCoroutine(initializeWhenReadyRoutine);
            initializeWhenReadyRoutine = null;
        }
    }

    private void Start()
    {
        InitializeWhenCityIsReady();
    }

    public void RegisterFullTrashCan(TrashCanStatus trashCan)
    {
        if (trashCan == null || !trashCan.IsFull || pendingTrashCans.Contains(trashCan))
        {
            return;
        }

        pendingTrashCans.Add(trashCan);
        TryDispatchIdleTrucks();
    }

    public void NotifyTruckFinishedCollection(GarbageTruckController truck, TrashCanStatus trashCan, bool shouldReturnToDepot)
    {
        if (trashCan != null)
        {
            pendingTrashCans.Remove(trashCan);
            trashCan.MarkReserved(false);
        }

        if (shouldReturnToDepot)
        {
            SendTruckToDepot(truck);
            return;
        }

        if (!TryAssignNearestTrashCan(truck))
        {
            SendTruckToDepot(truck);
        }
    }

    public void NotifyTruckReturnedToDepot(GarbageTruckController truck)
    {
        TryDispatchIdleTrucks();
    }

    private void HandleCityGenerated()
    {
        InitializeWhenCityIsReady();
    }

    private void InitializeWhenCityIsReady()
    {
        if (TryInitializeForGeneratedCity())
        {
            return;
        }

        if (initializeWhenReadyRoutine == null)
        {
            initializeWhenReadyRoutine = StartCoroutine(InitializeWhenReadyRoutine());
        }
    }

    private IEnumerator InitializeWhenReadyRoutine()
    {
        while (!TryInitializeForGeneratedCity())
        {
            yield return null;
        }

        initializeWhenReadyRoutine = null;
    }

    private bool TryInitializeForGeneratedCity()
    {
        if (!HasUsableCity(false))
        {
            return false;
        }

        pendingTrashCans.Clear();
        EnsureTruckList();
        SpawnPlaceholderTrucksIfNeeded();
        RegisterExistingTrashCans();
        InitializeTrucks();
        TryDispatchIdleTrucks();
        return true;
    }

    private void EnsureTruckList()
    {
        if (autoFindReferences && trucks.Count == 0)
        {
            trucks.AddRange(FindObjectsByType<GarbageTruckController>());
        }

        for (int i = trucks.Count - 1; i >= 0; i--)
        {
            if (trucks[i] == null)
            {
                trucks.RemoveAt(i);
            }
        }

        while (trucks.Count > Mathf.Max(1, maxTruckCount))
        {
            trucks.RemoveAt(trucks.Count - 1);
        }
    }

    private void SpawnPlaceholderTrucksIfNeeded()
    {
        if (!spawnPlaceholderTrucksIfNone || trucks.Count > 0)
        {
            return;
        }

        int count = Mathf.Clamp(placeholderTruckCount, 1, Mathf.Max(1, maxTruckCount));
        for (int i = 0; i < count; i++)
        {
            GameObject truckObject = new GameObject($"GarbageTruck_{i + 1}");
            GarbageTruckController truck = truckObject.AddComponent<GarbageTruckController>();
            truck.depotParkingOffset = Vector3.zero;
            truck.maxLoad = 10;
            trucks.Add(truck);
        }
    }

    private void InitializeTrucks()
    {
        for (int i = trucks.Count - 1; i >= 0; i--)
        {
            if (trucks[i] == null)
            {
                trucks.RemoveAt(i);
                continue;
            }

            trucks[i].Initialize(cityGenerator, this);
            if (placeTrucksAtMapDepot)
            {
                PlaceTruckAtMapDepot(trucks[i]);
            }
        }
    }

    private bool PlaceTruckAtMapDepot(GarbageTruckController truck)
    {
        if (truck == null || cityGenerator == null || cityGenerator.CollectionDepotCell == null)
        {
            return false;
        }

        Vector2Int depotGridPosition = cityGenerator.CollectionDepotGridPosition;
        Vector3 depotWorldPosition = cityGenerator.GridToWorldPosition(depotGridPosition.x, depotGridPosition.y);
        depotWorldPosition.y = mapDepotTruckHeight;
        bool placed = truck.SnapToGridPosition(depotGridPosition, depotWorldPosition);

        if (placed && logTruckPlacement)
        {
            Debug.Log(
                $"Placed truck '{truck.name}' at map depot A: grid={depotGridPosition}, world={depotWorldPosition}",
                truck);
        }

        return placed;
    }

    private void RegisterExistingTrashCans()
    {
        UnsubscribeTrashCans();
        trashCans.Clear();

        TrashCanStatus[] foundTrashCans = FindObjectsByType<TrashCanStatus>();
        for (int i = 0; i < foundTrashCans.Length; i++)
        {
            TrashCanStatus trashCan = foundTrashCans[i];
            trashCan.FullChangedToTrue += HandleTrashCanFull;
            trashCans.Add(trashCan);

            if (trashCan.IsFull)
            {
                RegisterFullTrashCan(trashCan);
            }
        }
    }

    private void UnsubscribeTrashCans()
    {
        for (int i = 0; i < trashCans.Count; i++)
        {
            if (trashCans[i] != null)
            {
                trashCans[i].FullChangedToTrue -= HandleTrashCanFull;
            }
        }
    }

    private void HandleTrashCanFull(TrashCanStatus trashCan)
    {
        RegisterFullTrashCan(trashCan);
    }

    private void TryDispatchIdleTrucks()
    {
        if (!HasUsableCity())
        {
            return;
        }

        for (int i = 0; i < trucks.Count; i++)
        {
            GarbageTruckController truck = trucks[i];
            if (truck == null || !truck.IsIdle)
            {
                continue;
            }

            if (truck.currentLoad >= truck.maxLoad)
            {
                SendTruckToDepot(truck);
                continue;
            }

            if (!TryAssignNearestTrashCan(truck) && !IsTruckAtDepot(truck))
            {
                SendTruckToDepot(truck);
            }
        }
    }

    private bool TryAssignNearestTrashCan(GarbageTruckController truck)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        List<TrashCanStatus> candidateTrashCans = new List<TrashCanStatus>();

        for (int i = 0; i < pendingTrashCans.Count; i++)
        {
            TrashCanStatus trashCan = pendingTrashCans[i];
            if (trashCan == null || !trashCan.IsFull || trashCan.IsReserved)
            {
                continue;
            }

            candidates.Add(trashCan.gridPosition);
            candidateTrashCans.Add(trashCan);
        }

        Vector2Int target;
        List<Vector2Int> path;
        if (!BfsPathfinder.FindNearestReachable(cityGenerator, truck.CurrentGridPosition, candidates, out target, out path))
        {
            return false;
        }

        TrashCanStatus selectedTrashCan = null;
        for (int i = 0; i < candidateTrashCans.Count; i++)
        {
            if (candidateTrashCans[i].gridPosition == target)
            {
                selectedTrashCan = candidateTrashCans[i];
                break;
            }
        }

        if (selectedTrashCan == null)
        {
            return false;
        }

        selectedTrashCan.MarkReserved(true);
        if (!truck.TryAssignTrashCan(selectedTrashCan, path))
        {
            selectedTrashCan.MarkReserved(false);
            return false;
        }

        return true;
    }

    private void SendTruckToDepot(GarbageTruckController truck)
    {
        if (truck == null || !HasUsableCity() || cityGenerator.CollectionDepotCell == null)
        {
            return;
        }

        if (IsTruckAtDepot(truck))
        {
            truck.UnloadAtDepot();
            return;
        }

        List<Vector2Int> path = BfsPathfinder.FindPath(
            cityGenerator,
            truck.CurrentGridPosition,
            cityGenerator.CollectionDepotGridPosition);

        if (!truck.TryReturnToDepot(path) && logDispatchWarnings)
        {
            Debug.LogWarning($"Truck '{truck.name}' could not find a path back to the collection depot.");
        }
    }

    private bool IsTruckAtDepot(GarbageTruckController truck)
    {
        return truck != null
            && cityGenerator != null
            && cityGenerator.CollectionDepotCell != null
            && truck.CurrentGridPosition == cityGenerator.CollectionDepotGridPosition;
    }

    private bool HasUsableCity()
    {
        return HasUsableCity(logDispatchWarnings);
    }

    private bool HasUsableCity(bool shouldLogWarnings)
    {
        if (cityGenerator == null || cityGenerator.Grid == null)
        {
            return false;
        }

        if (cityGenerator.CollectionDepotCell == null && shouldLogWarnings)
        {
            Debug.LogWarning("Collection depot was not found. Add 'A' to the map file.");
        }

        return cityGenerator.CollectionDepotCell != null;
    }
}
