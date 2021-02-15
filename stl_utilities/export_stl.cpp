#include <iostream>
#include <fstream>
#include <sstream>
#include <vector>
#include <bits/stdc++.h> 

int main(int argc, char *argv[]){

    if(argc!=4){
        std::cout << "Correct format is \"export_stl inputFile outputFile innerVolume(bool)\"\n";
        return 0;
    }

    double env_min_x, env_min_y, env_min_z;
    double env_max_x, env_max_y, env_max_z;
    double cell_size;
    int env_cells_x, env_cells_y, env_cells_z;

    //model of the obstacles or of the inner volume?
    int occ, free;

    if(std::string(argv[3])==std::string("true")){
        occ=0; free=1;
    }else{
        occ=1; free=0;
    }

    std::ifstream infile(argv[1]);

    std::string line;

    //Line 1 (min values of environment)
    std::getline(infile, line);
    size_t pos = line.find(" ");
    line.erase(0, pos+1);
    pos = line.find(" ");
    env_min_x = atof(line.substr(0, pos).c_str());
    line.erase(0, pos+1);
    pos = line.find(" ");
    env_min_y = atof(line.substr(0, pos).c_str());
    env_min_z = atof(line.substr(pos+1).c_str());


    //Line 2 (max values of environment)
    std::getline(infile, line);
    pos = line.find(" ");
    line.erase(0, pos+1);
    pos = line.find(" ");
    env_max_x = atof(line.substr(0, pos).c_str());
    line.erase(0, pos+1);
    pos = line.find(" ");
    env_max_y = atof(line.substr(0, pos).c_str());
    env_max_z = atof(line.substr(pos+1).c_str());

    //Line 3 (Num cells on each dimension)
    std::getline(infile, line);
    pos = line.find(" ");
    line.erase(0, pos+1);
    pos = line.find(" ");
    env_cells_x = atoi(line.substr(0, pos).c_str());
    line.erase(0, pos+1);
    pos = line.find(" ");
    env_cells_y = atof(line.substr(0, pos).c_str());
    env_cells_z = atof(line.substr(pos+1).c_str());

    //Line 4 cell_size (m)
    std::getline(infile, line);
    pos = line.find(" ");
    cell_size = atof(line.substr(pos+1).c_str());

    std::vector<std::vector<std::vector<int> > > env(env_cells_x, 
            std::vector<std::vector<int> >(env_cells_y, 
            std::vector<int>(env_cells_z, 0)));

    int x=0, y=0, z=0;

    //each line is a y value, every column an x value, z values are separated by ";" lines
    while (std::getline(infile, line)){

        if(line==";"){
            z++;
            x=0;
            y=0;
        }else{
            std::stringstream ss(line);
            while(y<env_cells_y){
                ss >> std::skipws >>  env[x][y][z]; 
                y++;
            }
            x++;
            y=0;
        }
    }

    bool ascii = false;
    if(ascii){
        std::ofstream outfile(argv[2]);

        outfile<<"solid GADEN\n";
        //okay, this code is going to be a bit confusing, so here's my thinking:
        //we only want triangles of the surface, not the interior of solids, 
        //so we only write the ones that correspond to the faces of occupied cells that are in contact with free cells.
        //That means checking all the neighbours of each cell that is occupied
        for(int i=0;i<env.size();i++){
            for(int j=0;j<env[0].size();j++){
                for(int k=0;k<env[0][0].size();k++){
                    if(env[i][j][k]==occ){
                        //for each occupied cell, we check all six faces
                        //if it's the limit of the environment, or the end of a solid, we print two triangles (making a square)
                        if(i-1<0||env[i-1][j][k]!=occ){
                            outfile << 
                            "facet normal -1 0 0\n"<<
                            "outer loop\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "endloop\n"<<
                            "endfacet\n";

                            outfile << 
                            "facet normal -1 0 0\n"<<
                            "outer loop\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "endloop\n"<<
                            "endfacet\n";
                        }
                        if(i+1>=env.size()||env[i+1][j][k]!=occ){
                            outfile << 
                            "facet normal 1 0 0\n"<<
                            "outer loop\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "endloop\n"<<
                            "endfacet\n";

                            outfile << 
                            "facet normal 1 0 0\n"<<
                            "outer loop\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "endloop\n"<<
                            "endfacet\n";
                        }

                        if(j-1<0||env[i][j-1][k]!=occ){
                            outfile << 
                            "facet normal 0 -1 0\n"<<
                            "outer loop\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "endloop\n"<<
                            "endfacet\n";

                            outfile << 
                            "facet normal 0 -1 0\n"<<
                            "outer loop\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "endloop\n"<<
                            "endfacet\n";
                        }
                        if(j+1>=env[0].size()||env[i][j+1][k]!=occ){
                            outfile << 
                            "facet normal 0 1 0\n"<<
                            "outer loop\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "endloop\n"<<
                            "endfacet\n";

                            outfile << 
                            "facet normal 0 1 0\n"<<
                            "outer loop\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "endloop\n"<<
                            "endfacet\n";
                        }

                        if(k-1<0||env[i][j][k-1]!=occ){
                            outfile << 
                            "facet normal 0 0 -1\n"<<
                            "outer loop\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "endloop\n"<<
                            "endfacet\n";

                            outfile << 
                            "facet normal 0 0 -1\n"<<
                            "outer loop\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+k*cell_size<<"\n"<<
                            "endloop\n"<<
                            "endfacet\n";
                        }
                        if(k+1>=env[0][0].size()||env[i][j][k+1]!=occ){
                            outfile << 
                            "facet normal 0 0 1\n"<<
                            "outer loop\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "endloop\n"<<
                            "endfacet\n";

                            outfile << 
                            "facet normal 0 0 1\n"<<
                            "outer loop\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+i*cell_size<<" "<<(env_min_y)+(j+1)*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "   vertex "<<(env_min_x)+(i+1)*cell_size<<" "<<(env_min_y)+j*cell_size<<" "<<(env_min_z)+(k+1)*cell_size<<"\n"<<
                            "endloop\n"<<
                            "endfacet\n";
                        }
                    }
                }
            }
        }
        outfile<<"endSolid GADEN";

        outfile.close();
    }
    else{
        uint8_t a = 0;
        std::ofstream outfile(argv[2], std::ios::out | std::ios::binary);
        for( int i = 0; i<80;i++){
            outfile.write(reinterpret_cast<char*>(&a), sizeof(a));
        }

        std::vector<std::vector<float> > triangles;

        for(int i=0;i<env.size();i++){
            for(int j=0;j<env[0].size();j++){
                for(int k=0;k<env[0][0].size();k++){
                    if(env[i][j][k]==occ){
                        //for each occupied cell, we check all six faces
                        //if it's the limit of the environment, or the end of a solid, we print two triangles (making a square)
                        if(i-1<0||env[i-1][j][k]!=occ){
                            std::vector<float> tri;

                            tri.push_back(-1);
                            tri.push_back(0);
                            tri.push_back(0);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);
                            
                            triangles.push_back(tri);
                            tri.clear();

                            tri.push_back(-1);
                            tri.push_back(0);
                            tri.push_back(0);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            triangles.push_back(tri);
                        }
                        if(i+1>=env.size()||env[i+1][j][k]!=occ){

                            std::vector<float> tri;
                            
                            tri.push_back(1);
                            tri.push_back(0);
                            tri.push_back(0);
                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            triangles.push_back(tri);
                            tri.clear();

                            tri.push_back(1);
                            tri.push_back(0);
                            tri.push_back(0);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            triangles.push_back(tri);
                        }

                        if(j-1<0||env[i][j-1][k]!=occ){
                            std::vector<float> tri;

                            tri.push_back(0);
                            tri.push_back(-1);
                            tri.push_back(0);
                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            triangles.push_back(tri);
                            tri.clear();

                            tri.push_back(0);
                            tri.push_back(-1);
                            tri.push_back(0);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            triangles.push_back(tri);
                        }
                        if(j+1>=env[0].size()||env[i][j+1][k]!=occ){
                            std::vector<float> tri;
                            
                            tri.push_back(0);
                            tri.push_back(1);
                            tri.push_back(0);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            triangles.push_back(tri);
                            tri.clear();

                            tri.push_back(0);
                            tri.push_back(1);
                            tri.push_back(0);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            triangles.push_back(tri);
                        }

                        if(k-1<0||env[i][j][k-1]!=occ){
                            std::vector<float> tri;

                            tri.push_back(0);
                            tri.push_back(0);
                            tri.push_back(-1);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            triangles.push_back(tri);
                            tri.clear();

                            tri.push_back(0);
                            tri.push_back(0);
                            tri.push_back(-1);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+k*cell_size);

                            triangles.push_back(tri);
                        }
                        if(k+1>=env[0][0].size()||env[i][j][k+1]!=occ){
                            std::vector<float> tri;

                            tri.push_back(0);
                            tri.push_back(0);
                            tri.push_back(1);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            triangles.push_back(tri);
                            tri.clear();

                            tri.push_back(0);
                            tri.push_back(0);
                            tri.push_back(1);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            tri.push_back((env_min_x)+i*cell_size);
                            tri.push_back((env_min_y)+(j+1)*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);

                            tri.push_back((env_min_x)+(i+1)*cell_size);
                            tri.push_back((env_min_y)+j*cell_size);
                            tri.push_back((env_min_z)+(k+1)*cell_size);
                            triangles.push_back(tri);
                        }
                    }
                }
            }
        }

        uint32_t num = triangles.size();
        outfile.write(reinterpret_cast<char*>(&num), sizeof(num));

        bool t = true;
        uint16_t zero = 0;
        for(auto triangle : triangles){
            for(int i=0;i<triangle.size();i++){
                outfile.write(reinterpret_cast<char*>(&triangle[i]), sizeof(triangle[i]));
            }
            outfile.write(reinterpret_cast<char*>(&zero), sizeof(zero));
        }
        outfile.close();
    }
    
}