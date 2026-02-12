using Nexus.Networking.Debugging;
using Nexus.Networking.VR;
using UnityEditor;
using UnityEngine;

namespace Nexus.Networking.Editor
{
    [CustomEditor(typeof(NexusDebugPanel))]
    public class NexusDebugPanelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var panel = (NexusDebugPanel)target;
            bool isPlaying = Application.isPlaying;

            EditorGUILayout.Space(10);

            // State display
            EditorGUILayout.LabelField("Session State", panel.CurrentState.ToString(), EditorStyles.boldLabel);

            // Current room info
            if (isPlaying && panel.CurrentRoom != null)
            {
                var room = panel.CurrentRoom;
                EditorGUILayout.LabelField("Room",
                    $"{room.RoomName}  ({room.CurrentPlayers}/{room.MaxPlayers})");
            }

            EditorGUILayout.Space(5);

            // Action buttons
            EditorGUI.BeginDisabledGroup(!isPlaying);

            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Room (Host)", GUILayout.Height(30)))
                {
                    panel.CreateRoom();
                }

                if (GUILayout.Button("Leave Room", GUILayout.Height(30)))
                {
                    panel.LeaveRoom();
                }
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Discovery", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start Discovery", GUILayout.Height(28)))
                {
                    panel.StartDiscovery();
                }

                if (GUILayout.Button("Stop Discovery", GUILayout.Height(28)))
                {
                    panel.StopDiscovery();
                }
            }

            EditorGUI.EndDisabledGroup();

            // Discovered rooms list
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Discovered Rooms", EditorStyles.boldLabel);

            if (isPlaying && panel.DiscoveredRooms.Count > 0)
            {
                for (int i = 0; i < panel.DiscoveredRooms.Count; i++)
                {
                    var room = panel.DiscoveredRooms[i];

                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(
                            $"{room.RoomName}  ({room.CurrentPlayers}/{room.MaxPlayers})  {room.HostAddress}:{room.Port}");

                        if (GUILayout.Button("Join", GUILayout.Width(60), GUILayout.Height(22)))
                        {
                            panel.JoinRoom(i);
                        }
                    }
                }
            }
            else if (isPlaying)
            {
                EditorGUILayout.HelpBox("No rooms discovered. Click 'Start Discovery' to search.", MessageType.Info);
            }

            // Connected players list
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Connected Players", EditorStyles.boldLabel);

            if (isPlaying && panel.Players != null && panel.Players.Count > 0)
            {
                foreach (var player in panel.Players)
                {
                    string role = player.IsHost ? " [Host]" : "";
                    string local = player.IsLocal ? " (You)" : "";
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(
                            $"{player.DisplayName}{role}{local}  conn={player.ConnectionId}");
                    }
                }
            }
            else if (isPlaying)
            {
                EditorGUILayout.HelpBox("No players connected.", MessageType.Info);
            }

            // VR Players
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("VR Players", EditorStyles.boldLabel);

            if (isPlaying && panel.VRPlayerManager != null)
            {
                NexusVRPlayerManager vrManager = panel.VRPlayerManager;

                // Local player
                NexusVRPlayer localPlayer = vrManager.LocalPlayer;
                if (localPlayer != null)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        string tracking = localPlayer.TrackingProvider != null ? "Active" : "None";
                        EditorGUILayout.LabelField("Local Player", $"netId={localPlayer.netId}  tracking={tracking}");
                        DrawPoseFields(localPlayer);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Local VR player not spawned.", MessageType.None);
                }

                // Remote players
                if (vrManager.RemotePlayers.Count > 0)
                {
                    foreach (NexusVRPlayer remote in vrManager.RemotePlayers)
                    {
                        if (remote == null)
                        {
                            continue;
                        }

                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            EditorGUILayout.LabelField("Remote Player", $"netId={remote.netId}");
                            DrawPoseFields(remote);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No remote VR players.", MessageType.None);
                }
            }
            else if (isPlaying)
            {
                EditorGUILayout.HelpBox("VRPlayerManager not available.", MessageType.Info);
            }

            if (!isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use.", MessageType.Info);
            }

            // Force repaint during play mode to keep state updated
            if (isPlaying)
            {
                Repaint();
            }
        }
        private static void DrawPoseFields(NexusVRPlayer player)
        {
            EditorGUI.indentLevel++;

            if (player.Head != null)
            {
                EditorGUILayout.LabelField("Head", FormatPose(player.Head));
            }

            if (player.LeftHand != null)
            {
                EditorGUILayout.LabelField("L Hand", FormatPose(player.LeftHand));
            }

            if (player.RightHand != null)
            {
                EditorGUILayout.LabelField("R Hand", FormatPose(player.RightHand));
            }

            EditorGUI.indentLevel--;
        }

        private static string FormatPose(Transform t)
        {
            Vector3 pos = t.localPosition;
            Vector3 rot = t.localRotation.eulerAngles;
            return $"pos({pos.x:F2}, {pos.y:F2}, {pos.z:F2})  rot({rot.x:F1}, {rot.y:F1}, {rot.z:F1})";
        }
    }
}
