using UnityEngine;

namespace Team11.AI
{
    /// <summary>
    /// Fills VisemePlayer.ShapeMap by NAME instead of by hand-typed index. VisemePlayer
    /// expects "blend shape index on Face for each of Azure's 55 positions, in order" —
    /// typing 55 raw integers into the Inspector by hand is slow and breaks the moment
    /// the mesh is reimported and indices shift. This resolves names to indices at
    /// runtime via SkinnedMeshRenderer.GetBlendShapeIndex(), which conveniently returns
    /// -1 for any name not found — exactly VisemePlayer's own "ignore this position"
    /// convention, so an empty/unmatched entry just does nothing rather than erroring.
    ///
    /// The default 55 names below are Azure's documented BlendShapes order (Microsoft
    /// Learn, "Get facial position with viseme" — Table: Order 1-55) matched to Jake's
    /// actual CC_Base morph target names, confirmed present in Jake.fbx. Entries left
    /// as "" are Azure positions with no real Jake equivalent — mostly eye-look and
    /// jaw-direction values, which this rig drives with BONES, not blendshapes, and a
    /// few pure head/eye ROTATION values (headRoll, leftEyeRoll, rightEyeRoll) that a
    /// blendshape weight can't represent at all regardless of the mesh.
    ///
    /// Several entries are best-guess single-target approximations where Azure has a
    /// left/right pair or a nuance Jake doesn't split out identically (e.g. cheekPuff
    /// only has Cheek_Blow_L/R separately, not a combined shape) — expect to retune a
    /// few of these by eye once you see Jake actually talking, not a guarantee of a
    /// perfect first pass.
    /// </summary>
    [RequireComponent(typeof(VisemePlayer))]
    public class VisemeShapeMapBuilder : MonoBehaviour
    {
        [Tooltip("Azure's 55 BlendShapes positions in order (1-55), each resolved by name " +
                 "against Face's mesh. Leave an entry blank to ignore that position.")]
        public string[] ShapeNames = new string[]
        {
            /*1  eyeBlinkLeft      */ "Eye_Blink_L",
            /*2  eyeLookDownLeft   */ "",
            /*3  eyeLookInLeft     */ "",
            /*4  eyeLookOutLeft    */ "",
            /*5  eyeLookUpLeft     */ "",
            /*6  eyeSquintLeft     */ "Eye_Squint_L",
            /*7  eyeWideLeft       */ "Eye_Wide_L",
            /*8  eyeBlinkRight     */ "Eye_Blink_R",
            /*9  eyeLookDownRight  */ "",
            /*10 eyeLookInRight    */ "",
            /*11 eyeLookOutRight   */ "",
            /*12 eyeLookUpRight    */ "",
            /*13 eyeSquintRight    */ "Eye_Squint_R",
            /*14 eyeWideRight      */ "Eye_Wide_R",
            /*15 jawForward        */ "",
            /*16 jawLeft           */ "",
            /*17 jawRight          */ "",
            /*18 jawOpen           */ "Mouth_Open",
            /*19 mouthClose        */ "Tight",
            /*20 mouthFunnel       */ "Mouth_Pucker_Open",
            /*21 mouthPucker       */ "Mouth_Pucker",
            /*22 mouthLeft         */ "Mouth_L",
            /*23 mouthRight        */ "Mouth_R",
            /*24 mouthSmileLeft    */ "Mouth_Smile_L",
            /*25 mouthSmileRight   */ "Mouth_Smile_R",
            /*26 mouthFrownLeft    */ "Mouth_Frown_L",
            /*27 mouthFrownRight   */ "Mouth_Frown_R",
            /*28 mouthDimpleLeft   */ "Mouth_Dimple_L",
            /*29 mouthDimpleRight  */ "Mouth_Dimple_R",
            /*30 mouthStretchLeft  */ "Mouth_Widen",
            /*31 mouthStretchRight */ "Mouth_Widen_Sides",
            /*32 mouthRollLower    */ "Mouth_Lips_Tuck",
            /*33 mouthRollUpper    */ "Mouth_Top_Lip_Under",
            /*34 mouthShrugLower   */ "Mouth_Bottom_Lip_Under",
            /*35 mouthShrugUpper   */ "Mouth_Top_Lip_Up",
            /*36 mouthPressLeft    */ "Mouth_Lips_Tight",
            /*37 mouthPressRight   */ "Mouth_Lips_Tight",
            /*38 mouthLowerDownLeft*/ "Mouth_Bottom_Lip_Down",
            /*39 mouthLowerDownRight*/ "Mouth_Bottom_Lip_Down",
            /*40 mouthUpperUpLeft  */ "Mouth_Top_Lip_Up",
            /*41 mouthUpperUpRight */ "Mouth_Top_Lip_Up",
            /*42 browDownLeft      */ "Brow_Drop_L",
            /*43 browDownRight     */ "Brow_Drop_R",
            /*44 browInnerUp       */ "Brow_Raise_Inner_L",
            /*45 browOuterUpLeft   */ "Brow_Raise_Outer_L",
            /*46 browOuterUpRight  */ "Brow_Raise_Outer_R",
            /*47 cheekPuff         */ "Cheek_Blow_L",
            /*48 cheekSquintLeft   */ "Cheek_Raise_L",
            /*49 cheekSquintRight  */ "Cheek_Raise_R",
            /*50 noseSneerLeft     */ "Nose_Flank_Raise_L",
            /*51 noseSneerRight    */ "Nose_Flank_Raise_R",
            /*52 tongueOut         */ "",   // Jake has tongue BONES, no tongue blendshape
            /*53 headRoll          */ "",   // rotation value, not a blendshape — out of scope
            /*54 leftEyeRoll       */ "",   // rotation value, not a blendshape — out of scope
            /*55 rightEyeRoll      */ "",   // rotation value, not a blendshape — out of scope
        };

        private void Awake() => Build();

        [ContextMenu("Build ShapeMap Now")]
        public void Build()
        {
            var player = GetComponent<VisemePlayer>();
            if (player == null || player.Face == null || player.Face.sharedMesh == null)
            {
                Debug.LogError("[VisemeShapeMapBuilder] VisemePlayer.Face (SkinnedMeshRenderer) " +
                               "must be assigned before building the map.");
                return;
            }

            var mesh = player.Face.sharedMesh;
            int shapeCount = mesh.blendShapeCount;
            var realNames = new string[shapeCount];
            for (int i = 0; i < shapeCount; i++) realNames[i] = mesh.GetBlendShapeName(i);

            var map = new int[ShapeNames.Length];
            int matched = 0;

            for (int i = 0; i < ShapeNames.Length; i++)
            {
                string name = ShapeNames[i];
                int index = -1;

                if (!string.IsNullOrEmpty(name))
                {
                    // Try an exact match first, in case the mesh really does use bare names.
                    index = mesh.GetBlendShapeIndex(name);

                    // Reallusion/Character Creator FBX exports (Jake included) prefix every
                    // channel with the source mesh name, e.g. "Morpher_CC_Base_Body.Eye_Blink_L"
                    // instead of just "Eye_Blink_L" — confirmed from the "not found" warnings.
                    // Fall back to matching any real name ending with "." + the short name, so
                    // this doesn't depend on knowing the exact prefix string.
                    if (index < 0)
                    {
                        for (int j = 0; j < realNames.Length; j++)
                        {
                            string real = realNames[j];
                            if (real.Equals(name, System.StringComparison.OrdinalIgnoreCase) ||
                                real.EndsWith("." + name, System.StringComparison.OrdinalIgnoreCase))
                            {
                                index = j;
                                break;
                            }
                        }
                    }

                    if (index < 0)
                        Debug.LogWarning($"[VisemeShapeMapBuilder] position {i + 1}: no blend shape " +
                                          $"matching \"{name}\" found on {mesh.name} (checked {shapeCount} shapes).");
                }

                if (index >= 0) matched++;
                map[i] = index;
            }

            player.ShapeMap = map;
            Debug.Log($"[VisemeShapeMapBuilder] Built ShapeMap: {matched}/{ShapeNames.Length} " +
                      $"positions matched a real blend shape on {mesh.name}.");
        }
    }
}