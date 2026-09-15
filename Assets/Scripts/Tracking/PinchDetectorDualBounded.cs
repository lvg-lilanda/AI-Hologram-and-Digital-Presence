/******************************************************************************
 * Copyright (C) Ultraleap, Inc. 2011-2022.                                   *
 *                                                                            *
 * Use subject to the terms of the Apache License 2.0 available at            *
 * http://www.apache.org/licenses/LICENSE-2.0, or another agreement           *
 * between Ultraleap and you, your company or other organization.             *
 ******************************************************************************/

using UnityEngine;
using UnityEngine.Events;
using Leap.Unity.Attributes;
using UnityEngine.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Leap.Unity {

  /// <summary>
  /// Derived from PinchDetectorBounded to allow to 'undectecting' pinches when outside the boundingbox
  /// The member releaseBoundingBox constrains the space in the detector can change its detected state to false
  /// </summary>
  public class PinchDetectorDualBounded : PinchDetectorBounded {
    public Collider releaseBoundingBox;

    public float releaseVelocityThreshold = 1.0f;

    public float _currentPinchVelocity = 0.0f;

    public bool IsPinchingAndMovingFast { get { return this.isPinchingAndMovingFast_; } }

    [Tooltip("Delay in firing OnActivate")]
    public float DelayOnActivateInSeconds = 1f;

    [Tooltip("Delay in firing OnDeactivate")]
    public float DelayOnDeactivateInSeconds = 1f;

    [Tooltip(@"Apply some weights depending on dimension of movement, we're only interested in y motion 
              Large x and z weights result in amplified velocities and contribute more to exceeding the threshold")]
    public Vector3 velocityWeights = new Vector3(10.0f, 1.0f, 10.0f);

    [Tooltip(@"Allow pinch events to work for hand playback, 
              i.e hands that not tracked, but previously recorded.")]
    public bool IgnoreModelTrackedState = false;
    public UnityEvent OnActivateDelayed = new UnityEvent();

    public UnityEvent OnDeactivateDelayed = new UnityEvent();

    public bool _delayedPinchActive = false;

    // private member fields below

    private bool isPinchingAndMovingFast_;
    private Vector3 _previousPinchPosition = new Vector3();

    private Coroutine _delayedOnActivateCoroutine = null;

    private Coroutine _delayedOnDeactivateCoroutine = null;

    // private methods below
    private float GetPinchDistance(Hand hand) {
      var indexTipPosition = hand.GetIndex().TipPosition;
      var thumbTipPosition = hand.GetThumb().TipPosition;
      return Vector3.Distance(indexTipPosition, thumbTipPosition);
    }

    private float calculateVelocity(Vector3 currentPosition, Vector3 directionMask)
    {
        Vector3 maskedPosition = new Vector3();
        maskedPosition.x = currentPosition.x * directionMask.x;
        maskedPosition.y = currentPosition.y * directionMask.y;
        maskedPosition.z = currentPosition.z * directionMask.z;

        var distance = Vector3.Distance(_previousPinchPosition, maskedPosition);
        _previousPinchPosition = maskedPosition;
        var _numFramesSinceLastTime = Time.frameCount - _lastUpdateFrame;
        float frameRatePerSecond_ = 60f; //todo check if we're at 60fps
        return distance / (1.0f + (float)_numFramesSinceLastTime) * frameRatePerSecond_;
    }

    // Used if we're considering an index+middle finger pinch.
    private List<float> GetPinchDistances(Hand hand)
    {
        var indexTipPosition = hand.GetIndex().TipPosition;
        var middleTipPosition = hand.GetMiddle().TipPosition;
        var thumbTipPosition = hand.GetThumb().TipPosition;
        _currentPinchVelocity = calculateVelocity(thumbTipPosition, velocityWeights);
        var distanceList = new List<float>{ Vector3.Distance(indexTipPosition, thumbTipPosition), Vector3.Distance(middleTipPosition, thumbTipPosition)};
        return distanceList;
    }

    bool IsHandModelTracked()
    {
      return IgnoreModelTrackedState || _handModel.IsTracked;
    }
    protected override void ensureUpToDate() {
      if (Time.frameCount == _lastUpdateFrame) {
        return;
      }
      _lastUpdateFrame = Time.frameCount;

      _didChange = false;

      Hand hand = _handModel.GetLeapHand();

      if (hand == null || !IsHandModelTracked()) {
        changeState(false);
        return;
      }

      _rotation = hand.Basis.rotation;
      if (!useMiddleAndIndex){
        _distance = GetPinchDistance(hand);
        _position = ((hand.Fingers[0].TipPosition + hand.Fingers[1].TipPosition) * .5f);
      }
      // Check for either close index-thumb, or middle-thumb distance
      else {
          List<float> _distances = GetPinchDistances(hand);
          int pinchiestFingerIndex = _distances.IndexOf(_distances.Min());
          _distance = _distances[pinchiestFingerIndex];
          // Thumb is index 0, index is 1, middle is 2, so add 1 to pinchiest finger...
          _position = ((hand.Fingers[0].TipPosition + hand.Fingers[pinchiestFingerIndex+1].TipPosition) * .5f);
      }

      if (IsActive) {
        // set releaseBoundingBox to boundingbox
        bool releaseBoundsContainHand = false;
        if (releaseBoundingBox != null){
          releaseBoundsContainHand = releaseBoundingBox.bounds.Contains(hand.PalmPosition);
        } else {
          releaseBoundsContainHand = true;
        }

        if (_distance > DeactivateDistance && releaseBoundsContainHand) {
          changeState(false);
          isPinchingAndMovingFast_ = false;
        } else {
          isPinchingAndMovingFast_ = (_currentPinchVelocity >= releaseVelocityThreshold);
        }

      } else {
        if (_distance < ActivateDistance && boundingBox.bounds.Contains(hand.PalmPosition)) {
            changeState(true);
        }

        isPinchingAndMovingFast_ = (_currentPinchVelocity >= releaseVelocityThreshold);

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


    private IEnumerator delayedOnActivateCoroutine()
    {
        yield return new WaitForSeconds(DelayOnActivateInSeconds);
        Debug.Log("Invoke delayed pinch activated event.");
        this.OnActivateDelayed?.Invoke();
        _delayedPinchActive = true;
        yield return null;
    }

    private IEnumerator delayedOnDeactivateCoroutine()
    {
        yield return new WaitForSeconds(DelayOnDeactivateInSeconds);
        //Debug.Log("Invoke delayed pinch deactivated event.");
        // always fire the deactivate event, even if we're still delaying the pinch active event
        this.OnDeactivateDelayed?.Invoke();
        _delayedPinchActive = false;
        yield return null;
    }

    void HandleBaseOnActivate()
    {
        if (_delayedOnActivateCoroutine != null)
        {
          StopCoroutine(_delayedOnActivateCoroutine);
        }

        if (_delayedOnDeactivateCoroutine != null)
        {
          StopCoroutine(_delayedOnDeactivateCoroutine);
        }

        _delayedPinchActive = false;

        _delayedOnActivateCoroutine =
          StartCoroutine(delayedOnActivateCoroutine());
        
    }
    void HandleBaseOnDeactivate()
    {
        if (_delayedOnActivateCoroutine != null)
        {
          //Debug.Log("StopCoroutine _delayedOnActivateCoroutine");
          StopCoroutine(_delayedOnActivateCoroutine);
        }
        else
        {
          //Debug.Log("_delayedOnActivateCoroutine is null");
        }

        if (_delayedOnDeactivateCoroutine != null)
        {
          //Debug.Log("StopCoroutine _delayedOnDeactivateCoroutine");
          StopCoroutine(_delayedOnDeactivateCoroutine);
        }
        else
        {
          //Debug.Log("_delayedOnDeactivateCoroutine is null");
        }

        if (!_delayedPinchActive )
          //Debug.LogWarning("_delayedPinchActive should be true!");

        _delayedOnDeactivateCoroutine = StartCoroutine(delayedOnDeactivateCoroutine());

    }

    void OnEnable()
    {
        base.OnActivate.AddListener(HandleBaseOnActivate);
        base.OnDeactivate.AddListener(HandleBaseOnDeactivate);
    }

    void OnDisable()
    {
        base.OnActivate.RemoveListener(HandleBaseOnActivate);
        base.OnDeactivate.RemoveListener(HandleBaseOnDeactivate);
    }

  }
}
