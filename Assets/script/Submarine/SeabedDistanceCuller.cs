using UnityEngine;
using System.Collections.Generic;

// 잠수함(카메라)과의 거리에 따라 해저 소품들을 꺼서 렌더링 부하를 줄임
// 맵이 2000x2000로 커서 멀리 있는 바위/구조물/쓰레기까지 매 프레임 그릴 필요가 없음
public class SeabedDistanceCuller : MonoBehaviour
{
    public Transform target; // 비워두면 Camera.main
    public float cullDistance = 300f;
    public float checkInterval = 0.3f;

    static readonly string[] ContainerNames = { "SeabedProps", "SeabedStructures", "SeabedBrinePools", "SeabedRockCrust", "SeabedDebris" };

    List<Transform> tracked = new List<Transform>();

    void Start()
    {
        if (target == null && Camera.main != null) target = Camera.main.transform;

        foreach (string containerName in ContainerNames)
        {
            GameObject container = GameObject.Find(containerName);
            if (container == null) continue;
            foreach (Transform child in container.transform) tracked.Add(child);
        }

        InvokeRepeating(nameof(UpdateCulling), 0f, checkInterval);
    }

    void UpdateCulling()
    {
        if (target == null)
        {
            if (Camera.main == null) return;
            target = Camera.main.transform;
        }

        float sqrCull = cullDistance * cullDistance;
        Vector3 p = target.position;
        for (int i = 0; i < tracked.Count; i++)
        {
            Transform t = tracked[i];
            if (t == null) continue;
            bool shouldShow = (t.position - p).sqrMagnitude < sqrCull;
            if (t.gameObject.activeSelf != shouldShow) t.gameObject.SetActive(shouldShow);
        }
    }
}
