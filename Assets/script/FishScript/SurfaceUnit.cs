using UnityEngine;

public class SurfaceUnit : MonoBehaviour
{
    public Transform myTransform { get; private set; }
    public Flock myFlock { get; private set; }
    public Vector3 currentVelocity;
    public float speed;
    public float FOVAngle = 270;   // 시야각
    public float smoothDamp = 0.1f; // 부드러운 회전/이동 값

    void Awake()
    {
        myTransform = transform;
    }

    // Flock 매니저와 연결
    public void AssignFlock(Flock flock)
    {
        myFlock = flock;
    }

    // 초기 속도 설정
    public void InitializeSpeed(float initialSpeed)
    {
        speed = initialSpeed;
    }
}
