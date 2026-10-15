using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    [SerializeField] bool yawOnly = true;

    Camera cam;

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 dir = transform.position - cam.transform.position;
        if (yawOnly) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }
}
