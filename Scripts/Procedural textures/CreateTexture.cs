using UnityEngine;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

// Create a texture and fill it with Perlin noise.
// Try varying the xOrg, yOrg and scale values in the inspector
// while in Play mode to see the effect they have on the noise.

public class CreateTexture : MonoBehaviour
{
    System.Random rand = new System.Random();
    // Width and height of the texture in pixels.
    public bool writeFile;
    public int pixWidth;
    public int pixHeight;
    // The number of cycles of the basic noise pattern that are repeated
    // over the width and height of the texture.
    public float scale = 1.0F;
    [Range(1,10)]
    public int octaves=3;
    [Range(0,5)]
    public float roughness;
    public float mX, mY;
    [Range(-0.25f,0.25f)]
    public float R;
    [Range(0,0.5f)]
    public float sX, sY;

    public int numFrames;
    [Range(1,10)]
    public int falloff;
    [SerializeField, HideInInspector]
    Texture2D noiseTex;
    Texture2D animationTex;
    private Color[] pix;
    private Color[] anim;
    private float[] pixValue;
    
    // The origin of the sampled area in the plane.
    float xOrg;
    float yOrg;

    //this is for creating perfect loops through 4D noise
    float angle=0;
    public float speedFactor=1;
    //this is for creating non-repeating (well, as much as that's possible) patterns through 3D noise
    float zCoord=0;

    Noise.OpenSimplex2S noiseGen;
    int iterations;

    void Start()
    {   
        noiseGen = new Noise.OpenSimplex2S(rand.Next());

        xOrg=(float)rand.NextDouble();
        yOrg=(float)rand.NextDouble();
        // Set up the texture and a Color array to hold pixels during processing.
        noiseTex = new Texture2D(pixWidth, pixHeight);
        pix = new Color[noiseTex.width * noiseTex.height];
        pixValue = new float[noiseTex.width * noiseTex.height];


        animationTex = new Texture2D(pixWidth*numFrames, pixHeight*numFrames);
        anim = new Color[numFrames*pixWidth * numFrames*pixHeight];
        iterations=0;
    }

    void CalcNoise()
    {
        // For each pixel in the texture...
        float[] max =new float[pixHeight];

        float u=Mathf.Cos(angle)*speedFactor;
        float v=Mathf.Sin(angle)*speedFactor;
        Parallel.For(0,pixHeight,y =>
        {
            for (int x = 0;x < pixWidth;x++)
            {
                //openSimplex noise
                float xCoord = xOrg + ((float) x) / pixWidth * scale;
                float yCoord = yOrg + ((float) y) / pixHeight * scale;

                
                float simplex=0;

                for(int i=1;i<=octaves;i++){
                    xCoord = xOrg + ((float) x) / pixWidth * scale * (2*i);
                    yCoord = yOrg + ((float) y) / pixHeight * scale * (2*i);
                    simplex = simplex + (roughness/i) * (0.5f+ 0.5f*(float) noiseGen.Noise4_XYBeforeZW(xCoord,yCoord,u,v) );
                }

                //gaussian pdf
                float gauss = gaussian( x, y, mX*pixHeight, mY*pixWidth, sX*pixHeight, sY*pixWidth, R)*pixHeight*pixWidth;

                float currentValue = gauss + gauss*simplex;

                pixValue[y*pixWidth + x]=currentValue;
               

                if(currentValue>max[y]){
                    max[y]=currentValue;
                }
            }
        });

        //normalize into [0,1]
        float maxvalue = max.Max();
        
        
        Parallel.For(0,pixHeight,y =>
        {
            for (int x = 0;x < pixWidth;x++)
            {
                float val = pixValue[y*pixWidth + x] / (maxvalue*falloff);
                pix[y*pixWidth + x]=new Color(val,val,val);
                if(writeFile&&iterations<numFrames*numFrames){
                    anim[((numFrames-1)-iterations/numFrames)*(numFrames*pixWidth*pixHeight) 
                        +(pixHeight-1-y)*numFrames*pixWidth 
                        + (iterations%numFrames)*pixWidth+x]
                                =new Color(val,val,val);
                }
            }
        });
        if(writeFile){
            iterations++;
        }
        angle+=2*Mathf.PI/(numFrames*numFrames);
        
        // Copy the pixel data to the texture and load it into the GPU.
        noiseTex.SetPixels(pix);
        noiseTex.Apply();

        if(iterations==numFrames*numFrames){
            animationTex.SetPixels(anim);
            animationTex.Apply();

            byte[] png=animationTex.EncodeToPNG();
            File.WriteAllBytes("Assets/animation.png",png);
            
            Debug.Log(anim.Length+" bytes written");
            Debug.Log(png.Length+" bytes written");
            writeFile=false;
            iterations=0;
            anim=new Color[numFrames*pixWidth*numFrames*pixHeight];
        }
    }

    float gaussian(float x, float y, float muX, float muY, float sigmaX, float sigmaY, float ro){
        float t1 = 1 / (2 * Mathf.PI * sigmaX*sigmaY * Mathf.Sqrt(1-ro*ro) );

        float tX = Mathf.Pow( (x-muX)/sigmaX , 2);
        float tY = Mathf.Pow( (y-muY)/sigmaY , 2);
        float tXY = 2*ro* (x-muX)*(y-muY)/(sigmaX*sigmaY);

        return t1*Mathf.Exp( ( -1/(2*(1-ro*ro)) ) * (tX+tY-tXY) );
    }
    
    void Update(){

    }

    void OnRenderImage(RenderTexture src,RenderTexture dest){
        CalcNoise();
        Graphics.Blit(noiseTex, dest);
        
    }
}

