using UnityEngine;

// Main Camera 에 추가하세요. 기존 카메라 따라가기 스크립트는 체크를 꺼주세요.
// 캐릭터 등 뒤에서 따라가고, 캐릭터가 방향을 틀면 부드럽게 등 뒤로 돌아옵니다.
// 나무 등이 캐릭터를 가리면 카메라가 앞으로 당겨집니다.
public class BehindFollowCamera : MonoBehaviour
{
    public Transform target;                 // 비워두면 "Player" 를 자동으로 찾음
    public float distance = 3.2f;            // 캐릭터 뒤로 떨어진 거리
    public float height = 1.7f;              // 카메라 높이 (캐릭터 발 기준)
    public float lookHeight = 1.0f;          // 카메라가 바라보는 높이 (캐릭터 머리·어깨쯤)
    public float followSharpness = 8f;       // 위치 따라가는 빠르기
    public float turnSharpness = 3f;         // 등 뒤로 돌아오는 빠르기 (작을수록 느긋하게)
    public float minDistance = 0.8f;         // 가려졌을 때 최대로 당겨지는 거리
    public LayerMask obstacleMask = ~0;      // 가림 판정할 레이어

    float yaw;

    void Start()
    {
        if (target == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) target = p.transform;
        }
        if (target != null) yaw = target.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 캐릭터가 바라보는 방향으로 천천히 따라 돔
        yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

        Vector3 focus = target.position + Vector3.up * lookHeight;
        Vector3 desired = focus + rot * new Vector3(0f, height - lookHeight, -distance);

        // 캐릭터와 카메라 사이를 나무 등이 가리면 앞으로 당김 (캐릭터 자신은 무시)
        Vector3 toCam = desired - focus;
        float len = toCam.magnitude;
        if (len > 0.001f)
        {
            var hits = Physics.SphereCastAll(focus, 0.2f, toCam / len, len, obstacleMask, QueryTriggerInteraction.Ignore);
            float nearest = len;
            foreach (var h in hits)
                if (!h.transform.IsChildOf(target) && h.distance < nearest) nearest = h.distance;
            if (nearest < len) desired = focus + toCam / len * Mathf.Max(minDistance, nearest - 0.1f);
        }

        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
        transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
    }
}
