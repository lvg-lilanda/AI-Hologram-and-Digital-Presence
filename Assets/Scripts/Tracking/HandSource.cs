using Leap;
using Leap.Unity;
using Leap.Unity.HandsModule;
using UnityEngine;

namespace HapE.Unity
{
    public class HandSource : MonoBehaviour
    {

        public enum SelectSource
        {
            Live,
            Playback,
            Available
        }

        public SelectSource selectSource = SelectSource.Live;

        public HandBinder genericHandBinderLeft = null;
        public HandBinder genericHandBinderRight = null;

        public Chirality chirality = Chirality.Left;

        public Hand GetLeapHand()
        {
            return GetHand(chirality);
        }

        // get hands from different sources, 
        // try global hands first, other wise use the hand binder for recorded hands
        public Hand GetHand(Chirality chirality)
        {

            Hand hand = Hands.Get(chirality);

            if (selectSource != SelectSource.Playback &&
                selectSource != SelectSource.Available)
            {
                return hand;
            }

            if (selectSource == SelectSource.Playback)
            {
                hand = null;
            }

            if (chirality == Chirality.Left)
            {
                hand = genericHandBinderLeft?.GetLeapHand();
            }
            else if (chirality == Chirality.Right)
            {
                hand = genericHandBinderRight?.GetLeapHand();
            }
            else
            {
                Debug.LogWarning("Chirality not recognised");
            }

            return hand;
        }

        // Static helper to copy with null handSource and fall back LeapHands
        // to maintain backwards compatibility
        public static Hand GetHand(Chirality chirality, HandSource handSource)
        {
            if (handSource != null)
            {
                return handSource.GetHand(chirality);
            }
            else
            {
                return Hands.Get(chirality);
            }
        }
    }
}
