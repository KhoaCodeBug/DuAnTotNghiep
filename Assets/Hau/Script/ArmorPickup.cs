using UnityEngine;

public class ArmorPickup : MonoBehaviour
{
    public ArmorItem armorItem;

    public float bobSpeed = 2f;
    public float bobHeight = 0.25f;
    public float rotateSpeed = 90f;

    private Vector3 startPos;
    private bool playerInRange = false;
    private PlayerArmor playerArmor;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // Nhún
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(startPos.x, newY, startPos.z);

        // Xoay
        transform.Rotate(Vector3.forward * rotateSpeed * Time.deltaTime);

        // Nhặt
        if (playerInRange && Input.GetKeyDown(KeyCode.F) && playerArmor != null)
        {
            playerArmor.AddArmor(armorItem);
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            playerArmor = other.GetComponent<PlayerArmor>();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            playerArmor = null;
        }
    }
}