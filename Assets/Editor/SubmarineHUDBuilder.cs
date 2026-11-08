using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class SubmarineHUDBuilder
{
    static readonly Color TextColor = new Color(0.85f, 0.95f, 1f, 1f);
    static readonly Color LabelColor = new Color(0.6f, 0.75f, 0.85f, 1f);
    static readonly Color Accent = new Color(1f, 0.85f, 0.2f, 1f);
    static readonly Color WarningColor = new Color(1f, 0.35f, 0.3f, 1f);
    static readonly Color PanelColor = new Color(0.02f, 0.05f, 0.08f, 0.75f);
    static readonly Color FrameColor = new Color(0.4f, 0.7f, 0.85f, 1f);   // 미니맵과 동일
    static readonly Color BarBackColor = new Color(1f, 1f, 1f, 0.12f);

    [MenuItem("Tools/Submarine/Create HUD")]
    public static void CreateHUD()
    {
        // 캔버스
        GameObject canvasObj = new GameObject("HUD_Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Submarine HUD");

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        SubmarineHUD hud = canvasObj.AddComponent<SubmarineHUD>();
        hud.submarine = Object.FindAnyObjectByType<SubmarineController>();
        hud.targetCamera = Camera.main;

        // 모드별 루트
        GameObject tp = CreateRoot("ThirdPersonHUD", canvasObj.transform);
        GameObject fp = CreateRoot("FirstPersonHUD", canvasObj.transform);
        hud.thirdPersonRoot = tp;
        hud.firstPersonRoot = fp;

        BuildInfoPanel(tp.transform, hud);
        BuildCompass(tp.transform, hud);
        BuildWarning(tp.transform, hud);
        FirstPersonHUD fpHud = fp.AddComponent<FirstPersonHUD>();
        fpHud.submarine = hud.submarine;
        fpHud.targetCamera = hud.targetCamera;
        BuildFirstPerson(fp.transform, fpHud);

        Selection.activeGameObject = canvasObj;
        EditorSceneManager.MarkSceneDirty(canvasObj.scene);
    }

    // 좌측 하단 정보 패널 (높이는 내용에 맞춰 자동)
    static void BuildInfoPanel(Transform parent, SubmarineHUD hud)
    {
        Image panel = CreateImage("InfoPanel", parent, PanelColor);
        Place(panel.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(30f, 30f), new Vector2(460f, 0f));
        AddFrame(panel);

        VerticalLayoutGroup v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(18, 18, 14, 14);
        v.spacing = 10f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 주요 수치 (수심, 속력) - 크게
        GameObject main = new GameObject("MainStats",
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        main.transform.SetParent(panel.transform, false);
        main.GetComponent<LayoutElement>().preferredHeight = 84f;

        HorizontalLayoutGroup h = main.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 10f;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;

        hud.depthText = CreateStat(main.transform, "DEPTH");
        hud.speedText = CreateStat(main.transform, "SPEED");

        // 게이지 (배터리: 왼쪽부터 채움 / 부력: 가운데 기준)
        hud.batteryText = CreateGauge(panel.transform, "BATTERY", false, out hud.batteryFill);
        hud.buoyancyText = CreateGauge(panel.transform, "BUOYANCY", true, out hud.buoyancyFill);

        // 보조 정보 - 작게
        hud.lightText = CreateRow(panel.transform, "LIGHT");
        hud.vSpeedText = CreateRow(panel.transform, "V.SPEED");
        hud.modeText = CreateRow(panel.transform, "MODE");
        hud.positionText = CreateRow(panel.transform, "POS X,Y,Z");
    }

    // 상단 방위 표시줄
    static void BuildCompass(Transform parent, SubmarineHUD hud)
    {
        Image bar = CreateImage("CompassBar", parent, PanelColor);
        Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(640f, 50f));
        AddFrame(bar);

        // 눈금 컨테이너 (바 밖으로 나가는 눈금을 잘라냄)
        GameObject marks = new GameObject("Marks", typeof(RectTransform), typeof(RectMask2D));
        marks.transform.SetParent(bar.transform, false);
        Stretch(marks.GetComponent<RectTransform>());
        hud.compassMarks = marks.GetComponent<RectTransform>();

        // 눈금 원본 (비활성, 코드가 복제해서 사용)
        TextMeshProUGUI template = CreateText("MarkTemplate", marks.transform, "|", 22f,
            TextAlignmentOptions.Center);
        Place(template.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 50f));
        template.gameObject.SetActive(false);
        hud.markTemplate = template;

        // 중앙 기준선
        Image center = CreateImage("CenterLine", bar.transform, Accent);
        Place(center.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 0f));

        // 현재 방위 숫자
        TextMeshProUGUI heading = CreateText("HeadingText", bar.transform, "000°  N", 28f,
            TextAlignmentOptions.Center);
        Place(heading.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(300f, 36f));
        heading.color = Accent;
        hud.headingText = heading;
    }

    // 경고 문구 (방위 표시줄 아래)
    static void BuildWarning(Transform parent, SubmarineHUD hud)
    {
        TextMeshProUGUI warning = CreateText("WarningText", parent, "", 36f,
            TextAlignmentOptions.Center);
        Place(warning.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(700f, 50f));
        warning.color = WarningColor;
        warning.fontStyle = FontStyles.Bold;
        hud.warningText = warning;
    }

    // 1인칭 HUD: 조준점, 스캔 박스, 상태 줄, 키 안내, 임무/도감 패널
    static void BuildFirstPerson(Transform parent, FirstPersonHUD fp)
    {
        // 스캔 박스 (크기는 실행 중 코드가 스캐너 설정에 맞춰 조절)
        GameObject group = new GameObject("ScanBox", typeof(RectTransform));
        group.transform.SetParent(parent, false);
        RectTransform box = group.GetComponent<RectTransform>();
        Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(270f, 270f));
        fp.scanBox = box;

        // 테두리 4개
        const float t = 3f;
        Image top = CreateImage("EdgeTop", box, Color.white);
        Place(top.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, t));
        Image bottom = CreateImage("EdgeBottom", box, Color.white);
        Place(bottom.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, t));
        Image left = CreateImage("EdgeLeft", box, Color.white);
        Place(left.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(t, 0f));
        Image right = CreateImage("EdgeRight", box, Color.white);
        Place(right.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(1f, 0.5f), Vector2.zero, new Vector2(t, 0f));
        fp.boxEdges = new Graphic[] { top, bottom, left, right };

        // 조준점
        Image cross = CreateImage("Crosshair", box, Accent);
        Place(cross.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));

        // 박스 위: 대상 이름 + 거리
        TextMeshProUGUI target = CreateText("TargetText", box, "", 28f,
            TextAlignmentOptions.Center);
        Place(target.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(700f, 40f));
        fp.targetText = target;

        // 박스 아래: 상태 문구
        TextMeshProUGUI status = CreateText("StatusText", box, "", 32f,
            TextAlignmentOptions.Center);
        Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(700f, 44f));
        status.fontStyle = FontStyles.Bold;
        fp.statusText = status;

        // 식별 진행 막대
        Image back = CreateImage("ProgressBar", box, BarBackColor);
        Place(back.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(360f, 10f));
        Image fill = CreateImage("Fill", back.transform, Accent);
        Stretch(fill.rectTransform);
        fp.progressRoot = back.gameObject;
        fp.progressFill = fill;
        back.gameObject.SetActive(false);

        // 상단 중앙: 방위
        TextMeshProUGUI heading = CreateText("HeadingText", parent, "000°  N", 28f,
            TextAlignmentOptions.Center);
        Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(300f, 40f));
        heading.color = Accent;
        fp.headingText = heading;

        // 좌측 하단: 상태 줄 (수심, 배터리, 탐조등)
        Image statusPanel = CreateImage("StatusPanel", parent, PanelColor);
        Place(statusPanel.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(30f, 30f), new Vector2(460f, 46f));
        AddFrame(statusPanel);

        TextMeshProUGUI compact = CreateText("CompactStatus", statusPanel.transform, "", 22f,
            TextAlignmentOptions.Left);
        Stretch(compact.rectTransform);
        compact.rectTransform.offsetMin = new Vector2(16f, 0f);
        compact.rectTransform.offsetMax = new Vector2(-16f, 0f);
        fp.compactStatusText = compact;

        // 하단 중앙: 키 안내
        TextMeshProUGUI hint = CreateText("KeyHint", parent,
            "[F] 스캔(길게)   [J] 임무   [K] 도감   [L] 탐조등[V] 3인칭 ", 22f,
            TextAlignmentOptions.Center);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(900f, 36f));
        hint.color = LabelColor;

        // 임무 / 도감 패널 (처음엔 꺼 둠)
        fp.missionPanel = CreatePanel(parent, "MissionPanel", "임무  (J)", out fp.missionBody);
        fp.codexPanel = CreatePanel(parent, "CodexPanel", "도감  (K)", out fp.codexBody);
    }

    // 화면 가운데에 뜨는 패널 (제목 + 본문). 본문 텍스트를 out으로 전달
    static GameObject CreatePanel(Transform parent, string name, string title,
        out TMP_Text body)
    {
        Image panel = CreateImage(name, parent, new Color(0.02f, 0.05f, 0.08f, 0.92f));
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 640f));
        AddFrame(panel);

        TextMeshProUGUI titleText = CreateText("Title", panel.transform, title, 40f,
            TextAlignmentOptions.Left);
        Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(-80f, 56f));
        titleText.color = Accent;
        titleText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI bodyText = CreateText("Body", panel.transform, "", 26f,
            TextAlignmentOptions.TopLeft);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        Stretch(bodyText.rectTransform);
        bodyText.rectTransform.offsetMin = new Vector2(40f, 40f);
        bodyText.rectTransform.offsetMax = new Vector2(-40f, -100f);
        body = bodyText;

        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    #region 생성 도우미

    static GameObject CreateRoot(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        return go;
    }

    // 라벨(작게) + 값(크게)을 세로로 쌓은 블록. 값 텍스트를 반환
    static TextMeshProUGUI CreateStat(Transform parent, string label)
    {
        GameObject block = new GameObject("Stat_" + label,
            typeof(RectTransform), typeof(VerticalLayoutGroup));
        block.transform.SetParent(parent, false);

        VerticalLayoutGroup v = block.GetComponent<VerticalLayoutGroup>();
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        TextMeshProUGUI l = CreateText("Label", block.transform, label, 20f,
            TextAlignmentOptions.Left);
        l.color = LabelColor;
        l.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;

        TextMeshProUGUI value = CreateText("Value", block.transform, "-", 44f,
            TextAlignmentOptions.Left);
        value.fontStyle = FontStyles.Bold;
        value.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;

        return value;
    }

    // 게이지: 라벨 + 값 한 줄, 그 아래 막대. 값 텍스트를 반환하고 채움 Image를 out으로 전달
    static TextMeshProUGUI CreateGauge(Transform parent, string label, bool centered,
        out Image fill)
    {
        // 제목 줄
        GameObject row = new GameObject("Gauge_" + label,
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 30f;

        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        TextMeshProUGUI l = CreateText("Label", row.transform, label, 22f,
            TextAlignmentOptions.Left);
        l.color = LabelColor;
        l.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        TextMeshProUGUI value = CreateText("Value", row.transform, "-", 24f,
            TextAlignmentOptions.Right);
        value.fontStyle = FontStyles.Bold;
        value.gameObject.AddComponent<LayoutElement>().preferredWidth = 200f;

        // 막대
        Image back = CreateImage("Bar_" + label, parent, BarBackColor);
        back.gameObject.AddComponent<LayoutElement>().preferredHeight = 14f;

        fill = CreateImage("Fill", back.transform, TextColor);
        Stretch(fill.rectTransform);

        // 가운데 기준 게이지는 중앙 눈금을 표시
        if (centered)
        {
            Image tick = CreateImage("CenterTick", back.transform, Color.white);
            Place(tick.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 0f));
        }

        return value;
    }

    // 라벨 + 값 한 줄 (작은 글씨). 값 텍스트를 반환
    static TextMeshProUGUI CreateRow(Transform parent, string label)
    {
        GameObject row = new GameObject("Row_" + label,
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 30f;

        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        TextMeshProUGUI l = CreateText("Label", row.transform, label, 22f,
            TextAlignmentOptions.Left);
        l.color = LabelColor;
        l.gameObject.AddComponent<LayoutElement>().preferredWidth = 170f;

        TextMeshProUGUI value = CreateText("Value", row.transform, "-", 22f,
            TextAlignmentOptions.Left);
        value.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        return value;
    }

    // 테두리 (Outline으로 얇은 선을 그림)
    static void AddFrame(Image image)
    {
        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = FrameColor;
        outline.effectDistance = new Vector2(2f, -2f);
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, string text,
        float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = TextColor;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    #endregion
}