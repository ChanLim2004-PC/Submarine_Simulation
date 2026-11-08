using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class SubmarineMinimap : MonoBehaviour
{
    // 전체 지도가 열려 있는지 (카메라 등 다른 스크립트가 마우스 입력을 막을 때 사용)
    public static bool IsMapOpen { get; private set; }

    #region 인스펙터 설정

    [Header("참조")]
    // 잠수함 (비워두면 자동 탐색)
    public SubmarineController submarine;

    // 한글 표시가 필요하면 한글 TMP 폰트 에셋을 지정
    public TMP_FontAsset font;

    [Header("지도 범위")]
    // 원점 기준 지도의 반경 (m). 가로/세로 ±이 값
    public float mapHalfSize = 1000f;

    // 격자선 간격 (m). 원점 기준으로 ±간격마다 선을 그림
    public float gridSpacing = 500f;

    [Header("미니맵")]
    // 미니맵 한 변 크기 (1920x1080 기준 px)
    public float minimapSize = 300f;

    // 화면 왼쪽 위 모서리에서의 여백 (px)
    public Vector2 minimapMargin = new Vector2(30f, 30f);

    // 미니맵 중심(잠수함)에서 가장자리까지 보이는 거리 (m)
    public float minimapRange = 300f;

    [Header("전체 지도")]
    // 전체 지도 열기/닫기 키
    public Key fullMapKey = Key.M;

    // 화면의 짧은 변 대비 지도 크기 비율
    [Range(0.5f, 1f)]
    public float fullMapScreenRatio = 0.92f;

    // 최대 확대 배율 (1 = 전체가 꽉 찬 기본 상태)
    public float maxZoom = 10f;

    // 휠 감도 (클수록 한 번에 많이 확대/축소)
    public float zoomSensitivity = 0.01f;

    // 같은 핑으로 인정하는 클릭 반경 (화면 px)
    public float pingRemoveRadiusPixels = 18f;

    [Header("색상")]
    public Color frameColor = new Color(0.4f, 0.7f, 0.85f, 1f);
    public Color panelColor = new Color(0.02f, 0.05f, 0.08f, 0.9f);
    public Color mapColor = new Color(0.07f, 0.2f, 0.3f, 1f);
    public Color gridColor = new Color(1f, 1f, 1f, 0.25f);
    public Color originLineColor = new Color(1f, 1f, 1f, 0.55f);
    public Color dimColor = new Color(0f, 0f, 0f, 0.55f);
    public Color submarineColor = new Color(0.3f, 0.9f, 1f, 1f);
    public Color pingColor = new Color(1f, 0.55f, 0.2f, 1f);

    #endregion

    #region 내부 변수

    private class Ping
    {
        public Vector2 pos;          // 월드 (x, z)
        public RectTransform rt;
        public RectTransform labelRt;
        public TMP_Text label;
        public int lastDistance = -1;
    }

    private const float Border = 2f;          // 테두리 두께 (px)
    private const float LineThickness = 2f;   // 격자선 두께 (px)
    private const float PingEdgeInset = 14f;  // 범위 밖 핑을 가장자리에서 띄우는 거리 (px)

    private Canvas canvas;
    private RectTransform root;       // 테두리 (크기/위치를 바꾸는 대상)
    private RectTransform panel;      // 테두리 안쪽, 눈금/핑이 그려지는 영역
    private RectTransform mapBg;      // ±mapHalfSize 범위를 나타내는 배경
    private RectTransform subIcon;
    private Image dim;

    private readonly List<RectTransform> vLines = new List<RectTransform>();
    private readonly List<RectTransform> hLines = new List<RectTransform>();
    private readonly List<Ping> pings = new List<Ping>();

    private bool fullMap;
    private float zoom = 1f;
    private Vector2 center;     // 전체 지도에서 화면 중심에 오는 월드 좌표 (x, z)
    private bool dragging;

    #endregion

    #region 유니티 메시지

    void Awake()
    {
        IsMapOpen = false;

        if (submarine == null)
            submarine = FindAnyObjectByType<SubmarineController>();

        if (submarine == null)
        {
            Debug.LogError("SubmarineMinimap: 잠수함을 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        BuildUI();
        SetFullMap(false);
    }

    void OnDestroy()
    {
        IsMapOpen = false;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null && keyboard[fullMapKey].wasPressedThisFrame)
            SetFullMap(!fullMap);

        if (fullMap && mouse != null)
            HandleMapInput(mouse);

        Refresh();
    }

    #endregion

    #region 입력

    private void HandleMapInput(Mouse mouse)
    {
        Vector2 mousePos = mouse.position.ReadValue();
        bool inside = RectTransformUtility.RectangleContainsScreenPoint(panel, mousePos, null);

        // 휠: 커서가 가리키는 지점을 기준으로 확대/축소
        float scroll = mouse.scroll.ReadValue().y;
        if (inside && Mathf.Abs(scroll) > 0.01f)
        {
            Vector2 before = ScreenToWorld(mousePos);
            zoom = Mathf.Clamp(zoom * Mathf.Exp(scroll * zoomSensitivity), 1f, maxZoom);
            Vector2 after = ScreenToWorld(mousePos);

            center += before - after;
            ClampCenter();
        }

        // 좌클릭 드래그: 지도 이동
        if (mouse.leftButton.wasPressedThisFrame && inside)
            dragging = true;
        if (!mouse.leftButton.isPressed)
            dragging = false;

        if (dragging)
        {
            Vector2 delta = mouse.delta.ReadValue() / canvas.scaleFactor;
            float halfExtent = mapHalfSize / zoom;

            center -= delta / PanelHalf() * halfExtent;
            ClampCenter();
        }

        // 우클릭: 핑 생성 / 같은 위치면 제거
        if (mouse.rightButton.wasPressedThisFrame && inside)
        {
            Vector2 world = ScreenToWorld(mousePos);

            if (Mathf.Abs(world.x) <= mapHalfSize && Mathf.Abs(world.y) <= mapHalfSize)
                TogglePing(world);
        }
    }

    // 지도 밖으로 나가지 않도록 중심 제한
    private void ClampCenter()
    {
        float limit = Mathf.Max(0f, mapHalfSize - mapHalfSize / zoom);
        center.x = Mathf.Clamp(center.x, -limit, limit);
        center.y = Mathf.Clamp(center.y, -limit, limit);
    }

    private void SetFullMap(bool on)
    {
        fullMap = on;
        IsMapOpen = on;
        dim.enabled = on;
        dragging = false;

        // 열 때는 항상 전체가 꽉 찬 기본 상태로 시작
        zoom = 1f;
        center = Vector2.zero;
    }

    #endregion

    #region 핑

    private void TogglePing(Vector2 world)
    {
        // 클릭 반경(px)을 월드 거리(m)로 환산
        float halfExtent = mapHalfSize / zoom;
        float threshold =
            pingRemoveRadiusPixels / canvas.scaleFactor / PanelHalf() * halfExtent;

        // 가까운 기존 핑이 있으면 제거
        Ping nearest = null;
        float best = threshold;
        foreach (Ping p in pings)
        {
            float d = Vector2.Distance(p.pos, world);
            if (d <= best)
            {
                best = d;
                nearest = p;
            }
        }

        if (nearest != null)
        {
            Destroy(nearest.rt.gameObject);
            pings.Remove(nearest);
            return;
        }

        AddPing(world);
    }

    private void AddPing(Vector2 world)
    {
        GameObject go = new GameObject("Ping", typeof(RectTransform));
        go.transform.SetParent(panel, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        Center(rt);
        rt.sizeDelta = Vector2.zero;

        // 마름모 마커
        Image marker = CreateImage("Marker", rt, pingColor);
        Center(marker.rectTransform);
        marker.rectTransform.sizeDelta = new Vector2(14f, 14f);
        marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        // 거리 텍스트
        TMP_Text label = CreateText("Distance", rt, 18f);
        label.color = Color.white;
        Center(label.rectTransform);
        label.rectTransform.sizeDelta = new Vector2(160f, 26f);

        pings.Add(new Ping
        {
            pos = world,
            rt = rt,
            label = label,
            labelRt = label.rectTransform
        });

        // 잠수함 아이콘이 핑 위에 그려지도록
        subIcon.SetAsLastSibling();
    }

    private string FormatDistance(int meters)
    {
        return meters >= 1000 ? $"{meters / 1000f:F2} km" : $"{meters} m";
    }

    #endregion

    #region 갱신

    private void Refresh()
    {
        ApplyLayout();

        GetView(out Vector2 c, out float halfExtent);

        float half = PanelHalf();
        float s = half / halfExtent;     // 1m당 UI 픽셀 수
        float mapSpan = 2f * mapHalfSize * s;

        // 지도 배경 (±mapHalfSize 영역)
        mapBg.anchoredPosition = -c * s;
        mapBg.sizeDelta = new Vector2(mapSpan, mapSpan);

        // 격자선
        for (int i = 0; i < vLines.Count; i++)
        {
            float v = -mapHalfSize + i * gridSpacing;

            vLines[i].anchoredPosition = new Vector2((v - c.x) * s, -c.y * s);
            vLines[i].sizeDelta = new Vector2(LineThickness, mapSpan);

            hLines[i].anchoredPosition = new Vector2(-c.x * s, (v - c.y) * s);
            hLines[i].sizeDelta = new Vector2(mapSpan, LineThickness);
        }

        // 잠수함 아이콘 (북쪽 = 월드 +Z = 화면 위, 선수 방향으로 회전)
        Vector3 p = submarine.transform.position;
        Vector2 subPos = new Vector2(p.x, p.z);

        subIcon.anchoredPosition = (subPos - c) * s;
        subIcon.localRotation =
            Quaternion.Euler(0f, 0f, -submarine.transform.eulerAngles.y);

        // 핑 (범위 밖이면 같은 방향의 가장자리에 표시)
        float limit = half - PingEdgeInset;
        foreach (Ping ping in pings)
        {
            Vector2 local = (ping.pos - c) * s;
            float m = Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y));
            if (m > limit)
                local *= limit / m;

            ping.rt.anchoredPosition = local;

            // 아래쪽 가장자리에서는 텍스트가 잘리지 않도록 위로
            bool nearBottom = local.y < -(half - 40f);
            ping.labelRt.anchoredPosition = new Vector2(0f, nearBottom ? 22f : -22f);

            int distance = Mathf.RoundToInt(Vector2.Distance(subPos, ping.pos));
            if (distance != ping.lastDistance)
            {
                ping.lastDistance = distance;
                ping.label.text = FormatDistance(distance);
            }
        }
    }

    // 미니맵 / 전체 지도에 따라 위치와 크기 설정
    private void ApplyLayout()
    {
        if (fullMap)
        {
            float size =
                Mathf.Min(Screen.width, Screen.height) / canvas.scaleFactor * fullMapScreenRatio;

            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(size, size);
        }
        else
        {
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(minimapMargin.x, -minimapMargin.y);
            root.sizeDelta = new Vector2(minimapSize, minimapSize);
        }
    }

    // 현재 보여줄 영역: 미니맵은 잠수함 중심, 전체 지도는 줌/이동 상태
    private void GetView(out Vector2 c, out float halfExtent)
    {
        if (fullMap)
        {
            c = center;
            halfExtent = mapHalfSize / zoom;
        }
        else
        {
            Vector3 p = submarine.transform.position;
            c = new Vector2(p.x, p.z);
            halfExtent = minimapRange;
        }
    }

    // 안쪽 영역의 절반 크기 (UI px)
    private float PanelHalf()
    {
        return root.sizeDelta.x * 0.5f - Border;
    }

    // 화면 좌표 -> 월드 (x, z)
    private Vector2 ScreenToWorld(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panel, screen, null, out Vector2 local);

        GetView(out Vector2 c, out float halfExtent);
        return c + local / PanelHalf() * halfExtent;
    }

    #endregion

    #region UI 생성

    private void BuildUI()
    {
        // 캔버스 (HUD보다 위에 그림)
        GameObject canvasObj = new GameObject("Minimap_Canvas",
            typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(transform, false);

        canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 전체 지도일 때 뒤를 어둡게
        dim = CreateImage("Dim", canvasObj.transform, dimColor);
        Stretch(dim.rectTransform, 0f);

        // 테두리 + 안쪽 영역 (넘치는 부분은 RectMask2D로 잘라냄)
        Image frame = CreateImage("MapFrame", canvasObj.transform, frameColor);
        root = frame.rectTransform;

        GameObject panelObj = new GameObject("Panel",
            typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        panelObj.transform.SetParent(root, false);
        panel = panelObj.GetComponent<RectTransform>();
        Stretch(panel, Border);

        Image panelImage = panelObj.GetComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = false;

        // 지도 배경
        mapBg = CreateImage("Map", panel, mapColor).rectTransform;
        Center(mapBg);

        // 격자선 (-mapHalfSize ~ +mapHalfSize, gridSpacing 간격)
        int lineCount = Mathf.RoundToInt(2f * mapHalfSize / gridSpacing) + 1;
        for (int i = 0; i < lineCount; i++)
        {
            float v = -mapHalfSize + i * gridSpacing;
            Color color = Mathf.Approximately(v, 0f) ? originLineColor : gridColor;

            RectTransform vLine = CreateImage($"Grid_X_{v}", panel, color).rectTransform;
            Center(vLine);
            vLines.Add(vLine);

            RectTransform hLine = CreateImage($"Grid_Z_{v}", panel, color).rectTransform;
            Center(hLine);
            hLines.Add(hLine);
        }

        // 잠수함 아이콘 (몸통 + 선수 표시)
        GameObject iconObj = new GameObject("SubmarineIcon", typeof(RectTransform));
        iconObj.transform.SetParent(panel, false);
        subIcon = iconObj.GetComponent<RectTransform>();
        Center(subIcon);
        subIcon.sizeDelta = Vector2.zero;

        Image body = CreateImage("Body", subIcon, submarineColor);
        Center(body.rectTransform);
        body.rectTransform.sizeDelta = new Vector2(10f, 16f);

        Image nose = CreateImage("Nose", subIcon, Color.white);
        Center(nose.rectTransform);
        nose.rectTransform.sizeDelta = new Vector2(4f, 6f);
        nose.rectTransform.anchoredPosition = new Vector2(0f, 10f);
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TMP_Text CreateText(string name, Transform parent, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        if (font != null)
            t.font = font;
        return t;
    }

    // 앵커/피벗을 가운데로
    private void Center(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
    }

    // 부모를 꽉 채우고 사방에 같은 여백
    private void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    #endregion
}