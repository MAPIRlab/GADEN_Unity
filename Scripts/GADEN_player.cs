using UnityEngine;
using System;
using System.IO;

public class GADEN_player:MonoBehaviour{
    public float visibleConcentrationThreshold; //concentration, in ppm, above which gas should be visible
    public string filePath; //path to gas simulation logs, minus the iteration counter 
    public string occupancyFile; //OccupancyGrid3D.csv
    public float updateInterval; //minimum time before moving to next iteration
    File_reader g;
    new public ParticleSystem particleSystem;  
    void Start(){
        var stream = File_reader.decompress(filePath+"/iteration_0");
        BinaryReader br = new BinaryReader(stream);
        
        if(br.ReadInt32()==1){
            g = (Filament_reader)gameObject.AddComponent(typeof(Filament_reader));
        }else{
            g = (Concentration_reader)gameObject.AddComponent(typeof(Concentration_reader));
        }
        br.Close();
        stream.Close();

        g.filePath=filePath;
        g.visibleConcentrationThreshold=visibleConcentrationThreshold;
        g.occupancyFile=occupancyFile;
        g.updateInterval=updateInterval;
        g.particleSystem=particleSystem;
    }

    public GasMeasurement getConcentration(Vector3 position){
        return new GasMeasurement(g.gasType, g.getConcentration(position));
    }
    public Vector3 getWind(Vector3 position){
        return g.getWind(position);
    }
}

public struct GasMeasurement{
    public string gas_type;
    public float ppm;
    public GasMeasurement(string gt, float p){
        gas_type=gt;
        ppm=p;
    }
}