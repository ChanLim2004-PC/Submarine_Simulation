using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ScanState
{
    Idle,            // 1인칭이 아니거나 입력이 막힘
    NoTarget,        // 박스 안에 대상 없음
    Ready,           // 대상이 박스 안에 있음 (키를 누르면 시작)
    Scanning,        // 키를 누르는 중
    AlreadyScanned   // 이미 식별한 대상
}

public class SubmarineScanner : MonoBehaviour
{
    // HUD, 다른 스크립트가 접근하는 단일 인스턴스
    public static SubmarineScanner Current { get; private set; }

    #region 인스펙터 설정

    [Header("참조")]
    // 스캔 기준 카메라 (비워두면 Camera.main)
    public Camera scanCamera;

    // 시점 관리 스크립트 (비워두면 자동 탐색)
    public SubmarineViewMode viewMode;

    [Header("스캔")]
    // 스캔 키 (누르고 있는 동안 진행)
    public Key scanKey = Key.F;

    // 식별에 필요한 시간 (s)
    public float scanDuration = 2f;

    // 기본 스캔 가능 거리 (m)
    public float scanDistance = 60f;

    // 스캔 박스 한 변 크기 (화면 높이 대비 비율)
    [Range(0.05f, 0.6f)]
    public float scanBoxScreenRatio = 0.25f;

    // 대상과 카메라 사이에 다른 물체가 있으면 스캔 불가
    public bool requireLineOfSight = true;

    // 식별 완료 메시지를 보여줄 시간 (s)
    public float completeMessageSeconds = 2.5f;

    // 외부(패널, 지도)가 열려 있을 때 스캔을 막기 위한 플래그
    public bool inputBlocked;

    #endregion

    #region 상태 (HUD가 읽음)

    public ScanState State { get; private set; }
    public Scannable Target { get; private set; }
    public float TargetDistance { get; private set; }

    // 식별 진행도 (0 ~ 1)
    public float Progress { get; private set; }

    // 가장 최근에 식별한 대상과 완료 메시지 남은 시간
    public Scannable LastCompleted { get; private set; }
    public float CompletedTimeLeft { get; private set; }

    // 식별한 대상 목록 (도감 표시용)
    public readonly List<Scannable> ScannedTargets = new List<Scannable>();

    // 식별 완료 시 호출됨 (임무/도감 시스템이 구독)
    public event Action<Scannable> ScanCompleted;

    // 스캔 키가 대상에게 쓰이는 중인지 (같은 키를 쓰는 다른 기능이 양보하는 용도)
    public bool IsBusyWithTarget
    {
        get { return State == ScanState.Ready || State == ScanState.Scanning; }
    }

    #endregion

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly List<Vector3> cornerBuffer = new List<Vector3>(64);
    private readonly List<Vector2> screenPoints = new List<Vector2>(64);
    private readonly List<Vector2> hull = new List<Vector2>(64);

    void Awake()
    {
        Current = this;

        if (scanCamera == null)
            scanCamera = Camera.main;

        if (viewMode == null)
            viewMode = GetComponentInParent<SubmarineViewMode>();
    }

    void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    void Update()
    {
        if (CompletedTimeLeft > 0f)
            CompletedTimeLeft -= Time.deltaTime;

        bool firstPerson = viewMode != null && viewMode.mode == ViewMode.FirstPerson;

        if (!firstPerson || inputBlocked || scanCamera == null)
        {
            Target = null;
            Progress = 0f;
            State = ScanState.Idle;
            return;
        }

        // 박스 안의 가장 가까운(조준점 기준) 대상 선택
        Scannable candidate = FindCandidate();

        // 대상이 바뀌면 진행도 초기화
        if (candidate != Target)
        {
            Target = candidate;
            Progress = 0f;
        }

        if (Target == null)
        {
            State = ScanState.NoTarget;
            return;
        }

        if (Target.IsScanned)
        {
            State = ScanState.AlreadyScanned;
            Progress = 1f;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool holding = keyboard != null && keyboard[scanKey].isPressed;

        if (holding)
        {
            State = ScanState.Scanning;
            Progress += Time.deltaTime / scanDuration;

            if (Progress >= 1f)
                Complete();
        }
        else
        {
            State = ScanState.Ready;
            Progress = 0f;
        }
    }

    private void Complete()
    {
        Target.MarkScanned();
        ScannedTargets.Add(Target);

        LastCompleted = Target;
        CompletedTimeLeft = completeMessageSeconds;
        Progress = 1f;
        State = ScanState.AlreadyScanned;

        ScanCompleted?.Invoke(Target);
    }

    // 거리 안 + 박스와 겹침 + (선택) 시야가 트인 대상 중, 박스 중심에 가장 가까운 것
    private Scannable FindCandidate()
    {
        float boxHalf = Screen.height * scanBoxScreenRatio * 0.5f;
        Vector2 screenCenter = new Vector2(Screen.width, Screen.height) * 0.5f;
        Rect box = new Rect(
            screenCenter.x - boxHalf, screenCenter.y - boxHalf,
            boxHalf * 2f, boxHalf * 2f);

        Vector3 camPos = scanCamera.transform.position;

        Scannable best = null;
        float bestRectDist = float.MaxValue;
        float bestDistance = float.MaxValue;

        foreach (Scannable s in Scannable.All)
        {
            // 카메라에서 가장 가까운 표면까지의 거리
            Vector3 closest = s.ClosestPointTo(camPos);
            float distance = Vector3.Distance(camPos, closest);

            float limit = s.maxScanDistance > 0f ? s.maxScanDistance : scanDistance;
            if (distance > limit)
                continue;

            // 화면에 투영한 대상 모양이 스캔 박스와 실제로 겹치는지
            if (!BuildScreenHull(s, out Rect bounds))
                continue;

            if (!HullOverlapsRect(bounds, box))
                continue;

            if (requireLineOfSight && !HasLineOfSight(s, camPos, closest))
                continue;

            // 박스 중심에서 대상까지의 거리 (중심을 덮고 있으면 0)
            float rectDist = DistanceToRect(bounds, screenCenter);

            bool better =
                rectDist < bestRectDist - 0.01f ||
                (Mathf.Abs(rectDist - bestRectDist) <= 0.01f && distance < bestDistance);

            if (better)
            {
                best = s;
                bestRectDist = rectDist;
                bestDistance = distance;
            }
        }

        TargetDistance = best != null ? bestDistance : 0f;
        return best;
    }

    // 대상의 회전된 상자를 카메라 앞쪽만 잘라 화면에 투영하고, 그 볼록 껍질(hull)을 구함
    private bool BuildScreenHull(Scannable s, out Rect bounds)
    {
        const float near = 0.05f;
        Transform cam = scanCamera.transform;

        cornerBuffer.Clear();
        screenPoints.Clear();
        s.GetCorners(cornerBuffer);

        for (int g = 0; g + 8 <= cornerBuffer.Count; g += 8)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 a = cornerBuffer[g + i];
                float za = cam.InverseTransformPoint(a).z;

                if (za > near)
                    AddScreenPoint(a);

                // 상자의 모서리 중 카메라 앞뒤 경계를 가로지르는 것은 경계면에서 잘라 사용
                for (int bit = 1; bit < 8; bit <<= 1)
                {
                    if ((i & bit) != 0)
                        continue;   // 모서리마다 한 번만

                    Vector3 b = cornerBuffer[g + (i | bit)];
                    float zb = cam.InverseTransformPoint(b).z;

                    if ((za > near) != (zb > near))
                    {
                        float t = (near - za) / (zb - za);
                        AddScreenPoint(Vector3.Lerp(a, b, t));
                    }
                }
            }
        }

        if (screenPoints.Count == 0)
        {
            bounds = default;
            return false;
        }

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (Vector2 p in screenPoints)
        {
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }
        bounds = new Rect(minX, minY, maxX - minX, maxY - minY);

        BuildConvexHull(screenPoints, hull);
        return true;
    }

    private void AddScreenPoint(Vector3 world)
    {
        Vector3 sp = scanCamera.WorldToScreenPoint(world);
        screenPoints.Add(new Vector2(sp.x, sp.y));
    }

    // 볼록 다각형(hull)과 사각형이 겹치는지 (분리축 검사)
    private bool HullOverlapsRect(Rect bounds, Rect box)
    {
        // 사각형의 축(가로/세로) 기준 검사
        if (!bounds.Overlaps(box))
            return false;

        // 점이 너무 적어 다각형이 안 만들어지면 영역 검사 결과를 그대로 사용
        if (hull.Count < 3)
            return true;

        Vector2[] rectCorners =
        {
        new Vector2(box.xMin, box.yMin), new Vector2(box.xMax, box.yMin),
        new Vector2(box.xMax, box.yMax), new Vector2(box.xMin, box.yMax)
    };

        // 다각형 각 변의 수직 방향을 축으로 검사
        for (int i = 0; i < hull.Count; i++)
        {
            Vector2 e = hull[(i + 1) % hull.Count] - hull[i];
            Vector2 axis = new Vector2(-e.y, e.x);

            float hullMin = float.MaxValue, hullMax = float.MinValue;
            foreach (Vector2 p in hull)
            {
                float d = Vector2.Dot(p, axis);
                hullMin = Mathf.Min(hullMin, d);
                hullMax = Mathf.Max(hullMax, d);
            }

            float rectMin = float.MaxValue, rectMax = float.MinValue;
            foreach (Vector2 p in rectCorners)
            {
                float d = Vector2.Dot(p, axis);
                rectMin = Mathf.Min(rectMin, d);
                rectMax = Mathf.Max(rectMax, d);
            }

            if (hullMax < rectMin || rectMax < hullMin)
                return false;   // 분리되는 축이 있으면 겹치지 않음
        }

        return true;
    }

    // 볼록 껍질 (Andrew's monotone chain). points는 정렬되어 바뀜
    private static void BuildConvexHull(List<Vector2> points, List<Vector2> result)
    {
        result.Clear();

        if (points.Count < 3)
        {
            result.AddRange(points);
            return;
        }

        points.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));

        for (int i = 0; i < points.Count; i++)
        {
            while (result.Count >= 2 &&
                   Cross(result[result.Count - 2], result[result.Count - 1], points[i]) <= 0f)
                result.RemoveAt(result.Count - 1);
            result.Add(points[i]);
        }

        int lower = result.Count + 1;
        for (int i = points.Count - 2; i >= 0; i--)
        {
            while (result.Count >= lower &&
                   Cross(result[result.Count - 2], result[result.Count - 1], points[i]) <= 0f)
                result.RemoveAt(result.Count - 1);
            result.Add(points[i]);
        }

        result.RemoveAt(result.Count - 1);
    }

    private static float Cross(Vector2 o, Vector2 a, Vector2 b)
    {
        return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    }

    // 점에서 사각형까지의 거리 (사각형 안이면 0)
    private float DistanceToRect(Rect rect, Vector2 point)
    {
        float dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax);
        float dy = Mathf.Max(rect.yMin - point.y, 0f, point.y - rect.yMax);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // 카메라에서 대상 표면까지 처음 막는 물체가 대상 자신인지 확인 (잠수함 자신은 무시)
    private bool HasLineOfSight(Scannable target, Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f)
            return true;

        // 표면 위의 점이라 레이가 살짝 못 닿는 경우를 막기 위해 조금 더 길게 쏨
        int count = Physics.RaycastNonAlloc(
            from, dir / dist, hitBuffer, dist + 0.1f, ~0, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        Collider nearestCollider = null;

        for (int i = 0; i < count; i++)
        {
            Collider c = hitBuffer[i].collider;

            if (c.transform.IsChildOf(transform))
                continue;   // 잠수함 자신

            if (hitBuffer[i].distance < nearest)
            {
                nearest = hitBuffer[i].distance;
                nearestCollider = c;
            }
        }

        // 막는 것이 없거나, 처음 맞은 것이 대상 자신이면 통과
        return nearestCollider == null ||
               nearestCollider.GetComponentInParent<Scannable>() == target;
    }
}