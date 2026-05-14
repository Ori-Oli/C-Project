using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum TrashTestTargetMode
{
    Random,
    FirstAvailable,
    AllAvailable
}

public class TrashCollectionTestSignal : MonoBehaviour
{
    [Header("References")]
    public CityGenerator cityGenerator;
    public TrashCollectionDispatcher dispatcher;

    [Header("Setup")]
    public bool createDispatcherIfMissing = true;

    [Header("Trigger")]
    public bool triggerOnStart = true;
    [Min(0f)] public float startDelay = 2f;
    public bool triggerWithKey = true;
    public KeyCode triggerKey = KeyCode.F;
#if ENABLE_INPUT_SYSTEM
    public Key keyboardTriggerKey = Key.F;
#endif

    [Header("Fill")]
    public TrashTestTargetMode targetMode = TrashTestTargetMode.Random;
    [Min(1)] public int trashCansPerTrigger = 1;
    public bool fillRepeatedly = false;
    [Min(0.1f)] public float repeatInterval = 5f;
    public bool logSignals = true;

    private float startTimer;
    private float repeatTimer;
    private bool startSignalSent;

    private void Awake()
    {
        if (cityGenerator == null)
        {
            cityGenerator = FindAnyObjectByType<CityGenerator>();
        }

        EnsureDispatcher();
    }

    private void Update()
    {
        if (triggerOnStart && !startSignalSent)
        {
            startTimer += Time.deltaTime;
            if (startTimer >= startDelay)
            {
                startSignalSent = true;
                FillTrashCans();
            }
        }

        if (triggerWithKey && IsTriggerKeyPressed())
        {
            FillTrashCans();
        }

        if (fillRepeatedly)
        {
            repeatTimer += Time.deltaTime;
            if (repeatTimer >= repeatInterval)
            {
                repeatTimer = 0f;
                FillTrashCans();
            }
        }
    }

    [ContextMenu("Fill Trash Cans")]
    public void FillTrashCans()
    {
        EnsureDispatcher();

        TrashCanStatus[] allTrashCans = FindObjectsByType<TrashCanStatus>();
        List<TrashCanStatus> availableTrashCans = new List<TrashCanStatus>();

        for (int i = 0; i < allTrashCans.Length; i++)
        {
            TrashCanStatus trashCan = allTrashCans[i];
            if (trashCan != null && !trashCan.IsFull && !trashCan.IsReserved)
            {
                availableTrashCans.Add(trashCan);
            }
        }

        if (availableTrashCans.Count == 0)
        {
            if (logSignals)
            {
                Debug.Log("TrashCollectionTestSignal found no available trash cans to fill.");
            }

            return;
        }

        int filledCount = 0;
        switch (targetMode)
        {
            case TrashTestTargetMode.AllAvailable:
                for (int i = 0; i < availableTrashCans.Count; i++)
                {
                    FillOne(availableTrashCans[i]);
                    filledCount++;
                }

                break;
            case TrashTestTargetMode.FirstAvailable:
                filledCount = FillFirstAvailable(availableTrashCans);
                break;
            case TrashTestTargetMode.Random:
                filledCount = FillRandomAvailable(availableTrashCans);
                break;
        }

        if (logSignals)
        {
            Debug.Log($"TrashCollectionTestSignal filled {filledCount} trash can(s).");
        }
    }

    private bool IsTriggerKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current[keyboardTriggerKey].wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(triggerKey);
#else
        return false;
#endif
    }

    private void EnsureDispatcher()
    {
        if (dispatcher == null)
        {
            dispatcher = FindAnyObjectByType<TrashCollectionDispatcher>();
        }

        if (dispatcher != null)
        {
            return;
        }

        if (!createDispatcherIfMissing)
        {
            if (logSignals)
            {
                Debug.LogWarning("TrashCollectionTestSignal could not find a TrashCollectionDispatcher.");
            }

            return;
        }

        GameObject dispatcherObject = new GameObject("TrashCollectionDispatcher_Test");
        dispatcher = dispatcherObject.AddComponent<TrashCollectionDispatcher>();
        dispatcher.cityGenerator = cityGenerator;
        dispatcher.spawnPlaceholderTrucksIfNone = true;
        dispatcher.placeholderTruckCount = 5;
        dispatcher.placeTrucksAtMapDepot = true;
        dispatcher.mapDepotTruckHeight = 0.25f;

        if (logSignals)
        {
            Debug.Log("TrashCollectionTestSignal created a test TrashCollectionDispatcher.");
        }
    }

    private int FillFirstAvailable(List<TrashCanStatus> availableTrashCans)
    {
        int count = Mathf.Min(trashCansPerTrigger, availableTrashCans.Count);
        for (int i = 0; i < count; i++)
        {
            FillOne(availableTrashCans[i]);
        }

        return count;
    }

    private int FillRandomAvailable(List<TrashCanStatus> availableTrashCans)
    {
        int count = Mathf.Min(trashCansPerTrigger, availableTrashCans.Count);
        for (int i = 0; i < count; i++)
        {
            int selectedIndex = Random.Range(0, availableTrashCans.Count);
            TrashCanStatus selectedTrashCan = availableTrashCans[selectedIndex];
            availableTrashCans.RemoveAt(selectedIndex);
            FillOne(selectedTrashCan);
        }

        return count;
    }

    private void FillOne(TrashCanStatus trashCan)
    {
        trashCan.AddTrash(trashCan.capacity);

        if (logSignals)
        {
            Debug.Log($"Trash can full test signal sent: {trashCan.name} grid={trashCan.gridPosition}");
        }
    }
}
