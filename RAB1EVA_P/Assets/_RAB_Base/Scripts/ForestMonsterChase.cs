using System.Collections;
using UnityEngine;

public sealed class ForestMonsterChase : MonoBehaviour
{
    public ForestMazeGrid grid;
    public Transform player;
    public PlayerInteractor playerInteractor;
    public GameObject bodyVisual;
    public CapsuleCollider bodyCollider;
    public Rigidbody monsterBody;
    public AudioClip stinger;
    public Vector2Int spawnCell;
    public Vector2Int exitCell;
    public float speed = 10.25f;
    public float startDelay = 2.7f;
    public float repulseDelay = 2.5f;
    public float pathRefresh = 0.22f;

    bool chasing;
    bool restarting;
    int lastPointCount;
    float nextPathTime;
    Vector2Int nextCell;

    void Start()
    {
        if (playerInteractor != null) lastPointCount = playerInteractor.points;
    }

    void Update()
    {
        if (!chasing && playerInteractor != null && playerInteractor.points > 0)
        {
            lastPointCount = playerInteractor.points;
            StartCoroutine(BeginAfterWarning());
        }
    }

    IEnumerator BeginAfterWarning()
    {
        if (chasing || restarting) yield break;
        restarting = true;
        if (stinger != null && player != null) AudioSource.PlayClipAtPoint(stinger, player.position, 0.85f);
        yield return new WaitForSeconds(startDelay);
        if (bodyVisual != null) bodyVisual.SetActive(true);
        if (bodyCollider != null) bodyCollider.enabled = true;
        chasing = true;
        restarting = false;
    }

    void FixedUpdate()
    {
        if (!chasing || player == null || grid == null) return;
        Vector3 destination;
        var currentCell = grid.WorldToCell(transform.position);
        var playerCell = grid.WorldToCell(player.position);
        if (currentCell == playerCell)
        {
            destination = new Vector3(player.position.x, 0, player.position.z);
        }
        else
        {
            if (Time.time >= nextPathTime)
            {
                if (grid.TryGetNextCell(transform.position, player.position, out nextCell)) nextPathTime = Time.time + pathRefresh;
            }
            destination = grid.CellCenter(nextCell);
        }
        var current = transform.position;
        Vector3 direction = destination - current;
        direction.y = 0;
        if (direction.sqrMagnitude > 0.001f)
        {
            Vector3 nextPosition = Vector3.MoveTowards(current, new Vector3(destination.x, 0, destination.z), speed * Time.fixedDeltaTime);
            if (monsterBody != null) monsterBody.MovePosition(nextPosition); else transform.position = nextPosition;
            Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 280f * Time.fixedDeltaTime);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!chasing || !collision.gameObject.CompareTag("Player")) return;
        chasing = false;
        if (bodyCollider != null) bodyCollider.enabled = false;
        transform.position = grid != null ? grid.CellCenter(spawnCell) : Vector3.zero;
        if (stinger != null && player != null) AudioSource.PlayClipAtPoint(stinger, player.position, 0.55f);
        StartCoroutine(ResumeAfterHit());
    }

    IEnumerator ResumeAfterHit()
    {
        restarting = true;
        yield return new WaitForSeconds(repulseDelay);
        if (playerInteractor != null && playerInteractor.points >= playerInteractor.winPoints) yield break;
        if (bodyVisual != null) bodyVisual.SetActive(true);
        if (bodyCollider != null) bodyCollider.enabled = true;
        chasing = true;
        restarting = false;
    }
}
