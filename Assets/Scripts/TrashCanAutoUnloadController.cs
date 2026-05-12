using UnityEngine;

public class TrashCanAutoUnloadController : MonoBehaviour
{
    [Header("References")]
    public TrashCanFillSensor fillSensor;

    [Header("Auto Unload Settings")]
    [Tooltip("센서에 닿은 상태가 이 시간(초) 이상 지속되면 자동 삭제됩니다.")]
    [Min(0.1f)] public float holdDuration = 5f;
    public bool unloadLeftSide = true;
    public bool unloadRightSide = true;

    private float leftFullTimer;
    private float rightFullTimer;
    private float leftLastLogTime = -1f;
    private float rightLastLogTime = -1f;

    private void Awake()
    {
        if (fillSensor == null)
        {
            fillSensor = GetComponentInParent<TrashCanFillSensor>();
        }
    }

    private void Update()
    {
        if (fillSensor == null)
        {
            return;
        }

        UpdateSideTimer(isLeftSide: true, unloadLeftSide);
        UpdateSideTimer(isLeftSide: false, unloadRightSide);
    }

    private void UpdateSideTimer(bool isLeftSide, bool canUnloadSide)
    {
        if (!canUnloadSide)
        {
            ResetTimer(isLeftSide);
            return;
        }

        if (!fillSensor.HasContactOnSide(isLeftSide))
        {
            ResetTimer(isLeftSide);
            return;
        }

        AddTimer(isLeftSide, Time.deltaTime);
        float currentTimer = GetTimer(isLeftSide);
        float lastLogTime = isLeftSide ? leftLastLogTime : rightLastLogTime;

        // Log every 1 second
        if (currentTimer - lastLogTime >= 1f)
        {
            string sideName = isLeftSide ? "Left" : "Right";
            Debug.Log($"[TrashCanAutoUnload] {sideName} side contact: {currentTimer:F1}s / {holdDuration:F1}s");

            if (isLeftSide)
            {
                leftLastLogTime = currentTimer;
            }
            else
            {
                rightLastLogTime = currentTimer;
            }
        }

        if (currentTimer < holdDuration)
        {
            return;
        }

        fillSensor.ClearTrackedTrashOnSide(isLeftSide);
        ResetTimer(isLeftSide);
    }

    private void AddTimer(bool isLeftSide, float deltaTime)
    {
        if (isLeftSide)
        {
            leftFullTimer += deltaTime;
        }
        else
        {
            rightFullTimer += deltaTime;
        }
    }

    private float GetTimer(bool isLeftSide)
    {
        return isLeftSide ? leftFullTimer : rightFullTimer;
    }

    private void ResetTimer(bool isLeftSide)
    {
        if (isLeftSide)
        {
            leftFullTimer = 0f;
            leftLastLogTime = -1f;
        }
        else
        {
            rightFullTimer = 0f;
            rightLastLogTime = -1f;
        }
    }
}