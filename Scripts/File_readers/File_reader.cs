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
    protected int[][][] env; //occupancy grid
    protected float[][][] wind_u; //wind vector first component
    protected float[][][] wind_v; //wind vector second component
    protected float[][][] wind_w; //wind vector third component
    protected float envmin_x, envmin_y, envmin_z; //environment min coordinates
    protected float envmax_x, envmax_y, envmax_z; //environment max coordinates
    protected float cell_size;
    protected string gasType;

    /*  visualization   */
    new public ParticleSystem particleSystem;  
    protected bool updated; //has a new file been read yet?
    public float updateInterval; //minimum time before moving to next iteration
    protected float lastUpdateTimestamp; //timestamp of last update
    public float visibleConcentrationThreshold; //concentration above which gas should be visible
    protected ParticleSystem.Particle particleTemplate;


    protected bool readFilaments; //reading concentration files or filament files? see parameter "writeConcentrations" in GADEN


    //Important note: every file that comes from GADEN has Z-UP. Unity has Y-UP.
    //Therefore, whenever reading files, there will be wacky things like env[x][z][y]
    //All transposing is done when reading the files, so that using the information later on is consistent with Unity's coordinate system



    // Start is called before the first frame update
    void Start()
    {

        currentIteration=0;
        //read the parameters of the environment, gas type, occupancy of cells...
        Vector3Int size = checkSize(filePath+0);
        initEnv(occupancyFile, size);

        //Initializing jagged arrays is awful, but they are faster than multidimensional ones
        wind_u = new float[size.x][][];
        wind_v = new float[size.x][][];
        wind_w = new float[size.x][][];

        for(int i=0; i<size.x;i++){
            wind_u[i] = new float[size.y][];
            wind_v[i] = new float[size.y][];
            wind_w[i] = new float[size.y][];
        }

        for(int i=0; i<size.x;i++){
            for(int j=0; j<size.y;j++){
                wind_u[i][j] = new float[size.z];
                wind_v[i][j] = new float[size.z];
                wind_w[i][j] = new float[size.z];
            }    
        }

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
        return new Vector3(wind_u[i][j][k], wind_v[i][j][k], wind_w[i][j][k]);
    }

    protected abstract IEnumerator readLogFile(int framerate);

    protected abstract void showGas();

    protected Vector3Int checkSize(string file)
    {
        //we are going to read just the header here

        string text = decompress(file);
        string[] lines;

        var delim=new char[]{'\n'};
        lines=text.Split(delim, StringSplitOptions.RemoveEmptyEntries);
        string line;
        string[] words;
        //minimum coordinates of the environment
        line = lines[0];
        words = line.Split(' ');
        envmin_x=float.Parse(words[1],NumberStyles.Any, CultureInfo.InvariantCulture);
        envmin_z=float.Parse(words[2],NumberStyles.Any, CultureInfo.InvariantCulture);
        envmin_y=float.Parse(words[3],NumberStyles.Any, CultureInfo.InvariantCulture);

        //minimum coordinates of the environment
        line = lines[1];
        words = line.Split(' ');
        envmax_x=float.Parse(words[1],NumberStyles.Any, CultureInfo.InvariantCulture);
        envmax_z=float.Parse(words[2],NumberStyles.Any, CultureInfo.InvariantCulture);
        envmax_y=float.Parse(words[3],NumberStyles.Any, CultureInfo.InvariantCulture);

        //number of cells
        line = lines[2];
        words = line.Split(' ');
        Vector3Int size = new Vector3Int(Int32.Parse(words[1]), Int32.Parse(words[3]), Int32.Parse(words[2]));

        //cell size
        line = lines[3];
        words = line.Split(' ');
        cell_size=float.Parse(words[1],NumberStyles.Any, CultureInfo.InvariantCulture);

        //gas type
        line = lines[5];
        words = line.Split(' ');
        gasType=words[1];

        //filament centers or concentration in each cell?
        readFilaments=lines[7]=="Filaments";

        return size;
    }

    public static string decompress(string filename){
        string result;
        using (var output = new MemoryStream()){
            using(FileStream filestream = new FileStream(filename,FileMode.Open)){
                //we have to discard the first two bytes because the file is compressed with zlib, not just deflate
                filestream.ReadByte();
                filestream.ReadByte();

                using(DeflateStream decompressionStream = new DeflateStream(filestream, CompressionMode.Decompress)){
                    decompressionStream.CopyTo(output);
                }
                output.Position = 0;
            }
            StreamReader reader = new StreamReader(output);
            result = reader.ReadToEnd();
            reader.Close();
        }
        
        return result;
    }

    protected void initEnv(string filename, Vector3Int size){
        FileStream filestream = new FileStream(filename,FileMode.Open);
        StreamReader reader = new StreamReader(filestream);
        var result = reader.ReadToEnd();

        var delim=new char[]{'\n'};
        string[] lines=result.Split(delim, StringSplitOptions.RemoveEmptyEntries);

        env = new int[size.x][][];

        for(int i=0; i<env.Length;i++){
            env[i] = new int[size.y][];
        }

        for(int i=0; i<env.Length;i++){
            for(int j=0; j<env[0].Length;j++){
                env[i][j] = new int[size.z];
            }    
        }

        int x=0, y=0, z=0;
        for(int i =4; i<lines.Length;i++){
            if(lines[i]==";"){
                z++;
                x=0;
                y=0;
            }else{
                int j=0;
                while(y<size.z){
                    env[x][z][y]=lines[i][j];
                    j+=2;
                    y++;
                }
                x++;
                y=0;
            }
            
        }
    }
}
