using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Globalization;

using System.Threading.Tasks;
using System.Linq;

[ImageEffectAllowedInSceneView]
public class Filament_reader_raymarching: File_reader
{
    Dictionary<int,Vector4> filaments = new Dictionary<int,Vector4>(); //location + stdev of each existing filament
    Dictionary<int,Vector4> filaments_next_step = new Dictionary<int,Vector4>(); // we read where each filament will be in the next timestep to interpolate and create a smooth motion
    Dictionary<int,Vector4> filaments_change_rate = new Dictionary<int,Vector4>(); //The speed at which the filaments move and grow (from the aforementioned interpolation)
    Dictionary<int,Vector4> buffer_filaments = new Dictionary<int,Vector4>(); //we read into this dictionary before using the other two to avoid messing with the animation
    
    //constants to calculate concentrations
    float total_moles_in_filament;
    float num_moles_all_gases_in_cm3;

    public ComputeShader shader;
    public float noiseScale;
    float zCoord;
    public int numMarchingSteps;
    public int shadowMarchingSteps;
    public float absorptivity;
    public float scatteringCoefficient;
    [Range (0, 1)]
    public float forwardScattering = .83f;
    [Range (0, 1)]
    public float backScattering = .3f;
    [Range (0, 1)]
    public float baseBrightness = .8f;
    [Range (0, 1)]
    public float phaseFactor = .15f;
    Camera cam;
    public Light lightS;
    RenderTexture rendTexture;
    ComputeBuffer spheres;
    ComputeBuffer filamentsInSubspace;
    ComputeBuffer indicesOfSubspaceList;
    
    //for space subdivision
    public int numCellsx = 10;
    public int numCellsy = 10;
    public int numCellsz = 10;


    protected override void setUp()
    {
        //has any new file been read since the last update to the particle system?
        updated=false;

        
        cam = GetComponent<Camera>();
        cam.depthTextureMode=DepthTextureMode.Depth;
        if (rendTexture == null || rendTexture.width != cam.pixelWidth || rendTexture.height != cam.pixelHeight) {
            if (rendTexture != null) {
                rendTexture.Release ();
            }
            rendTexture = new RenderTexture (cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            rendTexture.enableRandomWrite = true;
            rendTexture.Create ();
        }
        
        System.Random rand = new System.Random();
        zCoord=(float)rand.NextDouble();
        

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
                buffer_filaments= new Dictionary<int, Vector4>();
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
                    buffer_filaments.Add(index,new Vector4(x/1000f, z/1000f,y/1000f, stdv/1000f));
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
        filaments=filaments_next_step;
        filaments_next_step=buffer_filaments;
        filaments_change_rate.Clear();
        foreach(int filament_index in filaments.Keys.ToList()){
            if(filaments_next_step.ContainsKey(filament_index))
                filaments_change_rate.Add(filament_index, 
                                    (filaments_next_step[filament_index]-filaments[filament_index])/updateInterval);
        }

    }

    Tuple<List<int>, List<Vector2Int>> subdivideSpace(List<Vector4> filamentList, Vector3 minCoordinates, Vector3 maxCoordinates){
        
        HashSet<int>[] divisionSpace = new HashSet<int>[numCellsx*numCellsy*numCellsz];

        for(int i=0; i<divisionSpace.Length; i++){
            divisionSpace[i]= new HashSet<int>();
        }

        Vector3 dimensions = new Vector3((maxCoordinates.x-minCoordinates.x)/numCellsx,
                                    (maxCoordinates.y-minCoordinates.y)/numCellsy,
                                    (maxCoordinates.z-minCoordinates.z)/numCellsz);


        for(int i=0; i<filamentList.Count; i++){
            Vector4 fil=filamentList[i];
            int x = (int) ((fil.x-minCoordinates.x-fil.w)/dimensions.x);
            int y = (int) ((fil.y-minCoordinates.y-fil.w)/dimensions.y);
            int z = (int) ((fil.z-minCoordinates.z-fil.w)/dimensions.z);

            int xEnd = (int) ((fil.x-minCoordinates.x+fil.w-0.001)/dimensions.x);
            int yEnd = (int) ((fil.y-minCoordinates.y+fil.w-0.001)/dimensions.y);
            int zEnd = (int) ((fil.z-minCoordinates.z+fil.w-0.001)/dimensions.z);

            for ( int r = x; r<=xEnd; r++){
                for( int h=y; h<=yEnd; h++){
                    for(int c=z; c<=zEnd;c++){
                        int cellIndex = c* (numCellsx*numCellsy) + h* numCellsx + r;
                        divisionSpace[cellIndex].Add(i);
                    }
                }
            }
        }

        List<int> indicesList = new List<int>();
        List<Vector2Int> sizesList = new List<Vector2Int>();

        int index =0;
        for (int i = 0; i < divisionSpace.Length; i++){
            List<int> aux = new List<int>(divisionSpace[i]);
            for(int j=0;j<aux.Count;j++){
                indicesList.Add(aux[j]);
            }
            sizesList.Add(new Vector2Int(index, index+aux.Count));
            index+=aux.Count;
        }
        return Tuple.Create(indicesList, sizesList);
    }

    void updateFilamentPositions(){
        Vector3 maxCoordinates= new Vector3(float.MinValue,float.MinValue,float.MinValue);
        Vector3 minCoordinates = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
        var keys = filaments_change_rate.Keys.ToList();
        List<Vector4> filamentList = new List<Vector4>();
        
        //move the filament and calculate the bounding box
        foreach(int filament_index in keys){
            Vector4 fil = filaments[filament_index];
            Vector4 speed = filaments_change_rate[filament_index]*Time.unscaledDeltaTime;
            fil = fil + speed;
            filaments.Remove(filament_index);
            filaments.Add(filament_index,fil);

            fil.w=fil.w/100*5;
            filamentList.Add(fil);
            
            maxCoordinates.x=Mathf.Max(maxCoordinates.x, fil.x+fil.w);
            maxCoordinates.y=Mathf.Max(maxCoordinates.y, fil.y+fil.w);
            maxCoordinates.z=Mathf.Max(maxCoordinates.z, fil.z+fil.w);

            minCoordinates.x=Mathf.Min(minCoordinates.x, fil.x-fil.w);
            minCoordinates.y=Mathf.Min(minCoordinates.y, fil.y-fil.w);
            minCoordinates.z=Mathf.Min(minCoordinates.z, fil.z-fil.w);
        }

        //Subdivide the space within the bounding box to avoid iterating over the entire list of filaments at each sample point
        var subdivisions = subdivideSpace(filamentList, minCoordinates, maxCoordinates);
        
        //--------------------------------
        //set the parameters for the shader
        shader.SetVector("minBoundingBox", minCoordinates);
        shader.SetVector("maxBoundingBox", maxCoordinates);

        if(spheres!=null){
            spheres.Release();
        }
        spheres = new ComputeBuffer(filamentList.Count, 4*sizeof(float));
        spheres.SetData(filamentList.ToArray());
        shader.SetBuffer(0,"spheres",spheres);
        shader.SetInt("numSpheres", filamentList.Count);

        if(filamentsInSubspace!=null){
            filamentsInSubspace.Release();
        }
        filamentsInSubspace = new ComputeBuffer(subdivisions.Item1.Count, sizeof(int));
        filamentsInSubspace.SetData(subdivisions.Item1.ToArray());
        shader.SetBuffer(0, "filamentsInSubspace", filamentsInSubspace);
        
        if(indicesOfSubspaceList!=null){
            indicesOfSubspaceList.Release();
        }
        indicesOfSubspaceList = new ComputeBuffer(subdivisions.Item2.Count, 2*sizeof(int));
        indicesOfSubspaceList.SetData(subdivisions.Item2.ToArray());
        shader.SetBuffer(0, "indicesOfSubspaceList", indicesOfSubspaceList);

        shader.SetInts("numSubdivisions", new int[3]{numCellsx, numCellsy, numCellsz});

        //all constants are fed to the shader as only one number to help with performance
        //it's less readable, but it really does help when >1000 concentration samples are taken per rendered pixel
        //it is 1000 (to convert concentration from moles/cm3 to moles/L) * moles of gas in a filament / sqrt(8*pi³) (see farrell's eq)
        shader.SetFloat("filament_concentration_constant", 1000* total_moles_in_filament/ Mathf.Sqrt( 8*Mathf.Pow(Mathf.PI,3) ) ); 
    }

    void OnRenderImage(RenderTexture src,RenderTexture dest){
        bool dispatched=false;
        if(spheres!=null){
            int numThreadsX = Mathf.CeilToInt(cam.pixelWidth/16.0f);
            int numThreadsY = Mathf.CeilToInt(cam.pixelHeight/16.0f);

            shader.SetMatrix ("_CameraToWorld", cam.cameraToWorldMatrix);
            shader.SetMatrix ("_CameraInverseProjection", cam.projectionMatrix.inverse);
            shader.SetTexture (0, "CamView", src);
            shader.SetTextureFromGlobal(0, "DepthTex", "_CameraDepthTexture");
            shader.SetTexture (0, "Result", rendTexture);

            shader.SetInt("numMarchingSteps", numMarchingSteps);

            //noise params
            shader.SetFloat("noiseScale", noiseScale);
            shader.SetFloat("zCoord", zCoord);
            
            //lighting
            shader.SetVector("lightPosition", lightS.transform.position);
            shader.SetVector("lightColor", new Vector4(lightS.color.r, lightS.color.g, lightS.color.b, 1));
            shader.SetFloat("absorptivity", absorptivity);
            shader.SetFloat("scatteringCoefficient", scatteringCoefficient);
            shader.SetFloat("lightThreshold", -Mathf.Log(0.1f)/(absorptivity+scatteringCoefficient));
            shader.SetInt("shadowMarchingSteps", shadowMarchingSteps);
            shader.SetVector ("phaseParams", new Vector4 (forwardScattering, backScattering, baseBrightness, phaseFactor));

            
            shader.Dispatch(0, numThreadsX, numThreadsY, 1);
            dispatched=true;
        }

        //calculate the filament positions for next iteration while GPU is busy rendering the current one
        if (filaments_change_rate.Count>0){

            updateFilamentPositions();
            zCoord+=0.5f*Time.deltaTime;
        }

        //join
        if(dispatched){ 
            
            Graphics.Blit(rendTexture, dest);
        }
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

    void OnDestroy(){
        spheres.Release();
        indicesOfSubspaceList.Release();
        filamentsInSubspace.Release();
    }

}