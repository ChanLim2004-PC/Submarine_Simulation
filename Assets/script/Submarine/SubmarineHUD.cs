using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SubmarineHUD : MonoBehaviour
{
    #region 인스펙터 설정

    [Header("참조")]
    // 잠수함 (비워두면 자동 탐색)
    public SubmarineController submarine;

    // 방위 기준 카메라 (비워두면 Camera.main)
    public Camera targetCamera;

    [Header("모드별 패널")]
    // 3인칭일 때만 켜지는 패널
    public GameObject thirdPersonRoot;

    // 1인칭일 때만 켜지는 패널
    public GameObject firstPersonRoot;

    [Header("3인칭: 주요 수치")]
    public TMP_Text depthText;
    public TMP_Text speedText;

    [Header("3인칭: 게이지")]
    public TMP_Text batteryText;
    public Image batteryFill;
    public TMP_Text buoyancyText;
    public Image buoyancyFill;

    [Header("3인칭: 보조 정보")]
    public TMP_Text lightText;
    public TMP_Text vSpeedText;
    public TMP_Text modeText;
    public TMP_Text positionText;

    [Header("3인칭: 경고")]
    public TMP_Text warningText;

    [Header("3인칭: 방위")]
    // 방위 눈금이 생성될 컨테이너 (RectMask2D 권장)
    public RectTransform compassMarks;

    // 눈금 원본 텍스트 (비활성 상태로 두면 코드가 복제해서 사용)
    public TMP_Text markTemplate;

    // 현재 방위 숫자
    public TMP_Text headingText;

    // 월드 +Z 방향이 북쪽(0°)이 되도록 한 기본값. 다르면 보정 (deg)
    public float northOffset = 0f;

    // 1도당 픽셀 수 (클수록 좁은 각도가 표시됨)
    public float pixelsPerDegree = 4f;

    [Header("부력 게이지")]
    // 이 값(N) 이내면 NEUTRAL로 표시
    public float neutralThresholdNewton = 500f;

    // 게이지가 가득 차는 순부력 (N). 잠수함 질량에 맞춰 조절
    public float buoyancyGaugeMaxNewton = 40000f;

    [Header("경고 기준")]
    // 이 값(%) 미만이면 배터리 경고
    public float lowBatteryPercent = 20f;

    // 하강 속도가 이 값(m/s)을 넘으면 경고
    public float fastDescentSpeed = 2.5f;

    [Header("색상")]
    public Color normalColor = new Color(0.85f, 0.95f, 1f, 1f);
    public Color warningColor = new Color(1f, 0.35f, 0.3f, 1f);
    public Color accentColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color risingColor = new Color(0.4f, 0.9f, 0.6f, 1f);
    public Color sinkingColor = new Color(1f, 0.6f, 0.25f, 1f);

    #endregion

    #region 내부 변수

    private SubmarineViewMode viewMode;
    private SubmarineBuoyancy buoyancy;
    private SubmarineSystems systems;

    private TMP_Text[] markLabels;
    private float[] markDegrees;

    private int lastMode = -1;   // 0 = 3인칭, 1 = 1인칭
    private float infoTimer;
    private string warningMessage = "";

    private const float InfoInterval = 0.1f;   // 정보 갱신 주기 (s)
    private const int MarkStep = 15;           // 방위 눈금 간격 (deg)

    #endregion

    void Awake()
    {
        if (submarine == null)
            submarine = FindAnyObjectByType<SubmarineController>();

        if (submarine == null)
        {
            Debug.LogError("SubmarineHUD: 잠수함을 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        viewMode = submarine.GetComponentInParent<SubmarineViewMode>();
        buoyancy = submarine.GetComponentInParent<SubmarineBuoyancy>();

        systems = submarine.GetComponentInChildren<SubmarineSystems>();
        if (systems == null)
            systems = FindAnyObjectByType<SubmarineSystems>();
        if (systems == null)
            Debug.LogWarning("SubmarineHUD: SubmarineSystems를 찾지 못했습니다.", this);

        if (targetCamera == null)
            targetCamera = Camera.main;

        BuildCompassMarks();
    }

    void Update()
    {
        bool isThirdPerson =
            viewMode == null || viewMode.mode == ViewMode.ThirdPerson;

        // 모드가 바뀌었을 때만 패널 전환
        int mode = isThirdPerson ? 0 : 1;
        if (mode != lastMode)
        {
            lastMode = mode;

            if (thirdPersonRoot != null)
                thirdPersonRoot.SetActive(isThirdPerson);
            if (firstPersonRoot != null)
                firstPersonRoot.SetActive(!isThirdPerson);
        }

        if (!isThirdPerson)
            return;

        UpdateCompass();
        UpdateWarningBlink();

        infoTimer += Time.unscaledDeltaTime;
        if (infoTimer >= InfoInterval)
        {
            infoTimer = 0f;
            UpdateInfo();
        }
    }

    #region 갱신

    // 카메라가 바라보는 방향으로 방위 눈금 갱신
    private void UpdateCompass()
    {
        if (targetCamera == null || markLabels == null)
            return;

        float heading = Mathf.Repeat(
            targetCamera.transform.eulerAngles.y - northOffset, 360f);

        float half = compassMarks.rect.width * 0.5f;

        for (int i = 0; i < markLabels.Length; i++)
        {
            float delta = Mathf.DeltaAngle(heading, markDegrees[i]);
            float x = delta * pixelsPerDegree;

            markLabels[i].rectTransform.anchoredPosition = new Vector2(x, 0f);

            // 양 끝으로 갈수록 서서히 투명하게
            float edge = Mathf.Clamp01(Mathf.Abs(x) / half);
            Color c = markLabels[i].color;
            c.a = Mathf.Clamp01(1f - edge * edge);
            markLabels[i].color = c;
            markLabels[i].enabled = edge < 1f;
        }

        SetText(headingText,
            $"{Mathf.RoundToInt(heading) % 360:000}°  {GetDirectionName(heading)}");
    }

    // 정보 텍스트와 게이지 갱신
    private void UpdateInfo()
    {
        Vector3 p = submarine.transform.position;
        float verticalSpeed = submarine.CurrentVerticalSpeed;

        // 주요 수치
        SetText(depthText, $"{submarine.depth:F1} m");
        SetText(speedText, $"{submarine.CurrentSpeed:F1} m/s");

        // 배터리 게이지
        float battery = systems != null ? systems.battery : 0f;
        bool lowBattery = battery < lowBatteryPercent;

        SetText(batteryText, $"{battery:F0} %");
        SetBar(batteryFill, 0f, Mathf.Clamp01(battery / 100f));
        SetColor(batteryFill, lowBattery ? warningColor : normalColor);
        SetColor(batteryText, lowBattery ? warningColor : normalColor);

        // 부력 게이지 (가운데 0, 오른쪽 상승 / 왼쪽 하강)
        if (buoyancy != null)
        {
            float f = buoyancy.netForce;
            float t = Mathf.Clamp(f / buoyancyGaugeMaxNewton, -1f, 1f);

            string state;
            Color stateColor;
            if (f > neutralThresholdNewton)
            {
                state = "RISING";
                stateColor = risingColor;
            }
            else if (f < -neutralThresholdNewton)
            {
                state = "SINKING";
                stateColor = sinkingColor;
            }
            else
            {
                state = "NEUTRAL";
                stateColor = normalColor;
            }

            SetText(buoyancyText, state);
            SetColor(buoyancyText, stateColor);

            if (t >= 0f)
                SetBar(buoyancyFill, 0.5f, 0.5f + t * 0.5f);
            else
                SetBar(buoyancyFill, 0.5f + t * 0.5f, 0.5f);
            SetColor(buoyancyFill, stateColor);
        }

        // 보조 정보
        bool lightOn = systems != null && systems.isLightOn;
        SetText(lightText, lightOn ? "ON" : "OFF");
        SetColor(lightText, lightOn ? accentColor : normalColor);

        SetText(vSpeedText, $"{verticalSpeed:+0.0;-0.0;0.0} m/s");

        string modeString = "-";
        if (submarine.IsBraking)
            modeString = "BRAKE";
        else if (submarine.isCruising)
            modeString = "CRUISE";
        SetText(modeText, modeString);

        SetText(positionText, $"{p.x:F0}, {p.y:F0}, {p.z:F0}");

        // 경고 메시지 (우선순위 순)
        if (battery <= 0f)
            warningMessage = "BATTERY EMPTY";
        else if (lowBattery)
            warningMessage = "LOW BATTERY";
        else if (verticalSpeed < -fastDescentSpeed)
            warningMessage = "DESCENDING FAST";
        else
            warningMessage = "";
    }

    // 경고 글자를 매 프레임 깜빡이게 함
    private void UpdateWarningBlink()
    {
        if (warningText == null)
            return;

        if (string.IsNullOrEmpty(warningMessage))
        {
            warningText.text = "";
            return;
        }

        warningText.text = warningMessage;

        Color c = warningColor;
        c.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
        warningText.color = c;
    }

    // 게이지 채우기 (from ~ to, 0~1 비율)
    private void SetBar(Image fill, float from, float to)
    {
        if (fill == null)
            return;

        RectTransform rt = fill.rectTransform;
        rt.anchorMin = new Vector2(from, 0f);
        rt.anchorMax = new Vector2(to, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void SetColor(Graphic target, Color color)
    {
        if (target != null)
            target.color = color;
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }

    private string GetDirectionName(float heading)
    {
        string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        return names[Mathf.RoundToInt(heading / 45f) % 8];
    }

    #endregion

    #region 방위 눈금 생성

    // 눈금 원본을 복제해서 15도 간격으로 배치
    private void BuildCompassMarks()
    {
        if (compassMarks == null || markTemplate == null)
            return;

        markTemplate.gameObject.SetActive(false);

        int count = 360 / MarkStep;
        markLabels = new TMP_Text[count];
        markDegrees = new float[count];

        for (int i = 0; i < count; i++)
        {
            int deg = i * MarkStep;
            markDegrees[i] = deg;

            TMP_Text label = Instantiate(markTemplate, compassMarks);
            label.gameObject.name = $"Mark_{deg}";
            label.gameObject.SetActive(true);
            label.text = GetMarkLabel(deg);
            label.fontSize = deg % 90 == 0 ? 34f : deg % 45 == 0 ? 26f : 22f;
            label.fontStyle = deg % 45 == 0 ? FontStyles.Bold : FontStyles.Normal;

            markLabels[i] = label;
        }
    }

    private string GetMarkLabel(int deg)
    {
        switch (deg)
        {
            case 0: return "N";
            case 45: return "NE";
            case 90: return "E";
            case 135: return "SE";
            case 180: return "S";
            case 225: return "SW";
            case 270: return "W";
            case 315: return "NW";
            default: return "|";
        }
    }

    #endregion
}