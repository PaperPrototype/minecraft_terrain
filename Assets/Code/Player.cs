using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class Player : MonoBehaviour
{
    public new GameObject camera;

    // rotational values
    private Vector3 selfRot;
    private Vector3 camRot;

    public float moveForce = 0.5f;
    public float jumpVelocity = 10f;
    private Rigidbody m_rigidbody;

    // Start is called before the first frame update
    void Start()
    {
        m_rigidbody = gameObject.GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        selfRot.y += Input.GetAxis("Mouse X");
        gameObject.transform.eulerAngles = selfRot;

        camRot.x -= Input.GetAxis("Mouse Y");
        camera.transform.localEulerAngles = camRot;

        if (Input.GetButtonDown("Jump"))
        {
            m_rigidbody.velocity += new Vector3(0f, jumpVelocity, 0f);
        }

        // move in the direction the camera is facing
        m_rigidbody.AddForce(Input.GetAxis("Vertical") * moveForce * camera.transform.forward, ForceMode.Impulse);
    }
}
