using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Globalization;

using System.Threading.Tasks;
using System.Linq;

public class Filament_reader: File_reader
{
    Dictionary<int,Vector4> filaments = new Dictionary<int,Vector4>(); //location + stdev of each existing filament
    Dictionary<int,Vector4> filaments_next_step = new Dictionary<int,Vector4>(); //for smooth visualization
    Dictionary<int,int> filamentToParticle = new Dictionary<int, int>();
    int last_wind_index = -1;

    float total_moles_in_filament;
    float num_moles_all_gases_in_cm3;
    protected override void setUp()
    {
        /* Get a sample particle so that we can create new ones that copy all default parameters */
        particleSystem.Emit(1);
        ParticleSystem.Particle[] temp = new ParticleSystem.Particle[1];
        particleSystem.GetParticles(temp);
        particleTemplate=temp[0];
        particleSystem.Clear();

        //has any new file been read since the last update to the particle system?
        updated=false;

        IEnumerator coroutine = readLogFile(10);
        StartCoroutine(coroutine); 
    }

    protected override IEnumerator readLogFile(int framerate){
        bool ended=false; //have all the files been read already?
        while (true){
            while(updated){
                yield return null;
            }
            if(File.Exists(filePath+"/iteration_"+currentIteration)){
                
                var stream = decompress(filePath+"/iteration_"+currentIteration);
                BinaryReader br = new BinaryReader(stream);
                br.BaseStream.Seek(5*sizeof(int) + 14*sizeof(double), SeekOrigin.Begin); //skip headers

                int wind_index;
                wind_index=br.ReadInt32();
                readWindFiles(wind_index);
                
                //now we read the filaments
                filaments=filaments_next_step;
                filaments_next_step=new Dictionary<int, Vector4>();
                while(br.BaseStream.Position != br.BaseStream.Length){
                    int filament_index;
                    filament_index=br.ReadInt32();
                    Vector4 filament= new Vector4();
                    filament.x=(float) br.ReadDouble();
                    filament.z=(float) br.ReadDouble();
                    filament.y=(float) br.ReadDouble();
                    filament.w=(float) br.ReadDouble();
                    
                    filaments_next_step.Add(filament_index, filament);
                }
                
                stream.Close();
                br.Close();
                currentIteration++;
                updated=true;
                yield return null;
            }
            else{
                if(!ended){
                    Debug.Log("[WARNING] No more data!");
                    ended=true;
                }
                yield return null;
            }
        }
    }

    void readWindFiles(int wind_index){
        if(wind_index==last_wind_index)
            return;
        last_wind_index=wind_index;
        FileStream filestream = new FileStream(filePath+"/wind/wind_iteration_"+wind_index,FileMode.Open);
        BinaryReader br = new BinaryReader(filestream);
        
        byte[] buffer = br.ReadBytes(wind_u.Length* sizeof(double));
        Buffer.BlockCopy(buffer,0,wind_u,0,buffer.Length);
        br.ReadBytes(wind_v.Length* sizeof(double));
        Buffer.BlockCopy(buffer,0,wind_v,0,buffer.Length);
        br.ReadBytes(wind_w.Length* sizeof(double));
        Buffer.BlockCopy(buffer,0,wind_w,0,buffer.Length);

        filestream.Close();
        br.Close();
    }

    protected override void showGas()
    {
        var rand = new System.Random();
        
        //if a filament already existed last step, we want to modify the particle that corresponds to it (which has already been emitted)
        //if the filament is new, we must create a new particle
        //if a filament disappeared, the particle must disappear
        ParticleSystem.Particle[] existingParticles= new ParticleSystem.Particle[particleSystem.particleCount];
        List<ParticleSystem.Particle> particlesToEmit = new List<ParticleSystem.Particle>();
        Dictionary<int, int> newDictionary = new Dictionary<int, int>();
        
        particleSystem.GetParticles(existingParticles);
        int newParticleIndex = 0;
        foreach(int filament_index in filaments.Keys.ToList()){
            ParticleSystem.Particle p;
            if(filamentToParticle.ContainsKey(filament_index)){
                p = existingParticles[filamentToParticle[filament_index]];
            }
            else{
                p=particleTemplate;
                p.rotation=(float)(rand.NextDouble())*360;
            }

            //set new values of the particle parameters
            Vector3 pos = new Vector3(filaments[filament_index].x, filaments[filament_index].y, filaments[filament_index].z);
            p.position=pos;
            if(filaments_next_step.ContainsKey(filament_index)){
                Vector3 vel = new Vector3((filaments_next_step[filament_index].x-filaments[filament_index].x)/updateInterval,
                                        (filaments_next_step[filament_index].y-filaments[filament_index].y)/updateInterval,
                                        (filaments_next_step[filament_index].z-filaments[filament_index].z)/updateInterval);
                p.velocity=vel;
            }
            p.startSize=visibleRadiusFromConcentrationThreshold(filaments[filament_index].w);
            
            int opacity = (int)(255*(5/filaments[filament_index].w));
            p.startColor=new Color32(255, 255, 255, (byte)opacity );

            //set this particle to be emitted and update the indexes so that it can be modified next iteration
            particlesToEmit.Add(p);
            newDictionary.Add(filament_index,newParticleIndex);
            newParticleIndex++;
        }
        filamentToParticle=newDictionary;
    
        particleSystem.SetParticles(particlesToEmit.ToArray(), particlesToEmit.Count);

    }

    public override float getConcentration(Vector3 position) {
        float accumulator =0;
        List<Vector4> allFilaments = filaments.Values.ToList();
        foreach (var filament in allFilaments){
            //if the filament is close enough and there is no obstacle in the path, add the corresponding gas concentration
            double dist = Math.Sqrt( Math.Pow(position.x-filament.x,2) + Math.Pow(position.y-filament.y,2) + Math.Pow(position.z-filament.z,2) );
            if(dist<=5*filament.w/100 && pathExists(position, filament)){
                accumulator+=concentrationFromFilament(position, filament);
            }
        }
        return accumulator;
    }
    bool pathExists(Vector3 start, Vector4 fil){
        //is there a free path between the center of the filament and the queried point?

        double vector_x = fil.x - start.x;
        double vector_y = fil.y - start.y;
        double vector_z = fil.z - start.z;
        double distance = Math.Sqrt(vector_x*vector_x + vector_y*vector_y + vector_z*vector_z);
        vector_x = vector_x/distance;
        vector_y = vector_y/distance;
        vector_z = vector_z/distance;

        int steps = (int)Math.Ceiling( distance / cell_size );	// Make sure no two iteration steps are separated more than 1 cell
        double increment = distance/steps;

        for(int i=1; i<steps-1; i++)
        {
            // Determine point in space to evaluate
            double pose_x = start.x + vector_x*increment*i;
            double pose_y = start.y + vector_y*increment*i;
            double pose_z = start.z + vector_z*increment*i;


            // Determine cell to evaluate (some cells might get evaluated twice due to the current code
            int x_idx = (int)( (pose_x-envmin_x)/cell_size );
            int y_idx = (int)( (pose_y-envmin_y)/cell_size );
            int z_idx = (int)( (pose_z-envmin_z)/cell_size );


            // Check if the cell is occupied
            if(env[indexFrom3D(x_idx,y_idx,z_idx)] != 0) { return false; }
        }
        return true;
    }

    float concentrationFromFilament(Vector3 pos, Vector4 filament){
        //calculate how much gas concentration does one filament contribute to the queried location
        double sigma = filament.w;
        double distance_cm = 100 * Math.Sqrt( Math.Pow(pos.x-filament.x,2) + Math.Pow(pos.y-filament.y,2) + Math.Pow(pos.z-filament.z,2) );

        double num_moles_target_cm3 = (total_moles_in_filament /
            (Math.Sqrt(8*Math.Pow(3.14159,3)) * Math.Pow(sigma,3) )) * Math.Exp( -Math.Pow(distance_cm,2)/(2*Math.Pow(sigma,2)) );

        double ppm = num_moles_target_cm3/num_moles_all_gases_in_cm3 * 1000000; //parts of target gas per million
        return (float) (ppm);
    }

    float visibleRadiusFromConcentrationThreshold(float sigma){
        //this is just solving the equation in concentrationFromFilament() for the distance, with a fixed concentration value
        double logTerm = visibleConcentrationThreshold * Math.Sqrt(8*Math.Pow(Math.PI,3)) * Math.Pow(sigma,3) 
            / (total_moles_in_filament*1000000/num_moles_all_gases_in_cm3);

        return (float) Math.Sqrt(-2*Math.Pow(sigma,2) * Math.Log(logTerm)) / 100;

    }

    protected override void checkSize(string file){
        var stream = decompress(file);
        BinaryReader br = new BinaryReader(stream);
        br.ReadInt32();
        envmin_x=(float) br.ReadDouble();
        envmin_z=(float) br.ReadDouble();
        envmin_y=(float) br.ReadDouble();

        envmax_x=(float) br.ReadDouble();
        envmax_z=(float) br.ReadDouble();
        envmax_y=(float) br.ReadDouble();

        Vector3Int size = new Vector3Int(br.ReadInt32(),br.ReadInt32(),br.ReadInt32());
        environment_cells= new Vector3Int(size.x, size.z, size.y);

        cell_size=(float) br.ReadDouble();
        br.ReadDouble();
        br.ReadDouble();
        br.ReadDouble();
        br.ReadDouble();
        br.ReadDouble();

        gasType= GasTypesByCode[br.ReadInt32()];

        total_moles_in_filament = (float) br.ReadDouble();
        num_moles_all_gases_in_cm3 = (float) br.ReadDouble();
        stream.Close();
        br.Close();
    } 
}