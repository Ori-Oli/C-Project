using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TrashCanCameraNavigator : MonoBehaviour
{
    [Header("Camera")]
    public Camera targetCamera;
    public Vector3 initialCameraPosition = new Vector3(25f, 45f, -25f);
    public Vector3 initialLookAt = new Vector3(25f, 0f, 25f);
    [Min(0.1f)] public float frontDistance = 3.5f;
    [Min(0.1f)] public float frontHeight = 2.2f;
    [Min(0.01f)] public float lookAtHeight = 0.8f;
    [Min(0f)] public float moveDuration = 0.35f;
    [Min(1f)] public float navigationFieldOfView = 55f;
    public float cameraDepth = 10f;

    [Header("Runtime UI")]
    public bool createRuntimeUi = true;
    public Vector2 panelOffset = new Vector2(-24f, -24f);
    public Vector2 inputSize = new Vector2(180f, 44f);
    public Vector2 buttonSize = new Vector2(92f, 44f);
    [Min(8)] public int fontSize = 20;
    public Font font;
    public Color panelColor = new Color(0.06f, 0.07f, 0.09f, 0.82f);
    public Color inputColor = new Color(1f, 1f, 1f, 0.96f);
    public Color buttonColor = new Color(0.05f, 0.42f, 0.85f, 0.95f);
    public Color statusColor = Color.white;

    private const string NavigatorObjectName = "TrashCan Camera Navigator";
    private readonly List<TrashCanStatus> sortedTrashCans = new List<TrashCanStatus>();
    private InputField trashCanInput;
    private Text statusLabel;
    private Coroutine moveRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<TrashCanCameraNavigator>() != null)
        {
            return;
        }

        GameObject navigatorObject = new GameObject(NavigatorObjectName);
        navigatorObject.AddComponent<TrashCanCameraNavigator>();
    }

    private void Awake()
    {
        bool createdNavigationCamera = false;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            targetCamera = CreateNavigationCamera();
            createdNavigationCamera = true;
        }

        if (createdNavigationCamera)
        {
            PositionCamera(initialCameraPosition, initialLookAt);
        }

        if (createRuntimeUi)
        {
            CreateRuntimeUi();
        }
    }

    private void OnEnable()
    {
        CityGenerator generator = FindAnyObjectByType<CityGenerator>();
        if (generator != null)
        {
            generator.CityGenerated += HandleCityGenerated;
        }

        StartCoroutine(RefreshTrashCansNextFrame());
    }

    private void OnDisable()
    {
        CityGenerator generator = FindAnyObjectByType<CityGenerator>();
        if (generator != null)
        {
            generator.CityGenerated -= HandleCityGenerated;
        }
    }

    public void MoveToTrashCanNumber(string rawNumber)
    {
        if (string.IsNullOrWhiteSpace(rawNumber))
        {
            SetStatus("번호를 입력하세요.");
            return;
        }

        if (!TrySelectTrashCan(rawNumber.Trim(), out TrashCanStatus trashCan, out string failureMessage))
        {
            SetStatus(failureMessage);
            return;
        }

        MoveToTrashCan(trashCan);
    }

    public void MoveToTrashCan(int trashCanNumber)
    {
        MoveToTrashCanNumber(trashCanNumber.ToString());
    }

    public void CancelNavigation()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }
    }

    private Camera CreateNavigationCamera()
    {
        GameObject cameraObject = new GameObject("TrashCan Navigation Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.depth = cameraDepth;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 1000f;
        camera.fieldOfView = navigationFieldOfView;

        if (FindAnyObjectByType<AudioListener>() == null)
        {
            cameraObject.AddComponent<AudioListener>();
        }

        return camera;
    }

    private IEnumerator RefreshTrashCansNextFrame()
    {
        yield return null;
        RefreshTrashCans();
        SetStatus(sortedTrashCans.Count > 0 ? $"1-{sortedTrashCans.Count}" : "쓰레기통 없음");
    }

    private void HandleCityGenerated()
    {
        RefreshTrashCans();
        SetStatus(sortedTrashCans.Count > 0 ? $"1-{sortedTrashCans.Count}" : "쓰레기통 없음");
    }

    private bool TrySelectTrashCan(string input, out TrashCanStatus trashCan, out string failureMessage)
    {
        RefreshTrashCans();
        trashCan = null;

        if (sortedTrashCans.Count == 0)
        {
            failureMessage = "쓰레기통 없음";
            return false;
        }

        if (TryParseGridPosition(input, out Vector2Int gridPosition))
        {
            for (int i = 0; i < sortedTrashCans.Count; i++)
            {
                if (sortedTrashCans[i].gridPosition == gridPosition)
                {
                    trashCan = sortedTrashCans[i];
                    failureMessage = null;
                    return true;
                }
            }

            failureMessage = $"좌표 없음: {gridPosition.x},{gridPosition.y}";
            return false;
        }

        if (!int.TryParse(input, out int number))
        {
            failureMessage = "숫자만 입력하세요.";
            return false;
        }

        int index = number - 1;
        if (index < 0 || index >= sortedTrashCans.Count)
        {
            failureMessage = $"범위: 1-{sortedTrashCans.Count}";
            return false;
        }

        trashCan = sortedTrashCans[index];
        failureMessage = null;
        return true;
    }

    private bool TryParseGridPosition(string input, out Vector2Int gridPosition)
    {
        gridPosition = Vector2Int.zero;
        string[] parts = input.Split(',');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0].Trim(), out int x) || !int.TryParse(parts[1].Trim(), out int y))
        {
            return false;
        }

        gridPosition = new Vector2Int(x, y);
        return true;
    }

    private void RefreshTrashCans()
    {
        sortedTrashCans.Clear();
        TrashCanStatus[] trashCans = FindObjectsByType<TrashCanStatus>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < trashCans.Length; i++)
        {
            if (trashCans[i] != null)
            {
                sortedTrashCans.Add(trashCans[i]);
            }
        }

        sortedTrashCans.Sort(CompareTrashCans);
    }

    private int CompareTrashCans(TrashCanStatus left, TrashCanStatus right)
    {
        int yCompare = left.gridPosition.y.CompareTo(right.gridPosition.y);
        if (yCompare != 0)
        {
            return yCompare;
        }

        int xCompare = left.gridPosition.x.CompareTo(right.gridPosition.x);
        if (xCompare != 0)
        {
            return xCompare;
        }

        return string.CompareOrdinal(left.name, right.name);
    }

    private void MoveToTrashCan(TrashCanStatus trashCan)
    {
        if (targetCamera == null || trashCan == null)
        {
            return;
        }

        ReleaseTruckCameraControl();

        Vector3 frontDirection = GetFrontDirection(trashCan.transform);
        Vector3 targetPosition = trashCan.transform.position + frontDirection * frontDistance + Vector3.up * frontHeight;
        Vector3 targetLookAt = trashCan.transform.position + Vector3.up * lookAtHeight;

        targetCamera.orthographic = false;
        targetCamera.fieldOfView = navigationFieldOfView;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        moveRoutine = StartCoroutine(MoveCameraRoutine(targetPosition, targetLookAt));

        int number = sortedTrashCans.IndexOf(trashCan) + 1;
        SetStatus(number > 0
            ? $"{number}: {trashCan.gridPosition.x},{trashCan.gridPosition.y}"
            : $"{trashCan.gridPosition.x},{trashCan.gridPosition.y}");
    }

    private IEnumerator MoveCameraRoutine(Vector3 targetPosition, Vector3 targetLookAt)
    {
        Vector3 startPosition = targetCamera.transform.position;
        Quaternion startRotation = targetCamera.transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(targetLookAt - targetPosition, Vector3.up);

        if (moveDuration <= 0f)
        {
            PositionCamera(targetPosition, targetLookAt);
            moveRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            float eased = t * t * (3f - 2f * t);

            targetCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, eased);
            targetCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);
            yield return null;
        }

        PositionCamera(targetPosition, targetLookAt);
        moveRoutine = null;
    }

    private Vector3 GetFrontDirection(Transform trashCan)
    {
        Transform front = FindFrontTransform(trashCan);
        if (front != null)
        {
            Vector3 direction = front.position - trashCan.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }
        }

        Vector3 fallback = trashCan.forward;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.forward;
    }

    private void ReleaseTruckCameraControl()
    {
        TruckCameraViewSwitcher switcher = targetCamera.GetComponent<TruckCameraViewSwitcher>();
        if (switcher == null)
        {
            switcher = FindAnyObjectByType<TruckCameraViewSwitcher>();
        }

        if (switcher != null)
        {
            switcher.ReleaseCameraControl();
        }
    }

    private Transform FindFrontTransform(Transform root)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>();
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name.Contains("Front") || children[i].name.Contains("front"))
            {
                return children[i];
            }
        }

        return null;
    }

    private void PositionCamera(Vector3 position, Vector3 lookAt)
    {
        if (targetCamera == null)
        {
            return;
        }

        targetCamera.transform.position = position;
        targetCamera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
    }

    private void CreateRuntimeUi()
    {
        EnsureEventSystem();

        Canvas canvas = CreateCanvas();
        RectTransform panel = CreatePanel(canvas.transform);
        trashCanInput = CreateInputField(panel);
        Button moveButton = CreateButton(panel, "이동");
        statusLabel = CreateStatusLabel(panel);

        trashCanInput.onEndEdit.AddListener(HandleInputSubmitted);
        moveButton.onClick.AddListener(() => MoveToTrashCanNumber(trashCanInput.text));
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("TrashCan Camera Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
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
        GameObject panelObject = new GameObject("TrashCan Camera Input Panel");
        panelObject.transform.SetParent(parent, false);

        RectTransform rectTransform = panelObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.anchoredPosition = panelOffset;

        Image image = panelObject.AddComponent<Image>();
        image.color = panelColor;

        HorizontalLayoutGroup layout = panelObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return rectTransform;
    }

    private InputField CreateInputField(Transform parent)
    {
        GameObject inputObject = new GameObject("TrashCan Number Input");
        inputObject.transform.SetParent(parent, false);

        RectTransform rectTransform = inputObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = inputSize;

        Image image = inputObject.AddComponent<Image>();
        image.color = inputColor;

        InputField input = inputObject.AddComponent<InputField>();
        input.contentType = InputField.ContentType.IntegerNumber;
        input.lineType = InputField.LineType.SingleLine;

        Text text = CreateText(inputObject.transform, "Text", string.Empty, TextAnchor.MiddleLeft, Color.black);
        text.rectTransform.offsetMin = new Vector2(10f, 0f);
        text.rectTransform.offsetMax = new Vector2(-10f, 0f);
        input.textComponent = text;

        Text placeholder = CreateText(inputObject.transform, "Placeholder", "번호", TextAnchor.MiddleLeft, new Color(0.35f, 0.35f, 0.35f, 0.75f));
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.rectTransform.offsetMin = new Vector2(10f, 0f);
        placeholder.rectTransform.offsetMax = new Vector2(-10f, 0f);
        input.placeholder = placeholder;

        return input;
    }

    private Button CreateButton(Transform parent, string label)
    {
        GameObject buttonObject = new GameObject("TrashCan Move Button");
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = buttonSize;

        Image image = buttonObject.AddComponent<Image>();
        image.color = buttonColor;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.95f, 1f, 1f);
        colors.pressedColor = new Color(0.75f, 0.86f, 1f, 1f);
        button.colors = colors;

        Text text = CreateText(buttonObject.transform, "Label", label, TextAnchor.MiddleCenter, Color.white);
        text.fontStyle = FontStyle.Bold;
        return button;
    }

    private Text CreateStatusLabel(Transform parent)
    {
        GameObject labelObject = new GameObject("TrashCan Camera Status");
        labelObject.transform.SetParent(parent, false);

        RectTransform rectTransform = labelObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(140f, inputSize.y);

        Text text = labelObject.AddComponent<Text>();
        text.text = string.Empty;
        text.font = GetFont();
        text.fontSize = Mathf.Max(12, fontSize - 4);
        text.alignment = TextAnchor.MiddleLeft;
        text.color = statusColor;
        return text;
    }

    private Text CreateText(Transform parent, string objectName, string value, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Text text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = GetFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.alignByGeometry = true;
        text.color = color;
        return text;
    }

    private void HandleInputSubmitted(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            MoveToTrashCanNumber(value);
        }
    }

    private void SetStatus(string message)
    {
        if (statusLabel != null)
        {
            statusLabel.text = message;
        }
    }

    private Font GetFont()
    {
        if (font != null)
        {
            return font;
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
