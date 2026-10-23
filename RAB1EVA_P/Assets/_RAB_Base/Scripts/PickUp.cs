using UnityEngine;

public class PickUp : MonoBehaviour
{
    [Header("Rotation Parameters")]
    public float speedX = 10;
    public float speedY = 20;
    public float speedZ = 30;

    // Update is called once per frame
    void Update()
    {
        RotateObject();
    }

    void RotateObject()
    {
        transform.Rotate(Vector3.right * speedX * Time.deltaTime);
        transform.Rotate(Vector3.up * speedY * Time.deltaTime);
        transform.Rotate(Vector3.forward * speedZ * Time.deltaTime);
    }
}
