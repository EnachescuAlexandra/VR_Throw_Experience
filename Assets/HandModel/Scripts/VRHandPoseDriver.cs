using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Uses the resting and closed finger rotations baked from the Blender FBX clips.
/// This bypasses Animator state transitions, so fingers remain closed while the grip/trigger is held.
/// Install / configure via Tools > VR Hands > Install or Repair in Current Scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class VRHandPoseDriver : MonoBehaviour
{
    [Serializable]
    public struct FingerPose
    {
        public Transform bone;
        public Quaternion open;
        public Quaternion closed;
    }

    [Header("Imported Blender poses (configured by installer)")]
    public FingerPose[] grabFingers = Array.Empty<FingerPose>();
    public FingerPose[] triggerFingers = Array.Empty<FingerPose>();

    [Header("XR input")]
    public InputActionAsset actions;
    public bool leftHand;
    [Min(0.1f)] public float blendSpeed = 8f;
    public bool allowKeyboardTest = true;

    private InputAction grabAction;
    private InputAction triggerAction;
    private float grabAmount;
    private float triggerAmount;

    private void Awake()
    {
        // Only our hand hierarchy is affected. The tracked XR Controller parent stays active.
        // Disabling the hand's Animator avoids it overwriting the sampled bone poses each frame.
        foreach (var animator in GetComponentsInChildren<Animator>(true))
            animator.enabled = false;

        string prefix = leftHand ? "XRI Left Interaction/" : "XRI Right Interaction/";
        if (actions != null)
        {
            grabAction = actions.FindAction(prefix + "Select Value", false);
            triggerAction = actions.FindAction(prefix + "Activate Value", false);
        }
    }

    private void OnEnable()
    {
        if (grabAction != null && !grabAction.enabled) grabAction.Enable();
        if (triggerAction != null && !triggerAction.enabled) triggerAction.Enable();
    }

    private void Update()
    {
        float targetGrab = ReadInput(grabAction);
        float targetTrigger = ReadInput(triggerAction);

        if (allowKeyboardTest && Keyboard.current != null)
        {
            // G and T work even without a headset, when the Game window has focus.
            if (Keyboard.current.gKey.isPressed) targetGrab = 1f;
            if (Keyboard.current.tKey.isPressed) targetTrigger = 1f;
        }

        float step = blendSpeed * Time.unscaledDeltaTime;
        grabAmount = Mathf.MoveTowards(grabAmount, Mathf.Clamp01(targetGrab), step);
        triggerAmount = Mathf.MoveTowards(triggerAmount, Mathf.Clamp01(targetTrigger), step);
    }

    private void LateUpdate()
    {
        Apply(grabFingers, grabAmount);
        Apply(triggerFingers, triggerAmount);
    }

    private static float ReadInput(InputAction action)
    {
        if (action == null || !action.enabled) return 0f;
        // The XRI Select Value / Activate Value actions are float controls.
        try { return Mathf.Clamp01(action.ReadValue<float>()); }
        catch (InvalidOperationException) { return action.IsPressed() ? 1f : 0f; }
    }

    private static void Apply(FingerPose[] fingers, float amount)
    {
        if (fingers == null) return;
        foreach (var finger in fingers)
            if (finger.bone != null)
                finger.bone.localRotation = Quaternion.Slerp(finger.open, finger.closed, amount);
    }
}
