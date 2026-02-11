using System;
using System.Collections.Generic;
using System.Threading;
using Nexus.Networking.VR.Calibration;
using UnityEngine;
#if META_XR_SDK
using System.Threading.Tasks;
#endif

namespace Nexus.Calibration.Meta
{
    /// <summary>
    /// Calibration provider using Meta Quest Shared Spatial Anchors (Group-based sharing).
    /// Requires Meta XR SDK (com.meta.xr.sdk.core).
    ///
    /// Host flow: Create OVRSpatialAnchor -> Share to group -> Fire OnAnchorShared
    /// Client flow: LoadSharedAnchor -> Localize -> Compute Y-axis CalibrationData offset
    /// </summary>
    public class MetaSpatialAnchorProvider : MonoBehaviour, IAnchorCalibrationProvider
    {
        [SerializeField] private float _anchorTimeoutSeconds = 10f;
        [SerializeField] private int _maxRetries = 3;
        [SerializeField] private float _retryDelaySeconds = 2f;

#if META_XR_SDK
        private OVRSpatialAnchor _anchor;
        private GameObject _clientAnchorObject;
#endif
        private CancellationTokenSource _cts;

        // Public properties
        public CalibrationState State { get; private set; }

        // Events
        public event Action<CalibrationData> OnCalibrationComplete;
        public event Action<string> OnCalibrationFailed;
        public event Action<AnchorShareData> OnAnchorShared;

        // ICalibrationProvider — not used by anchor-based calibration
        public void SetReferencePoints(Vector3 pointA, Vector3 pointB) { }

        /// <summary>
        /// Host entry point. Creates a spatial anchor at this GameObject's position,
        /// shares it to a new group, and fires OnAnchorShared + OnCalibrationComplete.
        /// </summary>
        public void StartCalibration()
        {
#if META_XR_SDK
            CancelInFlight();
            _cts = new CancellationTokenSource();
            State = CalibrationState.InProgress;
            CreateAndShareAnchor(_cts.Token);
#else
            Fail("Meta XR SDK not available. Install com.meta.xr.sdk.core.");
#endif
        }

        /// <summary>
        /// Client entry point. Loads a shared anchor by group UUID, localizes it,
        /// and computes the calibration offset relative to the host's anchor pose.
        /// </summary>
        public void LoadSharedAnchor(AnchorShareData data)
        {
#if META_XR_SDK
            CancelInFlight();
            _cts = new CancellationTokenSource();
            State = CalibrationState.InProgress;
            LocalizeSharedAnchor(data, _cts.Token);
#else
            Fail("Meta XR SDK not available. Install com.meta.xr.sdk.core.");
#endif
        }

        public void CancelCalibration()
        {
            CancelInFlight();
            State = CalibrationState.None;
        }

        private void CancelInFlight()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

#if META_XR_SDK
        private async void CreateAndShareAnchor(CancellationToken ct)
        {
            try
            {
                // 1. Create spatial anchor at current position
                _anchor = gameObject.AddComponent<OVRSpatialAnchor>();
                bool localized = await _anchor.WhenLocalizedAsync();
                if (ct.IsCancellationRequested || this == null) return;

                if (!localized)
                {
                    Fail("Spatial anchor creation/localization failed.");
                    return;
                }

                // 2. Share to a new group
                var groupUuid = Guid.NewGuid();
                var shareResult = await _anchor.ShareAsync(groupUuid);
                if (ct.IsCancellationRequested || this == null) return;

                if (!shareResult.Success)
                {
                    Fail($"Anchor share failed: {shareResult.Status}");
                    return;
                }

                Debug.Log($"[MetaSpatialAnchorProvider] Anchor created and shared. UUID={_anchor.Uuid}, Group={groupUuid}");

                // 3. Host calibration is Identity; notify manager to broadcast anchor data
                State = CalibrationState.Calibrated;
                OnCalibrationComplete?.Invoke(CalibrationData.Identity);
                OnAnchorShared?.Invoke(new AnchorShareData
                {
                    AnchorUuid = _anchor.Uuid,
                    GroupUuid = groupUuid,
                    HostPosition = _anchor.transform.position,
                    HostRotation = _anchor.transform.rotation
                });
            }
            catch (Exception e)
            {
                if (this != null && !ct.IsCancellationRequested)
                {
                    Fail($"Anchor creation exception: {e.Message}");
                }
            }
        }

        private async void LocalizeSharedAnchor(AnchorShareData data, CancellationToken ct)
        {
            try
            {
                // 1. Load shared anchors from group (with retry for cloud propagation delay)
                var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
                bool loaded = false;

                for (int attempt = 0; attempt <= _maxRetries; attempt++)
                {
                    if (ct.IsCancellationRequested || this == null) return;

                    unboundAnchors.Clear();
                    var loadResult = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(
                        data.GroupUuid, new[] { data.AnchorUuid }, unboundAnchors);
                    if (ct.IsCancellationRequested || this == null) return;

                    if (loadResult.Success && unboundAnchors.Count > 0)
                    {
                        loaded = true;
                        break;
                    }

                    if (attempt < _maxRetries)
                    {
                        Debug.Log($"[MetaSpatialAnchorProvider] Anchor not found, retrying ({attempt + 1}/{_maxRetries})...");
                        await Task.Delay((int)(_retryDelaySeconds * 1000), ct);
                    }
                }

                if (!loaded)
                {
                    Fail($"Failed to load shared anchor after {_maxRetries + 1} attempts.");
                    return;
                }

                // 2. Find target anchor and localize
                OVRSpatialAnchor.UnboundAnchor target = default;
                bool found = false;
                foreach (var unbound in unboundAnchors)
                {
                    if (unbound.Uuid == data.AnchorUuid)
                    {
                        target = unbound;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Fail($"Target anchor {data.AnchorUuid} not found in loaded results.");
                    return;
                }

                bool localizeSuccess = await target.LocalizeAsync(_anchorTimeoutSeconds);
                if (ct.IsCancellationRequested || this == null) return;

                if (!localizeSuccess)
                {
                    Fail("Anchor localization timed out.");
                    return;
                }

                // 3. Bind to OVRSpatialAnchor component
                _clientAnchorObject = new GameObject("NexusSharedAnchor");
                var spatialAnchor = _clientAnchorObject.AddComponent<OVRSpatialAnchor>();
                target.BindTo(spatialAnchor);

                bool anchorLocalized = await spatialAnchor.WhenLocalizedAsync();
                if (ct.IsCancellationRequested || this == null) return;

                if (!anchorLocalized)
                {
                    Fail("Bound anchor failed to localize.");
                    return;
                }

                Debug.Log($"[MetaSpatialAnchorProvider] Anchor localized. ClientPos={spatialAnchor.transform.position}, HostPos={data.HostPosition}");

                // 4. Compute calibration offset (Y-axis rotation only)
                var calibration = ComputeOffset(
                    data.HostPosition, data.HostRotation,
                    spatialAnchor.transform.position, spatialAnchor.transform.rotation);

                State = CalibrationState.Calibrated;
                OnCalibrationComplete?.Invoke(calibration);
            }
            catch (OperationCanceledException)
            {
                // Expected when CancelCalibration() is called during retry delay
            }
            catch (Exception e)
            {
                if (this != null && !ct.IsCancellationRequested)
                {
                    Fail($"Anchor localization exception: {e.Message}");
                }
            }
        }
#endif

        /// <summary>
        /// Compute Y-axis-only calibration offset between host and client anchor poses.
        /// Consistent with ManualCalibrationProvider.ComputeCalibration algorithm.
        /// </summary>
        private static CalibrationData ComputeOffset(
            Vector3 hostPos, Quaternion hostRot,
            Vector3 clientPos, Quaternion clientRot)
        {
            // Extract forward vectors and project to XZ plane (Y-axis rotation only)
            Vector3 hostFwd = hostRot * Vector3.forward;
            hostFwd.y = 0f;
            Vector3 clientFwd = clientRot * Vector3.forward;
            clientFwd.y = 0f;

            // Guard against degenerate input
            if (hostFwd.sqrMagnitude < 0.0001f || clientFwd.sqrMagnitude < 0.0001f)
            {
                return new CalibrationData
                {
                    Position = hostPos - clientPos,
                    Rotation = Quaternion.identity
                };
            }

            hostFwd.Normalize();
            clientFwd.Normalize();

            Quaternion rotation = Quaternion.FromToRotation(clientFwd, hostFwd);
            Vector3 position = hostPos - rotation * clientPos;

            return new CalibrationData
            {
                Position = position,
                Rotation = rotation
            };
        }

        private void Fail(string message)
        {
            State = CalibrationState.Failed;
            Debug.LogWarning($"[MetaSpatialAnchorProvider] {message}");
            OnCalibrationFailed?.Invoke(message);
        }

        private void OnDestroy()
        {
            CancelInFlight();

#if META_XR_SDK
            if (_anchor != null)
            {
                Destroy(_anchor);
                _anchor = null;
            }

            if (_clientAnchorObject != null)
            {
                Destroy(_clientAnchorObject);
                _clientAnchorObject = null;
            }
#endif
        }
    }
}
