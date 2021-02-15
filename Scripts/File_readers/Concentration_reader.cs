using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.IO.Compression;
using System.Globalization;

using System.Threading.Tasks;
using System.Linq;

public class  Concentration_reader: File_reader
{
    
    protected float[][][] concentration;
    protected List<ParticleSystem.Particle>[] particles; //List of cells with concentration above showThreshold (visualization when reading concentration files)


    protected override void setUp(){
        concentration = new float[wind_u.Length][][];

        for(int i=0;i<concentration.Length;i++){
            concentration[i]=new float[wind_u.Length][];
        }

        for(int i=0;i<concentration.Length;i++){
            for(int j=0;j<concentration.Length;j++){
                concentration[i][j]=new float[wind_u.Length];
            }
        }

        particles=new List<ParticleSystem.Particle>[concentration.Length];
        for(int i = 0; i<particles.Length;i++){
            particles[i]= new List<ParticleSystem.Particle>();
        }
        
        particleSystem.Emit(1);
        ParticleSystem.Particle[] temp = new ParticleSystem.Particle[1];
        particleSystem.GetParticles(temp);
        particleTemplate=temp[0];
        particleTemplate.startSize=cell_size*3;
        particleSystem.Clear();


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
               
                //read the file and split it by lines
                string text = decompress(filePath+currentIteration);
                var delim=new char[]{'\n'};
                string[] lines=text.Split(delim, StringSplitOptions.RemoveEmptyEntries);

                //this is just parsing the file over several frames so as to avoid freezing the animations
                //the manual parsing of each number pains me more than you, but it truly is much faster
                int offset = 7; //lines we have already skipped
                for(int index = 0;index<framerate;index++){
                    Parallel.For(offset+index*(lines.Length/framerate), (index+1)*lines.Length/framerate, i =>{
                        string line=lines[i];
                        int x=0,y=0,z=0;
                        int c=0,u=0,v=0,w=0;
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
                        
                        while(line[j]!=' '){
                            c=c*10+(line[j]-'0');
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

                        //transpose everything so it's Y-UP (change Y for Z; and V for W)
                        concentration[x][z][y]=c/1000f;
                        wind_u[x][z][y]=u/1000f;
                        wind_v[x][z][y]=w/1000f;
                        wind_w[x][z][y]=v/1000f;
                    });
                    offset=0;
                    yield return null;
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

    protected override void showGas(){
        //visualization!
        int s1=concentration.Length, s2=concentration[0].Length, s3=concentration[0][0].Length;

        for(int i = 0; i<particles.Length;i++){
            particles[i].Clear();
        }

        //concentrations
        Parallel.For(0,s1, i =>{
            for(int j=0;j<s2; j++){
                for(int k=0;k<s3; k++){
                    if(concentration[i][j][k]>visibleConcentrationThreshold){
                        ParticleSystem.Particle p = particleTemplate;
                        p.position=new Vector3(envmin_x+i*cell_size, envmin_y+j*cell_size, envmin_z+k*cell_size);
                        particles[i].Add(p);
                    }
                }
            }
        });
        
        
        ParticleSystem.Particle[] showParticles= particles.SelectMany(i => i).ToList().ToArray();

        var rand = new System.Random();
        for(int i = 0; i < showParticles.Length; i++) {
            var p = new Vector3(showParticles[i].position.x+(float) rand.NextDouble()*(cell_size/2)*(rand.NextDouble()>0.5?1:-1),
                                showParticles[i].position.y+(float) rand.NextDouble()*(cell_size/2)*(rand.NextDouble()>0.5?1:-1),
                                showParticles[i].position.z+(float) rand.NextDouble()*(cell_size/2)*(rand.NextDouble()>0.5?1:-1));
            showParticles[i].position = p;
            
        }
        particleSystem.SetParticles(showParticles, showParticles.Length);

    }
    public override float getConcentration(Vector3 position) {
        int i=(int)((position.x-envmin_x)/cell_size);
        int j=(int)((position.y-envmin_y)/cell_size);
        int k=(int)((position.z-envmin_z)/cell_size);
        return concentration[i][j][k];
    }
}