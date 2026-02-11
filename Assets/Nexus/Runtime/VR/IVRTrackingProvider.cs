namespace Nexus.Networking.VR
{
    /// <summary>
    /// Provides local VR tracking data. Implement this interface to bridge
    /// your VR SDK (XRI, OVR, etc.) into Nexus networking.
    /// </summary>
    public interface IVRTrackingProvider
    {
        VRPose GetCurrentPose();
    }
}
