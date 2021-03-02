using System;
using System.Collections.Generic;
using UnityEngine;

public class Simulated_PID : MonoBehaviour
{
    public GameObject gadenPlayer;
    public float stdDev;
    System.Random rand = new System.Random(); //generate noise for the measurements

    public float getMeasurement(Vector3 request){

        
        //get the ground-truth value from the gaden_player script
        GasMeasurement response=gadenPlayer.GetComponent<GADEN_player>().getConcentration(request);

        //corrupt the value with gaussian noise
        float u1 = 1.0f-(float)rand.NextDouble(); //uniform(0,1] random doubles
        float u2 = 1.0f-(float)rand.NextDouble();
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) *
                    Mathf.Sin(2.0f * Mathf.PI * u2); //random normal(0,1)
        float randNormal = stdDev * randStdNormal; //random normal(mean,stdDev^2)
        
        return response.ppm+randNormal;
    }

    void Update(){
        //Debug.Log(getMeasurement(gameObject.transform.position));
    }
}
