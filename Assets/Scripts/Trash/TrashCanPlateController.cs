using UnityEngine;

public class TrashCanPlateController : MonoBehaviour
{
    private enum PlateState
    {
        Center,
        Left,
        Right
    }

    [Header("References")]
    public TrashCanFillSensor fillSensor;
    public Transform plateTransform;

    [Header("Plate Motion")]
    public Vector3 slideAxis = Vector3.right;
    [Range(0f, 1f)] public float startSlidingFillRatio = 0.8f;
    public bool moveOnFullOnly = true;
    [Min(0f)] public float maxTravelDistance = 0.35f;
    [Min(0.01f)] public float moveSpeed = 0.2f;

    [Header("Sensor Capacity Link")]
    public bool syncSensorSplitOffset = true;

    private Vector3 plateStartLocalPosition;
    private Vector3 normalizedSlideAxis;
    private PlateState currentState = PlateState.Center;

    private void Awake()
    {
        if (fillSensor == null)
        {
            fillSensor = GetComponentInParent<TrashCanFillSensor>();
        }

        if (plateTransform == null)
        {
            plateTransform = transform;
        }

        plateStartLocalPosition = plateTransform.localPosition;
        normalizedSlideAxis = slideAxis.sqrMagnitude > 0f
            ? slideAxis.normalized
            : Vector3.right;
    }

    private void OnEnable()
    {
        if (fillSensor != null)
        {
            fillSensor.onFillRatioChanged.AddListener(HandleFillRatioChanged);
            fillSensor.onEmptied.AddListener(HandleEmptied);
            fillSensor.onSideEmptied.AddListener(HandleSideEmptied);
            currentState = DetermineState();
        }
    }

    private void OnDisable()
    {
        if (fillSensor != null)
        {
            fillSensor.onFillRatioChanged.RemoveListener(HandleFillRatioChanged);
            fillSensor.onEmptied.RemoveListener(HandleEmptied);
            fillSensor.onSideEmptied.RemoveListener(HandleSideEmptied);
        }
    }

    private void Update()
    {
        // Only evaluate transitions when currently centered.
        if (currentState == PlateState.Center)
        {
            currentState = DetermineState();
        }

        Vector3 targetPosition = plateStartLocalPosition + normalizedSlideAxis * GetStateOffset(currentState);

        plateTransform.localPosition = Vector3.MoveTowards(
            plateTransform.localPosition,
            targetPosition,
            moveSpeed * Time.deltaTime);

        SyncSensorSplitOffset();
    }

    private void HandleFillRatioChanged(float fillRatio)
    {
        if (currentState == PlateState.Center)
        {
            currentState = DetermineState();
        }
    }

    private void HandleEmptied()
    {
        ReturnToCenter();
    }

    private void HandleSideEmptied(bool isLeft)
    {
        // When a side is emptied (manual or auto-unload), return plate to center.
        ReturnToCenter();
    }

    public void ReturnToCenter()
    {
        currentState = PlateState.Center;
    }

    private PlateState DetermineState()
    {
        if (fillSensor == null)
        {
            return PlateState.Center;
        }

        float leftFill = fillSensor.LeftFillRatio;
        float rightFill = fillSensor.RightFillRatio;

        float threshold = moveOnFullOnly ? fillSensor.fullHeightRatio : startSlidingFillRatio;
        bool leftHigh = leftFill >= threshold;
        bool rightHigh = rightFill >= threshold;

        if (!leftHigh && !rightHigh)
        {
            return PlateState.Center;
        }

        if (leftHigh && rightHigh)
        {
            return currentState;
        }

        if (leftHigh)
        {
            return PlateState.Right;
        }

        if (rightHigh)
        {
            return PlateState.Left;
        }

        return PlateState.Center;
    }

    private float GetStateOffset(PlateState plateState)
    {
        switch (plateState)
        {
            case PlateState.Left:
                return -maxTravelDistance;
            case PlateState.Right:
                return maxTravelDistance;
            default:
                return 0f;
        }
    }

    private void SyncSensorSplitOffset()
    {
        if (!syncSensorSplitOffset || fillSensor == null || fillSensor.sensorTrigger == null || plateTransform == null)
        {
            return;
        }

        Vector3 localDelta = plateTransform.localPosition - plateStartLocalPosition;
        float axisDistance = Vector3.Dot(localDelta, normalizedSlideAxis);
        fillSensor.sideSplitOffset = axisDistance;
    }
}
