﻿using System.Collections;
using UnityEngine;
using System;
using System.IO;
using System.IO.Compression;
using System.Globalization;




//TO-DO: Animate size changes through interpolation (+ smooth the interpolations, maybe with a third point in the future)
//TO-DO: create wind data buffer to delay making readings available
public abstract class File_reader : MonoBehaviour
{
    public string filePath; 
    public string occupancyFile;
    protected int currentIteration;
    protected int[] env; //occupancy grid
    protected double[] wind_u; //wind vector first component
    protected double[] wind_v; //wind vector second component
    protected double[] wind_w; //wind vector third component
    protected Vector3Int environment_cells;
    protected float envmin_x, envmin_y, envmin_z; //environment min coordinates
    protected float envmax_x, envmax_y, envmax_z; //environment max coordinates
    protected float cell_size;
    public string gasType;

    /*  visualization   */
    new public ParticleSystem particleSystem;  
    protected bool updated; //has a new file been read yet?
    public float updateInterval; //minimum time before moving to next iteration
    protected float lastUpdateTimestamp; //timestamp of last update
    public float visibleConcentrationThreshold; //concentration above which gas should be visible
    protected ParticleSystem.Particle particleTemplate;



    protected string[] GasTypesByCode = {
        "ethanol",
		"methane",
		"hydrogen",
		"propanol",
		"chlorine",
		"flurorine",
		"acetone",
		"neon",
		"helium",
		"hot_air"
    };


    //Important note: every file that comes from GADEN has Z-UP. Unity has Y-UP.
    //Therefore, whenever reading files, there will be wacky things like env[x][z][y]
    //All transposing is done when reading the files, so that using the information later on is consistent with Unity's coordinate system



    // Start is called before the first frame update
    void Start()
    {

        currentIteration=0;
        //read the parameters of the environment, gas type, occupancy of cells...
        checkSize(filePath+"/iteration_0");
        initEnv(occupancyFile);

        wind_u = new double[environment_cells.x*environment_cells.y*environment_cells.z];
        wind_v = new double[environment_cells.x*environment_cells.y*environment_cells.z];
        wind_w = new double[environment_cells.x*environment_cells.y*environment_cells.z];

        //okay, that's done, let's go!

        setUp();
    }

    protected virtual void setUp(){}

    // Update is called once per frame
    protected virtual void LateUpdate()
    {
        //let's show the gas in the new locations
        if(updated&&Time.realtimeSinceStartup-lastUpdateTimestamp>updateInterval){
            showGas();
            updated=false;
            //Debug.Log(Time.realtimeSinceStartup-lastUpdateTimestamp);
            lastUpdateTimestamp=Time.realtimeSinceStartup;
        }
    }

    public abstract float getConcentration(Vector3 position);

    public Vector3 getWind(Vector3 position){
        int i=(int)((position.x-envmin_x)/cell_size);
        int j=(int)((position.y-envmin_y)/cell_size);
        int k=(int)((position.z-envmin_z)/cell_size);
        return new Vector3((float) wind_u[indexFrom3D(i,j,k)], (float) wind_v[indexFrom3D(i,j,k)], (float) wind_w[indexFrom3D(i,j,k)]);
    }

    protected abstract IEnumerator readLogFile(int framerate);

    protected abstract void showGas();

    protected abstract void checkSize(string file);

    public static MemoryStream decompress(string filename){
        var output = new MemoryStream();
        using(FileStream filestream = new FileStream(filename,FileMode.Open)){
            //we have to discard the first two bytes because the file is compressed with zlib, not just deflate
            filestream.ReadByte();
            filestream.ReadByte();

            using(DeflateStream decompressionStream = new DeflateStream(filestream, CompressionMode.Decompress)){
                decompressionStream.CopyTo(output);
            }
            output.Position = 0;
        }
        return output;
    }

    public string readFromStream(MemoryStream ms){
        StreamReader reader = new StreamReader(ms);
        string text = reader.ReadToEnd();
        reader.Close();
        ms.Close();
        return text;
    }

    protected void initEnv(string filename){
        FileStream filestream = new FileStream(filename,FileMode.Open);
        StreamReader reader = new StreamReader(filestream);
        var result = reader.ReadToEnd();

        var delim=new char[]{'\n'};
        string[] lines=result.Split(delim, StringSplitOptions.RemoveEmptyEntries);

        env = new int[environment_cells.x* environment_cells.y*environment_cells.z];

        int x=0, y=0, z=0;
        for(int i =4; i<lines.Length;i++){
            if(lines[i]==";"){
                z++;
                x=0;
                y=0;
            }else{
                int j=0;
                while(y<environment_cells.z){
                    env[indexFrom3D(x,z,y)]=lines[i][j];
                    j+=2;
                    y++;
                }
                x++;
                y=0;
            }
            
        }
    }

    protected int indexFrom3D(int x, int y, int z){
        return x + y*environment_cells.x + z*environment_cells.x*environment_cells.y;
    } 
}
