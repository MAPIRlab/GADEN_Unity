using UnityEngine;

public class Simulated_anemometer : MonoBehaviour
{
    public GameObject gadenPlayer;

    public float stdDev;
    System.Random rand = new System.Random(); //generate noise for the measurements

    public Vector2 getMeasurement(Vector3 anemoRequest){
        
        //get the ground-truth wind value from the gaden_player script
        Vector3 response=gadenPlayer.GetComponent<GADEN_player>().getWind(anemoRequest);
        float speed = Mathf.Sqrt(Mathf.Pow(response.x,2)+Mathf.Pow(response.z,2));
        float angle = Mathf.Atan2(response.z, response.x);
        
        //corrupt the value with gaussian noise
        float u1 = 1.0f-(float)rand.NextDouble(); //uniform(0,1] random doubles
        float u2 = 1.0f-(float)rand.NextDouble();
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) *
                    Mathf.Sin(2.0f * Mathf.PI * u2); //random normal(0,1)
        float randNormal = stdDev * randStdNormal; //random normal(mean,stdDev^2)
        
        return new Vector2(speed+randNormal,
                            angle+randNormal);
    }
}