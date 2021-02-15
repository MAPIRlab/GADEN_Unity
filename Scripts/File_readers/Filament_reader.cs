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
            if(File.Exists(filePath+currentIteration)){
                
                //this format is slightly less intuitive, but its MUCH more efficient in all aspects
                //we don't have the same information (concentration + wind per cell) in each file
                //instead, we have the locations and stdev of the filaments (concentrations will be computed on-demand later)
                //but none of this makes sense if we still have the wind information everywhere, it would still be slow
                //so, there is only wind information is _some_ files. If it has not changed since the last snapshot (steady state), it's just not included

                string text = decompress(filePath+currentIteration);
                var delim=new char[]{'\n'};
                string[] lines=text.Split(delim, StringSplitOptions.RemoveEmptyEntries);

                string[] lineEight=lines[8].Split(); //line eight is special, you see. It contains constants!
                total_moles_in_filament = float.Parse(lineEight[0],NumberStyles.Any, CultureInfo.InvariantCulture);
                num_moles_all_gases_in_cm3 = float.Parse(lineEight[1],NumberStyles.Any, CultureInfo.InvariantCulture);

                //the line in which the filament information starts
                int endW = 8;

                //if there is wind info
                if(lines[9]=="Wind"){

                    endW=lines.Length-1;
                    while(lines[endW]!="EndWind"){
                        endW--;
                    }
                    
                    int offset = 10; //lines we have already skipped
                    //same as the other version
                    for(int index = 0;index<framerate;index++){
                        Parallel.For(offset+index*(endW/framerate), (index+1)*endW/framerate, i =>{
                            string line=lines[i];
                            int x=0,y=0,z=0;
                            int u=0,v=0,w=0;
                            int j=0;
                            bool negative = false;

                            while(line[j]!=' '){
                                x=x*10+(line[j]-'0');
                                j++;
                            }
                            j++;

                            while(line[j]!=' '){
                                y=y*10+(line[j]-'0');
                                j++;
                            }
                            j++;
                            while(line[j]!=' '){
                                z=z*10+(line[j]-'0');
                                j++;
                            }
                            j++;
                            
                            if(line[j]=='-'){
                                negative=true;
                                j++;
                            }
                            while(line[j]!=' '){
                                u=u*10+(line[j]-'0');
                                j++;
                            }
                            j++;
                            u=negative?-u:u;
                            negative=false;

                            if(line[j]=='-'){
                                negative=true;
                                j++;
                            }
                            while(line[j]!=' '){
                                v=v*10+(line[j]-'0');
                                j++;
                            }
                            j++;
                            v=negative?-v:v;
                            negative=false;

                            if(line[j]=='-'){
                                negative=true;
                                j++;
                            }
                            while(j<line.Length){
                                w=w*10+(line[j]-'0');
                                j++;
                            }
                            w=negative?-w:w;

                            wind_u[x][z][y]=u/1000f;
                            wind_v[x][z][y]=w/1000f;
                            wind_w[x][z][y]=v/1000f;

                        });
                        offset=0;
                        yield return null;
                    }
                }

                //now we read the filaments
                filaments=filaments_next_step;
                filaments_next_step = new Dictionary<int, Vector4>();
                for(int i = endW+1; i<lines.Length; i++){
                    string line=lines[i];
                    int index=0, x=0,y=0,z=0,stdv=0;
                    int j=0;
                    bool negative = false;

                    while(line[j]!=' '){
                        index=index*10+(line[j]-'0');
                        j++;
                    }
                    j++;

                    if(line[j]=='-'){
                        negative=true;
                        j++;
                    }
                    while(line[j]!=' '){
                        x=x*10+(line[j]-'0');
                        j++;
                    }
                    j++;
                    x=negative?-x:x;
                    negative=false;

                    if(line[j]=='-'){
                        negative=true;
                        j++;
                    }
                    while(line[j]!=' '){
                        y=y*10+(line[j]-'0');
                        j++;
                    }
                    j++;
                    y=negative?-y:y;
                    negative=false;

                    if(line[j]=='-'){
                        negative=true;
                        j++;
                    }
                    while(line[j]!=' '){
                        z=z*10+(line[j]-'0');
                        j++;
                    }
                    j++;
                    z=negative?-z:z;
                   
                    while(j<line.Length){
                        stdv=stdv*10+(line[j]-'0');
                        j++;
                    }
                    filaments_next_step.Add(index,new Vector4(x/1000f, z/1000f,y/1000f, stdv/1000f));
                }
                
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
            if(env[x_idx][y_idx][z_idx] != 0) { return false; }
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
}