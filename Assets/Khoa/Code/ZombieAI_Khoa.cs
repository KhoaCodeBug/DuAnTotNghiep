using UnityEngine;

public class ZOmbieAI_Khoa : MonoBehaviour
{
    public float speed = 2f;
    public float detectionRange = 3f;
    public float viewAngle = 90f; // góc nhìn phía trước
    public LayerMask obstacleMask; // layer tường/vật cản
    Transform player;
    Rigidbody2D rb;
    Animator anim;

    Vector2 movement;
    Vector2 lastMove;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        float distance = Vector2.Distance(transform.position, player.position);
        
        if (CanSeePlayer())
        {
            Vector2 direction = player.position - transform.position;
            movement = direction.normalized;
            Debug.DrawRay(transform.position, (player.position - transform.position).normalized * detectionRange, Color.red);
        }
        else
        {
            movement = Vector2.zero;
        }

        if (movement != Vector2.zero)
        {
            lastMove = movement;
        }

        anim.SetFloat("MoveX", lastMove.x);
        anim.SetFloat("MoveY", lastMove.y);
        anim.SetFloat("Speed", movement.magnitude);
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + movement * speed * Time.fixedDeltaTime);
    }
    bool CanSeePlayer()
    {
        Vector2 toPlayer = player.position - transform.position;
        float distance = toPlayer.magnitude;

        if (distance > detectionRange)
            return false;

        // hướng zombie đang nhìn (dùng lastMove của bạn)
        Vector2 forward = lastMove.normalized;

        // nếu zombie đang đứng yên từ đầu game
        if (forward == Vector2.zero)
            forward = Vector2.up;

        float angle = Vector2.Angle(forward, toPlayer);

        if (angle > viewAngle * 0.5f)
            return false;

        // raycast kiểm tra vật cản
        RaycastHit2D hit = Physics2D.Raycast(transform.position, toPlayer.normalized, distance, obstacleMask);

        if (hit.collider != null)
            return false; // bị tường che

        return true;
    }
}