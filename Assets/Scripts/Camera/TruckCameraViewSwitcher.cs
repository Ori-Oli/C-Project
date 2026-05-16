using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TruckCameraViewSwitcher : MonoBehaviour
{
    [Header("Camera")]
    public Camera targetCamera;
    public Vector3 truckViewOffset = new Vector3(0f, 12f, 0f);
    [Min(0.1f)] public float truckOrthographicSize = 6f;
    [Min(0f)] public float followSmoothTime = 0.12f;

    [Header("Truck Buttons")]
    [Min(1)] public int maxTruckButtons = 5;
    [Min(0.1f)] public float truckRefreshIntervalSeconds = 0.5f;

    [Header("Runtime UI")]
    public bool createRuntimeUi = true;
    public Vector2 buttonSize = new Vector2(150f, 52f);
    [Min(8)] public int buttonFontSize = 24;
    public Font buttonFont;
    public float buttonSpacing = 12f;
    public Vector2 panelOffset = new Vector2(24f, 0f);
    public Color normalButtonColor = new Color(0.16f, 0.18f, 0.22f, 0.9f);
    public Color selectedButtonColor = new Color(0.05f, 0.42f, 0.85f, 0.95f);

    private readonly List<GarbageTruckController> trucks = new List<GarbageTruckController>();
    private Button overviewButton;
    private Button[] truckButtons;
    private Text[] truckButtonLabels;
    private Vector3 overviewPosition;
    private Quaternion overviewRotation;
    private bool overviewOrthographic;
    private float overviewOrthographicSize;
    private float overviewFieldOfView;
    private GarbageTruckController selectedTruck;
    private Vector3 followVelocity;
    private Coroutine refreshRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallOnMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null || mainCamera.GetComponent<TruckCameraViewSwitcher>() != null)
        {
            return;
        }

        mainCamera.gameObject.AddComponent<TruckCameraViewSwitcher>();
    }

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        CaptureOverviewView();

        if (createRuntimeUi)
        {
            CreateRuntimeUi();
        }
    }

    private void OnEnable()
    {
        refreshRoutine = StartCoroutine(RefreshTrucksRoutine());
    }

    private void OnDisable()
    {
        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
            refreshRoutine = null;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null || selectedTruck == null)
        {
            return;
        }

        Vector3 targetPosition = selectedTruck.transform.position + truckViewOffset;
        targetCamera.transform.position = Vector3.SmoothDamp(
            targetCamera.transform.position,
            targetPosition,
            ref followVelocity,
            followSmoothTime);
        targetCamera.transform.rotation = overviewRotation;
        targetCamera.orthographic = true;
        targetCamera.orthographicSize = truckOrthographicSize;
    }

    public void ShowOverview()
    {
        CancelTrashCanNavigation();
        selectedTruck = null;
        followVelocity = Vector3.zero;

        if (targetCamera == null)
        {
            return;
        }

        targetCamera.transform.position = overviewPosition;
        targetCamera.transform.rotation = overviewRotation;
        targetCamera.orthographic = overviewOrthographic;
        targetCamera.orthographicSize = overviewOrthographicSize;
        targetCamera.fieldOfView = overviewFieldOfView;
        UpdateButtonStates();
    }

    public void ShowTruck(int truckIndex)
    {
        CancelTrashCanNavigation();
        RefreshTrucks();

        if (truckIndex < 0 || truckIndex >= trucks.Count || trucks[truckIndex] == null)
        {
            return;
        }

        selectedTruck = trucks[truckIndex];
        followVelocity = Vector3.zero;
        UpdateButtonStates();
    }

    public void ReleaseCameraControl()
    {
        selectedTruck = null;
        followVelocity = Vector3.zero;
        UpdateButtonStates();
    }

    private IEnumerator RefreshTrucksRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.1f, truckRefreshIntervalSeconds));

        while (enabled)
        {
            RefreshTrucks();
            UpdateButtonStates();
            yield return wait;
        }
    }

    private void RefreshTrucks()
    {
        trucks.Clear();

        TrashCollectionDispatcher dispatcher = FindAnyObjectByType<TrashCollectionDispatcher>();
        if (dispatcher != null)
        {
            AddTrucks(dispatcher.trucks);
        }

        AddTrucks(FindObjectsByType<GarbageTruckController>(FindObjectsInactive.Exclude));
        trucks.Sort((left, right) => string.CompareOrdinal(left.name, right.name));

        while (trucks.Count > maxTruckButtons)
        {
            trucks.RemoveAt(trucks.Count - 1);
        }

        if (selectedTruck != null && !trucks.Contains(selectedTruck))
        {
            ShowOverview();
        }
    }

    private void AddTrucks(IReadOnlyList<GarbageTruckController> source)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            GarbageTruckController truck = source[i];
            if (truck != null && !trucks.Contains(truck))
            {
                trucks.Add(truck);
            }
        }
    }

    private void CaptureOverviewView()
    {
        if (targetCamera == null)
        {
            return;
        }

        overviewPosition = targetCamera.transform.position;
        overviewRotation = targetCamera.transform.rotation;
        overviewOrthographic = targetCamera.orthographic;
        overviewOrthographicSize = targetCamera.orthographicSize;
        overviewFieldOfView = targetCamera.fieldOfView;
    }

    private void CreateRuntimeUi()
    {
        EnsureEventSystem();

        Canvas canvas = CreateCanvas();
        RectTransform panel = CreatePanel(canvas.transform);

        overviewButton = CreateButton(panel, "전체", 0);
        overviewButton.onClick.AddListener(ShowOverview);

        truckButtons = new Button[maxTruckButtons];
        truckButtonLabels = new Text[maxTruckButtons];
        for (int i = 0; i < maxTruckButtons; i++)
        {
            int truckIndex = i;
            Button button = CreateButton(panel, $"트럭 {i + 1}", i + 1);
            button.onClick.AddListener(() => ShowTruck(truckIndex));
            truckButtons[i] = button;
            truckButtonLabels[i] = button.GetComponentInChildren<Text>();
        }

        UpdateButtonStates();
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Truck Camera View Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvas.pixelPerfect = true;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private RectTransform CreatePanel(Transform parent)
    {
        GameObject panelObject = new GameObject("Truck Camera Buttons");
        panelObject.transform.SetParent(parent, false);

        RectTransform rectTransform = panelObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rectTransform.anchorMax = new Vector2(0f, 0.5f);
        rectTransform.pivot = new Vector2(0f, 0.5f);
        rectTransform.anchoredPosition = panelOffset;

        VerticalLayoutGroup layoutGroup = panelObject.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = buttonSpacing;
        layoutGroup.childAlignment = TextAnchor.MiddleLeft;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return rectTransform;
    }

    private Button CreateButton(Transform parent, string label, int order)
    {
        GameObject buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = buttonSize;

        Image image = buttonObject.AddComponent<Image>();
        image.color = normalButtonColor;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.95f, 1f, 1f);
        colors.pressedColor = new Color(0.75f, 0.86f, 1f, 1f);
        colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.45f);
        button.colors = colors;

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObject.AddComponent<Text>();
        text.text = label;
        Font resolvedFont = GetButtonFont();
        if (resolvedFont != null)
        {
            text.font = resolvedFont;
        }

        text.fontSize = buttonFontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.alignByGeometry = true;
        text.color = Color.white;

        buttonObject.transform.SetSiblingIndex(order);
        return button;
    }

    private Font GetButtonFont()
    {
        if (buttonFont != null)
        {
            return buttonFont;
        }

        Font arialFont = TryGetBuiltinFont("Arial.ttf");
        if (arialFont != null)
        {
            return arialFont;
        }

        return TryGetBuiltinFont("LegacyRuntime.ttf");
    }

    private Font TryGetBuiltinFont(string fontName)
    {
        try
        {
            return Resources.GetBuiltinResource<Font>(fontName);
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    private void UpdateButtonStates()
    {
        SetButtonSelected(overviewButton, selectedTruck == null);

        if (truckButtons == null)
        {
            return;
        }

        for (int i = 0; i < truckButtons.Length; i++)
        {
            bool hasTruck = i < trucks.Count && trucks[i] != null;
            truckButtons[i].interactable = hasTruck;

            if (truckButtonLabels != null && i < truckButtonLabels.Length && truckButtonLabels[i] != null)
            {
                truckButtonLabels[i].text = $"트럭 {i + 1}";
            }

            SetButtonSelected(truckButtons[i], hasTruck && selectedTruck == trucks[i]);
        }
    }

    private void SetButtonSelected(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = selected ? selectedButtonColor : normalButtonColor;
        }
    }

    private void CancelTrashCanNavigation()
    {
        TrashCanCameraNavigator navigator = FindAnyObjectByType<TrashCanCameraNavigator>();
        if (navigator != null)
        {
            navigator.CancelNavigation();
        }
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }
}
