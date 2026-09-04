/******************************************************************************
 * Copyright (C) Ultraleap, Inc. 2011-2020.                                   *
 *                                                                            *
 * Use subject to the terms of the Apache License 2.0 available at            *
 * http://www.apache.org/licenses/LICENSE-2.0, or another agreement           *
 * between Ultraleap and you, your company or other organization.             *
 ******************************************************************************/

using UnityEngine;
using Leap;
using Leap.Unity;

namespace HapE.Unity {

    /// <summary>
    /// A basic utility class to aid in creating pinch based actions.  Once linked with a HandModelBase, it can
    /// be used to detect pinch gestures that the hand makes.
    /// </summary>
    public class GrabDetectorBounded : AbstractHoldDetector {

    // A collider inside which the pinch is accepted. Outside rejected.
    public Collider boundingBox;
    protected const float MM_TO_M = 0.001f;

    [Tooltip("The distance at which to enter the grabbing state.")]
    [Header("Grab Settings")]
    [Range(0f,1f)]
    public float ActivateStrength = .85f; // Fist Strength

    public bool IsGrabbing { get { return this.IsHolding; } }
    public bool DidStartGrab { get { return this.DidStartHold; } }
    public bool DidEndGrab { get { return this.DidRelease; } }

    protected bool _isGrabbing = false;

    protected float _lastGrabTime = 0.0f;
    protected float _lastUngrabTime = 0.0f;

    public Hand hand;

    protected override void ensureUpToDate() {
      if (Time.frameCount == _lastUpdateFrame) {
        return;
      }
      _lastUpdateFrame = Time.frameCount;

      _didChange = false;

      //Hand hand = _handModel.GetLeapHand();

      if (hand == null) { // || !_handModel.IsTracked) {
        changeState(false);
        return;
      }

      //_distance = hand.GetFistStrength();
      _distance = hand.GrabStrength;
      if (IsActive) {
        if (_distance < ActivateStrength && boundingBox.bounds.Contains(hand.PalmPosition)) {
          changeState(false);
        }
      } else {
        if (_distance > ActivateStrength && boundingBox.bounds.Contains(hand.PalmPosition)) {
                    changeState(true);
        }
      }

      if (IsActive) {
        _lastPosition = _position;
        _lastRotation = _rotation;
        _lastDistance = _distance;
        _lastDirection = _direction;
        _lastNormal = _normal;
      }
      if (ControlsTransform) {
        transform.position = _position;
        transform.rotation = _rotation;
      }
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmos () {
      if (ShowGizmos && hand != null) {
        Color centerColor = Color.clear;
        Vector3 centerPosition = Vector3.zero;
        Quaternion circleRotation = Quaternion.identity;
        if (IsHolding) {
          centerColor = Color.green;
          centerPosition = Position;
          circleRotation = Rotation;
        } else {
          //Hand hand = _handModel.GetLeapHand();
          if (hand != null) {
            Finger thumb = hand.Fingers[0];
            Finger index = hand.Fingers[1];
            centerColor = Color.red;
            centerPosition = ((thumb.Bone(Bone.BoneType.TYPE_DISTAL).NextJoint + index.Bone(Bone.BoneType.TYPE_DISTAL).NextJoint) / 2);
            circleRotation = hand.Rotation;
          }
        }
        Vector3 axis;
        float angle;
        circleRotation.ToAngleAxis(out angle, out axis);
        Utils.DrawCircle(centerPosition, axis, _distance / ActivateStrength / 2, centerColor);
        Utils.DrawCircle(centerPosition, axis, _distance / ActivateStrength / 2, Color.blue);
      }
    }
    #endif
  }
}
