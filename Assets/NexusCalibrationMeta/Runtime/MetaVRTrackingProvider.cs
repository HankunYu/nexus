using Nexus.Networking.VR;
using UnityEngine;

namespace Nexus.Calibration.Meta
{
    /// <summary>
    /// IVRTrackingProvider implementation for Meta Quest using OVRCameraRig.
    /// Reads head and hand tracking data from OVR anchors each frame.
    /// </summary>
    public class MetaVRTrackingProvider : MonoBehaviour, IVRTrackingProvider
    {
#if META_XR_SDK
        [SerializeField] private OVRCameraRig _cameraRig;

        private void Awake()
        {
            if (_cameraRig == null)
            {
                _cameraRig = FindAnyObjectByType<OVRCameraRig>();
            }

            if (_cameraRig == null)
            {
                Debug.LogWarning("[MetaVRTrackingProvider] OVRCameraRig not found. Tracking will return identity poses.");
            }
        }

        public VRPose GetCurrentPose()
        {
            if (_cameraRig == null)
            {
                return default;
            }

            return new VRPose
            {
                Head = new Pose(
                    _cameraRig.centerEyeAnchor.position,
                    _cameraRig.centerEyeAnchor.rotation),
                LeftHand = new Pose(
                    _cameraRig.leftHandAnchor.position,
                    _cameraRig.leftHandAnchor.rotation),
                RightHand = new Pose(
                    _cameraRig.rightHandAnchor.position,
                    _cameraRig.rightHandAnchor.rotation)
            };
        }
#else
        public VRPose GetCurrentPose()
        {
            Debug.LogWarning("[MetaVRTrackingProvider] Meta XR SDK not available.");
            return default;
        }
#endif
    }
}
