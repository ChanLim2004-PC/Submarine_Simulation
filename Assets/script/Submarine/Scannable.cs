using System.Collections.Generic;
using UnityEngine;

// 스캔(식별) 대상에 붙이는 컴포넌트. 대상에는 Collider가 있어야 함
public class Scannable : MonoBehaviour
{
    // 씬에 있는 모든 대상 (스캐너가 매 프레임 검색하지 않도록 목록 유지)
    public static readonly List<Scannable> All = new List<Scannable>();

    // 도감용 고유 ID (비워두면 오브젝트 이름 사용)
    public string id;

    // 식별 후 보여줄 이름
    public string displayName = "Unknown";

    // 식별 후 도감에 보여줄 설명
    [TextArea]
    public string description;

    // 이 대상만 따로 쓸 스캔 거리 (0이면 스캐너 기본값 사용)
    public float maxScanDistance = 0f;

    // 식별 완료 여부
    public bool IsScanned { get; private set; }

    public string Id
    {
        get { return string.IsNullOrEmpty(id) ? name : id; }
    }

    private Collider[] colliders;
    private Renderer[] renderers;

    #region 영역 계산

    // 대상 전체를 감싸는 월드 축 기준 영역 (콜라이더 기준, 없으면 렌더러 기준)
    public Bounds WorldBounds
    {
        get
        {
            bool found = false;
            Bounds b = new Bounds(transform.position, Vector3.zero);

            if (colliders != null)
            {
                foreach (Collider c in colliders)
                {
                    if (!IsUsable(c))
                        continue;

                    if (!found) { b = c.bounds; found = true; }
                    else b.Encapsulate(c.bounds);
                }
            }

            if (!found && renderers != null)
            {
                foreach (Renderer r in renderers)
                {
                    if (r == null)
                        continue;

                    if (!found) { b = r.bounds; found = true; }
                    else b.Encapsulate(r.bounds);
                }
            }

            return b;
        }
    }

    // 콜라이더마다 회전을 반영한 상자의 꼭짓점 8개를 output에 추가 (8개씩 한 묶음)
    // 꼭짓점 번호의 비트: 1 = x, 2 = y, 4 = z (0이면 최소, 1이면 최대쪽)
    public void GetCorners(List<Vector3> output)
    {
        bool any = false;

        if (colliders != null)
        {
            foreach (Collider c in colliders)
            {
                if (!IsUsable(c))
                    continue;

                any = true;
                Vector3 center;
                Vector3 size;

                if (c is BoxCollider box)
                {
                    center = box.center;
                    size = box.size;
                }
                else if (c is SphereCollider sphere)
                {
                    center = sphere.center;
                    size = Vector3.one * sphere.radius * 2f;
                }
                else if (c is CapsuleCollider capsule)
                {
                    float d = capsule.radius * 2f;
                    center = capsule.center;
                    size = new Vector3(d, d, d);
                    size[capsule.direction] = Mathf.Max(capsule.height, d);
                }
                else if (c is MeshCollider mesh && mesh.sharedMesh != null)
                {
                    center = mesh.sharedMesh.bounds.center;
                    size = mesh.sharedMesh.bounds.size;
                }
                else
                {
                    AddAxisAlignedCorners(c.bounds, output);
                    continue;
                }

                AddOrientedCorners(c.transform.localToWorldMatrix, center, size, output);
            }
        }

        if (!any)
            AddAxisAlignedCorners(WorldBounds, output);
    }

    private static void AddOrientedCorners(Matrix4x4 matrix, Vector3 center, Vector3 size,
        List<Vector3> output)
    {
        Vector3 half = size * 0.5f;

        for (int i = 0; i < 8; i++)
        {
            Vector3 local = center + new Vector3(
                (i & 1) == 0 ? -half.x : half.x,
                (i & 2) == 0 ? -half.y : half.y,
                (i & 4) == 0 ? -half.z : half.z);

            output.Add(matrix.MultiplyPoint3x4(local));
        }
    }

    private static void AddAxisAlignedCorners(Bounds b, List<Vector3> output)
    {
        for (int i = 0; i < 8; i++)
        {
            output.Add(new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x,
                (i & 2) == 0 ? b.min.y : b.max.y,
                (i & 4) == 0 ? b.min.z : b.max.z));
        }
    }

    #endregion

    #region 거리 계산

    // 주어진 위치에서 가장 가까운 대상 표면의 점
    public Vector3 ClosestPointTo(Vector3 point)
    {
        Vector3 best = WorldBounds.ClosestPoint(point);
        float bestSqr = float.MaxValue;

        if (colliders == null)
            return best;

        foreach (Collider c in colliders)
        {
            if (!IsUsable(c))
                continue;

            // ClosestPoint를 지원하지 않는 콜라이더는 영역(bounds)으로 대신 계산
            Vector3 p = SupportsClosestPoint(c)
                ? c.ClosestPoint(point)
                : c.bounds.ClosestPoint(point);

            float sqr = (p - point).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = p;
            }
        }

        return best;
    }

    private static bool SupportsClosestPoint(Collider c)
    {
        return c is BoxCollider ||
               c is SphereCollider ||
               c is CapsuleCollider ||
               (c is MeshCollider mesh && mesh.convex);
    }

    private static bool IsUsable(Collider c)
    {
        return c != null && c.enabled && !c.isTrigger;
    }

    #endregion

    void Awake()
    {
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    public void MarkScanned()
    {
        IsScanned = true;
    }
}