using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Debug = ProjectSilentDebug;

public enum GarbageTruckState
{
    Idle,
    MovingToTrash,
    Collecting,
    ReturningToDepot
}

public class GarbageTruckController : MonoBehaviour
{
    [Header("Movement")]
    [Min(0.01f)] public float moveSpeed = 4f;
    [Min(0f)] public float collectDuration = 1f;
    public bool snapToDepotOnInitialize = true;
    public Vector3 depotParkingOffset = Vector3.zero;

    [Header("Capacity")]
    [Min(1)] public int maxLoad = 10;
    [Min(0)] public int currentLoad = 0;

    [Header("Placeholder Visual")]
    public bool createCubeBodyIfMissing = true;
    public Vector3 cubeBodyScale = new Vector3(0.8f, 0.45f, 1.2f);
    public Color cubeBodyColor = new Color(1f, 0.45f, 0.05f, 1f);

    [Header("Runtime")]
    public Vector2Int currentGridPosition = new Vector2Int(-1, -1);
    public GarbageTruckState State { get; private set; } = GarbageTruckState.Idle;
    public bool IsIdle => State == GarbageTruckState.Idle;
    public Vector2Int CurrentGridPosition => currentGridPosition;
    public TrashCanStatus AssignedTrashCan => assignedTrashCan;

    private CityGenerator cityGenerator;
    private TrashCollectionDispatcher dispatcher;
    private Coroutine activeRoutine;
    private TrashCanStatus assignedTrashCan;

    public void Initialize(CityGenerator city, TrashCollectionDispatcher owner)
    {
        cityGenerator = city;
        dispatcher = owner;

        SnapToDepotIfAvailable();

        EnsureCubeBody();
    }

    public void SnapToDepotIfAvailable()
    {
        SnapToDepotIfAvailable(false);
    }

    public bool SnapToDepotIfAvailable(bool forceSnap)
    {
        if ((!forceSnap && !snapToDepotOnInitialize) || cityGenerator == null || cityGenerator.CollectionDepotCell == null)
        {
            return false;
        }

        currentGridPosition = cityGenerator.CollectionDepotGridPosition;
        transform.position = cityGenerator.GridToWorldPosition(currentGridPosition.x, currentGridPosition.y) + depotParkingOffset;
        return true;
    }

    public bool SnapToGridPosition(Vector2Int gridPosition, Vector3 worldPosition)
    {
        currentGridPosition = gridPosition;
        transform.position = worldPosition + depotParkingOffset;
        return true;
    }

    public bool TryAssignTrashCan(TrashCanStatus trashCan, List<Vector2Int> path)
    {
        if (!IsIdle || trashCan == null || path == null || path.Count == 0)
        {
            return false;
        }

        assignedTrashCan = trashCan;
        State = GarbageTruckState.MovingToTrash;
        StartPathRoutine(path, MoveToTrashRoutine(path, trashCan));
        return true;
    }

    public bool TryReturnToDepot(List<Vector2Int> path)
    {
        if (path == null || path.Count == 0)
        {
            return false;
        }

        assignedTrashCan = null;
        State = GarbageTruckState.ReturningToDepot;
        StartPathRoutine(path, ReturnToDepotRoutine(path));
        return true;
    }

    public void UnloadAtDepot()
    {
        assignedTrashCan = null;
        currentLoad = 0;
        State = GarbageTruckState.Idle;
    }

    private void EnsureCubeBody()
    {
        if (!createCubeBodyIfMissing || GetComponentInChildren<Renderer>() != null)
        {
            return;
        }

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "GarbageTruckBody";
        body.transform.SetParent(transform, false);

        float tileSize = cityGenerator != null ? cityGenerator.tileSize : 1f;
        Vector3 bodyScale = new Vector3(
            tileSize * Mathf.Max(0.01f, cubeBodyScale.x),
            tileSize * Mathf.Max(0.01f, cubeBodyScale.y),
            tileSize * Mathf.Max(0.01f, cubeBodyScale.z));
        body.transform.localScale = bodyScale;
        body.transform.localPosition = Vector3.up * (bodyScale.y * 0.5f);

        Renderer renderer = body.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = cubeBodyColor;
        }

        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
        {
            Destroy(bodyCollider);
        }
    }

    private void StartPathRoutine(List<Vector2Int> path, IEnumerator routine)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(routine);
    }

    private IEnumerator MoveToTrashRoutine(List<Vector2Int> path, TrashCanStatus trashCan)
    {
        yield return FollowPath(path);
        State = GarbageTruckState.Collecting;

        if (collectDuration > 0f)
        {
            yield return new WaitForSeconds(collectDuration);
        }

        if (trashCan != null)
        {
            Debug.Log($"[GarbageTruckController] 쓰레기통 회수 중...", gameObject);

            // 센서에서 모든 쓰레기 제거 (센서 범위 밖 포함)
            TrashCanFillSensor fillSensor = trashCan.GetComponentInChildren<TrashCanFillSensor>();
            if (fillSensor != null)
            {
                int removedCount = fillSensor.ClearAllTrash();
                fillSensor.ClearFullSignal();
                Debug.Log($"[GarbageTruckController] 쓰레기통에서 {removedCount}개 쓰레기 수거", gameObject);
            }

            // 상태 상자 정리
            trashCan.Empty();
            currentLoad = Mathf.Min(maxLoad, currentLoad + 1);
            Debug.Log($"[GarbageTruckController] 쓰레기통 회수 완료! 현재 로드: {currentLoad}/{maxLoad}", gameObject);
        }

        assignedTrashCan = null;
        State = GarbageTruckState.Idle;
        activeRoutine = null;
        dispatcher?.NotifyTruckFinishedCollection(this, trashCan, currentLoad >= maxLoad);
    }

    private IEnumerator ReturnToDepotRoutine(List<Vector2Int> path)
    {
        yield return FollowPath(path);
        currentLoad = 0;
        State = GarbageTruckState.Idle;
        activeRoutine = null;
        dispatcher?.NotifyTruckReturnedToDepot(this);
    }

    private IEnumerator FollowPath(List<Vector2Int> path)
    {
        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int gridPosition = path[i];
            Vector3 target = cityGenerator.GridToWorldPosition(gridPosition.x, gridPosition.y);

            while ((transform.position - target).sqrMagnitude > 0.0001f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = target;
            currentGridPosition = gridPosition;
        }
    }
}
