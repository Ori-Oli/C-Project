using UnityEngine;
using Debug = ProjectSilentDebug;

public class TrashCanAutoUnloadController : MonoBehaviour
{
    [Header("References")]
    public TrashCanFillSensor fillSensor;

    [Header("Auto Unload / Full Signal Settings")]
    [Tooltip("센서에 닿은 상태가 이 시간(초) 이상 지속되면 '풀' 신호를 보냅니다.")]
    [Min(0.1f)] public float holdDuration = 3f;
    public bool watchLeftSide = true;
    public bool watchRightSide = true;

    private float leftFullTimer;
    private float rightFullTimer;
    private float leftLastLogTime = -1f;
    private float rightLastLogTime = -1f;
    private bool hasSignaledFull = false;

    private void Awake()
    {
        if (fillSensor == null)
        {
            fillSensor = GetComponentInParent<TrashCanFillSensor>();
        }
        if (fillSensor != null)
        {
            fillSensor.onEmptied.AddListener(() =>
            {
                hasSignaledFull = false;
                ResetTimer(true);
                ResetTimer(false);
            });
        }
    }

    private void Update()
    {
        if (fillSensor == null)
        {
            return;
        }

        if (!hasSignaledFull)
        {
            UpdateSideTimer(isLeftSide: true, watchLeftSide);
            UpdateSideTimer(isLeftSide: false, watchRightSide);
        }
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

        if (fillSensor.FillRatio < fillSensor.FullSignalRatio)
        {
            return;
        }

        // Signal full (no automatic clearing). Truck must be present to unload.
        fillSensor.SignalFull();
        hasSignaledFull = true;
        Debug.Log($"[TrashCanAutoUnload] Full signaled by side {(isLeftSide ? "Left" : "Right")}");
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
