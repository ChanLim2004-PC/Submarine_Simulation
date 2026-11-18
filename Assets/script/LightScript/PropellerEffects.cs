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
