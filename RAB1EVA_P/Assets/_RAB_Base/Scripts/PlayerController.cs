using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Parameters")]
    public float speed; //Variable que define la velocidad del player
    public float jumpForce; //Define la fuerza de salto
    public bool useLinearVelocity = true; //Si es verdadero, usará el movimiento físico por motor de aceleración, si no, el de fuerza de empuje
    public bool groundCheck; //Bool que determina si el pj puede saltar o no (cuando está en el suelo puede)


    [Header("Reference Variables")]
    public Rigidbody playerRb; //Almacén de referencia del rb, una vez relleno permite modificar sus variables internas
    public Vector2 moveInput; //Almacén de los valores de pulsación de movimiento (0, 1, -1)
    public AudioSource sfxSource; //Ref al altavoz del player, para reproducir sonidos
    public PlayerInteractor playerInteractor; //Ref al script, permite acceder a su lista de sonidos públicos



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //Ejecuta todo lo que depende del procesador y ejecuta el input
        //CinematicMove();
    }

    private void FixedUpdate()
    {
        //FixedUpdate ejecuta todo lo que tenga que ver con el motor de físicas
        PhysicalMove();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            groundCheck = true; //Al chocar con el suelo se devuelve la posibilidad de saltar
        }
    }

    void CinematicMove()
    {
        //Se usa transform.Translate para moverse (dirección * velocidad)
        transform.Translate(Vector3.forward * speed * moveInput.y * Time.deltaTime);
        transform.Translate(Vector3.right * speed * moveInput.x * Time.deltaTime);
    }

    void PhysicalMove()
    {
        if (useLinearVelocity == true)
        {
            //Tipo de movimiento por uso del motor de aceleración del rigidbody
            //A cada eje del rigidbody se le multiplica la velocidad * input por separado
            playerRb.linearVelocity = new Vector3(speed * moveInput.x, playerRb.linearVelocity.y, speed * moveInput.y);
        }
        else
        {
            //Tipo de movimiento por fuerza de empuje en cada eje del rigidbody
            //A cada eje se le aplica un AddForce en la dirección concreta
            playerRb.AddForce(Vector3.right * speed * moveInput.x);
            playerRb.AddForce(Vector3.forward * speed * moveInput.y);
        }
    }

    void Jump()
    {
        groundCheck = false; //Al saltar, se quita la posibilidad de volver a saltar hasta tocar suelo
        playerRb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        //Reproducir sonido de salto: accede al player interactor y busca el sonido exacto en la lista
        sfxSource.PlayOneShot(playerInteractor.sfxList[0]);
    }

    #region Input Methods
    //Declaramos los métodos de input (eventos de unity)
    //Siguen esta estructura: public void NombreAccion(referencia al contexto = botones de la acción)
    public void OnMove(InputAction.CallbackContext context)
    {
        //Se rellena la variable de referencia con el valor de cualquier contexto
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            //Si aprieto el botón...
            //Y si estoy en el suelo...
            if (groundCheck == true)
            {
                Jump();
            }
            
        }
        
    }

    #endregion
}
