using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// 1인칭 HUD: 스캔 UI, 상태 줄, 임무/도감 패널
public class FirstPersonHUD : MonoBehaviour
{
    #region 인스펙터 설정

    [Header("참조 (비워두면 자동 탐색)")]
    public SubmarineScanner scanner;
    public SubmarineController submarine;
    public Camera targetCamera;

    [Header("스캔 UI")]
    // 스캔 박스 (크기는 스캐너 설정에 맞춰 코드가 조절)
    public RectTransform scanBox;

    // 박스 테두리 4개 (상태에 따라 색 변경)
    public Graphic[] boxEdges;

    // 박스 위쪽: 대상 이름과 거리
    public TMP_Text targetText;

    // 박스 아래쪽: 상태 문구 ("대상 식별 중..." 등)
    public TMP_Text statusText;

    // 식별 진행 막대
    public GameObject progressRoot;
    public Image progressFill;

    [Header("상태 표시")]
    public TMP_Text compactStatusText;
    public TMP_Text headingText;

    [Header("임무 / 도감")]
    public Key missionKey = Key.J;
    public Key codexKey = Key.K;

    public GameObject missionPanel;
    public TMP_Text missionBody;

    public GameObject codexPanel;
    public TMP_Text codexBody;

    // 임무 내용이 구현되기 전까지 보여줄 문구
    [TextArea]
    public string missionPlaceholder = "임무 내용이 아직 없습니다.";

    [Header("색상")]
    public Color normalColor = new Color(0.85f, 0.95f, 1f, 1f);
    public Color idleColor = new Color(1f, 1f, 1f, 0.45f);
    public Color accentColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color completeColor = new Color(0.4f, 0.9f, 0.6f, 1f);

    #endregion

    #region 내부 변수

    private SubmarineSystems systems;
    private RectTransform canvasRect;
    private float northOffset;
    private float infoTimer;
    private int lastCodexCount = -1;

    private readonly StringBuilder sb = new StringBuilder();

    private const float InfoInterval = 0.1f;

    #endregion

    void Awake()
    {
        if (submarine == null)
            submarine = FindAnyObjectByType<SubmarineController>();

        if (scanner == null)
            scanner = FindAnyObjectByType<SubmarineScanner>();

        if (scanner == null)
            Debug.LogWarning("FirstPersonHUD: SubmarineScanner를 찾지 못했습니다. 잠수함에 추가하세요.", this);

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (submarine != null)
            systems = submarine.GetComponentInChildren<SubmarineSystems>();
        if (systems == null)
            systems = FindAnyObjectByType<SubmarineSystems>();

        // 방위 보정값은 3인칭 HUD와 같은 값을 사용
        SubmarineHUD thirdPersonHud = FindAnyObjectByType<SubmarineHUD>();
        if (thirdPersonHud != null)
            northOffset = thirdPersonHud.northOffset;

        if (scanBox != null)
        {
            Canvas canvas = scanBox.GetComponentInParent<Canvas>();
            if (canvas != null)
                canvasRect = canvas.rootCanvas.GetComponent<RectTransform>();
        }

        SetActive(missionPanel, false);
        SetActive(codexPanel, false);
    }

    // 3인칭으로 돌아가 이 HUD가 꺼질 때 패널을 닫고 스캔 입력 제한을 해제
    void OnDisable()
    {
        SetActive(missionPanel, false);
        SetActive(codexPanel, false);

        if (scanner != null)
            scanner.inputBlocked = false;
    }

    void Update()
    {
        HandlePanelKeys();

        bool panelOpen = IsActive(missionPanel) || IsActive(codexPanel);

        // 패널이나 전체 지도가 열려 있으면 스캔 중단
        if (scanner != null)
            scanner.inputBlocked = panelOpen || SubmarineMinimap.IsMapOpen;

        UpdateScanUI(panelOpen);
        UpdateHeading();

        infoTimer += Time.unscaledDeltaTime;
        if (infoTimer >= InfoInterval)
        {
            infoTimer = 0f;
            UpdateCompactStatus();
        }
    }

    #region 스캔 UI

    private void UpdateScanUI(bool panelOpen)
    {
        if (scanBox == null || scanner == null)
            return;

        // 스캔 박스 크기를 스캐너의 판정 영역과 맞춤
        if (canvasRect != null)
        {
            float size = canvasRect.rect.height * scanner.scanBoxScreenRatio;
            scanBox.sizeDelta = new Vector2(size, size);
        }

        // 패널이 열려 있으면 스캔 UI 숨김
        scanBox.gameObject.SetActive(!panelOpen);
        if (panelOpen)
            return;

        Color boxColor = idleColor;
        Color statusColor = accentColor;
        string target = "";
        string status = "";
        bool showProgress = false;

        switch (scanner.State)
        {
            case ScanState.Ready:
                boxColor = accentColor;
                target = TargetLabel();
                status = "F 길게 눌러 식별";
                break;

            case ScanState.Scanning:
                boxColor = accentColor;
                target = TargetLabel();
                status = "대상 식별 중...";
                showProgress = true;
                break;

            case ScanState.AlreadyScanned:
                target = TargetLabel();
                status = "식별 완료";
                statusColor = normalColor;
                break;
        }

        // 방금 식별을 마쳤다면 완료 메시지를 우선 표시
        if (scanner.CompletedTimeLeft > 0f && scanner.LastCompleted != null)
        {
            boxColor = completeColor;
            statusColor = completeColor;
            status = $"식별 완료: {scanner.LastCompleted.displayName}";
            showProgress = false;
        }

        foreach (Graphic edge in boxEdges)
        {
            if (edge != null)
                edge.color = boxColor;
        }

        SetText(targetText, target);
        SetText(statusText, status);
        if (statusText != null)
            statusText.color = statusColor;

        SetActive(progressRoot, showProgress);
        if (showProgress && progressFill != null)
        {
            RectTransform rt = progressFill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp01(scanner.Progress), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    // 식별 전에는 이름을 숨기고 "미확인 대상"으로 표시
    private string TargetLabel()
    {
        Scannable t = scanner.Target;
        if (t == null)
            return "";

        string label = t.IsScanned ? t.displayName : "미확인 대상";
        return $"{label}  {scanner.TargetDistance:F0} m";
    }

    #endregion

    #region 상태 표시

    private void UpdateCompactStatus()
    {
        if (compactStatusText == null || submarine == null)
            return;

        float battery = systems != null ? systems.battery : 0f;
        bool lightOn = systems != null && systems.isLightOn;

        compactStatusText.text =
            $"DEPTH {submarine.depth:F0} m    BAT {battery:F0} %    LIGHT {(lightOn ? "ON" : "OFF")}";
    }

    private void UpdateHeading()
    {
        if (headingText == null || targetCamera == null)
            return;

        float heading = Mathf.Repeat(
            targetCamera.transform.eulerAngles.y - northOffset, 360f);

        string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        string dir = names[Mathf.RoundToInt(heading / 45f) % 8];

        headingText.text = $"{Mathf.RoundToInt(heading) % 360:000}°  {dir}";
    }

    #endregion

    #region 임무 / 도감 패널

    private void HandlePanelKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // J: 임무 패널 토글 (열면 도감은 닫음)
        if (keyboard[missionKey].wasPressedThisFrame)
        {
            bool open = !IsActive(missionPanel);
            SetActive(missionPanel, open);
            SetActive(codexPanel, false);

            if (open)
                SetText(missionBody, missionPlaceholder);
        }

        // K: 도감 패널 토글 (열면 임무는 닫음)
        if (keyboard[codexKey].wasPressedThisFrame)
        {
            bool open = !IsActive(codexPanel);
            SetActive(codexPanel, open);
            SetActive(missionPanel, false);

            if (open)
                RefreshCodex();
        }

        // 도감이 열린 동안 새로 식별하면 목록 갱신
        if (IsActive(codexPanel) && scanner != null &&
            scanner.ScannedTargets.Count != lastCodexCount)
        {
            RefreshCodex();
        }
    }

    // 지금은 식별한 대상 이름과 설명만 나열. 도감 시스템이 생기면 이 함수를 교체
    private void RefreshCodex()
    {
        if (codexBody == null || scanner == null)
            return;

        lastCodexCount = scanner.ScannedTargets.Count;

        sb.Clear();

        if (lastCodexCount == 0)
        {
            sb.Append("아직 식별한 대상이 없습니다.");
        }
        else
        {
            sb.Append("식별한 대상: ").Append(lastCodexCount).AppendLine("\n");

            foreach (Scannable t in scanner.ScannedTargets)
            {
                sb.Append("• ").AppendLine(t.displayName);

                if (!string.IsNullOrEmpty(t.description))
                    sb.Append("   ").AppendLine(t.description);
            }
        }

        codexBody.text = sb.ToString();
    }

    #endregion

    #region 도우미

    private static bool IsActive(GameObject go)
    {
        return go != null && go.activeSelf;
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }

    #endregion
}