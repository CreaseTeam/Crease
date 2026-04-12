using UnityEngine;

public class ShadowPosition : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private float shadowDistance = 1f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        this.transform.position = new Vector3(player.transform.position.x, player.transform.position.y - shadowDistance, player.transform.position.z);
    }

    // Update is called once per frame
    void Update()
    {
         this.transform.position = new Vector3(player.transform.position.x, player.transform.position.y - shadowDistance, player.transform.position.z);
    }
}
