using UnityEngine;

public sealed class BosqueCameraFollow : MonoBehaviour
{
    public Transform target;
    public float fixedX;
    public float fixedY = 0.55f;
    public bool followX;

    public void Snap()
    {
        if (target != null)
            transform.position = new Vector3(followX ? target.position.x + fixedX : fixedX, fixedY, target.position.z);
    }

    void Update()
    {
        Snap();
    }
}
