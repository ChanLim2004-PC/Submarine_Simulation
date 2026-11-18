using UnityEngine;

// 잠수정 프로펠러 회전
// 잠수정(Submarine) 아래 자식 오브젝트에 붙인다. 조작 파트 코드는 읽기만 한다
// 모델의 프로펠러 부품을 자기 축(선체 길이 방향)으로 돌림. 모델 피벗이 선체 가운데라 RotateAround 사용
public class PropellerEffects : MonoBehaviour
{
    [Tooltip("모델 안의 프로펠러 부품 이름")]
    public string propellerPartName = "Object_5";
    [Tooltip("최고 속도일 때 초당 회전수")]
    public float maxRevolutionsPerSecond = 3f;
    [Tooltip("이 속도(m/s)에서 회전이 최대")]
    public float fullSpeed = 6f;   // 속도감 효과(SubmarineSpeedFeedback)의 기준 속도와 맞춤
    [Tooltip("회전 방향을 뒤집기")]
    public bool reverseSpin = false;
    [Tooltip("프로펠러 축이 지나는 점 (모델 메시 좌표의 y, z). 부품에 다른 작은 조각이 섞여 있어 메시 중심 대신 허브 위치를 직접 지정")]
    public Vector2 hubCenterYZ = new Vector2(0f, 15f);
    [Tooltip("축에서 이 거리(모델 메시 좌표) 안쪽만 돌림. 바깥 조각(모터 쪽 막대)은 멈춘 채로 남김")]
    public float spinRadius = 3f;

    Transform submarine;
    Rigidbody body;
    Transform propeller;
    Vector3 hubLocal;   // 허브 중심 (부품 기준 좌표)

    void Start()
    {
        var controller = GetComponentInParent<SubmarineController>();
        if (controller == null) { enabled = false; return; }
        submarine = controller.transform;
        body = controller.GetComponent<Rigidbody>();

        propeller = FindChild(submarine, propellerPartName);
        if (propeller == null)
        {
            Debug.LogWarning($"[프로펠러] '{propellerPartName}' 부품을 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        // 축이 지나는 점. x는 축 방향이라 회전에 영향이 없어 메시 끝값을 그대로 씀
        var mf = propeller.GetComponent<MeshFilter>();
        float hubX = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.center.x : 0f;
        hubLocal = new Vector3(hubX, hubCenterYZ.x, hubCenterYZ.y);

        SplitOffStillParts(mf);
    }

    // 프로펠러 부품에 같이 묶인 축 바깥 조각을 떼어 내, 돌지 않는 별도 오브젝트로 남긴다
    // (모델 가져오기 설정에서 Read/Write가 켜져 있어야 메시를 읽을 수 있음)
    void SplitOffStillParts(MeshFilter mf)
    {
        if (mf == null || mf.sharedMesh == null) return;
        Mesh src = mf.sharedMesh;
        if (!src.isReadable)
        {
            Debug.LogWarning("[프로펠러] 모델의 Read/Write가 꺼져 있어 프로펠러 옆 조각까지 같이 돕니다.", this);
            return;
        }

        Vector3[] verts = src.vertices;
        Mesh spin = Instantiate(src);
        Mesh still = Instantiate(src);
        spin.name = src.name + "_Spin";
        still.name = src.name + "_Still";
        bool anyStill = false;

        for (int s = 0; s < src.subMeshCount; s++)
        {
            int[] tris = src.GetTriangles(s);
            var spinTris = new System.Collections.Generic.List<int>(tris.Length);
            var stillTris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 c = (verts[tris[i]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3f;
                float r = new Vector2(c.y - hubLocal.y, c.z - hubLocal.z).magnitude;
                var list = r <= spinRadius ? spinTris : stillTris;
                list.Add(tris[i]); list.Add(tris[i + 1]); list.Add(tris[i + 2]);
            }
            spin.SetTriangles(spinTris, s);
            still.SetTriangles(stillTris, s);
            anyStill |= stillTris.Count > 0;
        }
        if (!anyStill) { Destroy(spin); Destroy(still); return; }

        spin.RecalculateBounds();
        still.RecalculateBounds();
        mf.mesh = spin;

        // 돌기 전 위치 그대로 형제 오브젝트로 둔다
        var go = new GameObject(propeller.name + "_Still");
        go.transform.SetParent(propeller.parent, false);
        go.transform.localPosition = propeller.localPosition;
        go.transform.localRotation = propeller.localRotation;
        go.transform.localScale = propeller.localScale;
        go.AddComponent<MeshFilter>().sharedMesh = still;
        var srcRenderer = propeller.GetComponent<MeshRenderer>();
        var r2 = go.AddComponent<MeshRenderer>();
        if (srcRenderer != null)
        {
            r2.sharedMaterials = srcRenderer.sharedMaterials;
            r2.shadowCastingMode = srcRenderer.shadowCastingMode;
            r2.receiveShadows = srcRenderer.receiveShadows;
        }
    }

    void Update()
    {
        // 앞뒤 속도 (+ 전진, - 후진)
        float forwardSpeed = body != null ? Vector3.Dot(body.linearVelocity, submarine.forward) : 0f;
        float ratio = Mathf.Clamp(forwardSpeed / Mathf.Max(0.01f, fullSpeed), -1f, 1f);

        float degrees = ratio * maxRevolutionsPerSecond * 360f * Time.deltaTime * (reverseSpin ? -1f : 1f);
        Vector3 pivot = propeller.TransformPoint(hubLocal);
        Vector3 axis = propeller.TransformDirection(Vector3.right);   // 메시 x축 = 프로펠러 축 (회전해도 그대로)
        propeller.RotateAround(pivot, axis, degrees);
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
