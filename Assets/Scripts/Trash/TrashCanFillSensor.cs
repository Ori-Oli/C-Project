using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TrashCanFillSensor : MonoBehaviour
{
    [Header("Sensor Volume")]
    public BoxCollider sensorTrigger;
    public bool autoCreateSensorTrigger = true;
    public Vector3 sensorCenter = new Vector3(0f, 0.45f, 0f);
    public Vector3 sensorSize = new Vector3(0.6f, 0.9f, 0.6f);
    [Tooltip("센서 중앙 기준 좌우를 나눌 분할 축 오프셋입니다. 0이면 정확히 중앙에서 나뉩니다.")]
    public float sideSplitOffset = 0f;

    [Header("Detection Filters")]
    [Tooltip("비어있지 않으면 해당 태그를 가진 오브젝트만 감지합니다.")]
    public string requiredTag = "";
    public LayerMask detectableLayers = ~0;
    public bool requireTrashItemMarker = true;
    public bool ignoreTriggerColliders = true;

    [Header("Capacity")]
    [Range(0f, 1f)] public float fullHeightRatio = 0.8f;

    [Header("Events")]
    public UnityEvent<float> onFillRatioChanged;
    public UnityEvent onBecameFull;
    public UnityEvent onEmptied;
    public UnityEvent<bool> onSideEmptied;
    public UnityEvent onFullSignaled;

    public int CurrentTrashCount => trackedObjects.Count;
    public float LeftFillRatio => CalculateSideFillRatio(isLeftSide: true);
    public float RightFillRatio => CalculateSideFillRatio(isLeftSide: false);
    public float AverageFillRatio => (LeftFillRatio + RightFillRatio) * 0.5f;
    public float FillRatio => Mathf.Max(LeftFillRatio, RightFillRatio);
    public bool IsFull => LeftIsFull || RightIsFull;
    public bool LeftIsFull => LeftFillRatio >= fullHeightRatio;
    public bool RightIsFull => RightFillRatio >= fullHeightRatio;

    private readonly HashSet<GameObject> trackedObjects = new HashSet<GameObject>();
    private readonly HashSet<GameObject> ownedObjects = new HashSet<GameObject>();
    private bool wasFull;

    private void Reset()
    {
        EnsureSensorTrigger();
    }

    private void Awake()
    {
        EnsureSensorTrigger();
    }

    private void Start()
    {
        ForceRescan();
    }

    private void Update()
    {
        if (CleanupDestroyedTrackedObjects())
        {
            NotifyFillChanged();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (TryGetTrackedObject(other, out GameObject trackedObject) && trackedObjects.Add(trackedObject))
        {
            NotifyFillChanged();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (TryGetTrackedObject(other, out GameObject trackedObject) && trackedObjects.Remove(trackedObject))
        {
            NotifyFillChanged();
        }
    }

    public void ForceRescan()
    {
        if (sensorTrigger == null)
        {
            return;
        }

        trackedObjects.Clear();

        Vector3 worldCenter = sensorTrigger.transform.TransformPoint(sensorTrigger.center);
        Vector3 halfExtents = Vector3.Scale(sensorTrigger.size, sensorTrigger.transform.lossyScale) * 0.5f;
        QueryTriggerInteraction queryMode = ignoreTriggerColliders
            ? QueryTriggerInteraction.Ignore
            : QueryTriggerInteraction.Collide;

        Collider[] hits = Physics.OverlapBox(
            worldCenter,
            halfExtents,
            sensorTrigger.transform.rotation,
            detectableLayers,
            queryMode);

        foreach (Collider hit in hits)
        {
            if (TryGetTrackedObject(hit, out GameObject trackedObject))
            {
                trackedObjects.Add(trackedObject);
            }
        }

        NotifyFillChanged();
    }

    public int ClearTrackedTrash(bool destroyImmediately = false)
    {
        if (trackedObjects.Count == 0)
        {
            NotifyFillChanged();
            return 0;
        }

        int removedCount = 0;
        List<GameObject> targets = new List<GameObject>(trackedObjects);

        foreach (GameObject target in targets)
        {
            if (target == null)
            {
                continue;
            }
            TryReturnToPoolOrDestroy(target, destroyImmediately);
            removedCount++;
        }

        trackedObjects.Clear();
        NotifyFillChanged();
        return removedCount;
    }

    public void RegisterOwnedTrash(GameObject trashObject)
    {
        if (trashObject == null)
        {
            return;
        }

        ownedObjects.Add(trashObject);
    }

    public void UnregisterOwnedTrash(GameObject trashObject)
    {
        if (trashObject == null)
        {
            return;
        }

        ownedObjects.Remove(trashObject);
    }

    public int ClearAllTrash(bool destroyImmediately = false)
    {
        int removedCount = 0;
        HashSet<GameObject> targets = new HashSet<GameObject>();

        foreach (GameObject owned in ownedObjects)
        {
            if (owned != null)
            {
                targets.Add(owned);
            }
        }

        foreach (GameObject tracked in trackedObjects)
        {
            if (tracked == null)
            {
                continue;
            }

            TrashItemMarker marker = tracked.GetComponent<TrashItemMarker>();
            if (marker == null || marker.ownerSensor == null || marker.ownerSensor == this)
            {
                targets.Add(tracked);
            }
        }

        foreach (GameObject target in targets)
        {
            if (target == null)
            {
                continue;
            }

            TryReturnToPoolOrDestroy(target, destroyImmediately);
            removedCount++;
        }

        foreach (GameObject target in targets)
        {
            trackedObjects.Remove(target);
            ownedObjects.Remove(target);
        }

        NotifyFillChanged();
        Debug.Log($"[TrashCanFillSensor] 소유 쓰레기 제거 완료: {removedCount}개");
        return removedCount;
    }

    public int ClearTrackedTrashOnSide(bool isLeftSide, bool destroyImmediately = false)
    {
        TrashItemMarker.SpawnSide targetSide = isLeftSide ? TrashItemMarker.SpawnSide.Left : TrashItemMarker.SpawnSide.Right;
        
        TrashItemMarker[] allMarkers = FindObjectsByType<TrashItemMarker>();
        List<GameObject> targets = new List<GameObject>();

        foreach (TrashItemMarker marker in allMarkers)
        {
            if (marker != null && marker.spawnSide == targetSide)
            {
                targets.Add(marker.gameObject);
            }
        }

        int removedCount = 0;
        foreach (GameObject target in targets)
        {
            if (target == null)
            {
                continue;
            }

            TryReturnToPoolOrDestroy(target, destroyImmediately);
            trackedObjects.Remove(target);
            removedCount++;
        }

        if (removedCount > 0)
        {
            string sideName = isLeftSide ? "Left" : "Right";
            Debug.Log($"[TrashCanFillSensor] Cleared {sideName} side: {removedCount} objects removed (all trash with matching flag)");
            NotifyFillChanged();
            onSideEmptied?.Invoke(isLeftSide);
        }

        return removedCount;
    }

    private void TryReturnToPoolOrDestroy(GameObject target, bool destroyImmediately)
    {
        if (target == null) return;

        trackedObjects.Remove(target);
        ownedObjects.Remove(target);

        TrashItemMarker marker = target.GetComponent<TrashItemMarker>();
        if (marker != null && marker.ownerSensor == this)
        {
            marker.ownerSensor = null;
        }

        TrashPooled pooled = target.GetComponent<TrashPooled>();
        if (pooled != null && pooled.pool != null)
        {
            // return to pool via interface if available
            try
            {
                pooled.pool.Return(target);
                return;
            }
            catch
            {
                // fallback to destroy
            }
        }

        if (destroyImmediately)
        {
            DestroyImmediate(target);
        }
        else
        {
            Destroy(target);
        }
    }

    // Full-signal: set when can reports full (e.g. after contact hold)
    private bool fullSignaled;

    public bool IsFullSignaled => fullSignaled;

    public void SignalFull()
    {
        if (fullSignaled)
        {
            return;
        }

        fullSignaled = true;
        Debug.Log("[TrashCanFillSensor] Full signal emitted");
        onFullSignaled?.Invoke();
    }

    public void ClearFullSignal()
    {
        if (!fullSignaled)
        {
            return;
        }

        fullSignaled = false;
        Debug.Log("[TrashCanFillSensor] Full signal cleared");
    }

    public float GetHeightFillRatio()
    {
        return FillRatio;
    }

    public float GetSideFillRatio(bool isLeftSide)
    {
        return CalculateSideFillRatio(isLeftSide);
    }

    public bool HasContactOnSide(bool isLeftSide)
    {
        if (sensorTrigger == null)
        {
            return false;
        }

        float splitLocalX = sensorTrigger.center.x + sideSplitOffset;

        foreach (GameObject trackedObject in trackedObjects)
        {
            if (trackedObject == null)
            {
                continue;
            }

            if (IsObjectOnRequestedSide(trackedObject, isLeftSide, splitLocalX))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetTrackedObject(Collider other, out GameObject trackedObject)
    {
        trackedObject = null;

        if (other == null)
        {
            return false;
        }

        if (ignoreTriggerColliders && other.isTrigger)
        {
            return false;
        }

        TrashItemMarker marker = other.GetComponentInParent<TrashItemMarker>();
        if (requireTrashItemMarker && marker == null)
        {
            return false;
        }

        if (marker != null && marker.ownerSensor != null && marker.ownerSensor != this)
        {
            return false;
        }

        if (marker != null)
        {
            trackedObject = marker.gameObject;
            if (marker.ownerSensor == null)
            {
                marker.ownerSensor = this;
            }

            if (marker.ownerSensor == this)
            {
                ownedObjects.Add(trackedObject);
            }
        }
        else if (other.attachedRigidbody != null)
        {
            trackedObject = other.attachedRigidbody.gameObject;
        }
        else
        {
            trackedObject = other.transform.root.gameObject;
        }

        if (!IsInDetectableLayers(trackedObject.layer))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(requiredTag) && !trackedObject.CompareTag(requiredTag))
        {
            return false;
        }

        return true;
    }

    private bool IsInDetectableLayers(int layer)
    {
        return (detectableLayers.value & (1 << layer)) != 0;
    }

    private float CalculateSideFillRatio(bool isLeftSide)
    {
        if (sensorTrigger == null)
        {
            return 0f;
        }

        if (trackedObjects.Count == 0)
        {
            return 0f;
        }

        float bottomLocalY = sensorTrigger.center.y - (sensorTrigger.size.y * 0.5f);
        float topLocalY = sensorTrigger.center.y + (sensorTrigger.size.y * 0.5f);
        float splitLocalX = sensorTrigger.center.x + sideSplitOffset;
        float leftBoundaryLocalX = sensorTrigger.center.x - (sensorTrigger.size.x * 0.5f);
        float rightBoundaryLocalX = sensorTrigger.center.x + (sensorTrigger.size.x * 0.5f);
        float highestLocalY = bottomLocalY;

        foreach (GameObject trackedObject in trackedObjects)
        {
            if (trackedObject == null)
            {
                continue;
            }

            if (!IsObjectOnRequestedSide(trackedObject, isLeftSide, splitLocalX))
            {
                continue;
            }

            float objectTopLocalY = GetObjectTopLocalY(trackedObject);
            if (objectTopLocalY > highestLocalY)
            {
                highestLocalY = objectTopLocalY;
            }
        }

        if (topLocalY <= bottomLocalY)
        {
            return 0f;
        }

        return Mathf.Clamp01((highestLocalY - bottomLocalY) / (topLocalY - bottomLocalY));
    }

    private bool IsObjectOnRequestedSide(GameObject target, bool isLeftSide, float splitLocalX)
    {
        float centerLocalX = GetObjectCenterLocalX(target);
        return isLeftSide ? centerLocalX <= splitLocalX : centerLocalX > splitLocalX;
    }

    private float GetObjectCenterLocalX(GameObject target)
    {
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            return sensorTrigger.transform.InverseTransformPoint(target.transform.position).x;
        }

        float minLocalX = float.PositiveInfinity;
        float maxLocalX = float.NegativeInfinity;

        foreach (Collider collider in colliders)
        {
            if (collider == null || (ignoreTriggerColliders && collider.isTrigger))
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            foreach (Vector3 corner in corners)
            {
                float localX = sensorTrigger.transform.InverseTransformPoint(corner).x;
                if (localX < minLocalX)
                {
                    minLocalX = localX;
                }

                if (localX > maxLocalX)
                {
                    maxLocalX = localX;
                }
            }
        }

        if (float.IsPositiveInfinity(minLocalX) || float.IsNegativeInfinity(maxLocalX))
        {
            return sensorTrigger.transform.InverseTransformPoint(target.transform.position).x;
        }

        return (minLocalX + maxLocalX) * 0.5f;
    }

    private float GetObjectTopLocalY(GameObject target)
    {
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            return sensorTrigger.transform.InverseTransformPoint(target.transform.position).y;
        }

        float highestLocalY = float.NegativeInfinity;

        foreach (Collider collider in colliders)
        {
            if (collider == null || (ignoreTriggerColliders && collider.isTrigger))
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            foreach (Vector3 corner in corners)
            {
                float localY = sensorTrigger.transform.InverseTransformPoint(corner).y;
                if (localY > highestLocalY)
                {
                    highestLocalY = localY;
                }
            }
        }

        if (float.IsNegativeInfinity(highestLocalY))
        {
            return sensorTrigger.transform.InverseTransformPoint(target.transform.position).y;
        }

        return highestLocalY;
    }

    private bool CleanupDestroyedTrackedObjects()
    {
        int trackedRemovedCount = trackedObjects.Count == 0
            ? 0
            : trackedObjects.RemoveWhere(trackedObject => trackedObject == null);

        int ownedRemovedCount = ownedObjects.Count == 0
            ? 0
            : ownedObjects.RemoveWhere(ownedObject => ownedObject == null);

        return trackedRemovedCount > 0 || ownedRemovedCount > 0;
    }

    private void NotifyFillChanged()
    {
        float fillRatio = FillRatio;
        int trashCount = CurrentTrashCount;

        if (trashCount == 0)
        {
            onEmptied?.Invoke();
        }

        onFillRatioChanged?.Invoke(fillRatio);

        bool nowFull = IsFull;
        if (!wasFull && nowFull)
        {
            onBecameFull?.Invoke();
        }

        wasFull = nowFull;
    }

    private void EnsureSensorTrigger()
    {
        if (sensorTrigger != null)
        {
            sensorTrigger.isTrigger = true;
            return;
        }

        sensorTrigger = GetComponent<BoxCollider>();
        if (sensorTrigger == null && autoCreateSensorTrigger)
        {
            sensorTrigger = gameObject.AddComponent<BoxCollider>();
        }

        if (sensorTrigger == null)
        {
            return;
        }

        sensorTrigger.isTrigger = true;
        sensorTrigger.center = sensorCenter;
        sensorTrigger.size = sensorSize;
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider target = sensorTrigger != null ? sensorTrigger : GetComponent<BoxCollider>();
        if (target == null)
        {
            return;
        }

        Gizmos.color = new Color(0f, 0.85f, 0.3f, 0.25f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = target.transform.localToWorldMatrix;
        Gizmos.DrawCube(target.center, target.size);
        Gizmos.color = new Color(0f, 0.85f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(target.center, target.size);
        Gizmos.matrix = oldMatrix;
    }
}