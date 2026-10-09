
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class KnifeReset : MonoBehaviour
{
    public float resetDelay = 5f;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private Rigidbody rb;
    private XRGrabInteractable grab;
    private Coroutine resetCoroutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void OnEnable()
    {
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrab);
            grab.selectExited.AddListener(OnRelease);
        }
    }

    private void OnDisable()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }

        CancelReset();
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        CancelReset();
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        CancelReset();
        resetCoroutine = StartCoroutine(ResetAfterDelay());
    }

    private IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);

        if (grab != null && grab.isSelected)
            yield break;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.position = startPosition;
        rb.rotation = startRotation;

        rb.Sleep();
        resetCoroutine = null;
    }

    private void CancelReset()
    {
        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }
    }
}
