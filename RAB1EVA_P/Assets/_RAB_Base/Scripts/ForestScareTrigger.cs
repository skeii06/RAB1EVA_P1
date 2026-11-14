using System.Collections;
using UnityEngine;

public sealed class ForestScareTrigger : MonoBehaviour
{
    public Transform player;
    public GameObject flash;
    public AudioClip stinger;
    bool activated;

    void OnTriggerEnter(Collider other)
    {
        if (activated || !other.CompareTag("Player")) return;
        activated = true;
        if (flash != null) flash.SetActive(true);
        if (stinger != null)
        {
            Vector3 position = player != null ? player.position : transform.position;
            AudioSource.PlayClipAtPoint(stinger, position, 0.65f);
        }
        StartCoroutine(EndFlash());
    }

    IEnumerator EndFlash()
    {
        yield return new WaitForSeconds(0.45f);
        if (flash != null) flash.SetActive(false);
    }
}
