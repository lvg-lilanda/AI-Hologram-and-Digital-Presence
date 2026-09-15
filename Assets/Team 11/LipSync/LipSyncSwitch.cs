using UnityEngine;

/// <summary>
/// The switch named in S2W2_plan checklist item 2: "Build still runs with lip sync
/// disabled, and the switch is verified." uLipSync sits entirely behind this toggle
/// so turning it off can never affect audio playback, the Animator, or the
/// AI-answering pipeline (DigitalHumanSpeech / AvatarController / VisemePlayer) —
/// none of which know this component exists, and none of which are touched by it.
///
/// Default is OFF. The minimum demo must work with lipSyncEnabled = false; that's
/// the whole point of the switch existing as its own component instead of being
/// baked into the avatar setup.
///
/// Uses the base `Behaviour` type for the two uLipSync references so this file
/// compiles even before the uLipSync package is imported — drop it in first, wire
/// the references once uLipSync is installed.
/// </summary>
public class LipSyncSwitch : MonoBehaviour
{
    [Tooltip("Master switch for lip sync. OFF by default — the demo must work with this false.")]
    [SerializeField] private bool lipSyncEnabled = false;

    [Tooltip("The uLipSync component analysing the AudioSource (drag the uLipSync component here once installed).")]
    [SerializeField] private Behaviour uLipSyncComponent;

    [Tooltip("The uLipSyncBlendShape component driving the face mesh.")]
    [SerializeField] private Behaviour uLipSyncBlendShapeComponent;

    public bool LipSyncEnabled => lipSyncEnabled;

    private void Awake() => Apply();

    private void OnValidate() => Apply();

    private void Apply()
    {
        if (uLipSyncComponent != null) uLipSyncComponent.enabled = lipSyncEnabled;
        if (uLipSyncBlendShapeComponent != null) uLipSyncBlendShapeComponent.enabled = lipSyncEnabled;
    }

    /// <summary>Flip lip sync on/off at runtime — e.g. from a debug key during the VX Lab demo
    /// if it needs to be killed live without a rebuild.</summary>
    public void SetEnabled(bool value)
    {
        lipSyncEnabled = value;
        Apply();
    }
}
