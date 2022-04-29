using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GADEN_player))]
public class GADEN_player_editor : Editor
{
    public override void OnInspectorGUI(){
        DrawDefaultInspector();
        if(GUILayout.Button("Select Simulation Data Folder")){
            ((GADEN_player)target).SelectFolder();
        }

        if(GUILayout.Button("Select Occupancy File")){
            ((GADEN_player)target).SelectOccupancyFile();
        }
    }
}
