using Nexus.Networking.Debugging;
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
            else
            {
                EditorGUILayout.HelpBox(
                    isPlaying ? "No rooms discovered. Click 'Start Discovery' to search." : "Enter Play Mode to use.",
                    MessageType.Info);
            }

            // Force repaint during play mode to keep state updated
            if (isPlaying)
            {
                Repaint();
            }
        }
    }
}
