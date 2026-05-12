using System.Collections.Generic;
using UnityEngine;

public class TrashCollectionDispatcher : MonoBehaviour
{
    [Header("References")]
    public CityGenerator cityGenerator;
    public List<GarbageTruckController> trucks = new List<GarbageTruckController>();

    [Header("Truck Limits")]
    [Min(1)] public int maxTruckCount = 5;
    public bool spawnPlaceholderTrucksIfNone = true;
    [Min(1)] public int placeholderTruckCount = 5;

    [Header("Discovery")]
    public bool autoFindReferences = true;
    public bool logDispatchWarnings = true;

    private readonly List<TrashCanStatus> trashCans = new List<TrashCanStatus>();
    private readonly List<TrashCanStatus> pendingTrashCans = new List<TrashCanStatus>();

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
    }

    private void Start()
    {
        EnsureTruckList();
        SpawnPlaceholderTrucksIfNeeded();
        InitializeTrucks();
        RegisterExistingTrashCans();
        TryDispatchIdleTrucks();
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
        pendingTrashCans.Clear();
        EnsureTruckList();
        SpawnPlaceholderTrucksIfNeeded();
        RegisterExistingTrashCans();
        InitializeTrucks();
        TryDispatchIdleTrucks();
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
        if (!spawnPlaceholderTrucksIfNone || trucks.Count > 0 || !HasUsableCity())
        {
            return;
        }

        int count = Mathf.Clamp(placeholderTruckCount, 1, Mathf.Max(1, maxTruckCount));
        float tileSize = cityGenerator != null ? cityGenerator.tileSize : 1f;
        for (int i = 0; i < count; i++)
        {
            GameObject truckObject = new GameObject($"GarbageTruck_{i + 1}");
            GarbageTruckController truck = truckObject.AddComponent<GarbageTruckController>();
            float centeredIndex = i - (count - 1) * 0.5f;
            truck.depotParkingOffset = new Vector3(centeredIndex * tileSize * 0.5f, 0f, -tileSize * 0.45f);
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
        }
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
        if (cityGenerator == null || cityGenerator.Grid == null)
        {
            return false;
        }

        if (cityGenerator.CollectionDepotCell == null && logDispatchWarnings)
        {
            Debug.LogWarning("Collection depot was not found. Add 'A' to the map file.");
        }

        return cityGenerator.CollectionDepotCell != null;
    }
}
