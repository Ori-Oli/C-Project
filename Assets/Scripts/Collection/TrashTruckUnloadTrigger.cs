using UnityEngine;
using UnityEngine.Events;

public class TrashTruckUnloadTrigger : MonoBehaviour
{
    [Header("References")]
    public TrashCanFillSensor fillSensor;

    [Header("Trigger Filters")]
    [Tooltip("비어있지 않으면 해당 태그를 가진 오브젝트만 쓰레기차로 인식합니다.")]
    public string truckTag = "TrashTruck";
    public LayerMask truckLayers = ~0;

    [Header("Condition")]
    [Range(0f, 1f)] public float requiredFillRatio = 0.8f;
    public bool onlyWhenSensorIsFull = false;

    [Header("Events")]
    public UnityEvent<int> onTrashUnloaded;

    private void Awake()
    {
        if (fillSensor == null)
        {
            fillSensor = GetComponentInParent<TrashCanFillSensor>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsTruck(other))
        {
            return;
        }

        TryUnloadTrash();
    }

    public bool TryUnloadTrash()
    {
        if (fillSensor == null)
        {
            return false;
        }

        if (!CanUnloadByFillCondition())
        {
            return false;
        }

        int removedCount = fillSensor.ClearTrackedTrash();
        if (removedCount <= 0)
        {
            return false;
        }

        // After unloading, clear any full signal so system can resume
        fillSensor.ClearFullSignal();

        onTrashUnloaded?.Invoke(removedCount);
        return true;
    }

    private bool IsTruck(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        GameObject candidate = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.transform.root.gameObject;

        if ((truckLayers.value & (1 << candidate.layer)) == 0)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(truckTag) && !candidate.CompareTag(truckTag))
        {
            return false;
        }

        return true;
    }

    private bool CanUnloadByFillCondition()
    {
        if (onlyWhenSensorIsFull)
        {
            return fillSensor != null && fillSensor.IsFullSignaled;
        }

        return fillSensor.FillRatio >= requiredFillRatio;
    }
}