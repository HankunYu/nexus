using Mirror;
using UnityEngine;

namespace Nexus.Networking.VR
{
    /// <summary>
    /// Network-synced VR player. Spawned per player in room.
    /// Local player samples from IVRTrackingProvider.
    /// Remote players receive interpolated pose data.
    /// </summary>
    public class NexusVRPlayer : NetworkBehaviour
    {
        // Private fields
        private Transform _head;
        private Transform _leftHand;
        private Transform _rightHand;
        private IVRTrackingProvider _trackingProvider;
        private IVRCustomState _customState;
        private VRPose _targetPose;
        private float _sendTimer;
        private float _sendInterval = 1f / 30f;
        private float _interpolationSpeed;
        private bool _hasTarget;

        // Public properties
        public Transform Head => _head;
        public Transform LeftHand => _leftHand;
        public Transform RightHand => _rightHand;

        public IVRTrackingProvider TrackingProvider
        {
            get => _trackingProvider;
            set => _trackingProvider = value;
        }

        public IVRCustomState CustomState
        {
            get => _customState;
            set => _customState = value;
        }

        // Unity callbacks
        private void Awake()
        {
            _head = CreateChildTransform("Head");
            _leftHand = CreateChildTransform("LeftHand");
            _rightHand = CreateChildTransform("RightHand");
        }

        private void Update()
        {
            if (_trackingProvider != null)
            {
                SampleLocalPose();
                SendPoseIfReady();
            }
            else if (_hasTarget)
            {
                InterpolateToTarget();
            }
        }

        // Public methods

        /// <summary>
        /// Configure network sync rate. Called by VRPlayerManager after spawn.
        /// </summary>
        public void ConfigureSync(int syncRateHz)
        {
            _sendInterval = 1f / syncRateHz;
            _interpolationSpeed = syncRateHz;
        }

        /// <summary>
        /// Directly apply a pose to the child transforms.
        /// </summary>
        public void ApplyPose(VRPose pose)
        {
            _head.localPosition = pose.Head.position;
            _head.localRotation = pose.Head.rotation;
            _leftHand.localPosition = pose.LeftHand.position;
            _leftHand.localRotation = pose.LeftHand.rotation;
            _rightHand.localPosition = pose.RightHand.position;
            _rightHand.localRotation = pose.RightHand.rotation;
        }

        /// <summary>
        /// Sample pose from tracking provider and apply immediately.
        /// </summary>
        public void SampleLocalPose()
        {
            if (_trackingProvider == null)
            {
                return;
            }

            VRPose pose = _trackingProvider.GetCurrentPose();
            ApplyPose(pose);
        }

        /// <summary>
        /// Set the target pose for interpolation (used for remote players).
        /// </summary>
        public void SetTargetPose(VRPose pose, float interpolationSpeed)
        {
            _targetPose = pose;
            _interpolationSpeed = interpolationSpeed;
            _hasTarget = true;
        }

        /// <summary>
        /// Interpolate toward target pose. Called in Update for remote players.
        /// deltaTime parameter exposed for testing; defaults to Time.deltaTime.
        /// </summary>
        public void InterpolateToTarget(float deltaTime = -1f)
        {
            if (!_hasTarget)
            {
                return;
            }

            if (deltaTime < 0f)
            {
                deltaTime = Time.deltaTime;
            }

            float t = Mathf.Clamp01(_interpolationSpeed * deltaTime);

            _head.localPosition = Vector3.Lerp(_head.localPosition, _targetPose.Head.position, t);
            _head.localRotation = Quaternion.Slerp(_head.localRotation, _targetPose.Head.rotation, t);
            _leftHand.localPosition = Vector3.Lerp(_leftHand.localPosition, _targetPose.LeftHand.position, t);
            _leftHand.localRotation = Quaternion.Slerp(_leftHand.localRotation, _targetPose.LeftHand.rotation, t);
            _rightHand.localPosition = Vector3.Lerp(_rightHand.localPosition, _targetPose.RightHand.position, t);
            _rightHand.localRotation = Quaternion.Slerp(_rightHand.localRotation, _targetPose.RightHand.rotation, t);
        }

        // Private methods
        private void SendPoseIfReady()
        {
            if (!isOwned)
            {
                return;
            }

            _sendTimer += Time.deltaTime;
            if (_sendTimer < _sendInterval)
            {
                return;
            }

            _sendTimer -= _sendInterval;

            var pose = new VRPose
            {
                Head = new Pose(_head.localPosition, _head.localRotation),
                LeftHand = new Pose(_leftHand.localPosition, _leftHand.localRotation),
                RightHand = new Pose(_rightHand.localPosition, _rightHand.localRotation)
            };

            CmdSendPose(pose);
        }

        [Command]
        private void CmdSendPose(VRPose pose)
        {
            RpcReceivePose(pose);
        }

        [ClientRpc(includeOwner = false)]
        private void RpcReceivePose(VRPose pose)
        {
            SetTargetPose(pose, _interpolationSpeed);
        }

        private Transform CreateChildTransform(string childName)
        {
            var child = new GameObject(childName).transform;
            child.SetParent(transform, false);
            return child;
        }
    }
}
