using UnityEngine;
using UnityEngine.SceneManagement;

// 실행 중 생성되는 오브젝트에 Scannable을 자동으로 붙임
public class ScannableAutoAttach : MonoBehaviour
{
    [Header("대상 찾기")]
    // 물고기가 생성되는 부모 (비워두면 씬 최상위 오브젝트에서 찾음)
    public Transform root;

    // 이름에 이 문자열이 들어간 오브젝트만 대상 (비워두면 root의 모든 자식)
    public string nameContains = "Fish";

    [Header("붙일 설정 (같은 종류는 같은 Id)")]
    public string id = "fish";
    public string displayName = "물고기";

    [TextArea]
    public string description;

    [Header("검사")]
    // 새 오브젝트를 찾는 주기 (s)
    public float interval = 0.5f;

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < interval)
            return;

        timer = 0f;

        if (root != null)
        {
            for (int i = 0; i < root.childCount; i++)
                TryAttach(root.GetChild(i));
        }
        else
        {
            // root가 없으면 씬의 최상위 오브젝트만 검사
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
                TryAttach(go.transform);
        }
    }

    private void TryAttach(Transform target)
    {
        if (nameContains.Length > 0 && !target.name.Contains(nameContains))
            return;

        if (target.GetComponent<Scannable>() != null)
            return;

        Scannable s = target.gameObject.AddComponent<Scannable>();
        s.id = id;
        s.displayName = displayName;
        s.description = description;
    }
}