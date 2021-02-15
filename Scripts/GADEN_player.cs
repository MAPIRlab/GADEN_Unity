using UnityEngine;
using System;

public class GADEN_player:MonoBehaviour{
    public float visibleConcentrationThreshold; //concentration, in ppm, above which gas should be visible
    public string filePath; //path to gas simulation logs, minus the iteration counter 
    public string occupancyFile; //OccupancyGrid3D.csv
    public float updateInterval; //minimum time before moving to next iteration
    File_reader g;
    new public ParticleSystem particleSystem;  
    void Start(){
        string text = File_reader.decompress(filePath+0);
        string[] lines;

        var delim=new char[]{'\n'};
        lines=text.Split(delim, StringSplitOptions.RemoveEmptyEntries);
        if(lines[7]=="Filaments"){
            g = (Filament_reader)gameObject.AddComponent(typeof(Filament_reader));
        }else{
            g = (Concentration_reader)gameObject.AddComponent(typeof(Concentration_reader));
        }
        g.filePath=filePath;
        g.visibleConcentrationThreshold=visibleConcentrationThreshold;
        g.occupancyFile=occupancyFile;
        g.updateInterval=updateInterval;
        g.particleSystem=particleSystem;
    }

    public float getConcentration(Vector3 position){
        return g.getConcentration(position);
    }
    public Vector3 getWind(Vector3 position){
        return g.getWind(position);
    }
}