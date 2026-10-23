using UnityEngine;

public class PlatformSwitch : MonoBehaviour
{
    public GameObject objectToActive; //Ref al gameobject que el switch activará cuando el player choque con él

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            objectToActive.SetActive(true); //El objeto almacenado se enciende
            gameObject.SetActive(false); //El propio switch se apaga y desaparece de escena
        }
    }
}
