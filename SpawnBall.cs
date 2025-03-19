using UnityEngine;

public class SpawnBall : MonoBehaviour
{
    public GameObject prefab;
    public float spawnSpeed = 5f;

    void Update()
    {
        if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch)) // Right-hand trigger
        {
            GameObject spawnedBall = Instantiate(prefab, transform.position, Quaternion.identity);
            Rigidbody spawnedBallRB = spawnedBall.GetComponent<Rigidbody>();
            spawnedBallRB.linearVelocity = transform.forward * spawnSpeed;
        }
    }
}
