
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class DartTarget : MonoBehaviour
{
    [Header("Score")]
    public int score = 0;
    public TMP_Text scoreText;

    [Header("Scoring Zones")]
    public float innerRadius = 0.15f;
    public float middleRadius = 0.30f;
    public float outerRadius = 0.50f;

    [Header("Visual Feedback")]
    public Renderer centerRenderer;
    public Renderer middleRenderer;
    public Renderer outerRenderer;
    public float flashDuration = 3f;

    [Header("XR Interaction")]
    public XRGrabInteractable dartGrab;

    private bool hasScored = false;
    private Coroutine flashCoroutine;
    private Renderer flashingRenderer;
    private Color originalFlashColor;

    private void Start()
    {
        UpdateScoreText();
    }

    private void OnEnable()
    {
        if (dartGrab != null)
            dartGrab.selectEntered.AddListener(OnGrab);
    }

    private void OnDisable()
    {
        if (dartGrab != null)
            dartGrab.selectEntered.RemoveListener(OnGrab);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        hasScored = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsValidHit(collision))
            return;

        Vector3 hitPoint = collision.GetContact(0).point;
        int points = CalculateScore(hitPoint);

        hasScored = true;
        score += points;

        UpdateScoreText();

        Debug.Log($"Puncte obtinute: {points} | Scor total: {score}");

        PlayHitEffect(points);
    }

    private bool IsValidHit(Collision collision)
    {
        if (dartGrab == null || dartGrab.isSelected || hasScored)
            return false;

        Rigidbody dartBody = dartGrab.GetComponent<Rigidbody>();

        return collision.rigidbody == dartBody && dartBody != null;
    }

    private int CalculateScore(Vector3 hitPoint)
    {
        Vector3 localHit = transform.InverseTransformPoint(hitPoint);
        float distance = new Vector2(localHit.x, localHit.z).magnitude;

        Debug.Log($"Distanta fata de centru: {distance}");

        if (distance <= innerRadius)
            return 50;

        if (distance <= middleRadius)
            return 25;

        if (distance <= outerRadius)
            return 10;

        return 0;
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
    }

    private Renderer GetHitRenderer(int points)
    {
        switch (points)
        {
            case 50:
                return centerRenderer;
            case 25:
                return middleRenderer;
            case 10:
                return outerRenderer;
            default:
                return null;
        }
    }

    private void PlayHitEffect(int points)
    {
        Renderer hitRenderer = GetHitRenderer(points);

        if (hitRenderer == null)
            return;

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);

            if (flashingRenderer != null)
                flashingRenderer.material.color = originalFlashColor;
        }

        flashCoroutine = StartCoroutine(FlashTarget(hitRenderer));
    }

    private IEnumerator FlashTarget(Renderer zoneRenderer)
    {
        flashingRenderer = zoneRenderer;
        originalFlashColor = zoneRenderer.material.color;

        zoneRenderer.material.color = Color.green;

        yield return new WaitForSeconds(flashDuration);

        zoneRenderer.material.color = originalFlashColor;

        flashingRenderer = null;
        flashCoroutine = null;
    }
}
